using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// A page and its translation land in the same commit (ADR-0022). These are what make that true
/// rather than intended.
/// </summary>
/// <remarks>
/// The failure this prevents has no reader who can report it: someone who cannot read the English
/// page is not in a position to tell anyone that the French one is missing. So it is checked here,
/// where a missing half is a red build instead of a silence.
/// </remarks>
public sealed class BilingualPairTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Every document whose name carries a language suffix. A decision record, which carries none
    /// yet, is simply absent — see <c>doc/CONVENTIONS.en.md</c>, "What the parity check actually
    /// sees".
    /// </summary>
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
    public void A_document_has_its_translation(string path)
    {
        MarkdownDocument document = Repository.Require(path);

        Assert.True(
            Repository.Exists(document.Sibling!),
            $"{path} has no {document.Sibling}. A page merged with its translation to follow does " +
            "not get its translation; write both in the same commit.");
    }

    /// <summary>
    /// Structural parity, which is what catches a page half translated. Two documents with the same
    /// headings are not necessarily saying the same thing — nothing here can tell — but one that
    /// stops six sections early is a translation somebody abandoned, and that is the common failure.
    /// </summary>
    [Theory]
    [MemberData(nameof(BilingualDocuments))]
    public void A_document_and_its_translation_have_the_same_headings(string path)
    {
        MarkdownDocument document = Repository.Require(path);
        MarkdownDocument? sibling = Repository.Find(document.Sibling!);
        if (sibling is null) return;   // A_document_has_its_translation reports the absence.

        Assert.True(
            document.Headings.Count == sibling.Headings.Count,
            $"{path} has {document.Headings.Count} headings and {sibling.Path} has " +
            $"{sibling.Headings.Count}. One of the two is missing a section, which is what a " +
            "translation left half finished looks like.");
    }

    /// <summary>
    /// The code samples are shared between the two languages, character for character: identifiers
    /// are not translated, so the C# on the French page IS the C# on the English one. Comparing the
    /// fence languages in order catches a sample dropped, added, or retagged on one side only.
    /// </summary>
    [Theory]
    [MemberData(nameof(BilingualDocuments))]
    public void A_document_and_its_translation_carry_the_same_code_samples(string path)
    {
        MarkdownDocument document = Repository.Require(path);
        MarkdownDocument? sibling = Repository.Find(document.Sibling!);
        if (sibling is null) return;

        Assert.True(
            document.FenceLanguages.SequenceEqual(sibling.FenceLanguages, StringComparer.Ordinal),
            $"The code blocks of {path} and {sibling.Path} do not line up.\n" +
            $"  {path}: {string.Join(", ", document.FenceLanguages)}\n" +
            $"  {sibling.Path}: {string.Join(", ", sibling.FenceLanguages)}\n" +
            "A sample present in one language and not the other means a reader is shown less " +
            "depending on which page they opened.");
    }

    /// <summary>
    /// A translation carries the same structure as the page it translates: the same list items, the
    /// same table rows, the same notes set apart from the prose.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The heading check above catches the translation somebody abandoned six sections early. This
    /// catches the one nobody abandoned — a page edited on one side only. A bullet added to the
    /// English, a table row appended, a warning set apart in a blockquote: every heading still lines
    /// up, both files still exist, and the French quietly says less than the English while looking
    /// finished. That is the failure ADR-0022 is about, and it is invisible to every other check
    /// here.
    /// </para>
    /// <para>
    /// Counted, not compared. Nothing here can read French, and a translation is not a transcription
    /// — sentences merge, split, and change length. What a faithful translation cannot do is carry a
    /// different number of items in a list or a different number of rows in a table, because those
    /// are the author's structure rather than the translator's prose.
    /// </para>
    /// <para>
    /// A note is counted as a BLOCK, never as a line. French runs longer than English, so the same
    /// blockquote routinely wraps onto one more line — five pairs in this repository differ that way
    /// and all five are correct. Counting lines would have reported every one of them and taught
    /// everybody to ignore the check.
    /// </para>
    /// <para>
    /// Bulleted and numbered items are one count, so a list renumbered as bullets in translation is
    /// not an offence. What is asserted is that the reader is offered the same number of things.
    /// </para>
    /// </remarks>
    [Theory]
    [MemberData(nameof(BilingualDocuments))]
    public void A_document_and_its_translation_have_the_same_shape(string path)
    {
        MarkdownDocument document = Repository.Require(path);
        MarkdownDocument? sibling = Repository.Find(document.Sibling!);
        if (sibling is null) return;   // A_document_has_its_translation reports the absence.

        AssertSameCount(document, sibling, "list items", ListItems);
        AssertSameCount(document, sibling, "table rows", TableRows);
        AssertSameCount(document, sibling, "notes set apart from the prose", Notes);
    }

    private static void AssertSameCount(
        MarkdownDocument document,
        MarkdownDocument sibling,
        string what,
        Func<MarkdownDocument, int> count)
    {
        int here = count(document);
        int there = count(sibling);

        Assert.True(
            here == there,
            $"{document.Path} has {here} {what} and {sibling.Path} has {there}. One of the two was " +
            "edited without the other: both files exist and their headings line up, so nothing else " +
            "here can see it, and the shorter page reads as though it were the whole of it. English " +
            "is canonical (ADR-0022) — bring the translation up to it.");
    }

    /// <summary>
    /// Bulleted and numbered items alike, outside any fenced block.
    /// </summary>
    private static int ListItems(MarkdownDocument document) =>
        CountLines(document, "^\\s*(?:[*-]|[0-9]+\\.) ");

    /// <summary>Every row of every table, the header and its separator included.</summary>
    private static int TableRows(MarkdownDocument document) =>
        CountLines(document, "^\\s*\\|");

    /// <summary>
    /// Blockquote blocks: a run of quoted lines counts once, however long it runs.
    /// </summary>
    private static int Notes(MarkdownDocument document)
    {
        int blocks = 0;
        bool inside = false;

        foreach (string line in document.Prose.Split('\n'))
        {
            bool quoted = Regex.IsMatch(line, "^\\s*>", RegexOptions.None, MatchTimeout);

            if (quoted && !inside) blocks++;

            inside = quoted;
        }

        return blocks;
    }

    /// <summary>
    /// Lines of the document matching a pattern, read from the prose so that a sample showing a
    /// table or a bullet list is not counted as one. The fence check above is what covers samples.
    /// </summary>
    private static int CountLines(MarkdownDocument document, string pattern) =>
        document.Prose
                .Split('\n')
                .Count(line => Regex.IsMatch(line, pattern, RegexOptions.None, MatchTimeout));

    /// <summary>
    /// Guards every theory above against passing by having nothing to say. A discovery that quietly
    /// returned an empty set would report a perfectly translated documentation set that does not
    /// exist, which is the one outcome a check written to be a reminder must not allow.
    /// </summary>
    [Fact]
    public void The_documentation_set_is_discovered()
    {
        IReadOnlyList<MarkdownDocument> bilingual = Repository.Bilingual;

        Assert.True(
            bilingual.Count >= 8,
            $"Only {bilingual.Count} language-suffixed documents were found under " +
            $"{Repository.Root}. The parity theories would assert almost nothing. Check that the " +
            "DocumentationRepositoryRoot stamp still points at the working tree.");

        Assert.Contains(bilingual, document => document.Path.StartsWith("doc/guide/", StringComparison.Ordinal));
    }

}
