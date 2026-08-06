using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The reading orders of <c>doc/guide/</c>, as expressed by the previous/next footers. Each TRACK
/// is one total order — its pages on it, once, with a single start and a single end — and every
/// page of the folder belongs to exactly one track.
/// </summary>
/// <remarks>
/// <para>
/// Tracks rather than one chain, because one chain made every reader everybody's reader. Threading
/// twenty-six pages end to end meant the last page a consumer needed sent them onward into
/// publishing a catalogue, and there was no way to say "you are done" — the only page with no next
/// link was the last page of the internals. A track ends, and its footer leaves the reader at the
/// map.
/// </para>
/// <para>
/// What this still prevents is the orphan: a page written, committed, and linked by nothing. It
/// costs exactly as much to write as any other page and is read by nobody, and no reader will
/// report it because a reader who never reaches a page does not know it is there. Every page having
/// to sit on exactly one track makes adding one without threading it a build failure.
/// </para>
/// <para>
/// The tracks are read from the MAP, which declares each with a <c>&lt;!-- track: id --&gt;</c>
/// marker and a numbered list. The map is where a reader meets them, so it is where they are
/// stated; the footers are then held to what it says, in both languages.
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
        TheoryData<string> paths = [];
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
    public void Every_page_sits_on_exactly_one_track(string language)
    {
        IReadOnlyList<MarkdownDocument> pages = PagesIn(language);
        List<Track> tracks = TracksIn(language);

        List<string> threaded = [.. tracks.SelectMany(track => track.Pages)];

        List<string> repeated = threaded
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            repeated.Count == 0,
            $"doc/guide/README.{language}.md lists {string.Join(", ", repeated)} on more than one " +
            "track. A page on two tracks has two predecessors, so a reader who goes back does not " +
            "return where they were.");

        List<string> orphans = pages
            .Select(page => page.FileName)
            .Except(threaded, StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            orphans.Count == 0,
            $"doc/guide/README.{language}.md puts {string.Join(", ", orphans)} on no track at all. " +
            "A page nothing navigates to is a page nobody reads.");

        List<string> strangers = threaded
            .Except(pages.Select(page => page.FileName), StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            strangers.Count == 0,
            $"doc/guide/README.{language}.md lists {string.Join(", ", strangers)} on a track, and no " +
            "such page exists in doc/guide/.");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Each_track_is_threaded_by_the_footers_in_the_order_the_map_lists(string language)
    {
        Dictionary<string, Navigation> navigation = PagesIn(language).ToDictionary(
            page => page.FileName,
            Navigation.Of,
            StringComparer.Ordinal);

        foreach (Track track in TracksIn(language))
        {
            List<string> threaded = Walk(track.Pages[0], navigation, language, track.Id);

            Assert.True(
                threaded.SequenceEqual(track.Pages, StringComparer.Ordinal),
                $"doc/guide/README.{language}.md lists the {track.Id} track in an order the footers " +
                "do not thread.\n" +
                $"  listed:   {string.Join(" → ", track.Pages)}\n" +
                $"  threaded: {string.Join(" → ", threaded)}");
        }
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void A_track_starts_and_ends_where_the_map_says(string language)
    {
        Dictionary<string, Navigation> navigation = PagesIn(language).ToDictionary(
            page => page.FileName,
            Navigation.Of,
            StringComparer.Ordinal);

        foreach (Track track in TracksIn(language))
        {
            string first = track.Pages[0];
            string last = track.Pages[^1];

            Assert.True(
                Name(navigation[first].Previous) is null,
                $"{first} opens the {track.Id} track and carries a ← to " +
                $"{Name(navigation[first].Previous)}. The way in to a track is the map, which its ↑ " +
                "already offers.");

            Assert.True(
                Name(navigation[last].Next) is null,
                $"{last} ends the {track.Id} track and carries a → to {Name(navigation[last].Next)}. " +
                "A track that runs on into the next one is the single chain these tracks replace: a " +
                "reader who finished what they came for is sent into somebody else's chapter.");
        }
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
    public void The_map_opens_onto_the_first_page_of_the_default_track(string language)
    {
        MarkdownDocument map = Repository.Require($"doc/guide/README.{language}.md");
        Navigation navigation = Navigation.Of(map);
        List<Track> tracks = TracksIn(language);

        Assert.True(
            tracks.Count > 0,
            $"doc/guide/README.{language}.md declares no track, so the map opens onto nothing.");

        Assert.True(
            Name(navigation.Next) == tracks[0].Pages[0],
            $"doc/guide/README.{language}.md sends the reader to " +
            $"{Name(navigation.Next) ?? "nothing"}, but the first track starts at " +
            $"{tracks[0].Pages[0]}. The map is the way in; it has to open onto the page the default " +
            "track actually begins with.");
    }

    [Fact]
    public void The_two_languages_thread_the_same_tracks()
    {
        List<Track> english = [.. TracksIn("en")];
        List<Track> french = [.. TracksIn("fr")];

        Assert.True(
            english.Select(track => track.Id).SequenceEqual(
                french.Select(track => track.Id), StringComparer.Ordinal),
            "The English and French maps declare different tracks.\n" +
            $"  en: {string.Join(", ", english.Select(track => track.Id))}\n" +
            $"  fr: {string.Join(", ", french.Select(track => track.Id))}");

        for (int index = 0; index < english.Count; index++)
        {
            Assert.True(
                english[index].Pages.Select(Stem).SequenceEqual(
                    french[index].Pages.Select(Stem), StringComparer.Ordinal),
                $"The {english[index].Id} track differs between the two languages.\n" +
                $"  en: {string.Join(" → ", english[index].Pages.Select(Stem))}\n" +
                $"  fr: {string.Join(" → ", french[index].Pages.Select(Stem))}\n" +
                "A translation is the same document in another language, and that includes the " +
                "order it walks the reader through.");
        }
    }

    [Fact]
    public void The_guide_is_discovered()
    {
        Assert.True(
            Repository.Guide.Count >= 4,
            $"Only {Repository.Guide.Count} pages were found under doc/guide/, so the navigation " +
            "theories would assert almost nothing.");

        foreach (string language in new[] { "en", "fr" })
        {
            Assert.True(
                TracksIn(language).Count >= 2,
                $"doc/guide/README.{language}.md declares fewer than two tracks. The markers are " +
                "<!-- track: id --> followed by a numbered list; a map written another way leaves " +
                "every footer here unchecked.");
        }
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
    private static List<MarkdownDocument> PagesIn(string language) =>
        Repository.Guide
            .Where(page => page.Language == language && !IsMap(page))
            .OrderBy(page => page.Path, StringComparer.Ordinal)
            .ToList();

    /// <summary>One reading track, as the map declares it.</summary>
    private sealed record Track(string Id, IReadOnlyList<string> Pages);

    /// <summary>
    /// The tracks a map declares: a <c>&lt;!-- track: id --&gt;</c> marker, then the numbered list
    /// that follows it, until the next marker.
    /// </summary>
    /// <remarks>
    /// A marker rather than the heading above it, because the heading is prose in two languages and
    /// the id has to be the same word in both — that is what lets the two maps be compared at all.
    /// </remarks>
    private static List<Track> TracksIn(string language)
    {
        MarkdownDocument map = Repository.Require($"doc/guide/README.{language}.md");

        List<Track> tracks = [];
        string? id = null;
        List<string> pages = [];

        foreach (string line in map.Lines)
        {
            Match marker = Regex.Match(line, "^<!--\\s*track:\\s*(?<id>[a-z-]+)\\s*-->$",
                                       RegexOptions.None, MatchTimeout);
            if (marker.Success)
            {
                if (id is not null) tracks.Add(new Track(id, pages));
                id = marker.Groups["id"].Value;
                pages = [];

                continue;
            }

            if (id is null) continue;

            Match item = Regex.Match(
                line,
                "^(?<number>\\d+)\\.\\s+\\[[^\\]]*\\]\\((?<target>[^)#\\s]+)",
                RegexOptions.None,
                MatchTimeout);

            if (item.Success)
            {
                string target = item.Groups["target"].Value;
                pages.Add(target[(target.LastIndexOf('/') + 1)..]);
            }
        }

        if (id is not null) tracks.Add(new Track(id, pages));

        return tracks;
    }

    /// <summary>
    /// Follows one track from its start, stopping if it ever revisits a page. A cycle would
    /// otherwise walk forever, and reporting "visited fewer pages than the track lists" is the same
    /// failure a reader meets: pages the order never reaches.
    /// </summary>
    private static List<string> Walk(
        string start, Dictionary<string, Navigation> navigation, string language, string track)
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
            $"The {language} {track} track loops back to {current}. A chain with a cycle has no end, " +
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
