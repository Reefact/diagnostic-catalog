using System.Text.Json;

namespace CatalogGen;

// Acquisition from a NuGet feed: resolve the release, fetch the package, and hand the reader the
// analyzer assemblies it carries for one language.
//
// Everything here is a property of how a FEED works — the flat-container URLs, the "latest means
// latest stable" policy. None of it is a property of reading descriptors, which is why it lives on
// this side of AnalyzerAssemblySet; and the layout question, which a local .nupkg asks in exactly
// the same words, lives in NupkgReader rather than here.
internal static class NuGetPackageSource
{
    private const string FlatContainer = "https://api.nuget.org/v3-flatcontainer";

    internal static async Task<AnalyzerAssemblySet?> AcquireAsync(
        string packageId, string requestedVersion, string language, string workDir, HttpClient http,
        CancellationToken cancellation = default)
    {
        string? version = await ResolveVersionAsync(packageId, requestedVersion, http, cancellation);
        if (version is null) return null;

        string nupkg = Path.Combine(workDir, "package.nupkg");
        string url = $"{FlatContainer}/{packageId.ToLowerInvariant()}/{version}/" +
                     $"{packageId.ToLowerInvariant()}.{version}.nupkg";
        Console.WriteLine($"downloading {url}");
        await using (Stream s = await http.GetStreamAsync(url, cancellation))
        await using (FileStream f = File.Create(nupkg))
            await s.CopyToAsync(f, cancellation);

        IReadOnlyList<string>? paths =
            NupkgReader.ExtractAnalyzerAssemblies(nupkg, $"{packageId} {version}", workDir, language);

        return paths is null ? null : new AnalyzerAssemblySet(paths, packageId, version);
    }

    // The release to mirror: the requested one, or the newest matching what "latest" is allowed to
    // mean. Null when the package has none that qualifies.
    private static async Task<string?> ResolveVersionAsync(
        string packageId, string requested, HttpClient http, CancellationToken cancellation)
    {
        if (requested is not ("latest" or "latest-any")) return requested;

        string index = await http.GetStringAsync($"{FlatContainer}/{packageId.ToLowerInvariant()}/index.json",
                                                 cancellation);
        List<string> all = JsonDocument.Parse(index)
            .RootElement.GetProperty("versions").EnumerateArray()
            .Select(v => v.GetString()!).ToList();

        // "latest" means latest *stable*. A catalogue mirrors a release people actually
        // consume; resolving to a preview would silently pin the catalogue to one.
        List<string> candidates = requested == "latest" ? all.Where(v => !v.Contains('-')).ToList() : all;
        if (candidates.Count == 0)
        {
            // S6966 asks for WriteLineAsync here. Console.WriteLine is a static method with no async
            // counterpart, while Console.Error is a TextWriter that has one — but both streams are
            // synchronized writers whose async overloads complete synchronously, so awaiting would
            // yield to nothing and leave this tool's diagnostics half-async on a technicality of
            // where the method happens to be declared.
#pragma warning disable S6966 // Awaitable method should be used
            Console.Error.WriteLine($"{packageId} has no stable version; use latest-any or an explicit version");
#pragma warning restore S6966
            return null;
        }

        string resolved = candidates[^1];
        Console.WriteLine($"resolved {packageId} => {resolved}" +
                          (resolved == all[^1] ? "" : $" (latest overall is {all[^1]}, a prerelease)"));

        return resolved;
    }
}
