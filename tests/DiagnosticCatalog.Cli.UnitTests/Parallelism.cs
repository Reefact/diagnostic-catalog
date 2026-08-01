using Xunit;

// This assembly's tests run one at a time, and the reason is Console.Out.
//
// `dcat` reports to the console: that IS what `list` and `explain` do, so a test that asserts what
// they print has to capture Console.Out and Console.Error by installing writers of its own and
// restoring them afterwards.
//
// Console.Out is process-global and xUnit runs test CLASSES in parallel by default, so another
// class can resolve Console.Out to a captured writer, be pre-empted, and complete its write after
// the capturing test has restored the original — an ObjectDisposedException on a test that touched
// none of it. CatalogGen.UnitTests serialises itself for exactly this reason, on a race that was
// observed on CI rather than reasoned about; this suite now writes to the same global and takes the
// same precaution before it has to learn it the same way.
//
// Serialising the assembly rather than pairing up today's participants is deliberate: the shared
// resource is process-wide and any test here can reach it through the command tree, so a
// [Collection] naming the current classes would go stale the moment another test ran a verb.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
