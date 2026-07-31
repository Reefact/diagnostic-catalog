namespace DiagnosticCatalog.Cli;

/// <summary>
/// The exit codes every <c>dcat</c> command returns, named once.
/// </summary>
/// <remarks>
/// They are a contract rather than an internal detail: a nightly job branches on them, the README
/// documents them, and the command tests assert them — so the set is closed, and a command
/// answering with a number outside it would be a defect no compiler could see.
/// </remarks>
internal static class ExitCodes
{
    /// <summary>The command did what it was asked.</summary>
    internal const int Success = 0;

    /// <summary>
    /// The command ran and could not finish: an upstream package that would not resolve, an
    /// analyzer the reader could not construct, an output path it could not write.
    /// </summary>
    internal const int Failure = 1;

    /// <summary>
    /// The command line could not be parsed: an unknown command, a malformed option, an argument
    /// the settings reject. Distinct from <see cref="Failure"/> so a pipeline can tell "this
    /// invocation is wrong" — which a retry will never fix — from "the tool ran and could not
    /// finish". <c>64</c> is <c>EX_USAGE</c>, the conventional value for a command-line usage error.
    /// </summary>
    internal const int UsageError = 64;
}
