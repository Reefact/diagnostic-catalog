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

            int exitCode = RunWorker(worker, source, request, response);
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
    private static int RunWorker(
        string workerPath, AnalyzerAssemblySet source, string requestPath, string responsePath)
    {
        string workerDirectory = Path.GetDirectoryName(workerPath)!;
        ProcessStartInfo start = new()
        {
            FileName = DotnetCli.Host(),
            UseShellExecute = false,
            WorkingDirectory = workerDirectory,
        };

        // `exec` rather than `run`, so the worker's own runtimeconfig decides the runtime — which is
        // the entire point of the worker, since that is where RollForward=LatestMajor lives.
        start.ArgumentList.Add("exec");

        // Run against the TARGET's dependency graph when it has one, so an analyzer compiled
        // against a different Roslyn resolves its own rather than being read through this tool's.
        // It replaces the worker's own graph rather than adding to it, which is why the worker's
        // directory goes on the probing path below: that is where the worker's own assemblies —
        // CatalogGen, and the Roslyn it falls back on — are then found.
        if (source.DependencyContextPath is not null)
        {
            start.ArgumentList.Add("--depsfile");
            start.ArgumentList.Add(source.DependencyContextPath);
        }

        // The worker's own directory first, then every directory the assemblies live in, so a
        // sibling an analyzer needs resolves whether or not the graph above mentions it. Distinct
        // and ordinal for the same reason the assembly list is: the probing order must be a
        // property of the request rather than of the disk.
        foreach (string directory in ProbingPaths(workerDirectory, source))
        {
            start.ArgumentList.Add("--additionalprobingpath");
            start.ArgumentList.Add(directory);
        }

        start.ArgumentList.Add(workerPath);
        start.ArgumentList.Add(requestPath);
        start.ArgumentList.Add(responsePath);

        using Process process = Process.Start(start)!;
        process.WaitForExit();

        return process.ExitCode;
    }

    private static IEnumerable<string> ProbingPaths(string workerDirectory, AnalyzerAssemblySet source)
    {
        IEnumerable<string> assemblyDirectories = source.AssemblyPaths
            .Select(p => Path.GetDirectoryName(p))
            .Where(d => !string.IsNullOrEmpty(d))
            .Select(d => d!)
            .OrderBy(d => d, StringComparer.Ordinal);

        List<string> paths = [workerDirectory, .. assemblyDirectories];

        // A library's dependency graph names its packages by the path INSIDE the package, and
        // nothing in a class library says where packages live — an application would answer that
        // with a runtimeconfig.dev.json, and an analyzer, being netstandard2.0, has none. Without
        // the cache on the probing path the graph resolves to nothing: measured, the worker died on
        // "package: 'Microsoft.CodeAnalysis.Common', version: '4.8.0'".
        //
        // Best effort by nature. A machine whose cache does not hold what the analyzer was built
        // against gets the worker's own Roslyn through the AssemblyResolve unification, which is
        // what happened before any of this existed.
        string? cache = NuGetPackageCache();
        if (cache is not null) paths.Add(cache);

        return paths.Distinct(StringComparer.Ordinal);
    }

    private static string? NuGetPackageCache()
    {
        string? configured = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured)) return configured;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(home)) return null;

        string standard = Path.Combine(home, ".nuget", "packages");

        return Directory.Exists(standard) ? standard : null;
    }

    // Beside this assembly, which for the shipped tool is the directory PackAsTool lays the worker
    // into, and for a test run is wherever the bundling props copied it.
    private static string? ResolveWorker()
    {
        string candidate = Path.Combine(AppContext.BaseDirectory, WorkerAssemblyName);

        return File.Exists(candidate) ? candidate : null;
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
