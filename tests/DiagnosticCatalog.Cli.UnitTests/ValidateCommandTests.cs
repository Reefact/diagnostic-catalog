using System;
using System.IO;
using System.Threading.Tasks;
using CatalogGen;
using Xunit;

namespace DiagnosticCatalog.Cli.UnitTests;

/// <summary>
/// <c>dcat validate</c> run to completion, for the verdict it returns and the one it prints.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExitCodes"/> states that "the command tests assert them", and of the four codes
/// <see cref="ExitCodes.OutOfDate"/> was the one nothing asserted: every command test stopped at a
/// refusal — a usage error, or a source that was not there — so no test had ever run <c>validate</c>
/// far enough to reach a verdict. Both arms of the line that produces one were therefore free to
/// return anything, including <see cref="ExitCodes.Failure"/>, with the suite still green.
/// </para>
/// <para>
/// That is the code with the most riding on it. It exists precisely so a pipeline can tell a
/// catalogue that drifted from a tool that could not finish — the first is a regeneration, the
/// second a retry — and a nightly job branches on the difference. It is also the only one a caller
/// cannot infer from anything else the tool prints.
/// </para>
/// <para>
/// What makes a catalogue stale is settled elsewhere, in <c>CatalogueDriftTests</c>: the emitter's
/// comparison is the engine's contract and is covered rule by rule there. These tests are about the
/// shell's half of it — that a verdict is reached, reported on the line a human reads, and returned
/// as the number a script reads.
/// </para>
/// </remarks>
public sealed class ValidateCommandTests : IDisposable
{
    /// <summary>
    /// The source every test here reads: an assembly on disk that declares no analyzer.
    /// </summary>
    /// <remarks>
    /// Read offline and deterministically, which a package from a feed would be neither. That it
    /// declares no analyzer costs nothing — <c>DescriptorReaderTests</c> establishes that reading one
    /// is an empty result rather than a failure, and what is under test here is the verdict, not its
    /// contents. Staleness is then driven by <c>--source-version</c>, which is the honest signal for
    /// a source read off disk: an assembly's own version stays put across every rebuild, so the
    /// recorded release moving is exactly what "upstream is not what this catalogue says" means.
    /// </remarks>
    private static string Source => typeof(CatalogRun).Assembly.Location;

    private readonly string _work = Directory.CreateTempSubdirectory("dcat-validate-").FullName;

    public void Dispose() => Directory.Delete(_work, recursive: true);

    private string Output => Path.Combine(_work, "catalogue.g.cs");

    private Task<(int ExitCode, string Out, string Error)> GenerateAsync(string sourceVersion)
        => CliRun.Async("generate", "--assembly", Source,
                        "--source-name", "Acme", "--source-version", sourceVersion,
                        "--namespace", "Vendor.Catalog", "--container", "AcmeRules", "--output", Output);

    private Task<(int ExitCode, string Out, string Error)> ValidateAsync(string sourceVersion)
        => CliRun.Async("validate", "--assembly", Source,
                        "--source-name", "Acme", "--source-version", sourceVersion,
                        "--namespace", "Vendor.Catalog", "--container", "AcmeRules", "--output", Output);

    [Fact]
    public void The_worker_every_verdict_depends_on_is_deployed_beside_this_suite()
    {
        // Named rather than inferred, on the same reasoning as its twin in DescriptorReaderTests.
        // Descriptors are read by spawning CatalogGen.Worker, and a read with no worker returns
        // nothing — so every test below would report exit code 1 for a reason that has nothing to
        // do with what it asserts, and the suite would blame `validate` for a missing file.
        Assert.True(
            File.Exists(Path.Combine(AppContext.BaseDirectory, "CatalogGen.Worker.dll")),
            "the descriptor worker should be bundled beside this suite by build/BundleDescriptorWorker.props");
    }

    [Fact]
    public async Task A_catalogue_that_still_matches_its_source_is_current()
    {
        Assert.Equal(ExitCodes.Success, (await GenerateAsync("1.0.0")).ExitCode);

        (int exitCode, string output, _) = await ValidateAsync("1.0.0");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("RESULT: current", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_catalogue_whose_source_moved_is_out_of_date_rather_than_failed()
    {
        // The distinction the code exists for, asserted as a pair rather than as a number: the run
        // SUCCEEDED and the catalogue is what the caller must act on.
        Assert.Equal(ExitCodes.Success, (await GenerateAsync("1.0.0")).ExitCode);

        (int exitCode, string output, _) = await ValidateAsync("2.0.0");

        Assert.Equal(ExitCodes.OutOfDate, exitCode);
        Assert.NotEqual(ExitCodes.Failure, exitCode);
        Assert.Contains("RESULT: out of date", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_catalogue_that_was_never_generated_is_out_of_date()
    {
        // The first run of a new catalogue in a pipeline. Nothing to compare against is a catalogue
        // that does not tell the truth about its source yet — not a failure to check it.
        Assert.False(File.Exists(Output));

        (int exitCode, string output, _) = await ValidateAsync("1.0.0");

        Assert.Equal(ExitCodes.OutOfDate, exitCode);
        Assert.Contains("RESULT: out of date", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validating_a_stale_catalogue_writes_nothing()
    {
        // The property that lets this be run against a clean tree, and the reason `validate` is not
        // just `generate` with its answer read afterwards. Asserted on the STALE path on purpose:
        // that is the one where the emitter has a rewrite prepared and is asked not to perform it.
        await GenerateAsync("1.0.0");
        string generated = await File.ReadAllTextAsync(Output, TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.OutOfDate, (await ValidateAsync("2.0.0")).ExitCode);

        Assert.Equal(generated, await File.ReadAllTextAsync(Output, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_source_it_cannot_read_is_a_failure_rather_than_a_verdict()
    {
        // The other half of the same contract. Reporting an unreadable source as drift would tell a
        // pipeline its catalogue is stale on the strength of a source being absent, and the
        // regeneration that followed would be run against nothing.
        (int exitCode, string output, _) = await CliRun.Async(
            "validate", "--assembly", Path.Combine(_work, "no-such-assembly.dll"),
            "--namespace", "Vendor.Catalog", "--container", "AcmeRules", "--output", Output);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("RESULT: could not be checked", output, StringComparison.Ordinal);
    }
}
