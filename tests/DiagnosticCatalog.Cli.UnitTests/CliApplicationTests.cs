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
    public async Task An_option_left_without_a_value_is_a_usage_error()
    {
        // The pair-reading parser this replaced stopped when fewer than two arguments remained, so
        // a switch left dangling at the end was neither applied nor reported.
        Assert.Equal(ExitCodes.UsageError, await CliApplication.RunAsync(["generate", "--output"]));
    }
}
