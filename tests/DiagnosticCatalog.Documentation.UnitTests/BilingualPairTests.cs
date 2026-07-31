using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// A page and its translation land in the same commit (ADR-0020). These are what make that true
/// rather than intended.
/// </summary>
/// <remarks>
/// The failure this prevents has no reader who can report it: someone who cannot read the English
/// page is not in a position to tell anyone that the French one is missing. So it is checked here,
/// where a missing half is a red build instead of a silence.
/// </remarks>
public sealed class BilingualPairTests
{
    /// <summary>
    /// Every document whose name carries a language suffix. A decision record, which carries none
    /// yet, is simply absent — see <c>doc/CONVENTIONS.en.md</c>, "What the parity check actually
    /// sees".
    /// </summary>
    public static TheoryData<string> BilingualDocuments()
    {
        TheoryData<string> paths = new();
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
        MarkdownDocument document = Document(path);

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
        MarkdownDocument document = Document(path);
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
        MarkdownDocument document = Document(path);
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

    private static MarkdownDocument Document(string path)
    {
        MarkdownDocument? document = Repository.Find(path);
        Assert.True(document is not null, $"{path} was discovered and then could not be read.");

        return document!;
    }
}
