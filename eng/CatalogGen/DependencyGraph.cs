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

            if (!document.RootElement.TryGetProperty("targets", out JsonElement targets)) return false;

            foreach (JsonProperty target in targets.EnumerateObject())
            {
                if (target.Value.ValueKind != JsonValueKind.Object) continue;

                foreach (JsonProperty library in target.Value.EnumerateObject())
                {
                    if (library.Value.ValueKind != JsonValueKind.Object) continue;
                    if (!library.Value.TryGetProperty("runtime", out JsonElement runtime)) continue;
                    if (runtime.ValueKind != JsonValueKind.Object) continue;

                    foreach (JsonProperty asset in runtime.EnumerateObject())
                    {
                        // The key is a path inside the package, so the file name is what identifies
                        // the assembly: lib/netstandard2.0/Microsoft.CodeAnalysis.dll.
                        if (Path.GetFileName(asset.Name).Equals(Roslyn, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                             or JsonException)
        {
            // A graph that cannot be read cannot be judged, and an unjudged graph is not handed over.
            // Refusing to use it costs the analyzer's own Roslyn; using it risks the worker's.
            return false;
        }
    }
}
