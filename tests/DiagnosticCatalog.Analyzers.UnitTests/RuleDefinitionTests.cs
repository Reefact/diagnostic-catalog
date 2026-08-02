using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// The structural contract of specification §8, as DCAT0002, DCAT0003 and DCAT0004 enforce it.
/// </summary>
public sealed class RuleDefinitionTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new DiagnosticRuleDefinitionAnalyzer();

    // The marker taken from the real foundation assembly, which the test project references.
    private const string UsingFoundation = "using DiagnosticCatalog;\n";

    // §8.5 applies to every rule, so a fixture that carries a category at all declares the container
    // and reaches its value through it. Writing the literal instead would add a DCAT0011 to tests that
    // are about the type, the id or the marker; CategoryReferenceTests is where the category is what
    // is under test.
    private const string CategoryContainer = """
        [DiagnosticCategory]
        internal static class Cat
        {
            public const string Usage = "Usage";
        }

        """;

    [Fact]
    public Task A_rule_satisfying_the_contract_is_not_reported() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = Cat.Usage;
            }
            """);

    [Fact]
    public Task A_top_level_rule_is_valid_without_a_container() =>
        // §7.3: nesting inside a container is the canonical full form, not a requirement. An analyzer
        // demanding a container would reject the minimal example the specification itself blesses.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class JD0001
            {
                public const string Id = nameof(JD0001);
                public const string Category = Cat.Usage;
            }
            """);

    [Fact]
    public Task An_id_that_is_not_a_valid_identifier_is_still_valid() =>
        // §8.2 explicitly blesses this pair. It is also why DCAT0005 needs its IsValidIdentifier guard.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class RULE_001
            {
                public const string Id = "RULE-001";
                public const string Category = Cat.Usage;
            }
            """);

    // --- DCAT0002, the type itself ------------------------------------------------------------

    [Fact]
    public Task A_non_static_rule_type_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public sealed class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = Cat.Usage;
            }
            """, "DCAT0002");

    [Fact]
    public Task A_generic_rule_type_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007<T>
            {
                public const string Id = "JD0007";
                public const string Category = Cat.Usage;
            }
            """, "DCAT0002");

    [Fact]
    public Task A_rule_declared_as_a_struct_is_reported() =>
        // Not reachable through the foundation's own marker: its AttributeUsage is AttributeTargets.Class,
        // so the compiler rejects [DiagnosticRule] on a struct with CS0592 before any analyzer runs. It IS
        // reachable through a catalogue that declares its own marker (§7.2) and gives it a wider usage —
        // and through a referenced assembly, which is what DCAT0010 will read later. The contract check is
        // written over TypeKind for those paths, so it is exercised through one of them here rather than
        // being assumed.
        AnalyzerHarness.ReportsAsync(Analyzer, """
            namespace DiagnosticCatalog
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                internal sealed class DiagnosticRuleAttribute : System.Attribute
                {
                }
            }

            namespace Vendor.Catalog
            {
                [global::DiagnosticCatalog.DiagnosticCategory]
                internal static class Cat
                {
                    public const string Usage = "Usage";
                }

                [global::DiagnosticCatalog.DiagnosticRule]
                public struct JD0007
                {
                    public const string Id = nameof(JD0007);
                    public const string Category = Cat.Usage;
                }
            }
            """, "DCAT0002");

    // --- DCAT0003 and DCAT0004, the two constants ---------------------------------------------

    [Fact]
    public Task A_missing_id_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Category = Cat.Usage;
            }
            """, "DCAT0003");

    [Fact]
    public Task A_missing_category_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
            }
            """, "DCAT0004");

    [Fact]
    public Task A_static_readonly_id_is_reported() =>
        // The distinction that matters: this holds a value at run time but cannot be an attribute
        // argument, which is the whole reason the contract demands a constant.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                public static readonly string Id = "JD0007";
                public const string Category = Cat.Usage;
            }
            """, "DCAT0003");

    [Fact]
    public Task A_non_public_id_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                internal const string Id = "JD0007";
                public const string Category = Cat.Usage;
            }
            """, "DCAT0003");

    [Fact]
    public Task An_id_of_the_wrong_type_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const int Id = 7;
                public const string Category = Cat.Usage;
            }
            """, "DCAT0003");

    [Fact]
    public Task An_empty_category_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "";
            }
            """, "DCAT0004");

    [Fact]
    public Task A_whitespace_category_is_reported() =>
        // §8.2 requires "not whitespace-only" of Id and §11.4 extends Id's validations to Category.
        // A category of " " is no more usable than one of "".
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "   ";
            }
            """, "DCAT0004");

    [Fact]
    public Task Every_applicable_violation_is_reported_at_once() =>
        // Not the first one only: fixing a violation should not reveal the next on the following build.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + """
            [DiagnosticRule]
            public sealed class JD0007
            {
            }
            """, "DCAT0002", "DCAT0003", "DCAT0004");

    // --- what must NOT be treated as a rule ---------------------------------------------------

    [Fact]
    public Task An_unmarked_type_shaped_like_a_rule_is_ignored() =>
        // §7.2 offers the purely structural shape as a "may". It is not adopted, so the marker remains
        // the only signal — and a type that merely looks like a rule is somebody else's constants.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, """
            public sealed class NotARule
            {
                public static readonly string Id = "X";
                public const int Category = 1;
            }
            """);

    [Fact]
    public Task An_ordinary_type_is_ignored() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, """
            public sealed class Ordinary
            {
                public int Value { get; set; }
            }
            """);
}
