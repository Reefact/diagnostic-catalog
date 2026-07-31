using System.IO.Compression;
using System.Xml.Linq;

namespace CatalogGen;

// Acquisition from a .nupkg already on disk: a package built locally, fetched by hand, or sitting
// on a share — anything that never came through a feed this tool can reach.
//
// It shares NupkgReader with the feed acquisition, so the layout question is answered once. What is
// its own is where the file comes from and how the package names itself.
internal static class LocalPackageSource
{
    internal static AnalyzerAssemblySet? Acquire(
        string nupkgPath, string? sourceName, string? sourceVersion, string language, string workDir)
    {
        string full = Path.GetFullPath(nupkgPath);
        if (!File.Exists(full))
        {
            Console.Error.WriteLine($"no such package: {nupkgPath}");

            return null;
        }

        // The .nuspec inside the package, not the file name. A .nupkg can be renamed, and a name
        // that has been renamed is exactly the kind of thing a catalogue must not record as the
        // release it was generated from.
        (string? declaredId, string? declaredVersion) = ReadIdentity(full);

        string name = sourceName ?? declaredId ?? Path.GetFileNameWithoutExtension(full);
        string version = sourceVersion ?? declaredVersion ?? "0.0.0";

        Console.WriteLine($"resolved {name} => {version} (from {full})");

        IReadOnlyList<string>? paths =
            NupkgReader.ExtractAnalyzerAssemblies(full, $"{name} {version}", workDir, language);

        return paths is null ? null : new AnalyzerAssemblySet(paths, name, version);
    }

    // Nulls rather than a throw when the manifest is absent or unreadable: a package this tool
    // cannot introspect may still carry analyzers, and the caller can always say what it is with
    // --source-name and --source-version. Refusing here would turn a naming inconvenience into a
    // failure to read rules that are plainly there.
    private static (string? Id, string? Version) ReadIdentity(string nupkgPath)
    {
        try
        {
            using ZipArchive zip = ZipFile.OpenRead(nupkgPath);
            ZipArchiveEntry? nuspec = zip.Entries.FirstOrDefault(
                e => !e.FullName.Contains('/') && e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            if (nuspec is null) return (null, null);

            using Stream stream = nuspec.Open();
            XDocument document = XDocument.Load(stream);

            // Matched on local names: a .nuspec declares one of several versioned namespaces, and
            // which one is an accident of the SDK that produced it rather than anything meaningful.
            XElement? metadata = document.Root?.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "metadata");

            return (Value(metadata, "id"), Value(metadata, "version"));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException)
        {
            return (null, null);
        }

        static string? Value(XElement? metadata, string name)
        {
            string? text = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
    }
}
