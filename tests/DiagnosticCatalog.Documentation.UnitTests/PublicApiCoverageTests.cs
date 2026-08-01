using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every public type this repository publishes for a consumer to name is described in
/// <c>doc/specification</c>.
/// </summary>
/// <remarks>
/// <para>
/// The same shape as <see cref="DiagnosticCoverageTests"/>, applied to the other half of the
/// published contract. A <c>DCAT</c> id reaches a consumer as a warning they can look up; a public
/// type reaches them as something they write in their own source, and a type nobody wrote down is
/// met by a reader who cannot know it exists. Both failures are silent, and the second one is worse:
/// <c>RS0016</c> already forces a new public member to be recorded in <c>PublicAPI.Unshipped.txt</c>,
/// so the build is satisfied by a file no consumer ever opens.
/// </para>
/// <para>
/// That file is exactly why the check is possible. The public API files are Roslyn's own tracking
/// format and the <c>RS0016</c>/<c>RS0017</c> analyzers fail the build when they drift from the
/// compiled surface — so they are a statement of the set that something else is keeping true, which
/// is the standard ADR-0009 sets and the reason no claim here is compared against another claim.
/// </para>
/// <para>
/// The obligation lands on <c>doc/specification</c>, in both languages, because that is the document
/// that already declares this contract: it prints the signature of every attribute, and its
/// versioning section names them as the surface a major release is measured against. One named
/// document, as everywhere else here — an obligation any page could discharge is one no page has.
/// </para>
/// </remarks>
public sealed class PublicApiCoverageTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    private const string Reference = "doc/specification.{0}.md";

    private static readonly Lazy<List<PublicType>> Published = new(ReadPublicApiFiles);

    /// <summary>
    /// One public type, as a public API file records it, and whether a consumer is in a position to
    /// name it.
    /// </summary>
    private sealed record PublicType(string FullName, string SimpleName, bool DiscoveredByRoslyn);

    public static TheoryData<string, string> ConsumerFacingByLanguage()
    {
        TheoryData<string, string> data = [];
        foreach (PublicType type in Published.Value.Where(candidate => !candidate.DiscoveredByRoslyn))
        {
            data.Add(type.SimpleName, "en");
            data.Add(type.SimpleName, "fr");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ConsumerFacingByLanguage))]
    public void Every_type_a_consumer_can_name_is_documented(string type, string language)
    {
        MarkdownDocument reference = Repository.Require(string.Format(Reference, language));

        Assert.True(
            Regex.IsMatch(
                reference.Text,
                "(?<![A-Za-z0-9_])" + Regex.Escape(type) + "(?![A-Za-z0-9_])",
                RegexOptions.None,
                MatchTimeout),
            $"{reference.Path} never mentions {type}, which the public API files publish. RS0016 " +
            "made the author record it in PublicAPI.Unshipped.txt, and that file is not something a " +
            "consumer reads: a type nobody wrote down is a type nobody can know exists.");
    }

    /// <summary>
    /// Guards the theory against asserting nothing, and — the part that matters more — against the
    /// partition quietly excusing everything. Were the Roslyn probe to match every type, the theory
    /// would run on an empty set and pass in silence, which is indistinguishable from a repository
    /// whose whole surface is documented.
    /// </summary>
    [Fact]
    public void The_public_api_is_discovered()
    {
        List<PublicType> published = Published.Value;

        Assert.True(
            published.Count >= 5,
            $"Only {published.Count} public types were read from src/*/PublicAPI.*.txt. Check that " +
            "those files are still where this test looks for them.");

        List<string> consumerFacing = published
            .Where(type => !type.DiscoveredByRoslyn)
            .Select(type => type.SimpleName)
            .ToList();

        Assert.True(
            consumerFacing.Count >= 3,
            $"Only {consumerFacing.Count} of {published.Count} public types were taken as " +
            "consumer-facing, so the coverage theory asserts almost nothing. Either the surface " +
            "shrank or the Roslyn-discovery probe is now matching types it should not.");

        Assert.Contains("DiagnosticRuleAttribute", consumerFacing);

        Assert.True(
            published.Any(type => type.DiscoveredByRoslyn),
            "No public type was recognised as discovered by Roslyn, although this repository ships " +
            "analyzers and code fixes. The probe reads the overrides their base classes require, so " +
            "a change in Roslyn's shape would silently push every provider into the documented set.");
    }

    /// <summary>
    /// Every type entry in every public API file, with the Roslyn verdict attached.
    /// </summary>
    /// <remarks>
    /// A type entry is a line carrying no <c>-&gt;</c>: in this format every member — method,
    /// property, field, constant alike — states its type after an arrow, and only a type declaration
    /// stands alone.
    /// </remarks>
    private static List<PublicType> ReadPublicApiFiles()
    {
        List<PublicType> types = [];

        foreach (string file in PublicApiFiles())
        {
            string text = File.ReadAllText(file);

            foreach (string line in text.Split('\n'))
            {
                string? fullName = TypeDeclaredBy(line);
                if (fullName is null) continue;

                types.Add(new PublicType(
                    fullName,
                    fullName[(fullName.LastIndexOf('.') + 1)..],
                    IsDiscoveredByRoslyn(text, fullName)));
            }
        }

        return types;
    }

    /// <summary>
    /// The type a public API line declares, or <c>null</c> when the line declares none — a directive,
    /// a member, or the record of something removed.
    /// </summary>
    private static string? TypeDeclaredBy(string line)
    {
        string entry = line.Trim();

        if (entry.Length == 0) return null;
        if (entry.StartsWith('#')) return null;
        if (entry.StartsWith("*REMOVED*", StringComparison.Ordinal)) return null;
        if (entry.Contains("->", StringComparison.Ordinal)) return null;

        // `~` marks an entry the analyzers treat as nullable-oblivious. It qualifies the record, not
        // the type it names.
        string fullName = entry.TrimStart('~');

        return Regex.IsMatch(fullName, "^[A-Za-z_][A-Za-z0-9_.]*$", RegexOptions.None, MatchTimeout)
            ? fullName
            : null;
    }

    /// <summary>
    /// Whether the host discovers this type rather than the consumer naming it — an analyzer, a code
    /// fix provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Such a type is public because Roslyn has to instantiate it, never because anybody writes it
    /// down. Requiring a page for <c>UseCatalogReferenceCodeFixProvider</c> would push the class name
    /// of a light bulb into the specification; what a reader actually meets is the diagnostic it
    /// repairs, and <see cref="DiagnosticCoverageTests"/> already binds every <c>DCAT</c> id to a page.
    /// </para>
    /// <para>
    /// Read off the overrides Roslyn's own base classes make abstract — <c>SupportedDiagnostics</c>
    /// for an analyzer, <c>FixableDiagnosticIds</c> for a code fix — rather than off the project the
    /// file sits in. A project is a place; these are what the type IS, so a genuine API type added
    /// under <c>src/DiagnosticCatalog.Analyzers</c> is not excused by its address.
    /// </para>
    /// </remarks>
    private static bool IsDiscoveredByRoslyn(string publicApiFile, string fullName) =>
        Regex.IsMatch(
            publicApiFile,
            "^override\\s+" + Regex.Escape(fullName) + "\\.(SupportedDiagnostics|FixableDiagnosticIds)\\.get\\b",
            RegexOptions.Multiline,
            MatchTimeout);

    private static IEnumerable<string> PublicApiFiles()
    {
        string source = Path.Combine(Repository.Root, "src");
        if (!Directory.Exists(source)) yield break;

        foreach (string file in Directory.EnumerateFiles(source, "PublicAPI.*.txt", SearchOption.AllDirectories))
        {
            // A build output holds a copy of whatever the project staged beside its binary, and
            // reading it would report the same surface twice — against a path nobody edits.
            string relative = file[Repository.Root.Length..].Replace(Path.DirectorySeparatorChar, '/');
            if (relative.Contains("/bin/", StringComparison.Ordinal)) continue;
            if (relative.Contains("/obj/", StringComparison.Ordinal)) continue;

            yield return file;
        }
    }

}
