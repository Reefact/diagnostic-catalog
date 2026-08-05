using System;
using System.IO;
using System.Threading.Tasks;
using Spectre.Console;

namespace DiagnosticCatalog.Cli.UnitTests;

/// <summary>
/// Runs the real command tree with <see cref="Console.Out"/> and <see cref="Console.Error"/>
/// captured, and hands back what the tool answered together with what it printed.
/// </summary>
/// <remarks>
/// <para>
/// One copy, because the capture is not the two lines it looks like. What a command PRINTS is part
/// of its contract — `list` and `explain` produce nothing else, and `validate` states its verdict on
/// a line before returning it — so several suites need this, and a second copy would be a second
/// place for the hazard below to be got wrong.
/// </para>
/// <para>
/// Nothing installed here is ever CLOSED, and that is the whole of what keeps these suites green.
/// A redirected writer outlives the redirection: something downstream holds the one that was
/// installed first, so closing it makes the next command that writes fail inside the tool with
/// "Cannot write to a closed TextWriter" — measured, and reported by `dcat` as exit code 1 on
/// `--help`, which turned two tests that touch none of this red about one run in three depending on
/// the order the classes happened to run in. A StringWriter holds no unmanaged resource, so leaving
/// it open costs a few hundred bytes for the life of the run and removes the failure mode outright.
/// </para>
/// <para>
/// Spectre's console is swapped as well as Console.Out, so that what the parser renders — usage
/// errors, help — lands in the capture rather than on the real console. Its previous value is read
/// BEFORE the redirection: <see cref="AnsiConsole.Console"/> is a process-wide singleton whose
/// getter initialises it on first read, so reading it here binds it to the real console and makes
/// the restore in the finally sound.
/// </para>
/// </remarks>
internal static class CliRun
{
    internal static async Task<(int ExitCode, string Out, string Error)> Async(params string[] args)
    {
        TextWriter previousOut = Console.Out;
        TextWriter previousError = Console.Error;
        IAnsiConsole previousAnsi = AnsiConsole.Console;

        StringWriter captured = new();
        StringWriter capturedError = new();
        try
        {
            Console.SetOut(captured);
            Console.SetError(capturedError);
            AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(captured) });

            int exitCode = await CliApplication.RunAsync(args);

            return (exitCode, captured.ToString(), capturedError.ToString());
        }
        finally
        {
            AnsiConsole.Console = previousAnsi;
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }
}
