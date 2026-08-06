using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every suppression a documentation page shows in a C# fence carries a non-blank
/// <c>Justification</c>, unless the fence is declared as showing the omission on purpose.
/// </summary>
/// <remarks>
/// <para>
/// A reader copies what a page shows. Since ADR-0040 a suppression with no reason is an
/// <c>Error</c>, so a sample written without one hands its reader a build failure off the page that
/// was teaching them — and it does so for a reason the page never mentioned, because almost every
/// sample here is about something else entirely: a wrong identifier, a mismatched pair, the form the
/// fix rewrites. A second diagnostic on that line is noise the author did not mean to teach.
/// </para>
/// <para>
/// It was not hypothetical. Eighty-two samples across the guides, the specification and the package
/// pages showed a suppression with no reason at the moment the rule requiring one became an error,
/// including every "accepted syntactic form" the rule contract lists — the page whose whole subject
/// is what a correct suppression looks like.
/// </para>
/// <para>
/// <b>The exemption is local and needs a reason.</b> Some fences have to show the omission —
/// <c>DCAT0014</c>'s own trigger is a suppression that says nothing about why it exists. Such a fence
/// declares it on the line before, as
/// <c>&lt;!-- dcat-doc:missing-justification why --&gt;</c>, and the declaration covers the NEXT
/// fenced block only. Per block rather than per document, because the documents that must show one
/// incorrect sample are exactly the documents that show the most correct ones: a page-wide exemption
/// would switch the check off precisely where it is worth having.
/// </para>
/// <para>
/// Scope. Markdown fences only, and not the XML documentation that ships inside the packages: a
/// <c>&lt;c&gt;</c> element quoting an attribute mid-sentence is prose about C# syntax rather than a
/// sample somebody copies, and the one this repository carries illustrates named-argument order —
/// adding a third argument to it would bury what it was written to show. Decision records are out of
/// scope too: an accepted ADR is a historical record and is never edited in place
/// (<c>doc/adr/README.en.md</c>), so a check that failed on one would be asking for the one edit the
/// process forbids.
/// </para>
/// </remarks>
public sealed class SuppressionSampleTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A suppression attribute, at the opening parenthesis. <c>assembly:</c> and <c>module:</c>
    /// targets are matched as well: a file-level suppression silences a warning exactly as an
    /// in-place one does, and says exactly as little about why.
    /// </summary>
    private const string Attribute =
        @"\[\s*(?:assembly\s*:\s*|module\s*:\s*)?(?:Unconditional)?SuppressMessage\s*\(";

    /// <summary>The declaration that a fence shows an unjustified suppression on purpose.</summary>
    private const string Exemption =
        @"<!--\s*dcat-doc:missing-justification\s+(?<reason>[^>]*?)\s*-->";

    public static TheoryData<string> DocumentsShowingCSharp()
    {
        TheoryData<string> paths = [];
        foreach (MarkdownDocument document in Repository.Documents)
        {
            if (IsDecisionRecord(document)) { continue; }

            if (Fences(document).Count > 0) { paths.Add(document.Path); }
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(DocumentsShowingCSharp))]
    public void Every_suppression_a_sample_shows_carries_a_justification(string path)
    {
        MarkdownDocument document = Repository.Require(path);

        foreach (Fence fence in Fences(document))
        {
            if (fence.Exempt) { continue; }

            foreach (string suppression in Unjustified(fence.Body))
            {
                Assert.Fail(
                    $"{path}:{fence.Line} shows a suppression with no non-blank Justification:\n"
                    + $"  {suppression}\n"
                    + "A reader copies it and meets DCAT0014, which is an error — off the page that "
                    + "was teaching them something else entirely.\n"
                    + "Either write the reason, or declare the omission on the line before the fence:\n"
                    + "  <!-- dcat-doc:missing-justification why this block shows it -->");
            }
        }
    }

    /// <summary>
    /// An exemption states a reason, and covers a fence that really does show the omission.
    /// </summary>
    /// <remarks>
    /// Both halves matter and neither is the same check as the theory above. An exemption without a
    /// reason is a hole nobody can judge; one whose fence has since been corrected is a hole nobody
    /// closed, and it covers whatever gets written there next.
    /// </remarks>
    [Theory]
    [MemberData(nameof(DocumentsShowingCSharp))]
    public void Every_exemption_states_a_reason_and_covers_a_fence_that_needs_it(string path)
    {
        MarkdownDocument document = Repository.Require(path);

        foreach (Match declaration in Regex.Matches(document.Text, Exemption, RegexOptions.None, MatchTimeout))
        {
            Assert.True(
                declaration.Groups["reason"].Value.Length > 0,
                $"{path} declares a suppression sample as deliberately unjustified and gives no "
                + "reason. An exemption without one is a hole nobody can judge.");
        }

        foreach (Fence fence in Fences(document))
        {
            if (!fence.Exempt) { continue; }

            Assert.True(
                Unjustified(fence.Body).Count > 0,
                $"{path}:{fence.Line} is declared as showing a suppression with no justification, and "
                + "every suppression in it carries one. Delete the declaration: an exemption nothing "
                + "uses covers whatever gets written there next.");
        }
    }

    /// <summary>
    /// Guards both theories against passing on an empty world: a fence reader that stopped matching
    /// would produce no cases at all, and a green run that checked nothing.
    /// </summary>
    [Fact]
    public void The_samples_are_still_found()
    {
        int fences = 0;
        int exempt = 0;

        foreach (MarkdownDocument document in Repository.Documents)
        {
            if (IsDecisionRecord(document)) { continue; }

            foreach (Fence fence in Fences(document))
            {
                fences++;
                if (fence.Exempt) { exempt++; }
            }
        }

        Assert.True(
            fences > 20,
            $"Only {fences} C# fences showing a suppression were found across the documentation, "
            + "which is far below what it carries. The fence reader stopped matching, and every "
            + "assertion above is passing by looking at nothing.");

        Assert.True(
            exempt > 0,
            "No fence is declared as showing an unjustified suppression, and at least one has to be: "
            + "the diagnostics guide and the specification both document DCAT0014 by showing its "
            + "trigger. The exemption reader stopped matching.");
    }

    /// <summary>An ADR, which is a historical record rather than a page anybody maintains.</summary>
    private static bool IsDecisionRecord(MarkdownDocument document) =>
        document.Path.StartsWith("doc/adr/", StringComparison.Ordinal);

    /// <summary>Every C# fence that shows at least one suppression, with its exemption state.</summary>
    private static List<Fence> Fences(MarkdownDocument document)
    {
        List<Fence> fences = [];

        string[] lines = [.. document.Text.Split('\n')];
        int index = 0;

        while (index < lines.Length)
        {
            Match opening = Regex.Match(
                lines[index], @"^\s*(?<ticks>`{3,})(?<language>[A-Za-z0-9+#-]*)\s*$",
                RegexOptions.None, MatchTimeout);

            if (!opening.Success) { index++; continue; }

            string ticks = opening.Groups["ticks"].Value;
            string language = opening.Groups["language"].Value;
            int opened = index;
            List<string> body = [];

            index++;
            while (index < lines.Length)
            {
                Match closing = Regex.Match(
                    lines[index], @"^\s*(?<ticks>`{3,})\s*$", RegexOptions.None, MatchTimeout);

                if (closing.Success && closing.Groups["ticks"].Value.Length >= ticks.Length) { break; }

                body.Add(lines[index]);
                index++;
            }

            index++;

            if (!IsCSharp(language)) { continue; }

            string source = string.Join("\n", body);
            if (!Regex.IsMatch(source, Attribute, RegexOptions.None, MatchTimeout)) { continue; }

            fences.Add(new Fence(opened + 1, source, DeclaredBefore(lines, opened)));
        }

        return fences;
    }

    private static bool IsCSharp(string language) =>
        string.Equals(language, "csharp", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(language, "cs", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether an exemption sits in the lines immediately before the fence, blank lines aside.
    /// </summary>
    /// <remarks>
    /// A short window rather than the whole document. The declaration has to be legible as belonging
    /// to the block it covers — a reader scrolling past should meet the reason and the sample
    /// together — and a window wide enough to reach the previous section would quietly exempt
    /// samples nobody meant to.
    /// </remarks>
    private static bool DeclaredBefore(string[] lines, int fence)
    {
        for (int line = fence - 1; line >= 0 && line >= fence - 4; line--)
        {
            if (lines[line].Trim().Length == 0) { continue; }

            return Regex.IsMatch(lines[line], Exemption, RegexOptions.None, MatchTimeout);
        }

        return false;
    }

    /// <summary>The suppressions in a sample that carry no non-blank <c>Justification</c>.</summary>
    private static List<string> Unjustified(string source)
    {
        List<string> found = [];

        foreach (Match attribute in Regex.Matches(source, Attribute, RegexOptions.None, MatchTimeout))
        {
            int opening = attribute.Index + attribute.Length - 1;
            string arguments = Arguments(source, opening);

            Match justification = Regex.Match(
                arguments, @"Justification\s*=\s*(?<value>[^,)]*)", RegexOptions.None, MatchTimeout);

            if (justification.Success && IsNonBlank(justification.Groups["value"].Value)) { continue; }

            int end = Math.Min(opening + arguments.Length + 1, source.Length);
            found.Add(Collapsed(source[attribute.Index..end]));
        }

        return found;
    }

    /// <summary>
    /// A value that is neither absent, nor <c>null</c>, nor an empty or whitespace-only literal.
    /// This is the analyzer's own boundary, restated over source text rather than over a symbol —
    /// what the sample SHOWS is all a reader copies.
    /// </summary>
    private static bool IsNonBlank(string value)
    {
        string trimmed = value.Trim();

        if (trimmed.Length == 0) { return false; }
        if (string.Equals(trimmed, "null", StringComparison.Ordinal)) { return false; }

        return trimmed.Trim('"', '@', '$').Trim().Length > 0;
    }

    /// <summary>The argument text of a call, from its opening parenthesis to the matching close.</summary>
    private static string Arguments(string source, int openingParenthesis)
    {
        int depth = 0;

        for (int position = openingParenthesis; position < source.Length; position++)
        {
            if (source[position] == '(') { depth++; }
            else if (source[position] == ')')
            {
                depth--;
                if (depth == 0) { return source[openingParenthesis..position]; }
            }
        }

        // An attribute the fence shows only the beginning of. Reported as written rather than
        // guessed at: an elided sample is one a reader still copies.
        return source[openingParenthesis..];
    }

    private static string Collapsed(string text) =>
        Regex.Replace(text, @"\s+", " ", RegexOptions.None, MatchTimeout).Trim();

    /// <summary>One fenced C# block showing at least one suppression.</summary>
    /// <param name="Line">The 1-based line the fence opens on, so a failure names where to look.</param>
    /// <param name="Body">The sample itself, without the fence markers.</param>
    /// <param name="Exempt">Whether the block is declared as showing the omission on purpose.</param>
    private sealed record Fence(int Line, string Body, bool Exempt);
}
