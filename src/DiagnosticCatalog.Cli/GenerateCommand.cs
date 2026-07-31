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
        CommandContext context, GenerateSettings settings, CancellationToken cancellationToken)
    {
        IReadOnlyList<Job>? jobs = await CatalogueJobs.ReadAsync(settings, cancellationToken);
        if (jobs is null) return ExitCodes.Failure;

        RunOutcome outcome = await CatalogRun.ExecuteAsync(jobs, settings.Date, cancellation: cancellationToken);

        if (settings.Summary is not null)
        {
            await File.WriteAllTextAsync(Path.GetFullPath(settings.Summary),
                                         outcome.Summary.ReplaceLineEndings("\n") + "\n",
                                         new UTF8Encoding(false), cancellationToken);
            Console.WriteLine();
            Console.WriteLine($"summary written to {settings.Summary}");
        }

        Console.WriteLine();
        Console.WriteLine(outcome.ChangedAny ? "RESULT: catalogues changed" : "RESULT: no change");

        return outcome.ExitCode == 0 ? ExitCodes.Success : ExitCodes.Failure;
    }
}
