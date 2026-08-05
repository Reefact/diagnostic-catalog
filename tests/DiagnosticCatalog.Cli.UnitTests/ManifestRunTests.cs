using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CatalogGen;
using Xunit;

namespace DiagnosticCatalog.Cli.UnitTests;

/// <summary>
/// <c>--manifest</c> as a run rather than as a command line: the file actually read.
/// </summary>
/// <remarks>
/// <para>
/// <c>GenerateSettingsTests</c> covers thoroughly which switches a manifest refuses beside it, and
/// stops where the parser does. Nothing then read one. So the step between an accepted command line
/// and a run — open the file, parse it, turn it into catalogues, and report a file that cannot be
/// turned into any — had no test at all, on the one input this tool takes that a human edits by
/// hand and that no compiler checks.
/// </para>
/// <para>
/// What is asserted here is mostly the SHAPE of the refusals. A manifest is where a typo lands, and
/// the tool goes to some trouble to answer one with a line naming the file and the entry rather than
/// a stack trace — trouble that is invisible to a test that only reads the exit code, and that a
/// later edit could undo without anything saying so.
/// </para>
/// </remarks>
public sealed class ManifestRunTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("dcat-manifest-").FullName;

    public void Dispose() => Directory.Delete(_work, recursive: true);

    /// <summary>Writes a manifest into the working directory and returns its full path.</summary>
    private async Task<string> ManifestAsync(string json)
    {
        string path = Path.Combine(_work, "catalogs.json");
        await File.WriteAllTextAsync(path, json);

        return path;
    }

    /// <summary>An entry reading an assembly on disk — offline, and the same source the validate suite uses.</summary>
    private static string Entry(string @namespace, string container, string output) => $$"""
        {
          "assemblies": [{{JsonSerializer.Serialize(typeof(CatalogRun).Assembly.Location)}}],
          "namespace": "{{@namespace}}",
          "container": "{{container}}",
          "output": "{{output}}",
          "sourceName": "Acme",
          "sourceVersion": "1.0.0"
        }
        """;

    /// <summary>The entries wrapped in the one key a manifest must carry.</summary>
    private static string Catalogs(params string[] entries)
        => "{ \"catalogs\": [" + string.Join(",", entries) + "] }";

    [Fact]
    public async Task A_manifest_is_read_into_every_catalogue_it_declares()
    {
        // Two entries rather than one: the count is what a manifest is FOR, and a reader that
        // stopped after the first would pass every single-entry test there is.
        string manifest = await ManifestAsync(Catalogs(
            Entry("Vendor.One", "OneRules", "one.g.cs"),
            Entry("Vendor.Two", "TwoRules", "two.g.cs")));

        (int exitCode, string output, _) = await CliRun.Async("generate", "--manifest", manifest);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("2 catalogue(s)", output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_work, "one.g.cs")));
        Assert.True(File.Exists(Path.Combine(_work, "two.g.cs")));
    }

    [Fact]
    public async Task A_manifest_resolves_its_paths_against_itself_rather_than_the_caller()
    {
        // The property that lets the tool be run from anywhere. "one.g.cs" above is written beside
        // the manifest, and the assertion is that it is NOT written beside the caller — which is
        // where a manifest read with the process's directory would have put it.
        string manifest = await ManifestAsync(Catalogs(Entry("Vendor.One", "OneRules", "one.g.cs")));

        Assert.Equal(ExitCodes.Success, (await CliRun.Async("generate", "--manifest", manifest)).ExitCode);

        Assert.True(File.Exists(Path.Combine(_work, "one.g.cs")));
        Assert.False(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "one.g.cs")));
    }

    [Fact]
    public async Task A_manifest_drives_validate_as_well_as_generate()
    {
        // The two commands share one reader, which is the reason it is a type of its own. A validate
        // that could not read a manifest would leave every manifest-driven catalogue uncheckable.
        string manifest = await ManifestAsync(Catalogs(Entry("Vendor.One", "OneRules", "one.g.cs")));

        Assert.Equal(ExitCodes.Success, (await CliRun.Async("generate", "--manifest", manifest)).ExitCode);

        (int exitCode, string output, _) = await CliRun.Async("validate", "--manifest", manifest);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("RESULT: current", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_entry_that_is_incomplete_names_the_file_and_the_entry_once()
    {
        // The ManifestException arm, and the "would say it twice" it is written for: the exception
        // already names the file, so the reporting must NOT prefix the path again. Asserted by
        // passing an absolute path and requiring the message not to carry it — a re-prefix would be
        // invisible to an assertion that only looked for the file name, which appears either way.
        string manifest = await ManifestAsync(
            """{ "catalogs": [ { "assemblies": ["a.dll"], "namespace": "N", "container": "C" } ] }""");

        (int exitCode, _, string error) = await CliRun.Async("generate", "--manifest", manifest);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.StartsWith("error: catalogs.json: catalogs[0]:", error.Trim(), StringComparison.Ordinal);
        Assert.DoesNotContain(_work, error, StringComparison.Ordinal);
        Assert.Contains("\"output\" is missing", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_manifest_declaring_no_catalogue_is_refused_rather_than_run_as_nothing()
    {
        // A run that generated nothing and reported success is the failure with no symptom: the
        // scheduled job goes green and every catalogue it was supposed to refresh stays as it was.
        string manifest = await ManifestAsync("""{ "catalogs": [] }""");

        (int exitCode, _, string error) = await CliRun.Async("generate", "--manifest", manifest);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("declares no entry", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_manifest_that_does_not_parse_is_reported_on_one_line()
    {
        // The JsonException arm. A hand-edited file's likeliest fault, and the one where a stack
        // trace would bury the only thing the caller needs — which file, and roughly where.
        string manifest = await ManifestAsync("""{ "catalogs": [ { """);

        (int exitCode, _, string error) = await CliRun.Async("generate", "--manifest", manifest);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.StartsWith("error: ", error.Trim(), StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_manifest_that_is_not_there_names_the_path_the_caller_typed()
    {
        // The IOException arm. The path is prefixed here — unlike a ManifestException, the framework's
        // message names the file it could not find but not the switch that asked for it.
        string manifest = Path.Combine(_work, "no-such-manifest.json");

        (int exitCode, _, string error) = await CliRun.Async("generate", "--manifest", manifest);

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("no-such-manifest.json", error, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", error, StringComparison.Ordinal);
    }
}
