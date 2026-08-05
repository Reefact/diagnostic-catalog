using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using DiagnosticCatalog.CodeFixes;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// DCAT0007 and its fix — a suppression half migrated: one reference, one string literal.
/// </summary>
/// <remarks>
/// §11.7 calls it the most common partially migrated state and the <b>only</b> case where the fix is
/// fully deterministic: the migrated argument names the rule, so there is nothing to choose. It needs no
/// rule index for the same reason, which is what keeps the index's laziness worth having.
/// </remarks>
public sealed class MixedSuppressionTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new SuppressionUsageAnalyzer();
    private static readonly CodeFixProvider Provider = new CompleteCatalogReferenceCodeFixProvider();

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
        }

        """;

    private const string NamespacedRules = """
        namespace Vendor.Catalog
        {
            public static class SonarRules
            {
                [DiagnosticCatalog.DiagnosticRule]
                public static class S1144
                {
                    public const string Id = nameof(S1144);
                    public const string Category = "Major Code Smell";
                }
            }
        }

        """;

    // --- the diagnostic -----------------------------------------------------------------------

    [Fact]
    public Task A_referenced_category_with_a_literal_identifier_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Category, "S1144", Justification = "j")]
            public sealed class Target { }
            """, "DCAT0007");

    [Fact]
    public Task A_literal_category_with_a_referenced_identifier_is_reported() =>
        // The other half of the same state. Migrating the identifier first is just as common, and an
        // implementation handling only one direction stays silent on the other.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage("Major Code Smell", SonarRules.S1144.Id, Justification = "j")]
            public sealed class Target { }
            """, "DCAT0007");

    [Fact]
    public Task A_literal_that_names_something_else_is_still_reported() =>
        // Reported — it is still a mixed suppression — but the fix below refuses it.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Category, "S9999", Justification = "j")]
            public sealed class Target { }
            """, "DCAT0007");

    [Fact]
    public Task A_fully_migrated_pair_is_not_reported() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Category, SonarRules.S1144.Id, Justification = "j")]
            public sealed class Target { }
            """);

    [Fact]
    public Task A_pair_of_literals_belongs_to_DCAT0006_alone() =>
        // The three diagnostics partition the pair by what its halves are, so exactly one can fire.
        // The expected set naming DCAT0006 alone is what asserts DCAT0007 kept out of it.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage("Major Code Smell", "S1144", Justification = "j")]
            public sealed class Target { }
            """, "DCAT0006");

    [Fact]
    public Task A_pair_from_two_rules_belongs_to_DCAT0001_alone() =>
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + """
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

            [SuppressMessage(SonarRules.S1144.Category, SonarRules.S2094.Id, Justification = "j")]
            public sealed class Target { }
            """, "DCAT0001");

    // --- the fix ------------------------------------------------------------------------------

    [Fact]
    public async Task The_literal_identifier_is_completed_from_the_referenced_rule()
    {
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Category, "S1144", Justification = "j")]
            public sealed class Target { }
            """);

        Assert.Contains("[SuppressMessage(SonarRules.S1144.Category, SonarRules.S1144.Id", fixedSource);
        Assert.Contains("Justification = \"j\"", fixedSource);
    }

    [Fact]
    public async Task The_literal_category_is_completed_from_the_referenced_rule()
    {
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + Rules + """
            [SuppressMessage("Major Code Smell", SonarRules.S1144.Id, Justification = "j")]
            public sealed class Target { }
            """);

        Assert.Contains(
            "[SuppressMessage(SonarRules.S1144.Category, SonarRules.S1144.Id",
            fixedSource);
    }

    [Fact]
    public async Task The_already_migrated_side_keeps_the_spelling_the_author_chose()
    {
        // A fix rewriting both sides "from the same rule" would look harmless and would still discard
        // the alias the author chose. Only the literal is the fix's business, so the result is
        // deliberately mixed in spelling: Rule.Category survives, and only the identifier is written.
        string fixedSource = await CodeFixHarness.ApplyAsync(
            Analyzer,
            Provider,
            Usings + "using Rule = SonarRules.S1144;\n\n" + Rules + """
                [SuppressMessage(Rule.Category, "S1144", Justification = "j")]
                public sealed class Target { }
                """);

        Assert.Contains("[SuppressMessage(Rule.Category, SonarRules.S1144.Id", fixedSource);
    }

    [Fact]
    public async Task A_friendly_name_suffix_is_recognised_and_dropped()
    {
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Category, "S1144:Unused private members should be removed")]
            public sealed class Target { }
            """);

        Assert.DoesNotContain("Unused private members should be removed", fixedSource);
        Assert.Contains("SonarRules.S1144.Id", fixedSource);
    }

    [Fact]
    public Task A_literal_that_names_something_else_gets_no_fix() =>
        // The line that keeps this a migration rather than an edit. "S9999" is what is suppressed
        // today; completing it from S1144 would silence a different diagnostic and let the original
        // warning back in — a decision for the author, not for a lightbulb.
        CodeFixHarness.OffersNothingAsync(Analyzer, Provider, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Category, "S9999", Justification = "j")]
            public sealed class Target { }
            """);

    [Fact]
    public async Task A_literal_matching_a_suffixed_declared_identifier_is_completed()
    {
        // The other end of the same asymmetry as DCAT0006's: here `declared` is the raw declared Id
        // and `written` is normalised, so a literal that is byte-identical to what the rule declares
        // compares unequal. The fix was withheld and the message said the value "names something
        // else" — about an exact match.
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + SuffixedRules + """
            [SuppressMessage(TrimRules.IL2026.Category, "IL2026:Members annotated with RequiresUnreferencedCode", Justification = "j")]
            public sealed class Target { }
            """);

        Assert.Contains("TrimRules.IL2026.Id", fixedSource, StringComparison.Ordinal);
    }

    /// <summary>A rule whose declared Id carries a friendly-name suffix (§8.2 blesses the form).</summary>
    private const string SuffixedRules = """
        public static class TrimRules
        {
            [DiagnosticRule]
            public static class IL2026
            {
                public const string Id = "IL2026:Members annotated with RequiresUnreferencedCode";
                public const string Category = "Trimming";
            }
        }

        """;

    [Fact]
    public Task A_reference_in_the_wrong_slot_is_still_reported() =>
        // The category slot holds S1144's Id. Half-migrated, so still DCAT0007's — but the migrated
        // half is itself wrong, which is the part the completion must notice.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Id, "S1144", Justification = "j")]
            public sealed class Target { }
            """, "DCAT0007");

    [Fact]
    public Task A_reference_in_the_wrong_slot_gets_no_fix() =>
        // Completing from the other slot here writes SonarRules.S1144.Id into BOTH arguments: the
        // literal agrees with the declared Id, so the completion reads as deterministic while the
        // reference it completes from is in the category's place. The result compiles, resolves,
        // suppresses nothing, and — both halves now being rule members of one rule in the wrong
        // slots — is exactly the shape DCAT0001 was widened to report. The fix must not manufacture
        // the defect the analyzer next door exists to catch.
        CodeFixHarness.OffersNothingAsync(Analyzer, Provider, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Id, "S1144", Justification = "j")]
            public sealed class Target { }
            """);

    [Fact]
    public async Task The_using_is_inserted_when_the_rule_lives_in_another_namespace()
    {
        string fixedSource = await CodeFixHarness.ApplyAsync(
            Analyzer,
            Provider,
            "using Vendor.Catalog;\n" + Usings + NamespacedRules + """
                [SuppressMessage(SonarRules.S1144.Category, "S1144", Justification = "j")]
                public sealed class Target { }
                """);

        Assert.Contains("SonarRules.S1144.Id", fixedSource);
    }

    [Fact]
    public async Task Every_occurrence_offers_the_same_equivalence_key()
    {
        ImmutableArray<string?> keys = await CodeFixHarness.EquivalenceKeysAsync(Analyzer, Provider, Usings + """
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

            [SuppressMessage(SonarRules.S1144.Category, "S1144", Justification = "j")]
            public sealed class First { }

            [SuppressMessage("Code Smell", SonarRules.S2094.Id, Justification = "j")]
            public sealed class Second { }
            """);

        Assert.Equal(2, keys.Length);
        Assert.Single(keys.Distinct());
    }
    [Fact]
    public Task An_identifier_hoisted_into_a_constant_still_names_its_rule() =>
        // The form the guide promotes under its ACCEPTED list: a rule member hoisted into a named
        // constant so a second suppression can reuse it. Nothing here is a literal — the constant's
        // initialiser names the rule — so DCAT0007, which exists for one reference and one literal,
        // has no business firing.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + Rules + """
            public sealed class Target
            {
                private const string RuleId = SonarRules.S1144.Id;

                [SuppressMessage(SonarRules.S1144.Category, RuleId, Justification = "j")]
                public void M() { }
            }
            """);

    [Fact]
    public Task A_category_hoisted_into_a_constant_still_names_its_rule() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + Rules + """
            public sealed class Target
            {
                private const string RuleCategory = SonarRules.S1144.Category;

                [SuppressMessage(RuleCategory, SonarRules.S1144.Id, Justification = "j")]
                public void M() { }
            }
            """);

    [Fact]
    public async Task The_message_names_a_value_rather_than_a_literal()
    {
        // A constant NOT initialised from a rule member is still reported — the value is all the
        // analyzer has — but there is no literal anywhere in the source it points at. Saying "the
        // literal" would send its author hunting for something that is not written down.
        ImmutableArray<Diagnostic> reported = await AnalyzerHarness.RunAsync(Analyzer, Usings + Rules + """
            public sealed class Target
            {
                private const string RuleId = "S1144";

                [SuppressMessage(SonarRules.S1144.Category, RuleId, Justification = "j")]
                public void M() { }
            }
            """);

        Diagnostic mixed = Assert.Single(reported, d => d.Id == "DCAT0007");

        Assert.Contains("the string value \"S1144\"", mixed.GetMessage());
        Assert.DoesNotContain("literal", mixed.GetMessage());
    }

}
