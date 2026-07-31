using System.Text.Json;

namespace CatalogGen;

// Whether a .deps.json beside an analyzer is worth running the worker against.
//
// The handoff is not additive. `dotnet exec --depsfile` REPLACES the host's dependency graph, so a
// graph that does not carry Roslyn does not leave the worker with its own — it leaves it with none,
// and the worker's own Microsoft.CodeAnalysis reference stops resolving before it has read a thing.
// Nor does --additionalprobingpath rescue it: probing paths are searched in the NuGet package layout
// (<path>/<id>/<version>/<asset>), so the worker's flat output directory is not a place a bare
// assembly name can be found.
//
// So the question is not "does this assembly have a graph" but "does its graph supply what the
// worker needs to read it". Only then is the handoff a gain — an analyzer compiled against a
// different Roslyn read through its own. Otherwise it can only subtract.
internal static class DependencyGraph
{
    private const string Roslyn = "Microsoft.CodeAnalysis.dll";

    internal static bool SuppliesRoslyn(string depsJsonPath)
    {
        try
        {
            using FileStream stream = File.OpenRead(depsJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);

            // A .deps.json declares one library set per target framework, and the question is asked
            // of the file rather than of a target: any of them carrying Roslyn is enough, because
            // the worker resolves against whichever one it is launched for.
            return document.RootElement.TryGetProperty("targets", out JsonElement targets)
                && targets.EnumerateObject()
                          .Where(target => target.Value.ValueKind == JsonValueKind.Object)
                          .SelectMany(target => target.Value.EnumerateObject())
                          .Any(CarriesRoslyn);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                             or JsonException)
        {
            // A graph that cannot be read cannot be judged, and an unjudged graph is not handed over.
            // Refusing to use it costs the analyzer's own Roslyn; using it risks the worker's.
            return false;
        }
    }

    // Whether one library entry lays a Roslyn assembly down at runtime. An entry with no "runtime"
    // section contributes nothing to the worker's resolution, whatever else it declares.
    private static bool CarriesRoslyn(JsonProperty library)
    {
        if (library.Value.ValueKind != JsonValueKind.Object) return false;
        if (!library.Value.TryGetProperty("runtime", out JsonElement runtime)) return false;
        if (runtime.ValueKind != JsonValueKind.Object) return false;

        // The key is a path inside the package, so the file name is what identifies the assembly:
        // lib/netstandard2.0/Microsoft.CodeAnalysis.dll.
        return runtime.EnumerateObject()
                      .Any(asset => Path.GetFileName(asset.Name).Equals(Roslyn, StringComparison.OrdinalIgnoreCase));
    }
}
