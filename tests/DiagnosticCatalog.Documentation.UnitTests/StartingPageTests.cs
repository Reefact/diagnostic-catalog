using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The pages that tell a reader which package to reference send them to the central catalogue index,
/// and quote a version a reader can actually restore.
/// </summary>
/// <remarks>
/// <para>
/// Two failures, and both were live. The starting pages listed <b>eight</b> catalogues in prose,
/// written when there were eight; five more shipped and the list stayed. A reader running NUnit,
/// ASP.NET Core or the public-API analyzers read that page, did not find their analyzer, and
/// concluded it was not covered — which is the same outcome as never having generated the catalogue.
/// And the same pages quoted <c>Version="0.1.0"</c>, the last version of the foundation, on packages
/// whose first release is the 1.0 line: a reader copying it restores something that does not exist.
/// </para>
/// <para>
/// The repair for the first is not a longer list. It is a link: the index lives in the project
/// README, is checked against <c>eng/catalogs.json</c> by <see cref="CatalogueListingTests"/>, and is
/// where every package page already sends its reader. One list, generated from the manifest that
/// produces the packages, and every other page pointing at it.
/// </para>
/// <para>
/// The pages are NAMED rather than discovered. An obligation any file can discharge is one no file
/// has, and a page that starts telling a reader what to reference should be added here deliberately
/// — which is also the moment somebody asks whether it should be doing that at all.
/// </para>
/// </remarks>
public sealed class StartingPageTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The pages that ask a reader to add a <c>PackageReference</c> to a catalogue.
    /// </summary>
    private static readonly string[] Starting =
    [
        "doc/guide/getting-started.{0}.md",
        "doc/guide/writing-suppressions.{0}.md",
    ];

    /// <summary>
    /// Every current user guide, plus the pages a package ships. A version quoted in any of them is
    /// one a reader copies; a version in a changelog or a decision record is a historical fact and
    /// is deliberately not in scope.
    /// </summary>
    private static IEnumerable<MarkdownDocument> CurrentGuides =>
        Repository.Documents.Where(document =>
            document.Path.StartsWith("doc/guide/", StringComparison.Ordinal) ||
            document.Path is "README.md" or "doc/README.fr.md" ||
            (document.Path.StartsWith("src/", StringComparison.Ordinal) &&
             document.FileName.StartsWith("README.", StringComparison.Ordinal)));

    /// <summary>The version the `lib` train last published, and the one no user guide may quote.</summary>
    private const string SupersededVersion = "0.1.0";

    /// <summary>
    /// How many distinct catalogue package ids a starting page may name. One is the worked example;
    /// the second is room for a page that contrasts two. Anything beyond that is a list.
    /// </summary>
    private const int MostCataloguesNamed = 2;

    public static TheoryData<string> StartingPages()
    {
        TheoryData<string> paths = [];
        foreach (string page in Starting)
        {
            paths.Add(string.Format(page, "en"));
            paths.Add(string.Format(page, "fr"));
        }

        return paths;
    }

    public static TheoryData<string> Guides()
    {
        TheoryData<string> paths = [];
        foreach (MarkdownDocument document in CurrentGuides)
        {
            paths.Add(document.Path);
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(StartingPages))]
    public void A_starting_page_points_at_the_central_index(string path)
    {
        MarkdownDocument page = Repository.Require(path);

        bool reaches = page.Links.Any(link =>
            link.PathPart.EndsWith("README.md", StringComparison.Ordinal) ||
            link.PathPart.EndsWith("README.fr.md", StringComparison.Ordinal));

        Assert.True(
            reaches,
            $"{path} tells a reader to reference a catalogue and links no catalogue index. The index "
            + "is in the project README, checked against eng/catalogs.json, and is the only list of "
            + "catalogues this repository maintains.");
    }

    [Theory]
    [MemberData(nameof(StartingPages))]
    public void A_starting_page_copies_no_partial_list_of_the_catalogues(string path)
    {
        MarkdownDocument page = Repository.Require(path);

        HashSet<string> named = new(StringComparer.Ordinal);

        foreach (Match reference in Regex.Matches(
                     page.Text,
                     @"(?<!\w)DiagnosticCatalog\.(?<catalogue>[A-Za-z]+)(?![\w.])",
                     RegexOptions.None,
                     MatchTimeout))
        {
            string catalogue = "DiagnosticCatalog." + reference.Groups["catalogue"].Value;

            if (CatalogueManifest.Vendor.Any(entry =>
                    string.Equals(entry.Namespace, catalogue, StringComparison.Ordinal)))
            {
                named.Add(catalogue);
            }
        }

        Assert.True(
            named.Count <= MostCataloguesNamed,
            $"{path} names {named.Count} catalogues: {string.Join(", ", named.OrderBy(id => id, StringComparer.Ordinal))}.\n"
            + "That is a second list of the catalogues, kept by hand, and it goes stale the day one "
            + "is added — a reader whose analyzer is missing from it concludes it is not covered. "
            + "Show one as the worked example and link the index for the rest.");
    }

    [Theory]
    [MemberData(nameof(Guides))]
    public void A_current_guide_quotes_no_superseded_version(string path)
    {
        MarkdownDocument guide = Repository.Require(path);

        Assert.DoesNotContain(
            $"Version=\"{SupersededVersion}\"",
            guide.Text,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Guards the theories against passing on an empty world: a renamed page or a manifest that
    /// stopped parsing would leave nothing to compare and nothing to report.
    /// </summary>
    [Fact]
    public void The_pages_and_the_manifest_are_still_found()
    {
        foreach (string page in Starting)
        {
            foreach (string language in new[] { "en", "fr" })
            {
                string path = string.Format(page, language);

                Assert.True(
                    Repository.Find(path) is not null,
                    $"{path} was not found. A starting page renamed without editing this test leaves "
                    + "its successor unchecked.");
            }
        }

        Assert.True(
            CatalogueManifest.Vendor.Count >= 4,
            $"eng/catalogs.json yields {CatalogueManifest.Vendor.Count} vendor catalogues, which is "
            + "too few for the list check above to mean anything.");

        Assert.True(
            CurrentGuides.Count() > 20,
            "Fewer than twenty current guides were discovered, so the version check is reading "
            + "almost nothing.");
    }
}
