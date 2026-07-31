using System.Diagnostics;
using System.Globalization;

namespace CatalogGen;

// How long a child process is given before it is killed and reported.
//
// Both things this engine spawns — the descriptor worker and MSBuild — were awaited with an
// unbounded WaitForExit. A child that wedges then takes the tool with it: no output, no exit code,
// and a pipeline that runs until the runner's own timeout kills it with nothing to read. Refusing
// late is this repository's rule; refusing never is not a variant of it.
internal static class ChildProcess
{
    internal const string Override = "DCAT_TIMEOUT_SECONDS";

    // Measured rather than guessed, and then set far past the measurement. A full descriptor read of
    // the largest catalogue here — SonarAnalyzer.CSharp, 448 analyzers and 465 descriptors — takes
    // about eleven seconds including the download, and an MSBuild evaluation under two. A budget is
    // not a performance expectation: it is the point past which a process is not slow but stuck, and
    // it sits far enough out that a loaded runner never arrives there.
    internal static readonly TimeSpan DescriptorRead = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan ProjectEvaluation = TimeSpan.FromMinutes(2);

    /// The budget to apply, honouring <see cref="Override"/> when it is set.
    internal static TimeSpan Budget(TimeSpan fallback)
        => Budget(fallback, Environment.GetEnvironmentVariable(Override));

    // Split from the environment so the parsing is testable without a test mutating the process's
    // environment underneath every other test running beside it.
    internal static TimeSpan Budget(TimeSpan fallback, string? declared)
    {
        if (string.IsNullOrEmpty(declared)) return fallback;

        if (int.TryParse(declared, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)
            && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        // Reported rather than ignored: it was set by somebody who believed it took effect, and a
        // budget that silently reverted is exactly the kind of thing found out much later.
        Console.Error.WriteLine(
            $"{Override}=\"{declared}\" is not a positive whole number of seconds; " +
            $"using the default of {fallback.TotalSeconds:0}.");

        return fallback;
    }

    /// <summary>
    /// Waits for <paramref name="process"/> within <paramref name="budget"/>, killing its whole tree
    /// and reporting when it does not finish. True when it exited on its own.
    /// </summary>
    /// <remarks>
    /// The tree, not the process. MSBuild spawns worker nodes and the descriptor worker is itself
    /// started through the <c>dotnet</c> host, so killing the one this engine holds a handle to
    /// would leave the ones actually doing the work behind.
    /// </remarks>
    internal static bool WaitOrKill(Process process, TimeSpan budget, string what)
    {
        // WaitForExit(int) can report an exit before the redirected streams have been drained; the
        // parameterless overload is what guarantees they have. Both calls are needed, in this order.
        if (process.WaitForExit(Milliseconds(budget)))
        {
            process.WaitForExit();

            return true;
        }

        try
        {
            process.Kill(entireProcessTree: true);

            // So the caller can rely on it being gone rather than merely asked to go.
            process.WaitForExit();
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                             or System.ComponentModel.Win32Exception
                                             or NotSupportedException)
        {
            // It exited between the wait expiring and the kill, or the platform refused. Either way
            // the run is over and the message below is still the right one.
        }

        Console.Error.WriteLine(
            $"{what} did not finish within {budget.TotalSeconds:0} seconds and was stopped. " +
            $"Set {Override} to a number of seconds to give it longer.");

        return false;
    }

    // WaitForExit takes milliseconds as an int, which the override can outrun: it accepts any
    // positive int of SECONDS, so 68 years is expressible and about 24 days of milliseconds is not.
    //
    // Written out rather than left to the cast. .NET Core saturates a double that will not fit —
    // measured, (int)TimeSpan.FromDays(365).TotalMilliseconds is int.MaxValue — so the cast alone
    // would in fact be correct here. It is spelled anyway because the value it would otherwise
    // produce is a contract of the runtime rather than of this code, and .NET Framework, which this
    // repository still targets elsewhere, gave int.MinValue for the same expression. A negative
    // budget is not a short wait: WaitForExit throws on one.
    private static int Milliseconds(TimeSpan budget)
        => budget.TotalMilliseconds >= int.MaxValue ? int.MaxValue : (int)budget.TotalMilliseconds;
}
