using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// A page and its translation state the same FACTS: the same diagnostic ids, the same package ids,
/// the same command-line options, the same addresses, the same versions.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BilingualPairTests"/> counts structure — headings, list items, table rows — and that
/// catches a translation somebody abandoned or a page edited on one side only. What it cannot catch
/// is the pair that lines up perfectly and disagrees: a rule id corrected in English, an option
/// renamed in one half, a version bumped in the other, an address updated once. Every heading still
/// matches, both files still exist, and the French reader is told something that is no longer true.
/// </para>
/// <para>
/// None of these tokens is translatable. A rule id, a package name, an MSBuild property, a
/// <c>--switch</c>, a URL and a release number are the same characters in both languages, because
/// they are the same characters in the code — which is exactly what
/// <c>doc/CONVENTIONS.en.md</c> already asks of a translator, and what nothing checked.
/// </para>
/// <para>
/// Compared as SETS: the question is whether one half states a fact the other does not, which is
/// what "the same document in another language" means. How MANY times a page repeats a version in
/// its prose is the translator's business — a sentence that merges two mentions into one has lost
/// nothing — and counting occurrences would report every such pair while teaching everybody to
/// ignore the check.
/// </para>
/// <para>
/// A link is normalised to its language-free form first — <c>concepts.en.md</c> and
/// <c>concepts.fr.md</c> are the same target — because the one address a translation SHOULD differ
/// on is the sibling it points at. Everything else about a link is compared as written, so an
/// English URL left in a French page is reported by the check that a French page reaches French
/// pages, next door, rather than silently accepted here.
/// </para>
/// <para>
/// <b>The exemption is local and needs a reason.</b> A page that genuinely has to state a fact on one
/// side only declares it, in that page, as
/// <c>&lt;!-- dcat-doc:diverges TOKEN why --&gt;</c>. Per document rather than in this file, so the
/// reason sits where the divergence is and the same token elsewhere still fails.
/// </para>
/// </remarks>
public sealed class BilingualFactTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// What counts as a fact, and what each kind is called when one goes missing.
    /// </summary>
    /// <remarks>
    /// Each pattern matches something a translator may not touch. Bare numbers are deliberately
    /// absent: a sentence can carry a different count of things in two languages without either being
    /// wrong — "the two halves" against "les deux moitiés" — and the numbers that DO matter are
    /// carried by a version, an id, or a table this repository recounts from the tree
    /// (<see cref="CatalogueFactsTests"/>, <see cref="ReleaseTrainTests"/>).
    /// </remarks>
    private static readonly (string What, string Pattern)[] Facts =
    [
        ("diagnostic ids", "(?<!\\w)(?:DCAT|CA|IDE|SA|RS|IL|SYSLIB|ASP|BL|MSTEST|CS)[0-9]{4,5}(?!\\w)"),
        ("Sonar rule ids", "(?<!\\w)S[0-9]{3,4}(?!\\w)"),
        ("package ids", "(?<!\\w)DiagnosticCatalog(?:\\.[A-Za-z]+)?(?![\\w.])"),
        ("command-line options", "(?<!\\w)--[a-z][a-z0-9-]{2,}"),
        ("versions", "(?<![\\w.])[0-9]+\\.[0-9]+\\.[0-9]+(?:\\.[0-9]+)?(?![\\w.])"),
        ("addresses", "https?://[^\\s)>\\]\"]+"),
    ];

    public static TheoryData<string> BilingualDocuments()
    {
        TheoryData<string> paths = [];
        foreach (MarkdownDocument document in Repository.Bilingual)
        {
            if (document.Language == "en") paths.Add(document.Path);
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(BilingualDocuments))]
    public void A_document_and_its_translation_state_the_same_facts(string path)
    {
        MarkdownDocument document = Repository.Require(path);
        MarkdownDocument? sibling = Repository.Find(document.Sibling!);
        if (sibling is null) return;   // BilingualPairTests reports the absence.

        HashSet<string> exempt = Diverging(document);
        exempt.UnionWith(Diverging(sibling));

        foreach ((string what, string pattern) in Facts)
        {
            List<string> here = Occurrences(document, pattern, exempt);
            List<string> there = Occurrences(sibling, pattern, exempt);

            string onlyHere = OnlyIn(here, there);
            string onlyThere = OnlyIn(there, here);

            Assert.True(
                onlyHere.Length == 0 && onlyThere.Length == 0,
                $"{document.Path} and {sibling.Path} do not state the same {what}.\n" +
                $"  only in {document.Path}: {(onlyHere.Length == 0 ? "—" : onlyHere)}\n" +
                $"  only in {sibling.Path}: {(onlyThere.Length == 0 ? "—" : onlyThere)}\n" +
                "None of these is translatable: it is the same word in both languages because it is " +
                "the same word in the code. English is canonical (ADR-0022), so the English page is " +
                "normally the one that is right.\n" +
                "If one half genuinely has to state it alone, declare it in that page:\n" +
                "  <!-- dcat-doc:diverges TOKEN why this half states it alone -->");
        }
    }

    /// <summary>
    /// An accepted record and its translation carry the same status, pointing at the same successor.
    /// </summary>
    /// <remarks>
    /// The status is the one line of an ADR a reader checks before reading any of it, and it is the
    /// line most likely to be edited on one side alone: superseding a record means touching the
    /// predecessor's status in both halves, long after both were written. A French reader met with
    /// "Accepted" on a record English calls superseded is reading a decision that is not in force.
    /// </remarks>
    [Theory]
    [MemberData(nameof(BilingualDocuments))]
    public void An_architecture_decision_carries_the_same_status_in_both_languages(string path)
    {
        if (!path.Contains("/adr/", StringComparison.Ordinal)) return;

        MarkdownDocument document = Repository.Require(path);
        MarkdownDocument? sibling = Repository.Find(document.Sibling!);
        if (sibling is null) return;

        (string Kind, string Target) here = Status(document);
        (string Kind, string Target) there = Status(sibling);

        Assert.True(
            string.Equals(here.Kind, there.Kind, StringComparison.OrdinalIgnoreCase),
            $"{document.Path} is {here.Kind} and {sibling.Path} is {there.Kind}. A record is in force " +
            "or it is not, in both languages at once.");

        Assert.True(
            string.Equals(here.Target, there.Target, StringComparison.Ordinal),
            $"{document.Path} is superseded by {(here.Target.Length == 0 ? "nothing" : here.Target)} " +
            $"and {sibling.Path} by {(there.Target.Length == 0 ? "nothing" : there.Target)}. One half " +
            "sends its reader to a different successor.");
    }

    [Fact]
    public void The_documentation_set_is_discovered()
    {
        Assert.True(
            Repository.Bilingual.Count >= 4,
            $"Only {Repository.Bilingual.Count} bilingual documents were found, so these theories " +
            "would assert almost nothing.");
    }

    /// <summary>The status line of a decision record: its kind, and the record it defers to.</summary>
    private static (string Kind, string Target) Status(MarkdownDocument document)
    {
        Match status = Regex.Match(
            document.Text,
            "^\\*\\*(?:Status|Statut)\\s*:?\\*\\*\\s*(?<kind>[A-Za-zÀ-ÿ]+)(?<rest>[^\\n]*)$",
            RegexOptions.Multiline,
            MatchTimeout);

        if (!status.Success) return (string.Empty, string.Empty);

        Match target = Regex.Match(status.Groups["rest"].Value, "ADR-(?<number>[0-9]{4})",
                                   RegexOptions.None, MatchTimeout);

        return (Normalised(status.Groups["kind"].Value),
                target.Success ? target.Groups["number"].Value : string.Empty);
    }

    /// <summary>
    /// The English word for a status, so the two halves are comparable. Only the words an ADR's
    /// status line may carry, which <c>doc/adr/template.md</c> fixes.
    /// </summary>
    private static string Normalised(string kind) => kind switch
    {
        "Accepté" or "Acceptée" => "Accepted",
        "Proposé" or "Proposée" => "Proposed",
        "Remplacé" or "Remplacée" or "Supersédé" or "Supersédée" => "Superseded",
        "Déprécié" or "Dépréciée" => "Deprecated",
        _ => kind,
    };

    /// <summary>
    /// The facts a document states, sorted, with the language suffix of a sibling link normalised
    /// away and the declared divergences removed.
    /// </summary>
    private static List<string> Occurrences(MarkdownDocument document, string pattern, HashSet<string> exempt)
    {
        List<string> found = [];
        foreach (Match occurrence in Regex.Matches(Scanned(document), pattern, RegexOptions.None, MatchTimeout))
        {
            string token = Normalise(occurrence.Value);

            if (!exempt.Contains(token)) found.Add(token);
        }

        found.Sort(StringComparer.Ordinal);

        return found;
    }

    /// <summary>
    /// The document with every link fragment removed.
    /// </summary>
    /// <remarks>
    /// An anchor is generated from the heading it names, so it is translated with that heading —
    /// <c>#--solution-and-why-it-needs-a-declaration</c> against
    /// <c>#--solution-et-pourquoi-il-exige-une-déclaration</c> is one target, twice. Left in, it also
    /// reads as a command-line option, which is the shape that reported it.
    /// </remarks>
    private static string Scanned(MarkdownDocument document) =>
        Regex.Replace(document.Text, "\\]\\((?<target>[^)\\s]*?)#[^)\\s]*\\)", "](${target})",
                      RegexOptions.None, MatchTimeout);

    /// <summary>
    /// One occurrence, reduced to the fact it states.
    /// </summary>
    /// <remarks>
    /// Three things are language-specific about an address and are not a divergence. A link to a
    /// sibling page carries the language suffix of the half it is written in. A link to the project
    /// README reaches the English half at the repository root and the French half under
    /// <c>doc/</c>, because GitHub composes the landing page from the root and nothing else
    /// (ADR-0029). And a FRAGMENT is generated from a heading, so it is translated with the heading
    /// it names — the page is the fact, the anchor is its prose.
    /// <para>
    /// A vendor's own localised documentation is the fourth: <c>conventionalcommits.org/en/</c> and
    /// <c>/fr/</c> are one address in two languages, and sending a French reader to the English one
    /// would be the defect rather than the fix.
    /// </para>
    /// </remarks>
    private static string Normalise(string token)
    {
        string normalised = token.TrimEnd(',', '.', ';', ':', '`', '\'', '"');

        if (!normalised.StartsWith("http", StringComparison.Ordinal))
        {
            return normalised.Replace(".en.md", ".md", StringComparison.Ordinal)
                             .Replace(".fr.md", ".md", StringComparison.Ordinal);
        }

        int fragment = normalised.IndexOf('#');
        if (fragment >= 0) normalised = normalised[..fragment];

        return normalised
            .Replace(".en.md", ".md", StringComparison.Ordinal)
            .Replace(".fr.md", ".md", StringComparison.Ordinal)
            .Replace("/blob/main/doc/README.md", string.Empty, StringComparison.Ordinal)
            .Replace("/blob/main/README.md", string.Empty, StringComparison.Ordinal)
            .Replace("/fr/", "/{lang}/", StringComparison.Ordinal)
            .Replace("/en/", "/{lang}/", StringComparison.Ordinal)
            .TrimEnd('/');
    }

    /// <summary>The facts one side states that the other does not.</summary>
    private static string OnlyIn(IReadOnlyList<string> stated, IReadOnlyList<string> against)
    {
        HashSet<string> other = new(against, StringComparer.Ordinal);

        return string.Join(
            ", ",
            stated.Where(token => !other.Contains(token))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(token => token, StringComparer.Ordinal));
    }

    /// <summary>The tokens a document declares as stated on one side only, with the reason required.</summary>
    private static HashSet<string> Diverging(MarkdownDocument document)
    {
        HashSet<string> declared = new(StringComparer.Ordinal);

        foreach (Match declaration in Regex.Matches(
                     document.Text,
                     "<!--\\s*dcat-doc:diverges\\s+(?<token>\\S+)\\s+(?<reason>[^>]*?)\\s*-->",
                     RegexOptions.None,
                     MatchTimeout))
        {
            Assert.True(
                declaration.Groups["reason"].Value.Length > 0,
                $"{document.Path} declares {declaration.Groups["token"].Value} as diverging between " +
                "the two languages and gives no reason. An exemption without one is a hole nobody " +
                "can judge.");

            declared.Add(declaration.Groups["token"].Value);
        }

        return declared;
    }
}
