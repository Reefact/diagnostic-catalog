using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// One Markdown document, parsed far enough to answer the questions the documentation tests ask of
/// it: what it is called, which language it is in, what it links to, and — for a guide page — where
/// it sits in the reading order.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a Markdown parser. It is a reader for the conventions recorded in
/// <c>doc/CONVENTIONS.en.md</c>, and it is strict about them on purpose: a document that does not
/// match the shape is a document a reader meets in a shape nobody chose.
/// </para>
/// <para>
/// Fenced code blocks are stripped before anything is extracted. Every guide page shows Markdown,
/// XML and <c>.editorconfig</c> samples, and a link or a heading inside a sample is an illustration
/// rather than a claim — checking it would fail on documents that are exactly right.
/// </para>
/// </remarks>
internal sealed class MarkdownDocument
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    private MarkdownDocument(string path, string text)
    {
        Path = path;
        Text = text;

        Prose = StripFences(text, out IReadOnlyList<string> fenceLanguages);
        FenceLanguages = fenceLanguages;
        string prose = Prose;

        Lines = text.Split('\n');
        Title = FirstHeading(Lines);
        Headings = HeadingsOf(prose.Split('\n'));
        Links = LinksOf(prose);
        Language = LanguageOf(path);
        Sibling = SiblingOf(path, Language);
    }

    /// <summary>Repository-relative path, with forward slashes.</summary>
    internal string Path { get; }

    /// <summary>The whole document, with line endings normalised to <c>\n</c>.</summary>
    internal string Text { get; }

    /// <summary>
    /// The document with every fenced code block blanked out. What a page SHOWS inside a fence is an
    /// illustration — a footer a convention page demonstrates, a link a sample contains — and
    /// reading it as a claim would fail the documents that explain the conventions best.
    /// </summary>
    internal string Prose { get; }

    internal IReadOnlyList<string> Lines { get; }

    /// <summary>The text of the first <c>#</c> heading, or the empty string when there is none.</summary>
    internal string Title { get; }

    /// <summary>Every heading, at any level, outside a fenced code block.</summary>
    internal IReadOnlyList<string> Headings { get; }

    /// <summary>Every inline link outside a fenced code block, in document order.</summary>
    internal IReadOnlyList<MarkdownLink> Links { get; }

    /// <summary>The languages tagged on the fenced code blocks, in document order.</summary>
    internal IReadOnlyList<string> FenceLanguages { get; }

    /// <summary><c>"en"</c>, <c>"fr"</c>, or <c>null</c> when the name carries no language suffix.</summary>
    internal string? Language { get; }

    /// <summary>
    /// The repository-relative path this document's translation would have, or <c>null</c> when the
    /// name carries no language suffix.
    /// </summary>
    internal string? Sibling { get; }

    /// <summary>The file name alone, which is what a sibling link inside the document spells.</summary>
    internal string FileName => Path[(Path.LastIndexOf('/') + 1)..];

    internal static MarkdownDocument Load(string relativePath, string absolutePath) =>
        new(relativePath, File.ReadAllText(absolutePath).Replace("\r\n", "\n"));

    /// <summary>
    /// GitHub's heading anchor for a piece of heading text: lowercased, with everything that is not
    /// a letter, a digit, a space, a hyphen or an underscore removed, and spaces turned into
    /// hyphens. Unicode letters survive, which is what makes a French heading linkable.
    /// </summary>
    internal static string Slug(string heading)
    {
        StringBuilder slug = new(heading.Length);
        foreach (char character in heading.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
            {
                slug.Append(character);
            }
            else if (character == ' ')
            {
                slug.Append('-');
            }
        }

        return slug.ToString();
    }

    /// <summary>Whether the document declares a heading an anchor could point at.</summary>
    internal bool HasAnchor(string anchor) =>
        Headings.Any(heading => string.Equals(Slug(heading), anchor, StringComparison.Ordinal));

    private static string FirstHeading(IReadOnlyList<string> lines)
    {
        foreach (string line in lines)
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> HeadingsOf(IReadOnlyList<string> lines)
    {
        List<string> headings = [];
        foreach (string line in lines)
        {
            Match heading = Regex.Match(line, "^(#{1,6})\\s+(?<text>.+?)\\s*$", RegexOptions.None, MatchTimeout);
            if (heading.Success)
            {
                headings.Add(heading.Groups["text"].Value);
            }
        }

        return headings;
    }

    /// <summary>
    /// Inline Markdown links and HTML anchors alike. The navigation footer is written as
    /// <c>&lt;a href="..."&gt;</c> because GitHub honours the surrounding
    /// <c>&lt;div align="center"&gt;</c> and honours no Markdown equivalent, so a reader that saw
    /// only Markdown links would miss precisely the links the reading order is made of.
    /// </summary>
    private static IReadOnlyList<MarkdownLink> LinksOf(string prose)
    {
        List<MarkdownLink> links = [];

        foreach (Match link in Regex.Matches(
                     prose,
                     "\\[(?<text>[^\\]]*)\\]\\((?<target>[^)\\s]+)\\)",
                     RegexOptions.None,
                     MatchTimeout))
        {
            links.Add(new MarkdownLink(link.Groups["text"].Value, link.Groups["target"].Value));
        }

        foreach (Match link in Regex.Matches(
                     prose,
                     "<a\\s+href=\"(?<target>[^\"]+)\"\\s*>(?<text>.*?)</a>",
                     RegexOptions.Singleline,
                     MatchTimeout))
        {
            links.Add(new MarkdownLink(link.Groups["text"].Value, link.Groups["target"].Value));
        }

        return links;
    }

    /// <summary>
    /// Replaces every fenced block with blank lines, so that line-oriented extraction keeps working
    /// and nothing inside a sample is mistaken for a claim about this repository.
    /// </summary>
    private static string StripFences(string text, out IReadOnlyList<string> fenceLanguages)
    {
        List<string> languages = [];
        StringBuilder prose = new(text.Length);
        bool inside = false;
        string closing = string.Empty;

        foreach (string line in text.Split('\n'))
        {
            Match fence = Regex.Match(line, "^\\s*(?<ticks>`{3,})(?<language>[A-Za-z0-9+#-]*)\\s*$", RegexOptions.None, MatchTimeout);

            if (!inside && fence.Success)
            {
                inside = true;
                closing = fence.Groups["ticks"].Value;
                languages.Add(fence.Groups["language"].Value);
            }
            else if (inside && fence.Success && fence.Groups["ticks"].Value.Length >= closing.Length)
            {
                inside = false;
            }
            else if (!inside)
            {
                prose.Append(line);
            }

            prose.Append('\n');
        }

        fenceLanguages = languages;

        return prose.ToString();
    }

    private static string? LanguageOf(string path)
    {
        if (path.EndsWith(".en.md", StringComparison.Ordinal)) return "en";
        if (path.EndsWith(".fr.md", StringComparison.Ordinal)) return "fr";

        return null;
    }

    private static string? SiblingOf(string path, string? language) => language switch
    {
        "en" => string.Concat(path.AsSpan(0, path.Length - ".en.md".Length), ".fr.md"),
        "fr" => string.Concat(path.AsSpan(0, path.Length - ".fr.md".Length), ".en.md"),
        _ => null,
    };
}

/// <summary>One link: the text a reader sees, and where it points.</summary>
internal sealed record MarkdownLink(string Text, string Target)
{
    /// <summary>
    /// Whether the target leaves the repository. An external address is not this repository's to
    /// verify, and reaching for it would put a network call in a unit test.
    /// </summary>
    internal bool IsExternal =>
        Target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        Target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        Target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the target is an anchor inside the document that carries it.</summary>
    internal bool IsLocalAnchor => Target.StartsWith("#", StringComparison.Ordinal);

    /// <summary>The path half of the target, without any anchor.</summary>
    internal string PathPart =>
        Target.Contains('#') ? Target[..Target.IndexOf('#')] : Target;

    /// <summary>The anchor half of the target, without the <c>#</c>, or the empty string.</summary>
    internal string Anchor =>
        Target.Contains('#') ? Target[(Target.IndexOf('#') + 1)..] : string.Empty;
}
