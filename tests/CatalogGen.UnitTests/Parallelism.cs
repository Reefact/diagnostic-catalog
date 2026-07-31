using Xunit;

// This assembly's tests run one at a time, and the reason is Console.Out.
//
// CatalogGen is a command-line tool: reporting progress on the console is what it does, so almost
// every code path a test here exercises writes to it. One test class captures that output to assert
// on it — MirrorBannerTests.Capture — by installing a StringWriter as Console.Out and disposing it
// afterwards.
//
// Console.Out is process-global, and xUnit runs test CLASSES in parallel by default. So another
// class can resolve Console.Out to that StringWriter, be pre-empted, and complete its write after
// the capturing test has disposed it:
//
//     System.ObjectDisposedException : Cannot write to a closed TextWriter.
//        at System.IO.StringWriter.Write(String value)
//        at System.Console.WriteLine(String value)
//        at CatalogGen.CatalogParser.ReadPrevious(...)
//
// Observed on the windows-latest leg of CI, on a pull request that touched none of it — a race
// surfaces on timing, which is exactly why it cannot be left to be noticed again later.
//
// Serialising the assembly rather than pairing up the two classes involved is deliberate: the shared
// resource is a process-wide global that any test here can reach, so a [Collection] naming today's
// two participants would go stale the moment a third test called into the generator. The cost is a
// few seconds on a suite this size.
//
// The alternative worth knowing about is threading a TextWriter through CatalogGen instead of using
// Console directly, which would remove the global. That is a change to production code for a test's
// benefit, and a larger one than it looks: every progress line in the generator would take a
// parameter. Left alone on purpose.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
