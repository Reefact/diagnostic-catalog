using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using DiagnosticCatalog.CodeFixes;

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// The DCAT0006 code fix (§12.2): replace the literals with the catalog reference they match.
/// </summary>
/// <remarks>
/// §3.5 calls this the primary entry point of the library, and the reason is mechanical: a codebase
/// adopts a catalog by accepting this fix across its suppressions, not by hand-editing each one. What it
/// must never do is lose anything on the way — the surrounding arguments are the author's, and §21.4
/// spells out each one it has to leave alone.
/// </remarks>
public sealed class UseCatalogReferenceFixTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new SuppressionUsageAnalyzer();
    private static readonly CodeFixProvider Provider = new UseCatalogReferenceCodeFixProvider();

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

    /// <summary>The same rules, in a namespace, so the reference needs a using.</summary>
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

    [Fact]
    public async Task The_literals_become_a_catalog_reference()
    {
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + Rules + """
            [SuppressMessage("Major Code Smell", "S1144", Justification = "kept for reflection")]
            public sealed class Target { }
            """);

        Assert.Contains("SonarRules.S1144.Category", fixedSource);
        Assert.Contains("SonarRules.S1144.Id", fixedSource);
        Assert.DoesNotContain("\"Major Code Smell\", \"S1144\"", fixedSource);
    }

    [Fact]
    public async Task The_justification_survives_untouched()
    {
        // The one part of a suppression a reviewer actually reads. A fix that migrated the identifiers
        // and dropped the reason would be a net loss, however correct the reference.
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + Rules + """
            [SuppressMessage("Major Code Smell", "S1144", Justification = "kept for reflection")]
            public sealed class Target { }
            """);

        Assert.Contains("Justification = \"kept for reflection\"", fixedSource);
    }

    [Fact]
    public async Task Scope_target_and_message_id_survive_untouched()
    {
        // An assembly-level suppression, which §21.2 lists in its own right: it must precede every
        // other element in the file, so the rules follow it rather than lead.
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + """
            [assembly: SuppressMessage("Major Code Smell", "S1144", Justification = "j", Scope = "member", Target = "~M:Target.Method", MessageId = "the-id")]

            """ + Rules + """
            public sealed class Target { }
            """);

        Assert.Contains("Scope = \"member\"", fixedSource);
        Assert.Contains("Target = \"~M:Target.Method\"", fixedSource);
        Assert.Contains("MessageId = \"the-id\"", fixedSource);
        Assert.Contains("SonarRules.S1144.Category", fixedSource);
    }

    [Fact]
    public async Task The_pair_written_by_parameter_name_and_reversed_is_not_swapped()
    {
        // Legal C#, and a trap. Read by position, checkId lands in the category slot: the analyzer then
        // looks up ("S1144", "Major Code Smell"), finds nothing and reports nothing — and a fix reading
        // it the same way would write the category where the identifier belongs.
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + Rules + """
            [SuppressMessage(checkId: "S1144", category: "Major Code Smell", Justification = "j")]
            public sealed class Target { }
            """);

        Assert.Contains("checkId: SonarRules.S1144.Id", fixedSource);
        Assert.Contains("category: SonarRules.S1144.Category", fixedSource);
        Assert.Contains("Justification = \"j\"", fixedSource);
    }

    [Fact]
    public async Task Other_attributes_are_left_alone()
    {
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + Rules + """
            [System.Obsolete("do not use")]
            [SuppressMessage("Major Code Smell", "S1144", Justification = "j")]
            [System.Serializable]
            public sealed class Target { }
            """);

        Assert.Contains("System.Obsolete(\"do not use\")", fixedSource);
        Assert.Contains("System.Serializable", fixedSource);
    }

    [Fact]
    public async Task The_using_is_inserted_when_the_rule_lives_in_another_namespace()
    {
        // §12.2's explicit requirement. Without it the rewritten reference does not bind, and the
        // harness's compile check is what would catch that rather than a text comparison.
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + NamespacedRules + """
            [SuppressMessage("Major Code Smell", "S1144", Justification = "j")]
            public sealed class Target { }
            """);

        Assert.Contains("using Vendor.Catalog;", fixedSource);
        Assert.Contains("SonarRules.S1144.Category", fixedSource);
    }

    [Fact]
    public async Task The_using_is_not_duplicated_when_it_is_already_there()
    {
        string fixedSource = await CodeFixHarness.ApplyAsync(
            Analyzer,
            Provider,
            "using Vendor.Catalog;\n" + Usings + NamespacedRules + """
                [SuppressMessage("Major Code Smell", "S1144", Justification = "j")]
                public sealed class Target { }
                """);

        Assert.Equal(1, Occurrences(fixedSource, "using Vendor.Catalog;"));
    }

    [Fact]
    public async Task No_using_is_added_for_a_rule_in_the_global_namespace()
    {
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + Rules + """
            [SuppressMessage("Major Code Smell", "S1144", Justification = "j")]
            public sealed class Target { }
            """);

        Assert.DoesNotContain("using ;", fixedSource);
        Assert.Equal(2, Occurrences(fixedSource, "using "));
    }

    [Fact]
    public Task Several_matching_rules_get_no_automatic_fix() =>
        // §11.6: the diagnostic is still reported — the harness asserts that — but the fix offers
        // nothing rather than choosing one of the candidates for the author.
        CodeFixHarness.OffersNothingAsync(Analyzer, Provider, Usings + Rules + """
            namespace Other
            {
                public static class Duplicate
                {
                    [DiagnosticCatalog.DiagnosticRule]
                    public static class S1144
                    {
                        public const string Id = nameof(S1144);
                        public const string Category = "Major Code Smell";
                    }
                }
            }

            [SuppressMessage("Major Code Smell", "S1144", Justification = "j")]
            public sealed class Target { }
            """);

    [Fact]
    public async Task Every_occurrence_offers_the_same_equivalence_key()
    {
        // §12, and the failure it exists to prevent: Fix all occurrences groups by this key, so a key
        // varying per rule would fix only the occurrences of whichever rule the author invoked it on
        // and quietly leave the others. Two different rules here, one key expected.
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

            [SuppressMessage("Major Code Smell", "S1144", Justification = "j")]
            public sealed class First { }

            [SuppressMessage("Code Smell", "S2094", Justification = "j")]
            public sealed class Second { }
            """);

        Assert.Equal(2, keys.Length);
        Assert.Single(keys.Distinct());
    }

    [Fact]
    public async Task The_friendly_name_suffix_is_dropped()
    {
        // The accepted, documented trade-off of §11.6. The suffix is prose that duplicates the rule's
        // own Title; keeping it would mean writing a literal beside the reference that replaced it.
        string fixedSource = await CodeFixHarness.ApplyAsync(Analyzer, Provider, Usings + Rules + """
            [SuppressMessage("Major Code Smell", "S1144:Unused private members should be removed")]
            public sealed class Target { }
            """);

        Assert.DoesNotContain("Unused private members should be removed", fixedSource);
        Assert.Contains("SonarRules.S1144.Id", fixedSource);
    }

    private static int Occurrences(string text, string value)
    {
        int count = 0;
        int index = text.IndexOf(value, System.StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, System.StringComparison.Ordinal);
        }

        return count;
    }
}
