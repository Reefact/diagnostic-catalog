using System.Collections.Immutable;
using System.Threading.Tasks;

using DiagnosticCatalog.CodeFixes;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// The two DCAT0001 fixes (§12.1) — align the pair on the category's rule, or on the identifier's.
/// </summary>
/// <remarks>
/// The first fix in the library that offers a <b>choice</b>, and §12.1's closing constraint is what
/// shapes it: the fix must never guess which rule was intended. Only the author knows whether the
/// category or the identifier was the typo.
/// </remarks>
public sealed class AlignIncoherentPairFixTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new SuppressionUsageAnalyzer();
    private static readonly CodeFixProvider Provider = new AlignIncoherentPairCodeFixProvider();

    private const string Usings = """
        using DiagnosticCatalog;
        using System.Diagnostics.CodeAnalysis;

        """;

    private const string Rules = """
        public static class SonarRules
        {
            [DiagnosticRule]
            public static class S1144
            {
                public const string Id = nameof(S1144);
                public const string Category = "Major Code Smell";
            }

            [DiagnosticRule]
            public static class S2094
            {
                public const string Id = nameof(S2094);
                public const string Category = "Code Smell";
            }
        }

        """;

    private const string Incoherent = """
        [SuppressMessage(SonarRules.S1144.Category, SonarRules.S2094.Id, Justification = "j")]
        public sealed class Target { }
        """;

    // --- the constraint: never guess ----------------------------------------------------------

    [Fact]
    public async Task Both_corrections_are_offered_and_neither_is_ranked()
    {
        // The whole of §12.1's closing sentence, asserted. Offering one would decide for the author
        // which half was the typo, and the analyzer cannot know: both readings compile and both are
        // things people write.
        ImmutableArray<string?> keys = await CodeFixHarness.EquivalenceKeysAsync(
            Analyzer,
            Provider,
            Usings + Rules + Incoherent);

        Assert.Equal(2, keys.Length);
        Assert.Contains("AlignOnCategory", keys);
        Assert.Contains("AlignOnId", keys);
    }

    // --- the two corrections ------------------------------------------------------------------

    [Fact]
    public async Task Aligning_on_the_category_corrects_the_identifier()
    {
        string fixedSource = await CodeFixHarness.ApplyAsync(
            Analyzer,
            Provider,
            Usings + Rules + Incoherent,
            "AlignOnCategory");

        Assert.Contains(
            "[SuppressMessage(SonarRules.S1144.Category, SonarRules.S1144.Id",
            fixedSource);
        Assert.Contains("Justification = \"j\"", fixedSource);
    }

    [Fact]
    public async Task Aligning_on_the_identifier_corrects_the_category()
    {
        string fixedSource = await CodeFixHarness.ApplyAsync(
            Analyzer,
            Provider,
            Usings + Rules + Incoherent,
            "AlignOnId");

        Assert.Contains(
            "[SuppressMessage(SonarRules.S2094.Category, SonarRules.S2094.Id",
            fixedSource);
        Assert.Contains("Justification = \"j\"", fixedSource);
    }

    [Fact]
    public async Task Each_correction_rewrites_one_side_only()
    {
        // Which is what makes the two distinguishable at all. A provider rewriting both arguments
        // from its chosen rule produces the same text for one of them and would pass the two tests
        // above while quietly discarding whatever spelling the untouched side had.
        string alignedOnCategory = await CodeFixHarness.ApplyAsync(
            Analyzer,
            Provider,
            Usings + "using Smell = SonarRules.S1144;\n\n" + Rules + """
                [SuppressMessage(Smell.Category, SonarRules.S2094.Id, Justification = "j")]
                public sealed class Target { }
                """,
            "AlignOnCategory");

        Assert.Contains("[SuppressMessage(Smell.Category, SonarRules.S1144.Id", alignedOnCategory);
    }

    // --- §12.1's other requirement: the using --------------------------------------------------

    [Fact]
    public async Task The_using_is_inserted_when_the_two_rules_live_in_different_namespaces()
    {
        // "When the two rules live in different containers or namespaces, the fix must also add the
        // required using." A fixture keeping both rules in one file never exercises this, which is
        // why the second rule is placed elsewhere — and why the harness compiles the result.
        string fixedSource = await CodeFixHarness.ApplyAsync(
            Analyzer,
            Provider,
            Usings + """
                using Vendor.Other;

                public static class SonarRules
                {
                    [DiagnosticRule]
                    public static class S1144
                    {
                        public const string Id = nameof(S1144);
                        public const string Category = "Major Code Smell";
                    }
                }

                namespace Vendor.Other
                {
                    public static class OtherRules
                    {
                        [DiagnosticCatalog.DiagnosticRule]
                        public static class S2094
                        {
                            public const string Id = nameof(S2094);
                            public const string Category = "Code Smell";
                        }
                    }
                }

                [SuppressMessage(SonarRules.S1144.Category, OtherRules.S2094.Id, Justification = "j")]
                public sealed class Target { }
                """,
            "AlignOnId");

        Assert.Contains("OtherRules.S2094.Category, OtherRules.S2094.Id", fixedSource);
    }

    // --- the fact that makes the two corrections unequal --------------------------------------

    [Fact]
    public async Task Roslyn_matches_a_suppression_on_the_identifier_alone()
    {
        // Not a test of this library, and deliberately kept anyway: it is the load-bearing fact behind
        // offering both corrections rather than preferring the harmless one. Roslyn never consults the
        // category, so aligning on the identifier leaves what is suppressed untouched while aligning on
        // the category changes it. Anyone tempted to rank the two fixes on that basis should find this
        // here — and if a future Roslyn starts matching on the category, this is what will say so.
        const string Rule = """
            using DiagnosticCatalog;
            using System.Diagnostics.CodeAnalysis;

            [DiagnosticCategory]
            internal static class Cat
            {
                public const string X = "x";
            }

            [SuppressMessage("{0}", "{1}")]
            [DiagnosticRule]
            public static class BadRule
            {
                public const string Category = Cat.X;
            }
            """;

        ImmutableArray<Diagnostic> withWrongCategory = await AnalyzerHarness.RunAsync(
            new DiagnosticRuleDefinitionAnalyzer(),
            Rule.Replace("{0}", "TotallyWrongCategory").Replace("{1}", "DCAT0003"));

        ImmutableArray<Diagnostic> withWrongId = await AnalyzerHarness.RunAsync(
            new DiagnosticRuleDefinitionAnalyzer(),
            Rule.Replace("{0}", "TotallyWrongCategory").Replace("{1}", "DCAT9999"));

        Assert.Empty(withWrongCategory);
        Assert.Single(withWrongId);
    }
}
