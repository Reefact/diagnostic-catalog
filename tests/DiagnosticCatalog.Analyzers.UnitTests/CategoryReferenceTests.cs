using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// §8.5, as DCAT0011 enforces it: a rule reaches its category through a constant declared in a
/// <c>[DiagnosticCategory]</c> class, never through a value transcribed in place.
/// </summary>
/// <remarks>
/// The distinction under test is invisible in the compiled assembly. Every snippet here folds to the
/// same two strings in metadata, so nothing downstream — not Roslyn, not the emitted attribute, not a
/// reflecting consumer — can tell the accepted form from the rejected one. That is what makes the check
/// worth having a test suite rather than a spot check: the only thing that can observe the difference
/// is this analyzer.
/// </remarks>
public sealed class CategoryReferenceTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new DiagnosticRuleDefinitionAnalyzer();

    // The markers taken from the real foundation assembly, which the test project references.
    private const string UsingFoundation = "using DiagnosticCatalog;\n";

    // --- What the contract requires -----------------------------------------------------------

    [Fact]
    public Task A_category_reached_through_a_declared_constant_is_not_reported() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, UsingFoundation + """
            [DiagnosticCategory]
            internal static class ContosoCategory
            {
                public const string Usage = "Usage";
            }

            [DiagnosticRule]
            public static class CT0001
            {
                public const string Id = nameof(CT0001);
                public const string Category = ContosoCategory.Usage;
            }
            """);

    [Fact]
    public Task A_category_written_as_a_literal_is_reported() =>
        // The form the README blessed until this rule existed, and the one every catalogue starts from.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + """
            [DiagnosticRule]
            public static class CT0001
            {
                public const string Id = nameof(CT0001);
                public const string Category = "Usage";
            }
            """, "DCAT0011");

    [Fact]
    public Task A_category_borrowed_from_an_unmarked_class_is_reported() =>
        // The near miss that matters: the indirection is there, the marker is not, and without the
        // marker no tool can tell this constant from any other string in the assembly (§7.7).
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + """
            internal static class ContosoCategory
            {
                public const string Usage = "Usage";
            }

            [DiagnosticRule]
            public static class CT0001
            {
                public const string Id = nameof(CT0001);
                public const string Category = ContosoCategory.Usage;
            }
            """, "DCAT0011");

    // --- Spellings that bind to the same field --------------------------------------------------

    [Fact]
    public Task A_category_reached_through_an_alias_is_not_reported() =>
        // Resolution is semantic, so the container's spelling at the use site is irrelevant.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, UsingFoundation + """
            using Categories = Contoso.ContosoCategory;

            namespace Contoso
            {
                [DiagnosticCategory]
                internal static class ContosoCategory
                {
                    public const string Usage = "Usage";
                }
            }

            [DiagnosticRule]
            public static class CT0001
            {
                public const string Id = nameof(CT0001);
                public const string Category = Categories.Usage;
            }
            """);

    [Fact]
    public Task A_category_reached_through_using_static_is_not_reported() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, UsingFoundation + """
            using static Contoso.ContosoCategory;

            namespace Contoso
            {
                [DiagnosticCategory]
                internal static class ContosoCategory
                {
                    public const string Usage = "Usage";
                }
            }

            [DiagnosticRule]
            public static class CT0001
            {
                public const string Id = nameof(CT0001);
                public const string Category = Usage;
            }
            """);

    [Fact]
    public Task A_category_declared_in_a_referenced_assembly_is_not_reported() =>
        // §7.7 puts no assembly boundary on the container. A generated one is internal (ADR-0026), but
        // a hand-written catalogue may publish one and a consumer may build on it.
        AnalyzerHarness.ReportsAgainstReferenceAsync(
            Analyzer,
            referencedSource: UsingFoundation + """
            namespace Shared
            {
                [DiagnosticCategory]
                public static class SharedCategory
                {
                    public const string Usage = "Usage";
                }
            }
            """,
            source: UsingFoundation + """
            using Shared;

            [DiagnosticRule]
            public static class CT0001
            {
                public const string Id = nameof(CT0001);
                public const string Category = SharedCategory.Usage;
            }
            """);

    [Fact]
    public Task A_marker_the_catalogue_declares_itself_is_honoured() =>
        // §7.2, for the category marker: matching is by metadata name, so a catalogue that declares its
        // own copy rather than taking the package dependency is recognised. A symbol comparison would
        // reject this and turn a valid catalogue into a wall of errors.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, """
            using System;

            namespace DiagnosticCatalog
            {
                [AttributeUsage(AttributeTargets.Class)]
                internal sealed class DiagnosticRuleAttribute : Attribute { }

                [AttributeUsage(AttributeTargets.Class)]
                internal sealed class DiagnosticCategoryAttribute : Attribute { }
            }

            namespace Contoso
            {
                using DiagnosticCatalog;

                [DiagnosticCategory]
                internal static class ContosoCategory
                {
                    public const string Usage = "Usage";
                }

                [DiagnosticRule]
                public static class CT0001
                {
                    public const string Id = nameof(CT0001);
                    public const string Category = ContosoCategory.Usage;
                }
            }
            """);

    // --- Forms that are constant but not a single reference -------------------------------------

    [Fact]
    public Task A_category_assembled_from_two_constants_is_reported() =>
        // Still a compile-time constant, so the platform is satisfied and §8.3 holds. It is rejected
        // because a value spelled out of parts has no single declaration to be the source of truth.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + """
            [DiagnosticCategory]
            internal static class ContosoCategory
            {
                public const string Major = "Major";
                public const string Smell = " Code Smell";
            }

            [DiagnosticRule]
            public static class CT0001
            {
                public const string Id = nameof(CT0001);
                public const string Category = ContosoCategory.Major + ContosoCategory.Smell;
            }
            """, "DCAT0011");

    [Fact]
    public Task A_category_reached_through_nameof_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + """
            internal static class Usage { }

            [DiagnosticRule]
            public static class CT0001
            {
                public const string Id = nameof(CT0001);
                public const string Category = nameof(Usage);
            }
            """, "DCAT0011");

    // --- What DCAT0011 must stay out of ---------------------------------------------------------

    [Fact]
    public Task A_rule_with_no_usable_category_is_reported_once() =>
        // DCAT0004 owns the missing constant. Reporting DCAT0011 beside it would name two problems
        // where the author has one, and the second would disappear on fixing the first anyway.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + """
            [DiagnosticRule]
            public static class CT0001
            {
                public const string Id = nameof(CT0001);
            }
            """, "DCAT0004");

    [Fact]
    public Task A_blank_category_is_reported_once() =>
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + """
            [DiagnosticRule]
            public static class CT0001
            {
                public const string Id = nameof(CT0001);
                public const string Category = "   ";
            }
            """, "DCAT0004");

    [Fact]
    public Task A_static_readonly_category_is_reported_once() =>
        // Not a constant, so §8.3 fails and DCAT0004 owns it. DCAT0011 must not add a second finding
        // about a member that cannot be an attribute argument in the first place.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + """
            [DiagnosticCategory]
            internal static class ContosoCategory
            {
                public const string Usage = "Usage";
            }

            [DiagnosticRule]
            public static class CT0001
            {
                public const string Id = nameof(CT0001);
                public static readonly string Category = ContosoCategory.Usage;
            }
            """, "DCAT0004");

    [Fact]
    public Task A_category_constant_outside_a_rule_is_not_reported() =>
        // The action sees every field declaration in the compilation. A class named the same way, or a
        // constant named Category in ordinary code, is none of this analyzer's business.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, """
            public static class NotARule
            {
                public const string Category = "Usage";
            }
            """);

    [Fact]
    public Task The_category_container_itself_is_not_reported() =>
        // A [DiagnosticCategory] class is not a rule, so its own constants are literals by definition.
        // Reporting them would make the required form impossible to write.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, UsingFoundation + """
            [DiagnosticCategory]
            internal static class ContosoCategory
            {
                public const string Category = "Usage";
            }
            """);
}
