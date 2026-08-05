using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every vendor catalogue the generator produces is listed in the project README — in both halves —
/// and the number each half states in prose is the number of catalogues it actually lists.
/// </summary>
/// <remarks>
/// <para>
/// Adding a catalogue is the one change that reaches the front page from the outside. A new entry in
/// <c>eng/catalogs.json</c> produces a package, a release train and a generated <c>.g.cs</c>, and
/// nothing carries it into the table a reader consults to find out whether their analyzer is covered.
/// A catalogue missing from that table is invisible: the reader concludes it does not exist and stops
/// looking, which is the same outcome as never having generated it.
/// </para>
/// <para>
/// The count is the second half of the same problem, and the more brittle one. The table's size is
/// written out in words beside it — "These seven are generated" — and again inside the <c>dcat</c> row
/// that counts what the repository writes. Both are prose, both are updated by hand, and both are
/// wrong the moment a catalogue lands without them. Nothing else in the repository reads them.
/// </para>
/// <para>
/// The catalogues are read from <c>eng/catalogs.json</c> rather than from the <c>&lt;ReleaseTrain&gt;</c>
/// declarations, because the manifest is the generator's own input: a vendor catalogue cannot exist
/// without an entry there, whereas a train says only that a project is packable and is carried by
/// <c>DiagnosticCatalog</c> and <c>.Analyzers</c> too. An entry is a vendor catalogue when it names a
/// <c>package</c> — <c>DiagnosticCatalog.Self</c> is generated from <c>projects</c> already built here
/// and is documented as a tool rather than as something to reference.
/// </para>
/// </remarks>
public sealed class CatalogueListingTests
{
    /// <summary>
    /// The pair, spelled out because ADR-0029 displaces it: GitHub composes the landing page from a
    /// <c>README.md</c> at the root and from nothing else, so the halves do not sit beside each other
    /// and no suffix rule finds the second from the first.
    /// </summary>
    private const string ProjectReadme = "README.md";

    private const string ProjectReadmeTranslation = "doc/README.fr.md";

    /// <summary>
    /// The floor the guard holds the catalogue count to. Set below what the manifest carries so that
    /// retiring one does not fail it, and far enough above zero that a manifest which stopped parsing
    /// cannot pass for a repository that generates nothing.
    /// </summary>
    private const int FewestCataloguesWorthChecking = 4;

    /// <summary>
    /// A vendor catalogue: the package it mirrors, and the assembly a consumer references to use it.
    /// </summary>
    private sealed record Catalogue(string Package, string Namespace);

    private static readonly Lazy<IReadOnlyList<Catalogue>> Generated = new(ReadManifest);

    /// <summary>
    /// How each half writes the size of its catalogue table. Anchored on the sentence rather than on
    /// the table, because the number is what goes stale and the sentence is where it is written; a
    /// half whose sentence no longer matches is reported rather than skipped, so rewording it cannot
    /// quietly retire the check.
    /// </summary>
    private static readonly Dictionary<string, string> CountSentences =
        new(StringComparer.Ordinal)
        {
            [ProjectReadme] = "These\\s+(?<count>[A-Za-z]+)\\s+are\\s+\\*\\*generated\\*\\*",
            [ProjectReadmeTranslation] = "Ces\\s+(?<count>[A-Za-z]+)(?:-là)?\\s+sont\\s+\\*\\*générés\\*\\*",
        };

    public static TheoryData<string> ReadmeHalves() => [ProjectReadme, ProjectReadmeTranslation];

    [Theory]
    [MemberData(nameof(ReadmeHalves))]
    public void Every_generated_catalogue_is_listed(string path)
    {
        MarkdownDocument readme = Repository.Require(path);
        IReadOnlyList<string> listed = PackagesListed(readme);

        List<Catalogue> missing = Generated.Value
            .Where(catalogue => !listed.Contains(catalogue.Namespace, StringComparer.Ordinal))
            .ToList();

        if (missing.Count == 0) return;

        string absent = string.Join(
            ", ",
            missing.Select(catalogue => $"{catalogue.Namespace} (mirroring {catalogue.Package})"));

        Assert.Fail(
            $"{path} lists no row for {absent}.\n" +
            "eng/catalogs.json generates the catalogue, so the package exists and the page does not " +
            "say so — a reader looking for their analyzer concludes it is not covered. Add a row to " +
            "the catalogue table, in this half and in the other one.");
    }

    [Theory]
    [MemberData(nameof(ReadmeHalves))]
    public void The_count_stated_matches_the_catalogues_listed(string path)
    {
        MarkdownDocument readme = Repository.Require(path);

        Match sentence = ProseFigures.Require(
            readme,
            CountSentences[path],
            "the sentence saying how many catalogues it lists",
            $"{nameof(CatalogueListingTests)}.{nameof(CountSentences)}");

        string word = sentence.Groups["count"].Value;
        int stated = ProseFigures.Read(word, path, nameof(CatalogueListingTests));

        int listed = PackagesListed(readme)
            .Count(package => Generated.Value.Any(catalogue =>
                string.Equals(catalogue.Namespace, package, StringComparison.Ordinal)));

        Assert.True(
            stated == listed,
            $"{path} says \"{word}\" catalogues are generated and lists {listed}.\n" +
            "The table is the half a reader counts, so the sentence is usually the one to fix — " +
            "unless a catalogue was added to eng/catalogs.json and never given a row, which " +
            $"{nameof(Every_generated_catalogue_is_listed)} reports separately.");
    }

    /// <summary>
    /// Guards both theories against passing on an empty world. A manifest that stopped parsing, or a
    /// table written in a shape the row pattern does not read, would leave nothing to compare and
    /// nothing to report — which is indistinguishable from success.
    /// </summary>
    [Fact]
    public void The_readme_lists_catalogues_this_can_check()
    {
        IReadOnlyList<Catalogue> generated = Generated.Value;

        Assert.True(
            generated.Count >= FewestCataloguesWorthChecking,
            $"eng/catalogs.json yields {generated.Count} vendor catalogues, which is below the " +
            $"{FewestCataloguesWorthChecking} this expects. Either the repository stopped generating " +
            "them, or the manifest moved and every listing here is silently unchecked.");

        foreach (string path in new[] { ProjectReadme, ProjectReadmeTranslation })
        {
            MarkdownDocument readme = Repository.Require(path);

            Assert.True(
                PackagesListed(readme).Count > 0,
                $"{path} carries no package table row this can read. The rows are matched as a first " +
                "cell holding a bold, backticked package name; a table written another way leaves " +
                "every catalogue unchecked in this half.");
        }
    }

    /// <summary>
    /// The packages a half names in the first cell of a table row — the shape both package tables use
    /// and the Project status table does not, which is what keeps a package counted once. The row for
    /// <c>dcat</c> carries prose after the name; anchoring on the cell rather than on the whole row is
    /// what lets it through.
    /// </summary>
    private static List<string> PackagesListed(MarkdownDocument readme)
    {
        List<string> packages = [];

        foreach (Match row in ProseFigures.Sweep(
                     readme,
                     "^\\|\\s*\\*\\*`(?<package>DiagnosticCatalog(?:\\.[A-Za-z]+)?)`\\*\\*"))
        {
            packages.Add(row.Groups["package"].Value);
        }

        return packages;
    }

    /// <summary>
    /// The vendor catalogues, read from the manifest that generates them. Parsed as JSON rather than
    /// scanned with a regex, unlike <see cref="CatalogueProvenanceTests"/>: this needs two keys read
    /// off the SAME entry, and a pattern that pairs them across an array is a parser written badly
    /// rather than a pattern avoided.
    /// </summary>
    private static List<Catalogue> ReadManifest()
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
            // An entry generated from `projects` is this repository's own rules, which the README
            // documents as a tool rather than as a catalogue to reference.
            if (!entry.TryGetProperty("package", out JsonElement package)) continue;
            if (!entry.TryGetProperty("namespace", out JsonElement catalogueNamespace)) continue;

            catalogues.Add(new Catalogue(
                package.GetString() ?? string.Empty,
                catalogueNamespace.GetString() ?? string.Empty));
        }

        return catalogues;
    }
}
