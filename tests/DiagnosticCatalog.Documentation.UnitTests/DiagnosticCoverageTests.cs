using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every <c>DCAT</c> diagnostic this repository ships is documented, and every one the guide
/// documents is shipped.
/// </summary>
/// <remarks>
/// <para>
/// Both directions fail silently without a check. A diagnostic that reaches a release with no page
/// describing it is met by a consumer as an unexplained warning id, and the only way anyone finds
/// out is that consumer asking. A page describing an id that was never implemented — or one that was
/// removed — sends a reader looking for behaviour that does not exist, and reads exactly like a page
/// that is right.
/// </para>
/// <para>
/// The shipped set is read from <c>AnalyzerReleases.Shipped.md</c> and
/// <c>AnalyzerReleases.Unshipped.md</c> rather than from the guide's own table or from
/// <c>DiagnosticIds.cs</c> parsed by hand. Those two files are Roslyn's own release-tracking format,
/// and the RS2000-series analyzers already fail the build when a declared descriptor is missing from
/// them — so they are the one statement of the set that something else is keeping true. Comparing
/// prose against a document that is itself checked is the same move as ADR-0009's: never compare a
/// claim against another claim.
/// </para>
/// </remarks>
public sealed class DiagnosticCoverageTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The page that carries the obligation. One document, named, rather than "anywhere in the
    /// documentation": an id mentioned in passing on some other page is not documentation of it, and
    /// an obligation that any file can discharge is one no file has.
    /// </summary>
    private const string Reference = "doc/guide/diagnostics.{0}.md";

    public static TheoryData<string, string> ShippedByLanguage()
    {
        TheoryData<string, string> data = new();
        foreach (string id in Shipped())
        {
            data.Add(id, "en");
            data.Add(id, "fr");
        }

        return data;
    }

    public static TheoryData<string> Languages() => new("en", "fr");

    [Theory]
    [MemberData(nameof(ShippedByLanguage))]
    public void Every_shipped_diagnostic_is_documented(string id, string language)
    {
        MarkdownDocument reference = Document(string.Format(Reference, language));

        Assert.True(
            reference.HasAnchor(id.ToLowerInvariant()),
            $"{reference.Path} has no section for {id}, which the analyzers declare in " +
            "AnalyzerReleases. A consumer who meets that warning has nothing to read.");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_documented_diagnostic_is_shipped(string language)
    {
        MarkdownDocument reference = Document(string.Format(Reference, language));
        IReadOnlyCollection<string> shipped = Shipped();

        foreach (string heading in reference.Headings)
        {
            Match id = Regex.Match(heading, "^`?(?<id>DCAT\\d{4})`?$", RegexOptions.None, MatchTimeout);
            if (!id.Success) continue;

            Assert.True(
                shipped.Contains(id.Groups["id"].Value),
                $"{reference.Path} documents {id.Groups["id"].Value}, which the analyzers do not " +
                "declare. Either it was removed and the page outlived it, or it was never " +
                "implemented — and a reader cannot tell those apart from a page that is right.");
        }
    }

    /// <summary>
    /// The identifiers not in 1.0 are named in the page, so that a reader who finds a gap in the
    /// sequence is told it is deliberate rather than left to wonder what happened to
    /// <c>DCAT0005</c>.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void The_gaps_in_the_sequence_are_accounted_for(string language)
    {
        MarkdownDocument reference = Document(string.Format(Reference, language));
        IReadOnlyCollection<string> shipped = Shipped();

        int highest = shipped.Select(id => int.Parse(id[4..])).Max();

        for (int number = 1; number <= highest; number++)
        {
            string id = $"DCAT{number:0000}";
            if (shipped.Contains(id)) continue;

            Assert.True(
                reference.Text.Contains(id, StringComparison.Ordinal),
                $"{reference.Path} never mentions {id}, which sits inside the range this package " +
                "publishes and ships in none of it. A reader meeting the gap has no way to tell a " +
                "deliberate omission from an accident.");
        }
    }

    [Fact]
    public void The_shipped_diagnostics_are_discovered()
    {
        IReadOnlyCollection<string> shipped = Shipped();

        Assert.True(
            shipped.Count >= 5,
            $"Only {shipped.Count} diagnostics were read from the AnalyzerReleases files. The " +
            "coverage theories would assert almost nothing — check that the files are still where " +
            "this test looks for them.");
    }

    /// <summary>
    /// Every id declared in either release file. Shipped and unshipped alike: a diagnostic that has
    /// not shipped yet is still one a contributor is about to release, and documenting it after the
    /// release is documenting it late.
    /// </summary>
    private static IReadOnlyCollection<string> Shipped()
    {
        SortedSet<string> ids = new(StringComparer.Ordinal);

        foreach (string file in new[] { "AnalyzerReleases.Shipped.md", "AnalyzerReleases.Unshipped.md" })
        {
            string path = Path.Combine(Repository.Root, "src", "DiagnosticCatalog.Analyzers", file);
            if (!File.Exists(path)) continue;

            foreach (Match id in Regex.Matches(
                         File.ReadAllText(path),
                         "^(?<id>DCAT\\d{4})\\s*\\|",
                         RegexOptions.Multiline,
                         MatchTimeout))
            {
                ids.Add(id.Groups["id"].Value);
            }
        }

        return ids;
    }

    private static MarkdownDocument Document(string path)
    {
        MarkdownDocument? document = Repository.Find(path);
        Assert.True(document is not null, $"{path} was not found under {Repository.Root}.");

        return document!;
    }
}
