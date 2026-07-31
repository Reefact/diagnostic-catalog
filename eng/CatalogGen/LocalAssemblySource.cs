using System.Reflection;

namespace CatalogGen;

// Acquisition from assemblies already on disk: the analyzer a project just built, or any set of
// paths named on the command line.
//
// It is the second acquisition rather than a second reader, which is the point of the seam: the
// stage that loads and constructs third-party code is shared with NuGetPackageSource untouched,
// and everything that differs — where the files are, and what release to call them — is here.
internal static class LocalAssemblySource
{
    // Null when a path does not resolve, which is a failure rather than an empty result: silently
    // reading fewer assemblies than asked for would emit a catalogue short of rules, and a catalogue
    // short of a rule is indistinguishable from one whose vendor retired it.
    internal static AnalyzerAssemblySet? Acquire(
        IReadOnlyList<string> paths, string? sourceName, string? sourceVersion)
    {
        List<string> resolved = [];
        foreach (string path in paths)
        {
            string full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                Console.Error.WriteLine($"no such assembly: {path}");
                return null;
            }

            resolved.Add(full);
        }

        if (resolved.Count == 0)
        {
            Console.Error.WriteLine("no assembly given");
            return null;
        }

        // The first assembly names the source unless told otherwise. It is the predictable choice
        // rather than the clever one — a vendor's pair is given analyzer first, code fixes second
        // (StyleCop ships exactly that) — and the caller controls the order, so a wrong guess is
        // corrected by reordering or by --source-name.
        string name = sourceName ?? AssemblyName.GetAssemblyName(resolved[0]).Name
                      ?? Path.GetFileNameWithoutExtension(resolved[0]);

        // Metadata only: GetAssemblyName reads the manifest without loading the assembly, so nothing
        // here runs third-party code. Constructing analyzers is the reader's job, and stays there.
        //
        // Why the override exists rather than this value alone. The version is what tells one
        // snapshot from the next: the emitter leaves the file untouched when neither it nor any rule
        // moved, and the banners next to the catalogue state it as the release mirrored. An assembly
        // built out of a working copy carries whatever its project last set — typically 1.0.0.0,
        // unchanged across every rebuild — so deriving it here would have a catalogue claim an
        // unmoved source while its rules changed underneath. A caller that has a meaningful version
        // (a package version, a tag, a commit) passes it; one that has none gets the assembly's own
        // and the honest ambiguity that comes with it.
        string version = sourceVersion ?? AssemblyName.GetAssemblyName(resolved[0]).Version?.ToString() ?? "0.0.0.0";

        Console.WriteLine($"resolved {name} => {version} (from {resolved.Count} assembly/assemblies on disk)");
        foreach (string full in resolved) Console.WriteLine($"  + {full}");

        // Ordinal and distinct, for the reason NuGetPackageSource states: when two assemblies declare
        // the same rule id the last read wins, so the order has to be a property of the request
        // rather than of the disk. The caller's own order is not used — naming the same two
        // assemblies the other way round must not produce a different catalogue.
        return new AnalyzerAssemblySet(
            [.. resolved.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal)],
            name,
            version);
    }
}
