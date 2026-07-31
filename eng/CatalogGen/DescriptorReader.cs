using System.Diagnostics;
using System.Text.Json;

namespace CatalogGen;

// The reading stage — which no longer happens here.
//
// Reading descriptors means loading somebody else's analyzer assemblies and constructing them:
// running third-party code compiled against a Roslyn this repository does not choose, on a runtime
// it does not choose either. That now happens in CatalogGen.Worker, a separate process, and this
// type is what asks it to.
//
// The move buys two things no in-process arrangement can. The worker rolls forward to the latest
// installed major, so `dcat`'s net8.0 floor — which exists to make it installable widely
// (ADR-0017) — stops deciding which analyzers it can read. And a construction that overflows the
// stack or kills the process takes the worker down rather than the tool, leaving something to
// report.
//
// It also takes Roslyn out of the engine entirely: nothing on this side of the process boundary
// references it any more.
internal static class DescriptorReader
{
    private const string WorkerAssemblyName = "CatalogGen.Worker.dll";

    /// The rules declared by one acquisition's assemblies, or null when the read was incomplete.
    /// The worker owns that judgement and reports it on its own stderr; anything but a clean exit
    /// is the same refusal this method produced when it read in-process.
    internal static SortedDictionary<string, RuleInfo>? Read(AnalyzerAssemblySet source)
    {
        string? worker = ResolveWorker();
        if (worker is null)
        {
            Console.Error.WriteLine(
                $"the descriptor worker ({WorkerAssemblyName}) is not beside this tool, in " +
                $"{AppContext.BaseDirectory}. It is bundled at build time; a tool package missing it is a " +
                "packaging fault rather than a usage error.");

            return null;
        }

        string request = Path.Combine(Path.GetTempPath(), $"cataloggen-req-{Guid.NewGuid():N}.json");
        string response = Path.Combine(Path.GetTempPath(), $"cataloggen-res-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(request,
                              JsonSerializer.Serialize(new DescriptorReadRequest { AssemblyPaths = [.. source.AssemblyPaths] }));

            int exitCode = RunWorker(worker, request, response);
            if (exitCode != WorkerExitCodes.Complete)
            {
                // The worker has already said what it could not read and why it refuses. Adding a
                // second account here would only make the first harder to find.
                if (exitCode != WorkerExitCodes.IncompleteRead)
                    Console.Error.WriteLine($"the descriptor worker exited with {exitCode}");

                return null;
            }

            if (!File.Exists(response))
            {
                Console.Error.WriteLine("the descriptor worker reported success but wrote no result");

                return null;
            }

            DescriptorReadResponse? read =
                JsonSerializer.Deserialize<DescriptorReadResponse>(File.ReadAllText(response));
            if (read is null)
            {
                Console.Error.WriteLine("the descriptor worker wrote a result that could not be read");

                return null;
            }

            SortedDictionary<string, RuleInfo> rules = new(StringComparer.Ordinal);
            foreach ((string id, ReadRule rule) in read.Rules)
                rules[id] = new RuleInfo(rule.Category, rule.HelpLinkUri, Retired: false, rule.Title);

            return rules;
        }
        finally
        {
            Delete(request);
            Delete(response);
        }
    }

    // The worker inherits this process's console rather than having its streams captured: its
    // output IS the run's diagnostics, and relaying it verbatim keeps the log reading exactly as it
    // did when this stage ran in-process.
    private static int RunWorker(string workerPath, string requestPath, string responsePath)
    {
        string workerDirectory = Path.GetDirectoryName(workerPath)!;
        ProcessStartInfo start = new()
        {
            FileName = DotnetHost(),
            UseShellExecute = false,
            WorkingDirectory = workerDirectory,
        };

        // `exec` rather than `run`, so the worker's own runtimeconfig decides the runtime — which is
        // the entire point of the worker, since that is where RollForward=LatestMajor lives. The
        // probing path is a fallback for assemblies the worker's own deps.json does not place.
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--additionalprobingpath");
        start.ArgumentList.Add(workerDirectory);
        start.ArgumentList.Add(workerPath);
        start.ArgumentList.Add(requestPath);
        start.ArgumentList.Add(responsePath);

        using Process process = Process.Start(start)!;
        process.WaitForExit();

        return process.ExitCode;
    }

    // Beside this assembly, which for the shipped tool is the directory PackAsTool lays the worker
    // into, and for a test run is wherever the bundling props copied it.
    private static string? ResolveWorker()
    {
        string candidate = Path.Combine(AppContext.BaseDirectory, WorkerAssemblyName);

        return File.Exists(candidate) ? candidate : null;
    }

    // DOTNET_HOST_PATH is set by the SDK and by `dotnet` itself, and is the authoritative answer
    // when present. Failing that, this process may already BE the host — a framework-dependent app
    // launched by `dotnet` reports it as its own path — and failing that, the name on PATH is all
    // that is left. Guessing wrong is survivable and reported: the process simply fails to start.
    private static string DotnetHost()
    {
        string? declared = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(declared) && File.Exists(declared)) return declared;

        string? current = Environment.ProcessPath;
        if (current is not null)
        {
            string name = Path.GetFileNameWithoutExtension(current);
            if (string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase)) return current;
        }

        return "dotnet";
    }

    private static void Delete(string path)
    {
        // A temp file left behind is untidy; a run that failed because it could not delete one
        // would be absurd.
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
