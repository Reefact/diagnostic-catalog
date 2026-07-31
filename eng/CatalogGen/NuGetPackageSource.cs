using System.IO.Compression;
using System.Text.Json;

namespace CatalogGen;

// Acquisition from a NuGet feed: resolve the release, fetch the package, and hand the reader the
// analyzer assemblies it carries for one language.
//
// Everything here is a property of how a NUGET PACKAGE is laid out — the flat-container URLs, the
// "latest means latest stable" policy, the analyzers/<lang>/ folder convention. None of it is a
// property of reading descriptors, which is why it lives on this side of AnalyzerAssemblySet and
// why a second acquisition strategy adds a file here rather than a second reader.
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

        using ZipArchive zip = ZipFile.OpenRead(nupkg);
        List<ZipArchiveEntry>? entries = SelectAnalyzerAssemblies(zip, packageId, version, language);
        if (entries is null) return null;

        List<string> paths = [];
        foreach (ZipArchiveEntry e in entries)
        {
            string path = Path.Combine(workDir, Path.GetFileName(e.FullName));
            e.ExtractToFile(path, overwrite: true);
            paths.Add(path);
        }

        // Ordinal, and distinct. Two entries whose leaf names collide extract onto one file, so the
        // list would otherwise name it twice and the reader would count its analyzer types twice.
        //
        // The order matters for a reason worth stating: when two assemblies declare the same rule id,
        // the last one read wins. Until this stage was extracted the assemblies were re-enumerated
        // with Directory.GetFiles, whose order .NET explicitly does not guarantee — so which
        // descriptor won could depend on the filesystem underneath. A generated catalogue is required
        // to be the same bytes on a maintainer's laptop and on the nightly runner, so the order it is
        // read in has to be a property of the package, not of the disk.
        return new AnalyzerAssemblySet(
            [.. paths.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal)],
            packageId,
            version);
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

    // The assemblies in the package that carry this language's descriptors, or null when the package
    // carries none at all — which is a failure, not an empty result.
    private static List<ZipArchiveEntry>? SelectAnalyzerAssemblies(
        ZipArchive zip, string packageId, string version, string language)
    {
        // Satellite assemblies hold localized rule text, never descriptors, and they sit in
        // culture-named folders that would otherwise be mistaken for language folders — note
        // that "cs" is both C# and Czech.
        List<ZipArchiveEntry> candidateDlls = zip.Entries
            .Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Where(e => !e.FullName.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
            .Where(e => e.FullName.StartsWith("analyzers/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidateDlls.Count == 0)
        {
            Console.Error.WriteLine(
                $"no analyzer assemblies under analyzers/ in {packageId} {version}. " +
                "If this is a metapackage, point --package at the one that actually carries them.");
            return null;
        }

        // Layouts differ, and the difference matters. Sonar ships one assembly straight under
        // analyzers/. StyleCop uses analyzers/dotnet/cs/. Microsoft.CodeAnalysis.NetAnalyzers
        // uses BOTH: the language-specific analyzers live under cs/ and vb/, but the bulk of the
        // rules sit in a language-neutral assembly at analyzers/dotnet/.
        //
        // So the rule is to exclude the OTHER languages, never to keep only the requested one:
        // keeping only .../cs/ would silently drop most of the CA rules, and keeping everything
        // would silently absorb Visual Basic rules into a C# catalogue. Both failures are
        // invisible in the output — you would just get a catalogue with the wrong rules in it.
        string[] knownLanguages = ["cs", "vb", "fs"];
        string[] otherLanguages = knownLanguages
            .Where(l => !string.Equals(l, language, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        List<ZipArchiveEntry> excluded = candidateDlls
            .Where(e => otherLanguages.Contains(Naming.ParentDir(e.FullName), StringComparer.OrdinalIgnoreCase))
            .ToList();
        List<ZipArchiveEntry> entries = candidateDlls.Except(excluded).ToList();

        Console.WriteLine($"analyzer assemblies for language '{language}': {entries.Count}");
        foreach (ZipArchiveEntry e in entries) Console.WriteLine($"  + {e.FullName}");
        foreach (ZipArchiveEntry e in excluded) Console.WriteLine($"  - {e.FullName} (other language)");

        return entries;
    }
}
