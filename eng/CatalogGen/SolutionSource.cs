using System.Diagnostics;

namespace CatalogGen;

// Acquisition from a solution: the projects in it that DECLARE they produce diagnostic rules.
//
// The distinction from guessing is the whole feature. Deciding which of a solution's projects
// produce analyzers cannot be inferred from the outside — measured on this repository, "references
// Microsoft.CodeAnalysis" matches six projects of which one is an analyzer, and "declares a
// DiagnosticAnalyzer subclass" matches two of which one is a fixture written to fail construction.
// Guessing short is the failure this tool exists to prevent: the catalogue is emitted, nothing
// reports the omission, and the missing rules are carried forward as [Obsolete] against a vendor
// that still declares them.
//
// So nothing here infers. A project joins by saying so, in its own file, exactly as a project joins
// a release train by declaring <ReleaseTrain> and never by appearing in a list somewhere else.
internal static class SolutionSource
{
    internal const string Marker = "ProducesDiagnosticRules";

    // Null on any refusal, which includes finding nothing: a solution whose projects declare none is
    // not an empty catalogue, it is a question that was not answered. Emitting nothing and exiting
    // zero would read as success to the scheduled job this exists to serve.
    internal static AnalyzerAssemblySet? Acquire(
        string solutionPath, string configuration, string? sourceName, string? sourceVersion)
    {
        string full = Path.GetFullPath(solutionPath);
        if (!File.Exists(full))
        {
            Console.Error.WriteLine($"no such solution: {solutionPath}");

            return null;
        }

        IReadOnlyList<string>? projects = ProjectsIn(full);
        if (projects is null) return null;

        List<string> declared = [];
        foreach (string project in projects)
        {
            bool? produces = Declares(project);
            if (produces is null) return null;
            if (produces.Value) declared.Add(project);
        }

        if (declared.Count == 0)
        {
            Console.Error.WriteLine(
                $"no project in {Path.GetFileName(full)} declares <{Marker}>true</{Marker}>. " +
                $"Add it to the projects whose analyzers should be catalogued, or name them with " +
                "--project. Reading none of them and emitting nothing would report success for a " +
                "catalogue that was never generated.");

            return null;
        }

        Console.WriteLine(
            $"{Path.GetFileName(full)}: {declared.Count} of {projects.Count} project(s) declare {Marker}");

        // Ordinal, so the set is a property of the request rather than of the solution file's order:
        // the first project names the source when nothing else does, and reordering a solution must
        // not silently change what a catalogue records.
        return ProjectSource.Acquire(
            [.. declared.OrderBy(p => p, StringComparer.Ordinal)], configuration, sourceName, sourceVersion);
    }

    // The SDK enumerates the solution, rather than this tool parsing one. .sln and .slnx are
    // different formats with different parsers, and both are the SDK's to know.
    private static List<string>? ProjectsIn(string solutionPath)
    {
        ProcessStartInfo start = new()
        {
            FileName = DotnetCli.Host(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(solutionPath)!,
        };

        start.ArgumentList.Add("sln");
        start.ArgumentList.Add(solutionPath);
        start.ArgumentList.Add("list");

        using Process? process = Process.Start(start);
        if (process is null)
        {
            Console.Error.WriteLine("could not start the SDK to enumerate the solution.");

            return null;
        }

        Task<string> outText = process.StandardOutput.ReadToEndAsync();
        Task<string> errText = process.StandardError.ReadToEndAsync();

        if (!ChildProcess.WaitOrKill(process, ChildProcess.Budget(ChildProcess.ProjectEvaluation),
                                     $"listing {Path.GetFileName(solutionPath)}"))
        {
            return null;
        }

        string stdout = outText.GetAwaiter().GetResult();
        string stderr = errText.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine($"could not read {Path.GetFileName(solutionPath)}:");
            foreach (string line in (stdout + stderr).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                Console.Error.WriteLine($"  {line.TrimEnd()}");
            }

            return null;
        }

        // The output carries a heading and a rule before the paths. Selecting on the extension rather
        // than skipping two lines keeps this working if the heading is localised or changes shape —
        // a project path is what a project path looks like.
        string solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        List<string> projects = [.. stdout
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
            .Select(l => Path.GetFullPath(Path.Combine(solutionDirectory, l.Replace('\\', Path.DirectorySeparatorChar))))];

        if (projects.Count == 0)
        {
            Console.Error.WriteLine($"{Path.GetFileName(solutionPath)} lists no project.");

            return null;
        }

        return projects;
    }

    // Null when the project could not be evaluated, which is a refusal rather than "does not declare
    // it": a project this tool failed to ask is not a project that answered no, and treating the two
    // alike is how a catalogue silently loses the rules of whichever project happened to be broken.
    private static bool? Declares(string project)
    {
        string? value = ProjectSource.EvaluateProperty(project, Marker);

        return value is null ? null : value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }
}
