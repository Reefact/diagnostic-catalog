using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The FAQ answers "why not take the constants from the analyzer packages themselves?" by surveying
/// the metadata of every package the catalogues mirror. How many packages that is, and which ones the
/// survey covers, are recounted from the manifest and held to what the pages state.
/// </summary>
/// <remarks>
/// <para>
/// The figure went stale exactly once already, and instructively. The survey was written when five
/// packages were mirrored and stayed at five while five more catalogues landed — each of which added
/// a package, a release train and a row to the front page, none of which reached the one page that
/// counts packages. A reader met a table of five and concluded it was the whole evidence, when it was
/// half of it.
/// </para>
/// <para>
/// <b>What is checkable here and what is not.</b> The per-package columns — public types, public
/// constants, rule ids — are measured against the released assemblies, which needs the packages
/// resolved from a feed and cannot be a file walk. Those numbers are not recounted here and go stale
/// silently; that is a known gap, not an oversight. What IS a file walk is the population: how many
/// packages the catalogues mirror, and which. That is the half that moved, and it is the half a new
/// catalogue moves again.
/// </para>
/// <para>
/// <b>The pages are found, not listed</b>, for the reason ADR-0023's figures were swept for rather
/// than enumerated: a hand-kept list of pages is itself a figure nothing recounts. The claim is swept
/// across every document, anchored on the backticked package names the survey rows carry, which is
/// what translation leaves alone.
/// </para>
/// </remarks>
public sealed class PackageSurveyTests
{
    /// <summary>
    /// The fewest rows a table must carry before it is taken for the survey. A page listing one
    /// vendor package in passing is not surveying them; requiring several keeps an unrelated table
    /// from being held to a population it never claimed to cover.
    /// </summary>
    private const int FewestRowsWorthTakingForTheSurvey = 3;

    /// <summary>
    /// How each language states the size of the surveyed population. Both shapes the FAQ writes are
    /// read: the sentence introducing the table, and the one that follows it saying a category is a
    /// constant in none of them.
    /// </summary>
    private static readonly string[] CountSentences =
    [
        "in\\s+the\\s+(?<count>[A-Za-z]+)\\s+packages\\s+the\\s+catalogues\\s+mirror",
        "zero,\\s+across\\s+all\\s+(?<count>[A-Za-z]+)",
        "des\\s+(?<count>[A-Za-zé]+)\\s+paquets\\s+que\\s+reflètent\\s+les\\s+catalogues",
        "zéro,\\s+dans\\s+les\\s+(?<count>[A-Za-zé]+)",
    ];

    /// <summary>
    /// A survey row: a first cell holding a backticked package name followed by the release measured.
    /// The version is what separates these rows from the catalogue tables on the front page, whose
    /// first cell is a bold package name carrying no release.
    /// </summary>
    private const string SurveyRow =
        "^\\|\\s*`(?<package>[A-Za-z0-9.]+)`\\s+(?<version>[0-9][0-9A-Za-z.\\-]*)\\s*\\|";

    private static IReadOnlyList<Catalogue> Mirrored => CatalogueManifest.Vendor;

    public static TheoryData<string> PagesStatingACount() => Pages(document => CountsIn(document).Count > 0);

    public static TheoryData<string> PagesCarryingTheSurvey() => Pages(CarriesTheSurvey);

    [Theory]
    [MemberData(nameof(PagesStatingACount))]
    public void Every_stated_package_count_is_the_number_mirrored(string path)
    {
        MarkdownDocument document = Repository.Require(path);

        foreach (Count count in CountsIn(document))
        {
            Assert.True(
                count.Stated == Mirrored.Count,
                $"{path} says the catalogues mirror {count.Stated} packages, and eng/catalogs.json " +
                $"declares {Mirrored.Count}: {string.Join(", ", Mirrored.Select(c => c.Package))}.\n" +
                $"Written as \"{count.Quoted}\".\nThe survey is evidence that no vendor publishes its " +
                "rule ids as constants, so a population stated short is the argument being made on " +
                "part of the evidence. Recount, and measure the packages the table is missing.");
        }
    }

    [Theory]
    [MemberData(nameof(PagesCarryingTheSurvey))]
    public void Every_mirrored_package_has_a_row(string path)
    {
        MarkdownDocument document = Repository.Require(path);
        List<string> surveyed = RowsIn(document).Select(row => row.Package).ToList();

        List<Catalogue> missing = Mirrored
            .Where(catalogue => !surveyed.Contains(catalogue.Package, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"{path} surveys the analyzer packages and has no row for " +
            $"{string.Join(", ", missing.Select(c => $"{c.Package} (mirrored by {c.Namespace})"))}.\n" +
            "The row cannot be guessed from the tree: its columns are measured against the released " +
            "assemblies. Resolve the package, read its metadata, and add the row to this half and to " +
            "the other one.");
    }

    /// <summary>
    /// Guards both theories against passing on an empty world. A survey nothing recognises as one, or
    /// a manifest that stopped parsing, would leave every claim unread — which reads like success.
    /// </summary>
    [Fact]
    public void The_survey_is_found_and_has_a_population_to_check()
    {
        Assert.True(
            Mirrored.Count >= 4,
            $"eng/catalogs.json yields {Mirrored.Count} package-backed catalogues, which is too few " +
            "to be the repository as it stands. The manifest moved or stopped parsing, and every " +
            "figure here is silently unchecked.");

        int surveys = Repository.Documents.Count(CarriesTheSurvey);

        Assert.True(
            surveys >= 2,
            $"{surveys} page(s) carry the package survey, and it is a bilingual pair, so fewer than " +
            "two means the table was reworded into a shape the row pattern no longer reads. Every " +
            "package would then be unchecked in that half.");

        // Each PATTERN has to still find its sentence, not merely each page still hold one.
        //
        // The distinction is the whole of this check. CountSentences carries four patterns across
        // two pages, so a page-level tally is satisfied twice over while half the patterns read
        // nothing: when #147 reworded the category sentence, its pattern stopped matching, the
        // English page went on matching the OTHER English pattern, the tally stayed at two, and the
        // suite stayed green with those figures unread. A rewording that outruns its pattern is
        // exactly the failure this exists to report, and it was the one shape it could not see.
        //
        // The patterns are language-specific — two English, two French — so requiring every one of
        // them also asserts what the page count was standing in for: both halves of the pair state a
        // figure. That is why the tally is replaced rather than kept beside this.
        List<string> unread = [];

        foreach (string pattern in CountSentences)
        {
            bool matched = false;

            foreach (MarkdownDocument document in Repository.Documents)
            {
                foreach (Match sentence in ProseFigures.Sweep(document, pattern))
                {
                    // A match whose figure is not a word ProseFigures knows produces no Count, so it
                    // leaves the pattern just as unread as no match at all.
                    if (ProseFigures.Knows(sentence.Groups["count"].Value))
                    {
                        matched = true;

                        break;
                    }
                }

                if (matched)
                {
                    break;
                }
            }

            if (!matched)
            {
                unread.Add(pattern);
            }
        }

        Assert.True(
            unread.Count == 0,
            $"{unread.Count} of the {CountSentences.Length} sentence patterns in " +
            $"{nameof(CountSentences)} match no page:\n" +
            string.Join("\n", unread.Select(pattern => $"  {pattern}")) + "\n" +
            "Each states how many packages the catalogues mirror, and a pattern reading nothing " +
            "leaves its figure unchecked while the others keep this green. The sentence was almost " +
            "certainly reworded: teach the pattern the new wording rather than dropping it.");
    }

    private static TheoryData<string> Pages(Func<MarkdownDocument, bool> carries)
    {
        TheoryData<string> paths = [];

        foreach (MarkdownDocument document in Repository.Documents)
        {
            if (carries(document)) paths.Add(document.Path);
        }

        return paths;
    }

    /// <summary>
    /// Whether a document carries the survey table, decided by the rows it holds rather than by its
    /// path. A page naming one vendor package in passing is not surveying them, so several rows are
    /// required — and at least one must name a package actually mirrored, which is what keeps the
    /// front page's catalogue tables from being taken for this one.
    /// </summary>
    private static bool CarriesTheSurvey(MarkdownDocument document)
    {
        List<Row> rows = RowsIn(document);

        return rows.Count >= FewestRowsWorthTakingForTheSurvey
               && rows.Any(row => Mirrored.Any(catalogue =>
                   string.Equals(catalogue.Package, row.Package, StringComparison.OrdinalIgnoreCase)));
    }

    private static List<Row> RowsIn(MarkdownDocument document) =>
        ProseFigures.Sweep(document, SurveyRow)
            .Select(match => new Row(match.Groups["package"].Value, match.Groups["version"].Value))
            .ToList();

    private static List<Count> CountsIn(MarkdownDocument document)
    {
        List<Count> counts = [];

        foreach (string pattern in CountSentences)
        {
            foreach (Match sentence in ProseFigures.Sweep(document, pattern))
            {
                string word = sentence.Groups["count"].Value;

                // A page may use the same words without counting packages. A word outside the shared
                // vocabulary is not a figure this can read, and reading it as zero would invent a
                // failure rather than find one.
                if (!ProseFigures.Knows(word)) continue;

                counts.Add(new Count(
                    ProseFigures.Read(word, document.Path, nameof(PackageSurveyTests)),
                    Quote(sentence)));
            }
        }

        return counts;
    }

    /// <summary>The matched text on one line, so a failure message stays readable.</summary>
    private static string Quote(Match match) =>
        Regex.Replace(match.Value.Replace('\n', ' '), "\\s+", " ", RegexOptions.None, ProseFigures.MatchTimeout).Trim();

    /// <summary>One surveyed package: the name in the first cell, and the release measured.</summary>
    private sealed record Row(string Package, string Version);

    /// <summary>One stated population size, and the words it was written in.</summary>
    private sealed record Count(int Stated, string Quoted);
}
