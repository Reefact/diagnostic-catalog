using System.IO.Compression;

namespace CatalogGen;

// Reading analyzer assemblies out of a .nupkg, wherever that file came from.
//
// It is shared rather than duplicated because "downloaded from a feed" and "already on disk" differ
// only in how the file arrived: once there is a .nupkg, the layout question is the same one, and it
// is the layout question that is subtle enough to be worth having in one place.
internal static class NupkgReader
{
    // Extracts this language's analyzer assemblies into workDir and returns their paths, or null
    // when the package carries none at all — which is a failure, not an empty result.
    internal static IReadOnlyList<string>? ExtractAnalyzerAssemblies(
        string nupkgPath, string label, string workDir, string language)
    {
        ZipArchive zip;
        // A file that is not a readable package is a plausible input rather than a defect here: a
        // download cut short, a Git LFS pointer checked out without its content, the wrong path.
        // Left to the run's catch-all it surfaces as "InvalidDataException: End of Central Directory
        // record could not be found", which names the format and not the file.
        try { zip = ZipFile.OpenRead(nupkgPath); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"{nupkgPath} is not a readable package ({ex.GetType().Name}: {ex.Message})");

            return null;
        }

        using ZipArchive _ = zip;

        List<ZipArchiveEntry>? entries = SelectAnalyzerAssemblies(zip, label, language);
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
        // the last one read wins. The assemblies were once re-enumerated with Directory.GetFiles,
        // whose order .NET explicitly does not guarantee — so which descriptor won could depend on
        // the filesystem underneath. A generated catalogue is required to be the same bytes on a
        // maintainer's laptop and on the nightly runner, so the order it is read in has to be a
        // property of the package, not of the disk.
        return [.. paths.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal)];
    }

    private static List<ZipArchiveEntry>? SelectAnalyzerAssemblies(ZipArchive zip, string label, string language)
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
                $"no analyzer assemblies under analyzers/ in {label}. " +
                "If this is a metapackage, point at the one that actually carries them.");

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
