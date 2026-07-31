using System.Text;
using CatalogGen;
using Spectre.Console.Cli;

namespace DiagnosticCatalog.Cli;

/// <summary>
/// <c>dcat generate</c> — turns the analyzers it is pointed at into a catalogue.
/// </summary>
/// <remarks>
/// The command's whole job is to turn a validated command line into <see cref="Job"/> values and
/// to decide where the run's report goes. Everything after that is the engine's: acquiring the
/// analyzer assemblies, reading the descriptors they declare, and emitting the catalogue.
/// </remarks>
internal sealed class GenerateCommand : AsyncCommand<GenerateSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, GenerateSettings settings, CancellationToken cancellation)
    {
        IReadOnlyList<Job> jobs;
        try
        {
            jobs = await JobsFrom(settings, cancellation);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // A manifest that cannot be read or does not parse is the caller's file, not a defect
            // here: report the reason on one line rather than a stack trace.
            Console.Error.WriteLine($"error: {settings.Manifest}: {ex.Message}");

            return ExitCodes.Failure;
        }

        if (jobs.Count == 0)
        {
            Console.Error.WriteLine("error: the manifest declares no catalogue.");

            return ExitCodes.Failure;
        }

        RunOutcome outcome = await CatalogRun.ExecuteAsync(jobs, settings.Date, cancellation);

        if (settings.Summary is not null)
        {
            await File.WriteAllTextAsync(Path.GetFullPath(settings.Summary),
                                         outcome.Summary.ReplaceLineEndings("\n") + "\n",
                                         new UTF8Encoding(false), cancellation);
            Console.WriteLine();
            Console.WriteLine($"summary written to {settings.Summary}");
        }

        Console.WriteLine();
        Console.WriteLine(outcome.ChangedAny ? "RESULT: catalogues changed" : "RESULT: no change");

        return outcome.ExitCode == 0 ? ExitCodes.Success : ExitCodes.Failure;
    }

    private static async Task<IReadOnlyList<Job>> JobsFrom(GenerateSettings settings, CancellationToken cancellation)
    {
        if (settings.Manifest is not null)
        {
            string path = Path.GetFullPath(settings.Manifest);
            IReadOnlyList<Job> jobs = CatalogRun.JobsFromManifest(await File.ReadAllTextAsync(path, cancellation), path);
            Console.WriteLine($"manifest {settings.Manifest}: {jobs.Count} catalogue(s)");

            return jobs;
        }

        bool fromAssemblies = settings.Assemblies.Length > 0;

        return
        [
            new Job(
                Package: fromAssemblies ? null : settings.Package,
                Version: fromAssemblies ? null : settings.PackageVersion,
                Namespace: settings.Namespace!,
                Container: settings.Container!,
                Output: Path.GetFullPath(settings.Output!),
                Language: settings.Language,
                Assemblies: fromAssemblies ? settings.Assemblies : null,
                SourceName: settings.SourceName,
                SourceVersion: settings.SourceVersion),
        ];
    }
}
