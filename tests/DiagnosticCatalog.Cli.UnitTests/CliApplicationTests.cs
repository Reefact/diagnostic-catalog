using System.Threading.Tasks;
using Xunit;

namespace DiagnosticCatalog.Cli.UnitTests;

/// <summary>
/// The exit codes <c>dcat</c> answers with, exercised through the real command tree.
/// </summary>
/// <remarks>
/// They are a contract: a nightly job branches on them, and a pipeline needs to tell "this
/// invocation is wrong", which no retry will fix, from "the tool ran and could not finish". The
/// command tree lives in a type rather than in top-level statements precisely so this can be
/// asserted without launching a process.
/// </remarks>
public sealed class CliApplicationTests
{
    [Fact]
    public async Task Help_is_offered_and_succeeds()
    {
        // The parser it replaced had none at all: `--help` was read as an unknown switch, printed
        // the usage text and exited non-zero. A published tool whose --help reports failure is the
        // first thing its first user meets.
        Assert.Equal(ExitCodes.Success, await CliApplication.RunAsync(["--help"]));
    }

    [Fact]
    public async Task An_unknown_command_is_a_usage_error()
        => Assert.Equal(ExitCodes.UsageError, await CliApplication.RunAsync(["fabricate"]));

    [Fact]
    public async Task An_undeclared_argument_is_refused_rather_than_collected()
    {
        // Spectre gathers a token no command declares into the remaining arguments, which this tool
        // never reads — so without the interceptor a mistyped flag is accepted, ignored, and
        // reported as a success.
        int exitCode = await CliApplication.RunAsync(
        [
            "generate", "--colour", "blue",
            "--package", "SonarAnalyzer.CSharp",
            "--namespace", "N", "--container", "C", "--output", "o.g.cs",
        ]);

        Assert.Equal(ExitCodes.UsageError, exitCode);
    }

    [Fact]
    public async Task A_command_line_naming_no_source_is_a_usage_error()
        => Assert.Equal(ExitCodes.UsageError, await CliApplication.RunAsync(["generate"]));

    [Fact]
    public async Task Validate_is_offered_alongside_generate()
    {
        // The tree is what --help lists, so a verb that is registered but unreachable would look
        // present and never run.
        Assert.Equal(ExitCodes.Success, await CliApplication.RunAsync(["validate", "--help"]));
        Assert.Equal(ExitCodes.Success, await CliApplication.RunAsync(["list", "--help"]));
        Assert.Equal(ExitCodes.Success, await CliApplication.RunAsync(["explain", "--help"]));
    }

    [Fact]
    public async Task Validate_refuses_a_command_line_naming_no_source()
        => Assert.Equal(ExitCodes.UsageError, await CliApplication.RunAsync(["validate"]));

    [Fact]
    public async Task Validate_takes_no_date_because_none_could_change_its_answer()
    {
        // A catalogue's generation date is precisely the field that moves without any rule moving,
        // so a switch that set it could not affect whether the catalogue is current. Accepting it
        // would suggest otherwise.
        int exitCode = await CliApplication.RunAsync(
            ["validate", "--date", "2026-01-01", "--package", "X",
             "--namespace", "N", "--container", "C", "--output", "o.g.cs"]);

        Assert.Equal(ExitCodes.UsageError, exitCode);
    }

    [Fact]
    public async Task Reading_a_catalogue_that_is_not_there_fails_rather_than_reporting_nothing()
        => Assert.Equal(ExitCodes.Failure, await CliApplication.RunAsync(["list", "no-such-catalogue.dll"]));

    [Fact]
    public async Task A_command_line_naming_two_sources_is_a_usage_error()
    {
        // Refused rather than resolved by precedence: each names a source, and picking one silently
        // would generate a catalogue from something the caller did not ask for.
        int exitCode = await CliApplication.RunAsync(
            ["generate", "--project", "a.csproj", "--assembly", "a.dll",
             "--namespace", "N", "--container", "C", "--output", "o.g.cs"]);

        Assert.Equal(ExitCodes.UsageError, exitCode);
    }

    [Fact]
    public async Task A_configuration_without_a_project_is_a_usage_error()
    {
        // It selects among a project's build outputs, and nothing else has any. Accepted and
        // discarded, it would have been passed by somebody who believed it chose what was read.
        int exitCode = await CliApplication.RunAsync(
            ["generate", "--package", "P", "--configuration", "Debug",
             "--namespace", "N", "--container", "C", "--output", "o.g.cs"]);

        Assert.Equal(ExitCodes.UsageError, exitCode);
    }

    [Fact]
    public async Task A_project_that_is_not_there_fails_rather_than_generating_an_empty_catalogue()
    {
        // Failure, not usage: the command line is well formed, and it is the project that is absent.
        // A pipeline branches on the difference — no retry fixes a usage error.
        int exitCode = await CliApplication.RunAsync(
            ["generate", "--project", "no-such-project.csproj",
             "--namespace", "N", "--container", "C", "--output", "o.g.cs"]);

        Assert.Equal(ExitCodes.Failure, exitCode);
    }

    [Fact]
    public async Task An_option_left_without_a_value_is_a_usage_error()
    {
        // The pair-reading parser this replaced stopped when fewer than two arguments remained, so
        // a switch left dangling at the end was neither applied nor reported.
        Assert.Equal(ExitCodes.UsageError, await CliApplication.RunAsync(["generate", "--output"]));
    }
}
