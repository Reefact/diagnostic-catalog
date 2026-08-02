using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The language banner is the only place a reader is offered the other language, so a page that
/// carries none is a page one half of the audience never finds — even though it was written for
/// them and is sitting in the same folder.
/// </summary>
/// <remarks>
/// The shape is checked structurally rather than by matching the exact wording: the label differs
/// between languages ("Languages:" / "Langues :") and would otherwise have to be listed here, which
/// puts a third spelling of the convention in a third place. What is required is that the banner
/// follows the title, offers both languages, and links to the file that actually exists.
/// </remarks>
public sealed class LanguageBannerTests
{
    private const string Globe = "🌍";
    private const string UnitedKingdom = "🇬🇧";
    private const string France = "🇫🇷";

    public static TheoryData<string> BilingualDocuments()
    {
        TheoryData<string> paths = [];
        foreach (MarkdownDocument document in Repository.Bilingual)
        {
            paths.Add(document.Path);
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(BilingualDocuments))]
    public void The_banner_follows_the_title(string path)
    {
        MarkdownDocument document = Repository.Require(path);

        Assert.True(
            document.Title.Length > 0,
            $"{path} has no `# ` heading, so a reader arriving from a link is not told what they " +
            "opened.");

        int title = Array.FindIndex(document.Lines.ToArray(), line => line.StartsWith("# ", StringComparison.Ordinal));
        string banner = document.Lines.Skip(title + 1).FirstOrDefault(line => line.Trim().Length > 0) ?? string.Empty;

        Assert.True(
            banner.Contains(Globe, StringComparison.Ordinal),
            $"{path}: the first block after the title is not the language banner.\n" +
            $"  found: {banner}\n" +
            "See doc/CONVENTIONS.en.md, \"One H1, then the language banner\".");
    }

    [Theory]
    [MemberData(nameof(BilingualDocuments))]
    public void The_banner_offers_both_languages(string path)
    {
        string banner = BannerOf(Repository.Require(path));

        Assert.True(
            banner.Contains(UnitedKingdom, StringComparison.Ordinal) &&
            banner.Contains(France, StringComparison.Ordinal),
            $"{path}: the language banner names only one language.\n  found: {banner}");
    }

    /// <summary>
    /// The banner offers exactly one link, and it is the sibling. Two links would mean the page
    /// offers a language it is already written in; a link to anything else means a reader clicking
    /// "Français" lands somewhere nobody intended.
    /// </summary>
    /// <remarks>
    /// The target is resolved the way a renderer resolves it, rather than matched against the
    /// sibling's file name. Almost every pair sits in one folder, where the two are the same
    /// question; the project README and its translation do not (ADR-0029), and a check that compares
    /// file names would read a correct <c>doc/project-readme.fr.md</c> as pointing somewhere else.
    /// </remarks>
    [Theory]
    [MemberData(nameof(BilingualDocuments))]
    public void The_banner_links_to_the_translation(string path)
    {
        MarkdownDocument document = Repository.Require(path);

        List<MarkdownLink> links = document.Links
            .Where(link => BannerOf(document).Contains($"]({link.Target})", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            links.Count == 1,
            $"{path}: the language banner carries {links.Count} links; it must carry exactly one, " +
            "pointing at the translation.");

        string? target = Repository.Resolve(document, links[0].PathPart);

        Assert.True(
            string.Equals(target, document.Sibling, StringComparison.Ordinal),
            $"{path}: the language banner points at {links[0].Target}, which resolves to " +
            $"{target ?? "nothing"}, not at {document.Sibling}.");
    }

    [Fact]
    public void The_banners_are_discovered()
    {
        Assert.True(
            Repository.Bilingual.Count >= 8,
            "Too few language-suffixed documents were found for these theories to assert anything.");
    }

    private static string BannerOf(MarkdownDocument document)
    {
        int title = Array.FindIndex(document.Lines.ToArray(), line => line.StartsWith("# ", StringComparison.Ordinal));
        if (title < 0) return string.Empty;

        // The banner is two lines joined by a hard break: the label, then the flags.
        List<string> banner = [];
        foreach (string line in document.Lines.Skip(title + 1))
        {
            if (line.Trim().Length == 0)
            {
                if (banner.Count > 0) break;

                continue;
            }

            banner.Add(line);
        }

        return string.Join("\n", banner);
    }

}
