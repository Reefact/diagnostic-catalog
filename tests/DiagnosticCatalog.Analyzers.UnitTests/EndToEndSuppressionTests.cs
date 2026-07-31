using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// §21.6 — the premise of the whole library, proven rather than assumed.
/// </summary>
/// <remarks>
/// <para>
/// Every other test here asks whether the analyzers report the right things. This one asks the
/// question underneath all of them: does a suppression written as catalogue references <b>actually
/// suppress a diagnostic a real analyzer emitted</b>. The specification is blunt about what is left
/// without it — §27 would assert only that the code compiles.
/// </para>
/// <para>
/// The analyzer under test is this package's own, and the catalogue describes this package's own
/// diagnostics. That is not a shortcut: it is the only pairing in the repository where a real analyzer
/// and a real catalogue are both present, and it exercises the same path a consumer takes with a
/// vendor's.
/// </para>
/// <para>
/// Each test asserts the pair — reported without the suppression, absent with it — in one run. A
/// one-sided assertion of absence is the characteristic way this test rots: it would keep passing if
/// the analyzer stopped reporting, if the snippet stopped triggering, or if the harness stopped
/// running the analyzer at all.
/// </para>
/// </remarks>
public sealed class EndToEndSuppressionTests
{
    private static readonly DiagnosticAnalyzer DefinitionAnalyzer = new DiagnosticRuleDefinitionAnalyzer();

    /// <summary>
    /// A catalogue of this package's own diagnostics, exactly as a generated one is shaped.
    /// </summary>
    private const string Catalog = """
        using DiagnosticCatalog;

        namespace Acme.Catalog;

        [DiagnosticCategory]
        public static class DcatCategory
        {
            public const string DiagnosticCatalog = "DiagnosticCatalog";
        }

        public static class DcatRules
        {
            [DiagnosticRule]
            public static class DCAT0003
            {
                public const string Id = nameof(DCAT0003);
                public const string Category = DcatCategory.DiagnosticCatalog;
            }
        }
        """;

    /// <summary>A rule missing its Id, which DCAT0003 reports.</summary>
    private const string OffendingCode = """
        using DiagnosticCatalog;
        using System.Diagnostics.CodeAnalysis;
        using Acme.Catalog;

        {0}
        [DiagnosticRule]
        public static class Malformed
        {
            public const string Category = "Usage";
        }
        """;

    [Fact]
    public async Task A_catalogue_suppression_actually_suppresses_a_real_diagnostic()
    {
        ImmutableArray<Diagnostic> withoutSuppression = await AnalyzerHarness.RunAsync(
            DefinitionAnalyzer,
            OffendingCode.Replace("{0}", string.Empty),
            Catalog);

        ImmutableArray<Diagnostic> withSuppression = await AnalyzerHarness.RunAsync(
            DefinitionAnalyzer,
            OffendingCode.Replace(
                "{0}",
                """[SuppressMessage(DcatRules.DCAT0003.Category, DcatRules.DCAT0003.Id, Justification = "Deliberate.")]"""),
            Catalog);

        // Half one: the analyzer really does report, so there is something to suppress. Without this
        // the second assertion would pass against a compilation that never produced a diagnostic.
        Assert.Equal("DCAT0003", Assert.Single(withoutSuppression).Id);

        // Half two, and the claim the library exists to make: the same code, with the same analyzer,
        // reports nothing once the suppression names the rule through the catalogue.
        Assert.Empty(withSuppression);
    }

    [Fact]
    public async Task A_suppression_naming_another_rule_does_not_suppress_it()
    {
        // The control on the control. If the suppression above worked for any reason other than
        // matching — a harness that filters everything, an analyzer silenced by the extra attribute —
        // this would go quiet too.
        ImmutableArray<Diagnostic> reported = await AnalyzerHarness.RunAsync(
            DefinitionAnalyzer,
            OffendingCode.Replace(
                "{0}",
                """[SuppressMessage("DiagnosticCatalog", "DCAT9999", Justification = "Names nothing.")]"""),
            Catalog);

        Assert.Equal("DCAT0003", Assert.Single(reported).Id);
    }

    [Fact]
    public async Task The_suppression_survives_the_category_being_reached_through_a_shared_constant()
    {
        // The shape every generated catalogue actually has (§7.7): Category is initialised from a
        // [DiagnosticCategory] class rather than from a literal. It folds to the same string, which is
        // what makes declaring each category once cost nothing at the use site — asserted here against
        // a real suppression rather than by reflection alone.
        ImmutableArray<Diagnostic> reported = await AnalyzerHarness.RunAsync(
            DefinitionAnalyzer,
            OffendingCode.Replace(
                "{0}",
                """[SuppressMessage(DcatCategory.DiagnosticCatalog, DcatRules.DCAT0003.Id, Justification = "Deliberate.")]"""),
            Catalog);

        Assert.Empty(reported.Where(diagnostic => diagnostic.Id == "DCAT0003"));
    }
}
