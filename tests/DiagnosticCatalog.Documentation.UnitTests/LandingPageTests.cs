using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The project README stays a landing page: short enough to read, and about the product rather than
/// about the repository that builds it.
/// </summary>
/// <remarks>
/// <para>
/// A README grows one true paragraph at a time. Every addition is a fact somebody wanted stated, and
/// the page that results is one nobody reads: this one reached 461 lines and eighteen sections, of
/// which the release trains, the nightly regeneration pipeline, the <c>buildTransitive</c> layout,
/// the packaging scripts and the repository's internal architecture belonged in guides that already
/// existed and already said it better.
/// </para>
/// <para>
/// So the budget is checked rather than intended. The numbers are ceilings with room in them — this
/// is not a style guide, and a page a few lines under the limit is not better than one a few lines
/// over — but a page that has doubled has stopped being a landing page whatever anyone meant.
/// </para>
/// <para>
/// <b>What this deliberately does NOT require.</b> Nothing here asks the README to list the
/// catalogues, the projects, the guides or the decision records. That is the failure mode this test
/// exists to prevent, and a check demanding completeness would rebuild it one assertion at a time.
/// The one enumeration the front page does owe a reader — which analyzers have a catalogue — is
/// checked by <see cref="CatalogueListingTests"/> against <c>eng/catalogs.json</c>, because that one
/// is generated from the tree rather than typed.
/// </para>
/// </remarks>
public sealed class LandingPageTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The pair, spelled out because ADR-0029 displaces it: GitHub composes the landing page from a
    /// <c>README.md</c> at the root and from nothing else.
    /// </summary>
    private const string ProjectReadme = "README.md";

    private const string ProjectReadmeTranslation = "doc/README.fr.md";

    /// <summary>Lines, in either language. French runs longer, so the ceiling is shared.</summary>
    private const int LongestReadable = 250;

    /// <summary>Level-two sections. Ten is what the structure needs; twelve leaves room.</summary>
    private const int MostSections = 12;

    /// <summary>
    /// The mechanisms a landing page must not explain, each with the page that does.
    /// </summary>
    /// <remarks>
    /// Matched as words in the PROSE, so a link whose address happens to contain one of them is not
    /// an explanation of it. Each entry is something a reader of the front page does not yet have a
    /// question about: they have not published a catalogue, joined a release train, or wondered how
    /// the analyzers are delivered. Naming the destination in the failure is the point — the rule is
    /// "move it", never "delete it".
    /// </remarks>
    private static readonly (string Pattern, string What, string Instead)[] Forbidden =
    [
        (@"buildTransitive", "the analyzer delivery layout", "doc/guide/packaging-a-catalogue"),
        (@"\bdcat-analyzers\b", "the analyzer delivery layout", "doc/guide/packaging-a-catalogue"),
        (@"EnableDiagnosticCatalogAnalyzers", "the opt-in property", "doc/guide/configuration"),
        (@"\.props\b", "the props file a catalogue packs", "doc/guide/packaging-a-catalogue"),
        (@"\.targets\b", "the targets file that reads it", "doc/guide/packaging-a-catalogue"),
        (@"verify-consumption\.sh", "the packaging verification script", "doc/guide/testing-strategy"),
        (@"pack\.sh", "the packaging script", "doc/guide/release-trains"),
        (@"trains\.sh", "the release-train script", "doc/guide/release-trains"),
        (@"<ReleaseTrain>", "release-train membership", "doc/guide/release-trains"),
        (@"release train", "the release trains", "doc/guide/release-trains"),
        (@"nightly", "the nightly regeneration workflow", "doc/guide/ci-integration"),
        (@"Directory\.Build", "the repository's build files", "doc/guide/architecture"),
        (@"AnalyzerReleases", "the analyzer release tracking files", "CONTRIBUTING.md"),
    ];

    public static TheoryData<string> ReadmeHalves() => [ProjectReadme, ProjectReadmeTranslation];

    [Theory]
    [MemberData(nameof(ReadmeHalves))]
    public void The_landing_page_stays_within_its_budget(string path)
    {
        MarkdownDocument readme = Repository.Require(path);

        int lines = readme.Lines.Count;
        int sections = readme.Text
            .Split('\n')
            .Count(line => line.StartsWith("## ", StringComparison.Ordinal));

        Assert.True(
            lines <= LongestReadable,
            $"{path} is {lines} lines; a landing page is capped at {LongestReadable}. Something that "
            + "belongs in a guide has been written here — move it rather than trimming prose.");

        Assert.True(
            sections <= MostSections,
            $"{path} has {sections} level-two sections; a landing page is capped at {MostSections}. "
            + "A section added here is a subject the front page now owns.");
    }

    [Theory]
    [MemberData(nameof(ReadmeHalves))]
    public void The_landing_page_explains_no_internal_mechanism(string path)
    {
        MarkdownDocument readme = Repository.Require(path);

        List<string> found = [];

        foreach ((string pattern, string what, string instead) in Forbidden)
        {
            if (Regex.IsMatch(readme.Prose, pattern, RegexOptions.IgnoreCase, MatchTimeout))
            {
                found.Add($"{what} ({pattern}) — that belongs in {instead}");
            }
        }

        Assert.True(
            found.Count == 0,
            $"{path} explains a mechanism a reader of the front page has no question about yet:\n  "
            + string.Join("\n  ", found)
            + "\nThe page may say that referencing a catalogue enables the checks and code fixes in "
            + "that project, and then link out for the boundary, the opt-in and the opt-out.");
    }

    /// <summary>
    /// No diagram. A landing page is read in one pass on a phone, and a diagram is the shape that
    /// pulls a mechanism back onto it: nothing in the front page's ten sections needs one, and the
    /// two that were here described the generation pipeline and the packaging graph.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReadmeHalves))]
    public void The_landing_page_draws_no_diagram(string path)
    {
        MarkdownDocument readme = Repository.Require(path);

        Assert.DoesNotContain(
            "mermaid",
            readme.FenceLanguages,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The page does not enumerate the repository's own project pages.
    /// </summary>
    /// <remarks>
    /// Sixteen of them were listed, one per package, and that list is the front page acting as a
    /// directory of the repository rather than as a description of the product. A reader who wants a
    /// package's page reaches it from the catalogue index or from nuget.org. The threshold is not
    /// zero: linking the foundation's own page from a sentence about declaring a catalogue is a
    /// pointer, not an index.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ReadmeHalves))]
    public void The_landing_page_is_not_an_index_of_project_pages(string path)
    {
        MarkdownDocument readme = Repository.Require(path);

        List<string> projectPages = readme.Links
            .Select(link => link.PathPart)
            .Where(target => target.Contains("src/", StringComparison.Ordinal) &&
                             target.Contains("README", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            projectPages.Count <= 2,
            $"{path} links {projectPages.Count} package pages under src/: "
            + string.Join(", ", projectPages)
            + ".\nThe front page is not a directory of the repository. The catalogue index sends a "
            + "reader to the package they are looking for, and nuget.org renders each page anyway.");
    }

    /// <summary>
    /// The one sentence the landing page IS allowed to make about the mechanism, and the links it
    /// owes the reader afterwards.
    /// </summary>
    /// <remarks>
    /// The forbidden list above is a set of prohibitions, and prohibitions alone would be satisfied
    /// by a page that said nothing at all about what a reference does. What a reader has to be told
    /// is that one reference is enough; where the boundary falls is a link away, and this asserts
    /// that both links are there rather than that the reader is left to guess.
    /// </remarks>
    [Theory]
    [MemberData(nameof(ReadmeHalves))]
    public void The_landing_page_sends_the_reader_on_for_the_boundary(string path)
    {
        MarkdownDocument readme = Repository.Require(path);

        HashSet<string> targets = new(
            readme.Links.Select(link => link.PathPart.Replace(".en.md", ".md", StringComparison.Ordinal)
                                                     .Replace(".fr.md", ".md", StringComparison.Ordinal)),
            StringComparer.Ordinal);

        foreach (string page in new[] { "configuration.md", "packaging-a-catalogue.md" })
        {
            Assert.True(
                targets.Any(target => target.EndsWith(page, StringComparison.Ordinal)),
                $"{path} links no page ending in {page}. The landing page may say that referencing a "
                + "catalogue enables the checks and code fixes in that project; it must then send the "
                + "reader somewhere for the boundary, the opt-in and the opt-out, because it is not "
                + "allowed to explain those itself.");
        }
    }
}
