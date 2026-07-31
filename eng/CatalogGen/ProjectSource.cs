using System.Diagnostics;
using System.Text.Json;

namespace CatalogGen;

// Acquisition from projects you build yourself: the same assemblies LocalAssemblySource reads, with
// the paths worked out by MSBuild rather than typed by hand.
//
// It is the fourth acquisition and the thinnest, because it stops as soon as it has the paths and
// hands the rest to LocalAssemblySource. What it removes is a bin/<config>/<tfm>/ path in a
// manifest — the one part of a catalogue's declaration that says nothing about the catalogue and
// breaks when the project retargets, is renamed, or is built somewhere else.
internal static class ProjectSource
{
    // Null when a project cannot be evaluated or its output is not there, which is a refusal rather
    // than an empty result, for LocalAssemblySource's reason: a catalogue short of a rule is
    // indistinguishable from one whose vendor retired it, and would be published as [Obsolete].
    internal static AnalyzerAssemblySet? Acquire(
        IReadOnlyList<string> projects, string configuration, string? sourceName, string? sourceVersion)
    {
        if (projects.Count == 0)
        {
            Console.Error.WriteLine("no project given");

            return null;
        }

        List<string> assemblies = [];
        Evaluation? first = null;

        foreach (string project in projects)
        {
            string full = Path.GetFullPath(project);
            if (!File.Exists(full))
            {
                Console.Error.WriteLine($"no such project: {project}");

                return null;
            }

            if (IsSolution(full))
            {
                // Refused rather than enumerated. Picking the analyzer projects out of a solution
                // means deciding which of its projects produce analyzers, and every rule for that is
                // a guess: a project can reference Microsoft.CodeAnalysis without declaring an
                // analyzer, and can declare one without any of the packaging that would advertise
                // it. Guessing short is the failure this tool exists to prevent — the catalogue is
                // emitted, nothing reports the omission, and the missing rules read as retired.
                Console.Error.WriteLine(
                    $"{Path.GetFileName(full)} is a solution: name the projects that produce " +
                    "analyzers instead, repeating --project. Which projects in a solution declare " +
                    "analyzers cannot be told from the outside, and a catalogue short of a rule " +
                    "reads as one whose vendor retired it.");

                return null;
            }

            Resolved? resolved = ResolveOutput(full, configuration);
            if (resolved is null) return null;

            Console.WriteLine($"resolved {Path.GetFileName(full)} => {resolved.TargetFramework} ({configuration})");
            assemblies.Add(resolved.Assembly);
            first ??= resolved.Evaluation;
        }

        // The first project names the source unless told otherwise, on LocalAssemblySource's
        // convention and for its reason: the caller controls the order, so a wrong guess is
        // corrected by reordering or by --source-name.
        //
        // The project's own Version, not the assembly's. They are usually the same number, but a
        // project states its release where an assembly only carries what was stamped into it — and
        // AssemblyVersion is routinely pinned to a major while the package version moves, which
        // would have the catalogue record a source that stood still while its rules did not.
        return LocalAssemblySource.Acquire(
            assemblies,
            sourceName ?? NullIfEmpty(first!.AssemblyName),
            sourceVersion ?? NullIfEmpty(first!.Version));
    }

    private static bool IsSolution(string path)
        => path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
           || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
           || path.EndsWith(".slnf", StringComparison.OrdinalIgnoreCase);

    private static string? NullIfEmpty(string value) => value.Length > 0 ? value : null;

    // Where MSBuild says this project's assembly is, provided it is there.
    private static Resolved? ResolveOutput(string project, string configuration)
    {
        Evaluation? evaluated = Evaluate(project, configuration, null);
        if (evaluated is null) return null;

        if (evaluated.TargetPath.Length > 0)
        {
            return File.Exists(evaluated.TargetPath)
                       ? new Resolved(evaluated.TargetPath, evaluated.TargetFramework, evaluated)
                       : NotBuilt(project, configuration, [evaluated.TargetPath]);
        }

        // A multi-targeted project answers with nothing: TargetPath is a per-framework property, and
        // the outer build has no framework. Ask again, once per declared one, and read whichever was
        // built. netstandard2.0 is tried first when the project declares it, because that is the
        // framework an analyzer actually ships to consumers — a project that also builds a modern
        // target builds it for its own tests, and reading that one could read a set of descriptors
        // no consumer is ever served.
        if (evaluated.TargetFrameworks.Length == 0)
        {
            Console.Error.WriteLine(
                $"{Path.GetFileName(project)}: MSBuild evaluated no TargetPath, so there is no " +
                "assembly to read. A project that produces no assembly produces no analyzers.");

            return null;
        }

        List<string> looked = [];
        foreach (string framework in evaluated.TargetFrameworks
                                              .Split(';', StringSplitOptions.RemoveEmptyEntries
                                                          | StringSplitOptions.TrimEntries)
                                              .OrderBy(f => f == "netstandard2.0" ? 0 : 1, Comparer<int>.Default))
        {
            Evaluation? perFramework = Evaluate(project, configuration, framework);
            if (perFramework is null) return null;
            if (perFramework.TargetPath.Length == 0) continue;

            looked.Add(perFramework.TargetPath);
            if (File.Exists(perFramework.TargetPath))
            {
                return new Resolved(perFramework.TargetPath, framework, perFramework);
            }
        }

        return NotBuilt(project, configuration, looked);
    }

    // Named separately because it is the likeliest outcome of a first run, and the least useful to
    // report as "file not found": the path is one MSBuild computed rather than one anybody typed,
    // so it means nothing without the command that would produce it.
    private static Resolved? NotBuilt(string project, string configuration, IReadOnlyList<string> looked)
    {
        Console.Error.WriteLine($"{Path.GetFileName(project)} is not built in {configuration}:");
        foreach (string path in looked) Console.Error.WriteLine($"  missing {path}");
        Console.Error.WriteLine(
            $"  build it with `dotnet build \"{project}\" -c {configuration}`, or name the " +
            "configuration you did build with --configuration.");

        return null;
    }

    // Evaluates without building. -getProperty runs MSBuild's evaluation phase and prints the
    // result as JSON, which is what makes this cheap enough to do twice and safe enough to do in
    // `validate`: checking a catalogue must not rebuild the caller's project, restore it, or touch
    // its output.
    private static Evaluation? Evaluate(string project, string configuration, string? targetFramework)
    {
        ProcessStartInfo start = new()
        {
            FileName = DotnetCli.Host(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("msbuild");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("-nologo");
        start.ArgumentList.Add($"-p:Configuration={configuration}");
        if (targetFramework is not null) start.ArgumentList.Add($"-p:TargetFramework={targetFramework}");
        foreach (string property in Wanted)
        {
            start.ArgumentList.Add($"-getProperty:{property}");
        }

        using Process? process = Process.Start(start);
        if (process is null)
        {
            Console.Error.WriteLine("could not start MSBuild to evaluate the project.");

            return null;
        }

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        // MSBuild reports an unloadable or unrestored project on stdout, as its own diagnostics with
        // codes and positions. Passing them through unedited is better than anything this tool could
        // say about them: they name the file and the line, and the reader already knows MSBuild.
        if (process.ExitCode != 0)
        {
            Console.Error.WriteLine($"could not evaluate {Path.GetFileName(project)}:");
            foreach (string line in (stdout + stderr).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                Console.Error.WriteLine($"  {line.TrimEnd()}");
            }

            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(stdout);
            JsonElement properties = document.RootElement.GetProperty("Properties");

            return new Evaluation(
                Read(properties, "TargetPath"),
                Read(properties, "TargetFramework"),
                Read(properties, "TargetFrameworks"),
                Read(properties, "AssemblyName"),
                Read(properties, "Version"));
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException)
        {
            // -getProperty landed in MSBuild 17.8. An older SDK ignores the switch and builds the
            // project instead, which prints a build log where JSON was expected.
            Console.Error.WriteLine(
                $"could not read MSBuild's evaluation of {Path.GetFileName(project)}. " +
                "--project needs an SDK new enough for `-getProperty` (.NET 8 or later); " +
                "--assembly works with any.");

            return null;
        }

        static string Read(JsonElement properties, string name)
            => properties.TryGetProperty(name, out JsonElement value) ? value.GetString() ?? "" : "";
    }

    private static readonly string[] Wanted =
        ["TargetPath", "TargetFramework", "TargetFrameworks", "AssemblyName", "Version"];

    private sealed record Evaluation(
        string TargetPath, string TargetFramework, string TargetFrameworks, string AssemblyName, string Version);

    private sealed record Resolved(string Assembly, string TargetFramework, Evaluation Evaluation);
}
