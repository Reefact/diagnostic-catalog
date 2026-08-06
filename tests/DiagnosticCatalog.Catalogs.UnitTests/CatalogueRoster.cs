using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DiagnosticCatalog.Catalogs.UnitTests;

/// <summary>
/// One catalogue as this repository declares it.
/// </summary>
/// <param name="Folder">The project folder, which is what ties a generated file to its documents.</param>
/// <param name="GeneratedFile">The <c>.g.cs</c> the generator writes there.</param>
/// <param name="Namespace">The namespace the catalogue declares, and the name it is published under.</param>
/// <param name="PackageId">What the project packs as, read from the project rather than assumed.</param>
/// <param name="Upstream">
/// The package it mirrors, or empty when it mirrors nothing outside this repository —
/// <c>DiagnosticCatalog.Self</c> is generated from projects built here, and is the only entry that is.
/// </param>
internal sealed record CatalogueEntry(
    string Folder, string GeneratedFile, string Namespace, string PackageId, string Upstream)
{
    /// <summary>Whether this catalogue mirrors somebody else's analyzer.</summary>
    internal bool MirrorsAVendor => Upstream.Length > 0;
}

/// <summary>
/// The catalogues, discovered from <c>eng/catalogs.json</c> — the file that produces them.
/// </summary>
/// <remarks>
/// <para>
/// Discovered rather than listed. A hand-written list carried the opposite reasoning for a while —
/// that a new catalogue absent from it would be noticed — and that reasoning is backwards: a
/// catalogue absent from the list is precisely a catalogue absent from the theory, so it passes by
/// not being tested. Nothing else in the repository would say so, because nothing compiles a README.
/// </para>
/// <para>
/// The manifest is the right source because a catalogue cannot exist without an entry there: the
/// generator and the nightly workflow both read it, and an entry is written before the project is.
/// What the manifest does NOT say is what the project packs as, so that half is read from the
/// project file beside it — neither is assumed to match the other.
/// </para>
/// <para>
/// Scanned rather than deserialised, because this suite runs on the .NET Framework 4.7.2 floor where
/// <c>System.Text.Json</c> is not present. The scan is a brace walk over the <c>catalogs</c> array
/// rather than a regex pairing keys across it: several keys have to be read off the SAME entry, and
/// a pattern that tries to pair them is a parser written badly rather than a parser avoided.
/// </para>
/// </remarks>
internal static class CatalogueRoster
{
    private static IReadOnlyList<CatalogueEntry>? _all;

    /// <summary>Every catalogue the manifest declares, vendor or not.</summary>
    internal static IReadOnlyList<CatalogueEntry> All => _all ??= Read();

    /// <summary>The catalogues that mirror somebody else's analyzer.</summary>
    internal static IReadOnlyList<CatalogueEntry> Vendor
    {
        get
        {
            List<CatalogueEntry> vendor = new();
            foreach (CatalogueEntry catalogue in All)
            {
                if (catalogue.MirrorsAVendor) vendor.Add(catalogue);
            }

            return vendor;
        }
    }

    private static List<CatalogueEntry> Read()
    {
        List<CatalogueEntry> catalogues = new();

        string manifest = Path.Combine(AppContext.BaseDirectory, "catalogmanifest", "catalogs.json");
        if (!File.Exists(manifest)) return catalogues;

        Dictionary<string, string> packaged = PackedAs();

        foreach (string entry in Objects(File.ReadAllText(manifest)))
        {
            string output = Value(entry, "output");
            if (output.Length == 0) continue;

            string[] segments = output.Split('/');
            if (segments.Length < 2) continue;

            string folder = segments[segments.Length - 2];
            packaged.TryGetValue(folder, out string? packageId);

            catalogues.Add(new CatalogueEntry(
                folder,
                segments[segments.Length - 1],
                Value(entry, "namespace"),
                packageId ?? string.Empty,
                Value(entry, "package")));
        }

        catalogues.Sort((left, right) => string.CompareOrdinal(left.Folder, right.Folder));

        return catalogues;
    }

    /// <summary>
    /// What each project folder publishes as: a project becomes packable by declaring
    /// <c>&lt;ReleaseTrain&gt;</c> in its own <c>.csproj</c> and nowhere else, and the SDK falls back
    /// to the file name when <c>PackageId</c> is not set.
    /// </summary>
    internal static Dictionary<string, string> PackedAs()
    {
        Dictionary<string, string> packaged = new(StringComparer.Ordinal);

        string root = Path.Combine(AppContext.BaseDirectory, "catalogprojects");
        if (!Directory.Exists(root)) return packaged;

        // project.xml, not .csproj: the release tooling discovers what to pack by grepping the tree
        // for <ReleaseTrain> in *.csproj, and a project file in a build output is indistinguishable
        // from a real one. See the copy in this project's .csproj.
        foreach (string path in Directory.GetFiles(root, "project.xml", SearchOption.AllDirectories))
        {
            string project = File.ReadAllText(path);
            if (!Declares(project, "ReleaseTrain", out string _)) continue;

            string folder = Path.GetFileName(Path.GetDirectoryName(path))!;
            packaged[folder] = Declares(project, "PackageId", out string id) ? id : folder;
        }

        return packaged;
    }

    private static bool Declares(string project, string element, out string value)
    {
        Match declaration = Regex.Match(
            project,
            "<" + element + ">\\s*(?<value>[^<]+?)\\s*</" + element + ">",
            RegexOptions.None,
            TimeSpan.FromSeconds(10));

        value = declaration.Success ? declaration.Groups["value"].Value : string.Empty;

        return declaration.Success;
    }

    /// <summary>One string value of an entry, or empty when the entry does not carry that key.</summary>
    private static string Value(string entry, string key)
    {
        Match found = Regex.Match(
            entry,
            "\"" + key + "\"\\s*:\\s*\"(?<value>[^\"]*)\"",
            RegexOptions.None,
            TimeSpan.FromSeconds(10));

        return found.Success ? found.Groups["value"].Value : string.Empty;
    }

    /// <summary>
    /// The top-level objects of the <c>catalogs</c> array, each as its own text.
    /// </summary>
    /// <remarks>
    /// Strings are tracked while walking, so a brace inside a comment string — the manifest's
    /// <c>$comment</c> arrays are prose — cannot open or close an entry.
    /// </remarks>
    private static IEnumerable<string> Objects(string manifest)
    {
        int array = manifest.IndexOf("\"catalogs\"", StringComparison.Ordinal);
        if (array < 0) yield break;

        int open = manifest.IndexOf('[', array);
        if (open < 0) yield break;

        StringBuilder entry = new();
        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int index = open + 1; index < manifest.Length; index++)
        {
            char character = manifest[index];

            if (depth > 0) entry.Append(character);

            if (inString)
            {
                if (escaped) escaped = false;
                else if (character == '\\') escaped = true;
                else if (character == '"') inString = false;

                continue;
            }

            switch (character)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    if (depth == 0) entry.Append(character);
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        yield return entry.ToString();
                        entry.Length = 0;
                    }

                    break;
                case ']':
                    if (depth == 0) yield break;
                    break;
                default:
                    break;
            }
        }
    }
}
