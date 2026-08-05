using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The catalogues <c>eng/catalogs.json</c> declares, read once for every documentation test that
/// holds a page to them.
/// </summary>
/// <remarks>
/// <para>
/// The manifest is the generator's own input, which is what makes it the truth here: a vendor
/// catalogue cannot exist without an entry, whereas a <c>&lt;ReleaseTrain&gt;</c> says only that a
/// project is packable and is carried by <c>DiagnosticCatalog</c> and <c>.Analyzers</c> too.
/// </para>
/// <para>
/// Shared rather than copied. Two tests reading the same file two ways is the drift they exist to
/// prevent, one level down — a catalogue one reader counts and the other does not would make the two
/// disagree about the repository while both stayed green.
/// </para>
/// </remarks>
internal static class CatalogueManifest
{
    private static readonly Lazy<IReadOnlyList<Catalogue>> Entries = new(Read);

    /// <summary>
    /// The catalogues generated from a published package — every entry naming a <c>package</c>.
    /// <c>DiagnosticCatalog.Self</c> is generated from <c>projects</c> already built here and mirrors
    /// nothing upstream, so it is not one of them.
    /// </summary>
    internal static IReadOnlyList<Catalogue> Vendor => Entries.Value;

    /// <summary>
    /// Parsed as JSON rather than scanned with a regex, unlike <see cref="CatalogueProvenanceTests"/>:
    /// this needs two keys read off the SAME entry, and a pattern that pairs them across an array is a
    /// parser written badly rather than a pattern avoided.
    /// </summary>
    private static List<Catalogue> Read()
    {
        List<Catalogue> catalogues = [];

        string path = Path.Combine(Repository.Root, "eng", "catalogs.json");
        if (!File.Exists(path)) return catalogues;

        using JsonDocument manifest = JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

        if (!manifest.RootElement.TryGetProperty("catalogs", out JsonElement entries)) return catalogues;

        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("package", out JsonElement package)) continue;
            if (!entry.TryGetProperty("namespace", out JsonElement catalogueNamespace)) continue;

            catalogues.Add(new Catalogue(
                package.GetString() ?? string.Empty,
                catalogueNamespace.GetString() ?? string.Empty));
        }

        return catalogues;
    }
}

/// <summary>
/// A vendor catalogue: the package it mirrors, and the assembly a consumer references to use it.
/// </summary>
internal sealed record Catalogue(string Package, string Namespace);
