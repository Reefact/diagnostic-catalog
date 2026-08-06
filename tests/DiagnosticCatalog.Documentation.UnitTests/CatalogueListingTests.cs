using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every vendor catalogue the generator produces is listed in the project README's central index —
/// in both halves — and that index lists nothing else.
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
/// That table is the CENTRAL INDEX: every catalogue's own README points at it instead of listing its
/// siblings, so it is the one page a reader is sent to from thirteen package pages. Both halves are
/// marked with <c>&lt;!-- catalogue-index:begin --&gt;</c> so the check reads a delimited region
/// rather than guessing which of a document's tables is the one that claims to be exhaustive.
/// </para>
/// <para>
/// The size of the table is deliberately NOT written out in prose beside it. A number spelled in
/// words is a second statement of what the rows already say, updated by hand and wrong the moment a
/// catalogue lands without it — and it tells a reader nothing the table does not.
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

    private static IReadOnlyList<Catalogue> Generated => CatalogueManifest.Vendor;

    /// <summary>The markers delimiting the central index inside each half.</summary>
    private const string IndexBegin = "<!-- catalogue-index:begin -->";

    private const string IndexEnd = "<!-- catalogue-index:end -->";

    public static TheoryData<string> ReadmeHalves() => [ProjectReadme, ProjectReadmeTranslation];

    [Theory]
    [MemberData(nameof(ReadmeHalves))]
    public void Every_generated_catalogue_is_listed(string path)
    {
        MarkdownDocument readme = Repository.Require(path);
        IReadOnlyList<string> listed = PackagesIndexed(readme);

        List<Catalogue> missing = Generated
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
    public void The_index_lists_nothing_this_repository_does_not_generate(string path)
    {
        MarkdownDocument readme = Repository.Require(path);

        foreach (string package in PackagesIndexed(readme))
        {
            Assert.True(
                Generated.Any(catalogue => string.Equals(catalogue.Namespace, package, StringComparison.Ordinal)),
                $"{path} indexes {package} among the ready-made catalogues, and eng/catalogs.json " +
                "generates no such catalogue. Thirteen package pages send their reader to this table, " +
                "so a row here that resolves to nothing is a dead end reached from all of them.");
        }
    }

    /// <summary>
    /// Guards both theories against passing on an empty world. A manifest that stopped parsing, or a
    /// table written in a shape the row pattern does not read, would leave nothing to compare and
    /// nothing to report — which is indistinguishable from success.
    /// </summary>
    [Fact]
    public void The_readme_lists_catalogues_this_can_check()
    {
        IReadOnlyList<Catalogue> generated = Generated;

        Assert.True(
            generated.Count >= FewestCataloguesWorthChecking,
            $"eng/catalogs.json yields {generated.Count} vendor catalogues, which is below the " +
            $"{FewestCataloguesWorthChecking} this expects. Either the repository stopped generating " +
            "them, or the manifest moved and every listing here is silently unchecked.");

        foreach (string path in new[] { ProjectReadme, ProjectReadmeTranslation })
        {
            MarkdownDocument readme = Repository.Require(path);

            Assert.True(
                PackagesIndexed(readme).Count > 0,
                $"{path} carries no row inside its {IndexBegin} … {IndexEnd} block that this can read. " +
                "The rows are matched as a first cell holding a bold, backticked package name; a table " +
                "written another way, or markers that moved, leave every catalogue unchecked here.");
        }
    }

    /// <summary>
    /// The packages the central index names in the first cell of a table row.
    /// </summary>
    /// <remarks>
    /// Read from BETWEEN the markers rather than from the whole document. Both halves carry a second
    /// package table — the toolkit — and a check that swept the page would count the foundation and
    /// the tool among the catalogues, then report every one of them as ungenerated. The markers say
    /// which table claims to be exhaustive, so nothing has to be inferred from the prose around it.
    /// </remarks>
    private static List<string> PackagesIndexed(MarkdownDocument readme)
    {
        int start = readme.Text.IndexOf(IndexBegin, StringComparison.Ordinal);
        int end = readme.Text.IndexOf(IndexEnd, StringComparison.Ordinal);

        Assert.True(
            start >= 0 && end > start,
            $"{readme.Path} carries no {IndexBegin} … {IndexEnd} block. That block is the central " +
            "index every catalogue's README points at, and without it nothing here can tell which " +
            "table claims to list them all.");

        List<string> packages = [];

        foreach (Match row in Regex.Matches(
                     readme.Text.Substring(start, end - start),
                     "^\\|\\s*\\*\\*`(?<package>DiagnosticCatalog(?:\\.[A-Za-z]+)?)`\\*\\*",
                     RegexOptions.Multiline,
                     TimeSpan.FromSeconds(10)))
        {
            packages.Add(row.Groups["package"].Value);
        }

        return packages;
    }
}
