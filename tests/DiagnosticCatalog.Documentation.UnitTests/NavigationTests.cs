using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The reading order of <c>doc/guide/</c>, as expressed by the previous/next footers, must be one
/// total order — every page on it, once, with a single start and a single end.
/// </summary>
/// <remarks>
/// <para>
/// What this really prevents is the orphan: a page written, committed, and linked by nothing. It
/// costs exactly as much to write as any other page and is read by nobody, and no reader will
/// report it because a reader who never reaches a page does not know it is there. A chain that has
/// to cover every file in the folder makes adding a page without threading it a build failure.
/// </para>
/// <para>
/// Both languages are checked separately and then compared, because a footer edited in one language
/// only produces two documentation sets that walk their reader through the material in two
/// different orders.
/// </para>
/// </remarks>
public sealed class NavigationTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    public static TheoryData<string> Languages() => new("en", "fr");

    public static TheoryData<string> GuidePages()
    {
        TheoryData<string> paths = new();
        foreach (MarkdownDocument document in Repository.Guide)
        {
            paths.Add(document.Path);
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(GuidePages))]
    public void A_guide_page_ends_with_a_navigation_footer(string path)
    {
        MarkdownDocument document = Repository.Require(path);
        Navigation navigation = Navigation.Of(document);

        Assert.True(
            navigation.Found,
            $"{path} carries no navigation footer. A page a reader cannot leave by going forward is " +
            "a page the reading order stops at. See doc/CONVENTIONS.en.md, \"The navigation footer\".");
    }

    /// <summary>
    /// Every page except the map points back at the map. The map is excluded because it IS the table
    /// of contents: a link from it to itself says nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(GuidePages))]
    public void A_guide_page_points_back_at_the_map(string path)
    {
        MarkdownDocument document = Repository.Require(path);
        if (IsMap(document)) return;

        Navigation navigation = Navigation.Of(document);

        Assert.True(
            navigation.TableOfContents is not null,
            $"{path}: the navigation footer offers no way back to the map.");

        string expected = $"./README.{document.Language}.md";
        Assert.True(
            navigation.TableOfContents == expected,
            $"{path}: the footer's table-of-contents link is {navigation.TableOfContents}, not {expected}.");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void The_footers_describe_one_total_order(string language)
    {
        IReadOnlyList<MarkdownDocument> pages = PagesIn(language);
        Dictionary<string, Navigation> navigation = pages.ToDictionary(
            page => page.FileName,
            Navigation.Of,
            StringComparer.Ordinal);

        List<string> starts = navigation
            .Where(entry => Name(entry.Value.Previous) is null)
            .Select(entry => entry.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            starts.Count == 1,
            $"The {language} reading order has {starts.Count} starting pages ({string.Join(", ", starts)}). " +
            "Exactly one page carries no previous link, and it is the map.");

        List<string> ends = navigation
            .Where(entry => Name(entry.Value.Next) is null)
            .Select(entry => entry.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            ends.Count == 1,
            $"The {language} reading order has {ends.Count} final pages ({string.Join(", ", ends)}). " +
            "Exactly one page carries no next link.");

        List<string> walked = Walk(starts[0], navigation, language);

        Assert.True(
            walked.Count == pages.Count,
            $"The {language} reading order visits {walked.Count} of the {pages.Count} pages in " +
            $"doc/guide/. Never reached: " +
            $"{string.Join(", ", pages.Select(page => page.FileName).Except(walked, StringComparer.Ordinal))}. " +
            "A page nothing navigates to is a page nobody reads.");
    }

    /// <summary>
    /// Every <c>←</c> is the exact inverse of the corresponding <c>→</c>. Without this a chain can
    /// walk forward correctly and still send a reader who goes back to the wrong page.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void Previous_and_next_are_inverse(string language)
    {
        IReadOnlyList<MarkdownDocument> pages = PagesIn(language);
        Dictionary<string, Navigation> navigation = pages.ToDictionary(
            page => page.FileName,
            Navigation.Of,
            StringComparer.Ordinal);

        foreach (KeyValuePair<string, Navigation> entry in navigation)
        {
            string? next = Name(entry.Value.Next);
            if (next is null) continue;

            Assert.True(
                navigation.ContainsKey(next),
                $"{entry.Key} points forward at {next}, which is not a page of doc/guide/.");

            Assert.True(
                Name(navigation[next].Previous) == entry.Key,
                $"{entry.Key} points forward at {next}, but {next} points back at " +
                $"{Name(navigation[next].Previous) ?? "nothing"}. A reader who goes forward and then " +
                "back does not return where they were.");
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void The_map_opens_onto_the_first_page_of_the_order(string language)
    {
        MarkdownDocument map = Repository.Require($"doc/guide/README.{language}.md");
        Navigation navigation = Navigation.Of(map);
        List<string> order = OrderIn(language);

        Assert.True(
            order.Count > 0,
            $"The {language} reading order is empty, so the map opens onto nothing.");

        Assert.True(
            Name(navigation.Next) == order[0],
            $"doc/guide/README.{language}.md sends the reader to " +
            $"{Name(navigation.Next) ?? "nothing"}, but the reading order starts at {order[0]}. " +
            "The map is the way in; it has to open onto the page the order actually begins with.");
    }

    [Fact]
    public void The_two_languages_thread_the_same_order()
    {
        List<string> english = OrderIn("en").Select(Stem).ToList();
        List<string> french = OrderIn("fr").Select(Stem).ToList();

        Assert.True(
            english.SequenceEqual(french, StringComparer.Ordinal),
            "The English and French reading orders differ.\n" +
            $"  en: {string.Join(" → ", english)}\n" +
            $"  fr: {string.Join(" → ", french)}\n" +
            "A translation is the same document in another language, and that includes the order it " +
            "walks the reader through.");
    }

    /// <summary>
    /// The map's own numbered reading list is the order the footers thread. Two statements of the
    /// same order, in one folder, that were allowed to disagree would leave a reader following one
    /// of them and a maintainer maintaining the other.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void The_map_lists_the_reading_order_it_threads(string language)
    {
        MarkdownDocument map = Repository.Require($"doc/guide/README.{language}.md");
        List<string> listed = NumberedTargets(map);
        List<string> threaded = OrderIn(language);

        Assert.True(
            listed.SequenceEqual(threaded, StringComparer.Ordinal),
            $"doc/guide/README.{language}.md lists a reading order the footers do not thread.\n" +
            $"  listed:   {string.Join(" → ", listed)}\n" +
            $"  threaded: {string.Join(" → ", threaded)}");
    }

    [Fact]
    public void The_guide_is_discovered()
    {
        Assert.True(
            Repository.Guide.Count >= 4,
            $"Only {Repository.Guide.Count} pages were found under doc/guide/, so the navigation " +
            "theories would assert almost nothing.");
    }

    /// <summary>A page's name without its language suffix, which is what the two orders share.</summary>
    private static string Stem(string fileName) =>
        fileName.EndsWith(".en.md", StringComparison.Ordinal) || fileName.EndsWith(".fr.md", StringComparison.Ordinal)
            ? fileName[..^".en.md".Length]
            : fileName;

    private static bool IsMap(MarkdownDocument document) =>
        document.FileName.StartsWith("README.", StringComparison.Ordinal);

    /// <summary>
    /// The content pages of one language, which is what the reading order threads. The map is
    /// excluded: it is the way IN to the order rather than a step along it, and its own footer
    /// points back out to the project README. Counting it as a link would give the chain two heads
    /// and force the first content page to carry a redundant way back to a table of contents it
    /// already links.
    /// </summary>
    private static IReadOnlyList<MarkdownDocument> PagesIn(string language) =>
        Repository.Guide
            .Where(page => page.Language == language && !IsMap(page))
            .OrderBy(page => page.Path, StringComparer.Ordinal)
            .ToList();

    private static List<string> OrderIn(string language)
    {
        IReadOnlyList<MarkdownDocument> pages = PagesIn(language);
        Dictionary<string, Navigation> navigation = pages.ToDictionary(
            page => page.FileName,
            Navigation.Of,
            StringComparer.Ordinal);

        string? start = navigation
            .Where(entry => Name(entry.Value.Previous) is null)
            .Select(entry => entry.Key)
            .FirstOrDefault();

        return start is null ? [] : Walk(start, navigation, language);
    }

    /// <summary>
    /// Follows the chain from its start, stopping if it ever revisits a page. A cycle would
    /// otherwise walk forever, and reporting "visited fewer pages than exist" is the same failure a
    /// reader meets: pages the order never reaches.
    /// </summary>
    private static List<string> Walk(string start, IReadOnlyDictionary<string, Navigation> navigation, string language)
    {
        List<string> walked = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        string? current = start;

        while (current is not null && seen.Add(current))
        {
            walked.Add(current);
            current = navigation.TryGetValue(current, out Navigation? page) ? Name(page.Next) : null;
        }

        Assert.True(
            current is null,
            $"The {language} reading order loops back to {current}. A chain with a cycle has no end, " +
            "and a reader following it never arrives anywhere.");

        return walked;
    }

    /// <summary>
    /// The file name a footer link points at, when it points inside <c>doc/guide/</c>, and
    /// <c>null</c> otherwise.
    /// <para>
    /// The map's footer carries a <c>←</c> out to the project README, which is a way back rather
    /// than a predecessor. Reading it as one would leave the chain with no start at all, so a target
    /// that climbs out of the folder counts as no link — the map is then the one page with nothing
    /// before it, which is what it is.
    /// </para>
    /// </summary>
    private static string? Name(string? target)
    {
        if (target is null) return null;

        string path = target.Contains('#') ? target[..target.IndexOf('#')] : target;
        if (path.Contains("..", StringComparison.Ordinal)) return null;

        return path[(path.LastIndexOf('/') + 1)..];
    }

    /// <summary>
    /// The link targets of the last numbered list in a document, which in the map is the reading
    /// order. The last one, because the map's earlier sections list pages by intent and repeat
    /// them: those are entry points, not an order.
    /// </summary>
    private static List<string> NumberedTargets(MarkdownDocument map)
    {
        List<string> targets = [];
        foreach (string line in map.Lines)
        {
            Match item = Regex.Match(
                line,
                "^(?<number>\\d+)\\.\\s+\\[[^\\]]*\\]\\((?<target>[^)#\\s]+)",
                RegexOptions.None,
                MatchTimeout);

            if (!item.Success) continue;

            if (item.Groups["number"].Value == "1")
            {
                targets.Clear();
            }

            targets.Add(item.Groups["target"].Value[(item.Groups["target"].Value.LastIndexOf('/') + 1)..]);
        }

        return targets;
    }

}

/// <summary>
/// The three links a navigation footer may carry, read off the last centred block in a document.
/// Which is which is decided by the arrow in the link text, not by position: a page at either end of
/// the order carries two links rather than three, so counting would misread them.
/// </summary>
internal sealed class Navigation
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    private Navigation(bool found, string? previous, string? tableOfContents, string? next)
    {
        Found = found;
        Previous = previous;
        TableOfContents = tableOfContents;
        Next = next;
    }

    internal bool Found { get; }

    internal string? Previous { get; }

    internal string? TableOfContents { get; }

    internal string? Next { get; }

    internal static Navigation Of(MarkdownDocument document)
    {
        // Prose, not Text: doc/CONVENTIONS.en.md shows a navigation footer inside a fenced block,
        // and a page that documents the convention must not be read as if it were applying it.
        MatchCollection blocks = Regex.Matches(
            document.Prose,
            "<div align=\"center\">(?<body>.*?)</div>",
            RegexOptions.Singleline,
            MatchTimeout);

        if (blocks.Count == 0) return new Navigation(false, null, null, null);

        string body = blocks[^1].Groups["body"].Value;

        string? previous = null;
        string? contents = null;
        string? next = null;

        foreach (Match link in Regex.Matches(
                     body,
                     "<a\\s+href=\"(?<target>[^\"]+)\"\\s*>(?<text>.*?)</a>",
                     RegexOptions.Singleline,
                     MatchTimeout))
        {
            string text = link.Groups["text"].Value.Trim();
            string target = link.Groups["target"].Value;

            if (text.StartsWith('←')) previous = target;
            else if (text.StartsWith('↑')) contents = target;
            else if (text.EndsWith('→')) next = target;
        }

        return new Navigation(true, previous, contents, next);
    }
}
