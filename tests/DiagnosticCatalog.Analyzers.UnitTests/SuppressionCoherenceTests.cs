using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// DCAT0001 — a suppression whose category and id come from two different rules.
/// </summary>
public sealed class SuppressionCoherenceTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new SuppressionUsageAnalyzer();

    /// <summary>
    /// The using directives the fixture needs. Kept apart from the rule declarations so a test can slip
    /// an alias in between: a using directive has to precede every type declaration in the file.
    /// </summary>
    private const string Usings = """
        using DiagnosticCatalog;
        using System.Diagnostics.CodeAnalysis;

        """;

    /// <summary>Two rules in one container, deliberately sharing a category.</summary>
    private const string SameCategoryDeclarations = """
        public static class SomeRules
        {
            [DiagnosticRule]
            public static class RULE001
            {
                public const string Id = nameof(RULE001);
                public const string Category = "Usage";
            }

            [DiagnosticRule]
            public static class RULE002
            {
                public const string Id = nameof(RULE002);
                public const string Category = "Usage";
            }
        }

        """;

    private const string SameCategoryRules = Usings + SameCategoryDeclarations;

    [Fact]
    public Task A_suppression_referencing_one_rule_is_not_reported() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, SameCategoryRules + """
            [SuppressMessage(SomeRules.RULE001.Category, SomeRules.RULE001.Id, Justification = "...")]
            public sealed class Target { }
            """);

    [Fact]
    public Task Members_from_two_rules_are_reported_even_when_the_categories_are_equal() =>
        // The heart of DCAT0001. RULE001 and RULE002 share the category "Usage", so the compiled
        // suppression is byte-identical to a correct one and works perfectly today. It is still a
        // defect: the pairing says "I took RULE002's category from RULE001", and the day the vendor
        // recategorises one of them the suppression carries the wrong category — with nothing in the
        // platform to say so. Comparing values instead of declaring types would miss exactly this.
        AnalyzerHarness.ReportsAsync(Analyzer, SameCategoryRules + """
            [SuppressMessage(SomeRules.RULE001.Category, SomeRules.RULE002.Id, Justification = "...")]
            public sealed class Target { }
            """, "DCAT0001");

    [Fact]
    public Task Members_from_two_rules_with_different_categories_are_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, """
            using DiagnosticCatalog;
            using System.Diagnostics.CodeAnalysis;

            public static class SomeRules
            {
                [DiagnosticRule]
                public static class RULE001
                {
                    public const string Id = nameof(RULE001);
                    public const string Category = "Usage";
                }

                [DiagnosticRule]
                public static class RULE002
                {
                    public const string Id = nameof(RULE002);
                    public const string Category = "Design";
                }
            }

            [SuppressMessage(SomeRules.RULE001.Category, SomeRules.RULE002.Id, Justification = "...")]
            public sealed class Target { }
            """, "DCAT0001");

    // --- the right member in the right slot ---------------------------------------------------

    /// <summary>A rule carrying the optional members a generated catalogue emits beside the pair.</summary>
    private const string RuleWithOptionalMembers = """
        [DiagnosticRule]
        public static class RULE003
        {
            public const string Id = nameof(RULE003);
            public const string Category = "Usage";
            public const string HelpLinkUri = "https://example.invalid/RULE003";
        }

        """;

    [Fact]
    public Task Members_of_one_rule_written_into_each_other_s_slots_are_reported() =>
        // One rule, both members referenced, and the suppression still silences nothing: Roslyn matches
        // on the identifier alone, so the identifier slot holding "Usage" names no diagnostic. Comparing
        // declaring types alone cannot see this — the two members come from the same type.
        AnalyzerHarness.ReportsAsync(Analyzer, SameCategoryRules + """
            [SuppressMessage(SomeRules.RULE001.Id, SomeRules.RULE001.Category, Justification = "...")]
            public sealed class Target { }
            """, "DCAT0001");

    [Fact]
    public Task A_member_that_is_neither_Id_nor_Category_in_the_identifier_slot_is_reported() =>
        // The shape a catalogue makes reachable: StyleCop and .NET analyzer catalogues declare a
        // HelpLinkUri on every rule, so the wrong member is one completion away and resolves to a
        // perfectly good constant.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + RuleWithOptionalMembers + """
            [SuppressMessage(RULE003.Category, RULE003.HelpLinkUri, Justification = "...")]
            public sealed class Target { }
            """, "DCAT0001");

    [Fact]
    public Task The_pair_in_its_own_slots_is_still_not_reported() =>
        // The control for the two above: the same rule, the same members, in their own slots.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + RuleWithOptionalMembers + """
            [SuppressMessage(RULE003.Category, RULE003.Id, Justification = "...")]
            public sealed class Target { }
            """);

    // --- the accepted syntactic forms of §10.5 ------------------------------------------------

    [Fact]
    public Task A_type_alias_resolves_to_the_same_rule() =>
        // §10.5 recommends the alias when the container name is long. Analysis is on symbols, so it
        // costs nothing — but only if the implementation never reads the source text.
        AnalyzerHarness.ReportsAsync(
            Analyzer,
            Usings + """
                using One = SomeRules.RULE001;
                using Two = SomeRules.RULE002;

                """ + SameCategoryDeclarations + """
                [SuppressMessage(One.Category, Two.Id, Justification = "...")]
                public sealed class Target { }
                """,
            "DCAT0001");

    [Fact]
    public Task An_aliased_attribute_name_is_still_recognised() =>
        // §9.3: the attribute is resolved through the semantic model, never by the short name written
        // in source. An implementation matching on "SuppressMessage" as text goes quiet here.
        AnalyzerHarness.ReportsAsync(
            Analyzer,
            Usings + """
                using Suppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

                """ + SameCategoryDeclarations + """
                [Suppress(SomeRules.RULE001.Category, SomeRules.RULE002.Id, Justification = "...")]
                public sealed class Target { }
                """,
            "DCAT0001");

    [Fact]
    public Task Using_static_resolves_to_the_same_rule() =>
        // §10.5 calls this "recognised but not recommended": two such directives in one file make
        // Category and Id ambiguous, so it works for one rule per file. The analyzer must resolve it
        // anyway, which it does for free by working on symbols.
        AnalyzerHarness.ReportsNothingAsync(
            Analyzer,
            Usings + """
                using static SomeRules.RULE001;

                """ + SameCategoryDeclarations + """
                [SuppressMessage(Category, Id, Justification = "...")]
                public sealed class Target { }
                """);

    // --- what must not be reported ------------------------------------------------------------

    [Fact]
    public Task A_literal_suppression_is_not_this_diagnostic_s_business() =>
        // Both arguments are literals: DCAT0006's territory, not DCAT0001's. The expected set names
        // DCAT0006 alone, which is precisely the assertion that DCAT0001 kept quiet — reporting it here
        // would fire on every unmigrated codebase from the first build.
        AnalyzerHarness.ReportsAsync(Analyzer, SameCategoryRules + """
            [SuppressMessage("Usage", "RULE001", Justification = "...")]
            public sealed class Target { }
            """, "DCAT0006");

    [Fact]
    public Task A_half_migrated_suppression_is_not_this_diagnostic_s_business() =>
        // One reference, one literal: DCAT0007's, and naming it alone in the expected set is what
        // asserts DCAT0001 stayed out of a pair that references exactly one rule.
        AnalyzerHarness.ReportsAsync(Analyzer, SameCategoryRules + """
            [SuppressMessage(SomeRules.RULE001.Category, "RULE001", Justification = "...")]
            public sealed class Target { }
            """, "DCAT0007");

    [Fact]
    public Task An_attribute_that_is_not_a_suppression_is_ignored() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, SameCategoryRules + """
            [System.Obsolete("...")]
            public sealed class Target { }
            """);

    [Fact]
    public Task A_category_reached_through_a_shared_constant_is_still_the_rule_s_own() =>
        // §7.7: a rule may initialise its Category from a [DiagnosticCategory] class. The initialiser
        // is NOT part of the resolution — Category still resolves to the field declared on the rule.
        // An implementation following the initialiser would compare Categories against the rule type
        // and report every correctly generated catalogue, including all three this repository ships.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, """
            using DiagnosticCatalog;
            using System.Diagnostics.CodeAnalysis;

            [DiagnosticCategory]
            public static class Categories
            {
                public const string Usage = "Usage";
            }

            [DiagnosticRule]
            public static class RULE001
            {
                public const string Id = nameof(RULE001);
                public const string Category = Categories.Usage;
            }

            [SuppressMessage(RULE001.Category, RULE001.Id, Justification = "...")]
            public sealed class Target { }
            """);
}
