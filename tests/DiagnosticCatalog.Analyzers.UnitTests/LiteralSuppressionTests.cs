using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// DCAT0006 — string literals that a known catalogue rule could replace.
/// </summary>
/// <remarks>
/// The core of the library (§3.5): what turns "my suppressions are catalogue references" from a
/// convention into something the compiler checks. It reports only when the literals actually match a
/// rule the compilation can see, so a codebase that has adopted no catalogue stays silent.
/// </remarks>
public sealed class LiteralSuppressionTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new SuppressionUsageAnalyzer();

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

    /// <summary>A catalogue in another assembly, taking the foundation as a dependency.</summary>
    private const string ReferencedCatalog = """
        using DiagnosticCatalog;

        namespace Vendor.Catalog;

        public static class VendorRules
        {
            [DiagnosticRule]
            public static class S1144
            {
                public const string Id = nameof(S1144);
                public const string Category = "Major Code Smell";
            }
        }
        """;

    /// <summary>
    /// The §7.2 catalogue: it embeds its own marker rather than depend on the foundation, so nothing
    /// links it to DiagnosticCatalog.dll at all.
    /// </summary>
    private const string SelfContainedCatalog = """
        namespace DiagnosticCatalog
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            internal sealed class DiagnosticRuleAttribute : System.Attribute { }
        }

        namespace Vendor.Embedded
        {
            public static class EmbeddedRules
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

    // --- §21.3, the matching rules ------------------------------------------------------------

    [Fact]
    public Task No_matching_rule_reports_nothing() =>
        // The whole reason adoption is not a flood: literals naming a vendor with no catalogue in the
        // compilation are left alone. Saying which identifiers exist at all is DCAT0008's opt-in job.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + Rules + """
            [SuppressMessage("Usage", "CA1822", Justification = "...")]
            public sealed class Target { }
            """);

    [Fact]
    public Task Exactly_one_matching_rule_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage("Major Code Smell", "S1144", Justification = "...")]
            public sealed class Target { }
            """, "DCAT0006");

    [Fact]
    public Task Several_matching_rules_are_still_reported() =>
        // Two catalogues describing the same vendor rule is legitimate. §11.6 gives this case the
        // diagnostic but no single automatic fix, so the report must survive the ambiguity.
        AnalyzerHarness.ReportsAgainstReferenceAsync(Analyzer, ReferencedCatalog, Usings + Rules + """
            [SuppressMessage("Major Code Smell", "S1144", Justification = "...")]
            public sealed class Target { }
            """, "DCAT0006");

    [Fact]
    public Task A_correct_category_with_an_unknown_identifier_reports_nothing() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + Rules + """
            [SuppressMessage("Major Code Smell", "S9999", Justification = "...")]
            public sealed class Target { }
            """);

    [Fact]
    public Task A_correct_identifier_with_the_wrong_category_reports_nothing() =>
        // The pair is the key. Reporting on the identifier alone would offer a replacement that changes
        // what the suppression means, since the category is what Roslyn matches on alongside it.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + Rules + """
            [SuppressMessage("Usage", "S1144", Justification = "...")]
            public sealed class Target { }
            """);

    [Fact]
    public Task An_identifier_carrying_a_friendly_name_is_reported() =>
        // THE case that decides whether this diagnostic is worth shipping. Visual Studio's built-in
        // Suppress → In Source fix writes exactly this form, so it is the bulk of what any real codebase
        // has. Roslyn truncates at the first colon and so must the lookup (§3.3, §11.6); an analyzer
        // skipping the step passes every hand-written fixture above and finds nothing in the wild.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage("Major Code Smell", "S1144:Unused private members should be removed")]
            public sealed class Target { }
            """, "DCAT0006");

    // --- §21.2, rules that live in another assembly -------------------------------------------

    [Fact]
    public Task Rules_from_a_referenced_assembly_are_found() =>
        // The normal shape of the product: the catalogue is a NuGet package, so its rules arrive as
        // metadata symbols with no syntax at all. Anything reading declaration syntax dies here.
        AnalyzerHarness.ReportsAgainstReferenceAsync(Analyzer, ReferencedCatalog, Usings + """
            [SuppressMessage("Major Code Smell", "S1144", Justification = "...")]
            public sealed class Target { }
            """, "DCAT0006");

    [Fact]
    public Task Rules_from_an_assembly_that_embeds_its_own_marker_are_found() =>
        // §13.1's pre-filter has two clauses, and this proves the second is not decoration. This
        // assembly is compiled WITHOUT the foundation, so it holds no reference to DiagnosticCatalog:
        // filtering on referenced assemblies alone skips it entirely, and its rules — an entire
        // catalogue — become invisible while every other test here still passes.
        AnalyzerHarness.ReportsAgainstSelfContainedReferenceAsync(Analyzer, SelfContainedCatalog, Usings + """
            [SuppressMessage("Major Code Smell", "S1144", Justification = "...")]
            public sealed class Target { }
            """, "DCAT0006");

    // --- the boundaries with the neighbouring diagnostics -------------------------------------

    [Fact]
    public Task A_pair_already_referencing_the_catalog_reports_nothing() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Category, SonarRules.S1144.Id, Justification = "...")]
            public sealed class Target { }
            """);

    [Fact]
    public Task A_half_migrated_pair_is_left_to_DCAT0007() =>
        // §21.3's last case. One reference, one literal: reporting it here would offer to rewrite the
        // half that is already correct. DCAT0007 alone in the expected set is that assertion.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Category, "S1144", Justification = "...")]
            public sealed class Target { }
            """, "DCAT0007");

    [Fact]
    public Task An_intermediate_constant_is_compared_by_value() =>
        // §10.6 — a constant declared outside any rule type is not a blind spot: its value is compared
        // exactly as a literal's would be, so the pair is reported. No fix is offered for it later.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            public static class Legacy
            {
                public const string Smell = "Major Code Smell";
                public const string Unused = "S1144";
            }

            [SuppressMessage(Legacy.Smell, Legacy.Unused, Justification = "...")]
            public sealed class Target { }
            """, "DCAT0006");

    [Fact]
    public Task A_literal_pair_matching_a_trim_rule_is_reported_on_the_unconditional_attribute() =>
        // The literal check is about literals, not about which suppression attribute carries them. The
        // id is a genuine IL one, so DCAT0009 stays quiet and only the migration is reported.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + """
            public static class TrimRules
            {
                [DiagnosticRule]
                public static class IL2026
                {
                    public const string Id = nameof(IL2026);
                    public const string Category = "Trimming";
                }
            }

            [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "...")]
            public sealed class Target { }
            """, "DCAT0006");
}
