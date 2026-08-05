using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CatalogGen;
using Xunit;

namespace DiagnosticCatalog.Cli.UnitTests;

/// <summary>
/// <c>--summary</c>, the file both commands write for a pull request body to carry.
/// </summary>
/// <remarks>
/// <para>
/// It is a FILE contract rather than a message: written as UTF-8 with no byte order mark, with LF
/// line endings whatever the platform, and ending in a newline. Every one of those is spelled out in
/// the two commands and none of them was checked. They are also the kind that fails silently — a BOM
/// or a CR reaches a Markdown reader as text that renders very nearly right, and the place this file
/// is read is a pull request body opened by a scheduled job, where nobody is watching.
/// </para>
/// <para>
/// Both commands are exercised for each, because the block that writes it is COPIED between
/// <c>GenerateCommand</c> and <c>ValidateCommand</c> rather than shared. Two copies are two things
/// to keep true, and a test that only ran one of them would say nothing about the other.
/// </para>
/// </remarks>
public sealed class SummaryFileTests : IDisposable
{
    /// <summary>Read offline and deterministically — the same source the validate suite uses.</summary>
    private static string Source => typeof(CatalogRun).Assembly.Location;

    private readonly string _work = Directory.CreateTempSubdirectory("dcat-summary-").FullName;

    public void Dispose() => Directory.Delete(_work, recursive: true);

    private string Output => Path.Combine(_work, "catalogue.g.cs");

    private string Summary(string name) => Path.Combine(_work, name);

    private Task<(int ExitCode, string Out, string Error)> RunAsync(
        string command, string sourceVersion, string? summary)
    {
        string[] head =
        [
            command, "--assembly", Source,
            "--source-name", "Acme", "--source-version", sourceVersion,
            "--namespace", "Vendor.Catalog", "--container", "AcmeRules", "--output", Output,
        ];

        return CliRun.Async(summary is null ? head : [.. head, "--summary", summary]);
    }

    /// <summary>
    /// Runs <paramref name="command"/> so that it has something to report, and returns the summary
    /// it wrote.
    /// </summary>
    /// <remarks>
    /// The two commands reach a non-empty report from opposite directions: a generation describes
    /// the catalogue it just wrote, so it needs none on disk, while a validation describes a
    /// catalogue that drifted, so it needs one recorded at an earlier release. Both then produce a
    /// heading, which is what the file-shape assertions below are about.
    /// </remarks>
    private async Task<string> ReportedAsync(string command, string name)
    {
        if (command == "validate") { await RunAsync("generate", "1.0.0", summary: null); }

        string summary = Summary(name);
        await RunAsync(command, "2.0.0", summary);

        return summary;
    }

    [Fact]
    public async Task Generate_writes_the_summary_where_it_was_told_and_says_so()
    {
        string summary = Summary("generate.md");

        (int exitCode, string output, _) = await RunAsync("generate", "1.0.0", summary);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(summary));
        Assert.Contains("summary written to", output, StringComparison.Ordinal);
        Assert.NotEmpty(await File.ReadAllTextAsync(summary, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Validate_writes_one_too()
    {
        // The second copy of the block. It also runs on the path where nothing is written to the
        // catalogue, which is the whole point of asking `validate` for a report.
        await RunAsync("generate", "1.0.0", summary: null);
        string summary = Summary("validate.md");

        (int exitCode, string output, _) = await RunAsync("validate", "2.0.0", summary);

        Assert.Equal(ExitCodes.OutOfDate, exitCode);
        Assert.True(File.Exists(summary));
        Assert.Contains("summary written to", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Neither_command_writes_one_unless_it_was_asked()
    {
        // The control. Without it every assertion here would pass against a command that wrote the
        // file unconditionally, and a run that named no --summary would litter the caller's tree.
        (int exitCode, string output, _) = await RunAsync("generate", "1.0.0", summary: null);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.DoesNotContain("summary written to", output, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_work, "*.md"));
    }

    [Theory]
    [InlineData("generate")]
    [InlineData("validate")]
    public async Task The_summary_carries_no_byte_order_mark(string command)
    {
        // `new UTF8Encoding(false)`, asserted on the bytes. The default UTF8Encoding emits a BOM,
        // so this is one constructor argument away from being wrong, and the result still opens
        // correctly in most editors — while landing as three stray characters at the top of a pull
        // request body, before the first heading, where they are read as text rather than as markup.
        string summary = await ReportedAsync(command, "bom.md");

        byte[] bytes = await File.ReadAllBytesAsync(summary, TestContext.Current.CancellationToken);

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                     "the summary should be written as UTF-8 with no byte order mark");
    }

    [Theory]
    [InlineData("generate")]
    [InlineData("validate")]
    public async Task The_summary_is_utf8_rather_than_the_platform_encoding(string command)
    {
        // Not a restatement of the test above: a BOM-less file written in the wrong encoding passes
        // that one and mangles this. The report is Markdown carrying an em dash in every catalogue
        // heading, so the failure is visible in the file these commands exist to produce.
        string summary = await ReportedAsync(command, "utf8.md");

        byte[] bytes = await File.ReadAllBytesAsync(summary, TestContext.Current.CancellationToken);

        Assert.Contains("—", new UTF8Encoding(false).GetString(bytes), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("generate")]
    [InlineData("validate")]
    public async Task The_summary_uses_lf_and_ends_with_one(string command)
    {
        // `ReplaceLineEndings("\n")` and the appended newline. Written on Windows without the first,
        // the file would carry CRLF; without the second it would end mid-line, and a body composed
        // by concatenating it with anything else would run the two together.
        string summary = await ReportedAsync(command, "endings.md");

        string text = await File.ReadAllTextAsync(summary, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_run_that_changed_nothing_still_reports_what_it_found()
    {
        // An empty file would be read as "the job did not run". The two commands answer differently
        // on purpose — CatalogRun.Summarise calls it two questions — so both are named here: a
        // generation says the mirror is already what upstream offers, a validation says the
        // catalogue on disk still tells the truth.
        await RunAsync("generate", "1.0.0", summary: null);

        string generated = Summary("unchanged-generate.md");
        await RunAsync("generate", "1.0.0", generated);

        string validated = Summary("unchanged-validate.md");
        await RunAsync("validate", "1.0.0", validated);

        Assert.Contains("No catalogue changed",
                        await File.ReadAllTextAsync(generated, TestContext.Current.CancellationToken),
                        StringComparison.Ordinal);
        Assert.Contains("Every catalogue is current",
                        await File.ReadAllTextAsync(validated, TestContext.Current.CancellationToken),
                        StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_summary_is_accepted_beside_a_manifest()
    {
        // CatalogueSettings leaves --summary out of the switches a manifest carries, on the
        // reasoning that it says what the RUN does rather than what a catalogue is. That decision
        // is written in a comment and was enforced by nothing: adding it to that list would refuse
        // this command line, and a manifest run is exactly the one whose report a scheduled job
        // wants — the case with the most catalogues to describe.
        string manifest = Path.Combine(_work, "catalogs.json");
        await File.WriteAllTextAsync(
            manifest,
            $$"""
              { "catalogs": [ { "assemblies": [{{System.Text.Json.JsonSerializer.Serialize(Source)}}],
                                "namespace": "Vendor.One", "container": "OneRules",
                                "output": "one.g.cs" } ] }
              """,
            TestContext.Current.CancellationToken);
        string summary = Summary("manifest.md");

        (int exitCode, string output, _) = await CliRun.Async(
            "generate", "--manifest", manifest, "--summary", summary);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("summary written to", output, StringComparison.Ordinal);
        Assert.Contains("Vendor.One",
                        await File.ReadAllTextAsync(summary, TestContext.Current.CancellationToken),
                        StringComparison.Ordinal);
    }
}
