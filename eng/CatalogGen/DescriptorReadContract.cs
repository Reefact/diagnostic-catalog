using System.Text.Json.Serialization;

namespace CatalogGen;

// The wire between the engine and the worker process that reads descriptors for it.
//
// It is JSON in two files rather than arguments and stdout, for two reasons. A set of assembly
// paths has no length limit worth betting a command line on, and the worker's stdout is already
// spoken for: it carries the run's diagnostics through to the caller's console verbatim, which is
// what keeps the log identical to the days when reading happened in this process.
//
// Deliberately narrow. Everything the worker knows that the engine needs fits in a rule's three
// values; everything else it knows — which analyzer would not construct, which assembly would not
// load — it reports itself and answers for with its exit code.

internal sealed class DescriptorReadRequest
{
    [JsonPropertyName("assemblyPaths")]
    public List<string> AssemblyPaths { get; set; } = [];
}

internal sealed class DescriptorReadResponse
{
    [JsonPropertyName("rules")]
    public Dictionary<string, ReadRule> Rules { get; set; } = [];
}

// Retired is absent on purpose: it is not something a descriptor declares. A rule becomes retired
// by disappearing from what the reader found, which only the emitter — comparing against the
// previous run — is in a position to notice.
internal sealed class ReadRule
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("helpLinkUri")]
    public string HelpLinkUri { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";
}

// What the worker's exit code means. The engine maps anything non-zero onto the same refusal it
// used to produce in-process, so the guard against an incomplete read survives the move.
internal static class WorkerExitCodes
{
    internal const int Complete = 0;
    internal const int IncompleteRead = 1;
    internal const int UsageError = 2;
}
