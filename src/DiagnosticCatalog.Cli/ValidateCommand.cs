using System.Text;
using CatalogGen;
using Spectre.Console.Cli;

namespace DiagnosticCatalog.Cli;

/// <summary>
/// <c>dcat validate</c> — answers whether a catalogue still tells the truth about its source.
/// </summary>
/// <remarks>
/// <para>
/// It is the same work <c>generate</c> does, stopped one step short: the source is acquired, its
/// descriptors are read, the catalogue that would be written is computed — and then nothing is
/// written. What it reports is the difference.
/// </para>
/// <para>
/// This is the question no analyzer can answer. <c>DCAT0001</c>–<c>DCAT0007</c> check that a
/// catalogue is well formed and used correctly, at compile time, which is the better place for
/// those. None of them can check that it is still <em>current</em>: that needs the vendor's package,
/// and a compiler has no business fetching one. And staleness is the failure with no symptom —
/// a category that moved upstream compiles, suppresses nothing, and says nothing (specification
/// §3.2), which is the whole reason this repository exists.
/// </para>
/// </remarks>
internal sealed class ValidateCommand : AsyncCommand<ValidateSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, ValidateSettings settings, CancellationToken cancellationToken)
    {
        IReadOnlyList<Job>? jobs = await CatalogueJobs.ReadAsync(settings, cancellationToken);
        if (jobs is null) return ExitCodes.Failure;

        RunOutcome outcome = await CatalogRun.ExecuteAsync(jobs, dateOverride: null, writeChanges: false,
                                                           cancellation: cancellationToken);

        if (settings.Summary is not null)
        {
            await File.WriteAllTextAsync(Path.GetFullPath(settings.Summary),
                                         outcome.Summary.ReplaceLineEndings("\n") + "\n",
                                         new UTF8Encoding(false), cancellationToken);
            Console.WriteLine();
            Console.WriteLine($"summary written to {settings.Summary}");
        }

        Console.WriteLine();
        if (outcome.ExitCode != 0)
        {
            // The run could not finish, so it never reached an opinion. Reporting that as drift
            // would tell a pipeline its catalogue is stale on the strength of a feed being down.
            Console.WriteLine("RESULT: could not be checked");

            return ExitCodes.Failure;
        }

        Console.WriteLine(outcome.ChangedAny
                              ? "RESULT: out of date — regenerate with `dcat generate`"
                              : "RESULT: current");

        return outcome.ChangedAny ? ExitCodes.OutOfDate : ExitCodes.Success;
    }
}
