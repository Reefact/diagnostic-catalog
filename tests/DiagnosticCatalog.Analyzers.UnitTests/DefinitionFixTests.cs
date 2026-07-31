using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using DiagnosticCatalog.CodeFixes;

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// The §12.4 definition fixes: what they repair, and — at greater length — what they refuse.
/// </summary>
/// <remarks>
/// The refusals carry most of the weight here, because each of the three diagnostics covers several faults
/// and only some of them have a repair the code determines. ADR-0018 asks that every such claim be a
/// testable assertion rather than a comment, and <c>OffersNothingAsync</c> is what makes it one: it fails
/// unless the diagnostic was still reported, so a refusal cannot pass by the analyzer having gone quiet.
/// </remarks>
public sealed class DefinitionFixTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new DiagnosticRuleDefinitionAnalyzer();

    private static readonly CodeFixProvider MakeStatic = new MakeRuleTypeStaticCodeFixProvider();

    private static readonly CodeFixProvider MakeConstant = new MakeRuleMemberConstantCodeFixProvider();

    private static readonly CodeFixProvider Declare = new AddRuleMemberCodeFixProvider();

    private const string UsingFoundation = "using DiagnosticCatalog;\n";

    // --- DCAT0002, making the type static -----------------------------------------------------

    [Fact]
    public async Task A_sealed_rule_type_becomes_static()
    {
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, MakeStatic, UsingFoundation + """
            [DiagnosticRule]
            public sealed class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """);

        Assert.Equal(UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """, fixteds);
    }

    [Fact]
    public async Task A_rule_type_with_no_modifiers_becomes_static()
    {
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, MakeStatic, UsingFoundation + """
            [DiagnosticRule]
            class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """);

        Assert.Equal(UsingFoundation + """
            [DiagnosticRule]
            static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """, fixteds);
    }

    [Fact]
    public Task A_generic_rule_type_is_left_alone() =>
        // Removing the type parameters would repair DCAT0002, and would also change what the author
        // declared. Only they know whether the generic was the mistake.
        CodeFixHarness.OffersNothingAsync(Analyzer, MakeStatic, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007<T>
            {
                public const string Id = "JD0007";
                public const string Category = "Usage";
            }
            """);

    [Fact]
    public Task A_rule_declared_as_a_struct_is_left_alone() =>
        // Reachable only through a catalogue's own marker (§7.2), as RuleDefinitionTests explains. Turning
        // a struct into a class is not a repair, it is a different type.
        CodeFixHarness.OffersNothingAsync(Analyzer, MakeStatic, """
            namespace DiagnosticCatalog
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                internal sealed class DiagnosticRuleAttribute : System.Attribute
                {
                }
            }

            namespace Vendor.Catalog
            {
                [global::DiagnosticCatalog.DiagnosticRule]
                public struct JD0007
                {
                    public const string Id = nameof(JD0007);
                    public const string Category = "Usage";
                }
            }
            """);

    [Fact]
    public Task A_rule_declared_as_a_record_is_left_alone() =>
        // Unlike the struct above, this one is reachable through the foundation's own marker: a record
        // class is a class, so AttributeTargets.Class admits it. `static record` does not exist, so there
        // is no keyword to add — and rewriting the record into a class is not a repair of what was written.
        CodeFixHarness.OffersNothingAsync(Analyzer, MakeStatic, UsingFoundation + """
            [DiagnosticRule]
            public record JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """);

    [Fact]
    public async Task A_nested_rule_type_becomes_static()
    {
        // A static class nested in a non-static one is legal, so the outer type is none of this fix's
        // business — and the reported location is the inner identifier either way.
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, MakeStatic, UsingFoundation + """
            public sealed class JustDummiesRules
            {
                [DiagnosticRule]
                public sealed class JD0007
                {
                    public const string Id = nameof(JD0007);
                    public const string Category = "Usage";
                }
            }
            """);

        Assert.Equal(UsingFoundation + """
            public sealed class JustDummiesRules
            {
                [DiagnosticRule]
                public static class JD0007
                {
                    public const string Id = nameof(JD0007);
                    public const string Category = "Usage";
                }
            }
            """, fixteds);
    }

    [Fact]
    public async Task A_rule_type_with_a_static_constructor_becomes_static()
    {
        // Allowed where an instance constructor is not: a static class may have one.
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, MakeStatic, UsingFoundation + """
            [DiagnosticRule]
            public sealed class JD0007
            {
                static JD0007()
                {
                }

                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """);

        Assert.Equal(UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                static JD0007()
                {
                }

                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """, fixteds);
    }

    [Fact]
    public Task A_rule_type_with_an_instance_constructor_is_left_alone() =>
        // CS0710: a static class may not declare one. Nothing here says whether the constructor or the
        // marker was the mistake.
        CodeFixHarness.OffersNothingAsync(Analyzer, MakeStatic, UsingFoundation + """
            [DiagnosticRule]
            public sealed class JD0007
            {
                public JD0007()
                {
                }

                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """);

    [Fact]
    public Task A_rule_type_holding_an_instance_member_is_left_alone() =>
        // `static` would not compile here. A fix that traded a warning for CS0708 would be worse than none.
        CodeFixHarness.OffersNothingAsync(Analyzer, MakeStatic, UsingFoundation + """
            [DiagnosticRule]
            public sealed class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";

                public int Count { get; set; }
            }
            """);

    [Fact]
    public Task A_rule_type_with_a_base_list_is_left_alone() =>
        // A static class implements nothing and derives from nothing but object (CS0714).
        CodeFixHarness.OffersNothingAsync(Analyzer, MakeStatic, UsingFoundation + """
            public interface IMarker
            {
            }

            [DiagnosticRule]
            public sealed class JD0007 : IMarker
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """);

    [Fact]
    public Task A_partial_rule_type_is_left_alone() =>
        // The other parts may hold the instance members that decide the question, and this fix cannot see
        // them. The diagnostic is also reported once per part, so a fix-all would visit each.
        CodeFixHarness.OffersNothingAsync(Analyzer, MakeStatic, UsingFoundation + """
            [DiagnosticRule]
            public partial class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """);

    // --- DCAT0003 and DCAT0004, repairing the member ------------------------------------------

    [Fact]
    public async Task A_static_readonly_id_becomes_a_public_constant()
    {
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, MakeConstant, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public static readonly string Id = "JD0007";
                public const string Category = "Usage";
            }
            """);

        Assert.Equal(UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = "JD0007";
                public const string Category = "Usage";
            }
            """, fixteds);
    }

    [Fact]
    public async Task A_non_public_id_becomes_a_public_constant()
    {
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, MakeConstant, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                internal const string Id = "JD0007";
                public const string Category = "Usage";
            }
            """);

        Assert.Equal(UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = "JD0007";
                public const string Category = "Usage";
            }
            """, fixteds);
    }

    [Fact]
    public async Task Both_faults_at_once_are_repaired_by_one_action()
    {
        // §12.4 names "make it public" and "replace static readonly with const" separately. Applied
        // separately they would leave the diagnostic reported on the member just edited.
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, MakeConstant, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                private static readonly string Category = "Usage";
            }
            """);

        Assert.Equal(UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """, fixteds);
    }

    [Fact]
    public Task An_id_of_the_wrong_type_is_left_alone() =>
        // `const int Id = 7` says nothing about what string was meant.
        CodeFixHarness.OffersNothingAsync(Analyzer, MakeConstant, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const int Id = 7;
                public const string Category = "Usage";
            }
            """);

    [Fact]
    public Task An_empty_category_is_left_alone() =>
        // The member is already a public constant. What it lacks is a value, and only the analyzer this
        // rule mirrors has it.
        CodeFixHarness.OffersNothingAsync(Analyzer, MakeConstant, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "";
            }
            """);

    [Fact]
    public Task A_non_constant_initialiser_is_left_alone() =>
        // `const` cannot hold this expression at all, so there is no rewrite — only a different program.
        CodeFixHarness.OffersNothingAsync(Analyzer, MakeConstant, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public static readonly string Id = System.Guid.NewGuid().ToString();
                public const string Category = "Usage";
            }
            """);

    [Fact]
    public Task An_id_declared_as_a_property_is_left_alone() =>
        // Not a field, so there are no modifiers to respell — turning a property into a constant is a
        // change to the type's surface, not a repair of its modifiers.
        CodeFixHarness.OffersNothingAsync(Analyzer, MakeConstant, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public static string Id => "JD0007";
                public const string Category = "Usage";
            }
            """);

    // --- DCAT0003 and DCAT0004, declaring the member ------------------------------------------

    [Fact]
    public async Task A_missing_id_is_declared_from_the_type_name()
    {
        // `nameof` is §8.2's recommended form and is read off the declaration, so this one is not a
        // placeholder at all: for a catalogue named after its rules it is the value.
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, Declare, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Category = "Usage";
            }
            """);

        Assert.Equal(UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """, fixteds);
    }

    [Fact]
    public async Task A_missing_category_is_declared_as_a_placeholder()
    {
        // The placeholder §12.4 spells out, verbatim, and it goes after Id. Note what it costs: the string
        // is non-blank, so DCAT0004 stops being reported — the fix trades the warning for a marker. The
        // expected source below is where that word is written down; a comment naming it reads to S1135 as
        // an unfinished task.
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, Declare, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
            }
            """);

        Assert.Equal(UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "TODO";
            }
            """, fixteds);
    }

    [Fact]
    public Task A_missing_member_is_not_declared_beside_a_property_of_that_name() =>
        // The name is taken. A constant beside it would not compile.
        CodeFixHarness.OffersNothingAsync(Analyzer, Declare, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public static string Id => "JD0007";
                public const string Category = "Usage";
            }
            """);

    [Fact]
    public Task No_member_is_declared_in_a_partial_type() =>
        // Reported once per part, so a fix-all would declare the member in every one of them.
        CodeFixHarness.OffersNothingAsync(Analyzer, Declare, UsingFoundation + """
            [DiagnosticRule]
            public static partial class JD0007
            {
                public const string Category = "Usage";
            }
            """);

    // --- trivia, which a fix loses silently -----------------------------------------------------

    [Fact]
    public async Task A_documented_rule_type_keeps_its_comment()
    {
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, MakeStatic, UsingFoundation + """
            /// <summary>Unused private members should be removed.</summary>
            [DiagnosticRule]
            public sealed class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """);

        Assert.Equal(UsingFoundation + """
            /// <summary>Unused private members should be removed.</summary>
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = "Usage";
            }
            """, fixteds);
    }

    [Fact]
    public async Task A_documented_member_keeps_its_comment_and_its_attribute()
    {
        // Both hang off the member being respelled, and both are trivia to the modifier list this fix
        // rebuilds — the one place a rewrite of that kind drops them without any test noticing.
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, MakeConstant, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                /// <summary>The rule's identifier.</summary>
                [System.Obsolete]
                internal static readonly string Id = "JD0007";

                public const string Category = "Usage";
            }
            """);

        Assert.Equal(UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                /// <summary>The rule's identifier.</summary>
                [System.Obsolete]
                public const string Id = "JD0007";

                public const string Category = "Usage";
            }
            """, fixteds);
    }

    [Fact]
    public async Task A_member_is_declared_into_a_single_line_body()
    {
        // The shape with nowhere obvious to insert: no member sits on its own line to copy an indent from.
        // The layout the author chose is kept rather than corrected — the fix declares a member, it does
        // not reformat the type around it.
        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, Declare, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007 { public const string Category = "Usage"; }
            """);

        Assert.Equal(
            UsingFoundation
            + "[DiagnosticRule]\n"
            + "public static class JD0007 { public const string Id = nameof(JD0007); "
            + "public const string Category = \"Usage\"; }",
            fixteds);
    }

    [Fact]
    public async Task A_declared_member_keeps_the_line_endings_the_file_uses()
    {
        // The regression this file exists to hold. Leaving the layout to Roslyn's formatter meant it
        // reformatted a region AROUND the inserted member and wrote line endings with the platform's
        // newline: enough to rewrite the ending above the type declaration, invisible on Linux, and a red
        // Windows job. Written with explicit \r\n so it reproduces everywhere rather than on one runner.
        string source =
            "using DiagnosticCatalog;\r\n"
            + "[DiagnosticRule]\r\n"
            + "public static class JD0007\r\n"
            + "{\r\n"
            + "    public const string Category = \"Usage\";\r\n"
            + "}\r\n";

        string fixteds = await CodeFixHarness.ApplyAsync(Analyzer, Declare, source);

        Assert.Equal(
            "using DiagnosticCatalog;\r\n"
            + "[DiagnosticRule]\r\n"
            + "public static class JD0007\r\n"
            + "{\r\n"
            + "    public const string Id = nameof(JD0007);\r\n"
            + "    public const string Category = \"Usage\";\r\n"
            + "}\r\n",
            fixteds);
    }

    [Fact]
    public async Task Both_members_are_declared_into_an_empty_body()
    {
        // The one shape with no layout to copy: the type declares nothing, so neither indentation nor line
        // placement can be read off a sibling. Four spaces is the assumption, and this is where it is
        // written down rather than left in a comment.
        ImmutableArray<string> results = await CodeFixHarness.ApplyEachAsync(Analyzer, Declare, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
            }
            """);

        Assert.Equal(2, results.Length);

        Assert.Contains(UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
            }
            """, results);

        Assert.Contains(UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Category = "TODO";
            }
            """, results);
    }

    [Fact]
    public async Task One_equivalence_key_covers_both_constants()
    {
        // §21.4 asks that *Fix all occurrences* honour the key consistently. A rule missing both constants
        // is one thing wrong twice, so one key is what lets a codebase be repaired in a single pass rather
        // than in one pass per member.
        ImmutableArray<string?> keys = await CodeFixHarness.EquivalenceKeysAsync(
            Analyzer,
            Declare,
            UsingFoundation + """
                [DiagnosticRule]
                public static class JD0007
                {
                }
                """);

        Assert.Equal(2, keys.Length);
        Assert.Single(keys.Distinct());
    }

    [Fact]
    public Task No_id_is_declared_in_a_generic_type() =>
        // `nameof(JD0007)` does not bind inside `JD0007<T>`, and the value has nowhere else to come from.
        CodeFixHarness.OffersNothingAsync(Analyzer, Declare, UsingFoundation + """
            [DiagnosticRule]
            public static class JD0007<T>
            {
                public const string Category = "Usage";
            }
            """);
}
