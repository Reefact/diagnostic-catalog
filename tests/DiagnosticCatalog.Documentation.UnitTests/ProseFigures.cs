using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Reading a figure a page states in prose: the number words both languages spell counts in, and the
/// pattern discipline that keeps a rewritten sentence from quietly going unchecked.
/// </summary>
/// <remarks>
/// <para>
/// Several tests here hold a number written in prose to something recounted from the tree — how many
/// catalogues the generator produces, how many projects a rejected discovery heuristic would select.
/// They measure different things and share a hazard: the figure is prose, so it is read with a
/// pattern, and a pattern that stops matching reports nothing at all. Silence from a check that has
/// gone blind is indistinguishable from silence from a check that passed.
/// </para>
/// <para>
/// So <see cref="Require"/> exists, and it fails rather than returns empty. Every caller names itself
/// in the message, because the reader who meets the failure is holding a rewritten sentence and needs
/// to be sent to the file that reads it rather than left to search.
/// </para>
/// <para>
/// The vocabulary is shared for the same reason it is small. Two tests carrying two maps of number
/// words is the drift they both exist to prevent, one level down: a page spelled with a word one map
/// knows and the other does not fails in a way that points at nothing. English and French sit in one
/// map because the two overlap without disagreeing — <c>six</c> is six either way — and a page is
/// read by the shape of its sentence, not by which half of the pair it belongs to.
/// </para>
/// </remarks>
internal static class ProseFigures
{
    /// <summary>
    /// The ceiling every pattern here runs under. Shared so that a catastrophic pattern is bounded
    /// wherever it is written, rather than wherever somebody remembered to bound it.
    /// </summary>
    internal static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The number words the documentation spells counts in, in both languages. Deliberately stops at
    /// twelve: past that a page writes the digit, and a longer map would be a parser nobody asked for.
    /// A word outside it fails loudly through <see cref="Read"/> rather than being read as zero.
    /// </summary>
    private static readonly Dictionary<string, int> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
        ["six"] = 6,       // and French six, which is the same number spelled the same way
        ["seven"] = 7,
        ["eight"] = 8,
        ["nine"] = 9,
        ["ten"] = 10,
        ["eleven"] = 11,
        ["twelve"] = 12,
        ["un"] = 1,
        ["une"] = 1,
        ["deux"] = 2,
        ["trois"] = 3,
        ["quatre"] = 4,
        ["cinq"] = 5,
        ["sept"] = 7,
        ["huit"] = 8,
        ["neuf"] = 9,
        ["dix"] = 10,
        ["onze"] = 11,
        ["douze"] = 12,
    };

    /// <summary>
    /// The number a word spells, failing when the word is outside the shared vocabulary. The failure
    /// names the page and the reader, because the fix is either a word worth adding here or a sentence
    /// that drifted away from how the rest of the documentation counts.
    /// </summary>
    internal static int Read(string word, string path, string reader)
    {
        Assert.True(
            Words.ContainsKey(word),
            $"{path} spells a count as \"{word}\", which is outside the number words {nameof(ProseFigures)} " +
            $"reads. Add it there if the documentation has started counting that high, or spell the " +
            $"number the way the rest of the pages do. Read by {reader}.");

        return Words[word];
    }

    /// <summary>Whether a word is one this can read, for callers that decide rather than assert.</summary>
    internal static bool Knows(string word) => Words.ContainsKey(word);

    /// <summary>
    /// A pattern that MUST match, because a figure nothing reads is a figure nothing checks. The
    /// failure says which sentence went missing and where the pattern that looked for it lives.
    /// </summary>
    internal static Match Require(MarkdownDocument document, string pattern, string what, string reader)
    {
        Match match = Regex.Match(document.Prose, pattern, RegexOptions.Multiline, MatchTimeout);

        Assert.True(
            match.Success,
            $"{document.Path} no longer carries {what} in a shape this can read. The figure is checked " +
            $"against the repository, so a rewrite this reader cannot follow silently stops checking it " +
            $"— which is the failure it exists to prevent. Update the pattern in {reader} to match the " +
            "new wording, or restore a countable statement.");

        return match;
    }

    /// <summary>
    /// Every match of a pattern in a document, for readers that sweep the documentation rather than
    /// visit named pages. Returns nothing when the page makes no such claim, which is the normal case
    /// and not a failure — <see cref="Require"/> is for the pages that must carry one.
    /// </summary>
    internal static IEnumerable<Match> Sweep(MarkdownDocument document, string pattern)
    {
        foreach (Match match in Regex.Matches(document.Prose, pattern, RegexOptions.Multiline, MatchTimeout))
        {
            yield return match;
        }
    }
}
