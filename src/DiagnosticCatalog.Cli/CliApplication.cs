using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Spectre.Console.Cli;

namespace DiagnosticCatalog.Cli;

/// <summary>
/// Builds and runs the <c>dcat</c> command tree.
/// </summary>
/// <remarks>
/// It exists as a type rather than as the body of <c>Program.cs</c> so a test can reach it: a
/// top-level program's statements are reachable only by launching a process, and the exit code a
/// bad command line produces is part of what this tool publishes.
/// </remarks>
internal static class CliApplication
{
    internal static Task<int> RunAsync(string[] args)
    {
        CommandApp app = new();
        app.Configure(Configure);

        return app.RunAsync(args);
    }

    private static void Configure(IConfigurator config)
    {
        config.SetApplicationName("dcat");

        // So `dcat --version` answers the question everybody asks it — which version of the tool is
        // installed. The upstream release a catalogue mirrors is --package-version, on the command
        // that reads one.
        config.SetApplicationVersion(
            typeof(CliApplication).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                  ?.InformationalVersion ?? "0.0.0-dev");

        config.SetExceptionHandler(HandleUncaught);

        // An argument the command tree does not declare is refused rather than collected. The parser
        // gathers such a token into the remaining arguments, which this tool never reads — so a
        // mistyped flag would be accepted, ignored, and reported as a success, and a nightly job
        // asking for something the tool does not do would be told it had it.
        //
        // The parser's own strict mode (UseStrictParsing) would say the same thing and cannot be
        // used: in Spectre.Console.Cli 0.55 it makes an option declared without a value swallow the
        // internal "__default_command" token as that value, so `dcat generate --output` looks for a
        // file by that name instead of reporting a usage error. Refusing the leftovers here keeps
        // the diagnosis and leaves the parser's handling of a missing value intact.
        config.SetInterceptor(new RefuseUndeclaredArguments());

        config.AddCommand<GenerateCommand>("generate")
              .WithDescription("Generate a catalogue from a NuGet package or from analyzer assemblies on disk.");
    }

    /// <summary>
    /// Answers for whatever escapes the command tree, and keeps that answer inside
    /// <see cref="ExitCodes"/>.
    /// </summary>
    /// <remarks>
    /// Without a handler the parser's own failure path returns a value in no exit-code table. A
    /// wrong command line is a usage error, which <see cref="ExitCodes.UsageError"/> names; anything
    /// else reaching here is a failure the command did not catch, and reports as one rather than
    /// borrowing the usage code.
    /// </remarks>
    private static int HandleUncaught(Exception exception, ITypeResolver? resolver)
    {
        _ = resolver;

        // CommandRuntimeException is in the list because it is what a settings validation failure
        // surfaces as, and "you named no source" is the same kind of answer as "you named a switch
        // that does not exist": the invocation is wrong and no retry will fix it. Left out, the two
        // answered with different codes for the same class of mistake, which is precisely the
        // distinction a caller branches on. The type also covers a failure to resolve a command's
        // own type — unreachable here, since the tree registers no types and takes no registrar.
        bool usage = exception is CommandParseException or CommandTemplateException
                                  or CommandConfigurationException or CommandRuntimeException
                                  or UndeclaredArgumentException;

        Console.Error.WriteLine($"error: {exception.Message}");
        if (usage) Console.Error.WriteLine("Run 'dcat --help' to see the available commands.");

        return usage ? ExitCodes.UsageError : ExitCodes.Failure;
    }

    /// <summary>
    /// Refuses a command line carrying an argument no command declares, before the command runs.
    /// </summary>
    private sealed class RefuseUndeclaredArguments : ICommandInterceptor
    {
        public void Intercept(CommandContext context, CommandSettings settings)
        {
            IReadOnlyList<string> undeclared =
                [.. context.Remaining.Raw, .. context.Remaining.Parsed.Select(pair => pair.Key)];
            if (undeclared.Count == 0) return;

            throw new UndeclaredArgumentException(undeclared[0]);
        }
    }
}

/// <summary>
/// Raised when the command line carries an argument the command tree does not declare. It is the
/// tool's own usage refusal rather than the parser's, so it names the offending argument and
/// nothing else.
/// </summary>
[SuppressMessage("Minor Code Smell", "S3871:Exception types should be \"public\"",
                 Justification =
                     "The rule exists so a caller outside the assembly can catch the exception. This assembly is " +
                     "an executable: nothing references it, and the only code that catches this is the exit-code " +
                     "handler a few lines above. Making it public would advertise a type to callers that cannot exist.")]
internal sealed class UndeclaredArgumentException : Exception
{
    internal UndeclaredArgumentException(string argument) : base($"Unknown argument '{argument}'.")
    {
    }
}
