using System;
using System.Threading.Tasks;

using DiagnosticCatalog.CodeFixes;

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// <i>Fix all occurrences</i>, which is how a codebase adopts a catalog rather than one suppression at a
/// time (§3.5, §12).
/// </summary>
/// <remarks>
/// <para>
/// Every fix here already passed its single-occurrence tests. What those cannot see is what happens when
/// two occurrences of the same fix want to edit the SAME place in the document — and both of these do:
/// a suppression fix appends a <c>using</c> to the compilation unit's list, and the member fix inserts at
/// the top of a rule's body. Two edits at one offset with different text is a conflict, and a fix-all
/// engine that merges independently-computed edits resolves it by dropping one of them.
/// </para>
/// <para>
/// Dropping an edit here is not a partial result: the edit that goes is the whole document change for
/// that occurrence, so the occurrence stays exactly as it was and the operation reports success. These
/// tests assert what the operation NAMES — every occurrence — rather than the number of edits that
/// happened to survive.
/// </para>
/// </remarks>
public sealed class FixAllTests
{
    private static readonly DiagnosticAnalyzer UseSite = new SuppressionUsageAnalyzer();
    private static readonly DiagnosticAnalyzer Definition = new DiagnosticRuleDefinitionAnalyzer();

    /// <summary>Two catalogs in two namespaces, so the two fixes need two different imports.</summary>
    private const string TwoNamespacedCatalogs = """
        using System.Diagnostics.CodeAnalysis;

        namespace Vendor.Sonar
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

        namespace Vendor.StyleCop
        {
            public static class StyleCopRules
            {
                [DiagnosticCatalog.DiagnosticRule]
                public static class SA1600
                {
                    public const string Id = nameof(SA1600);
                    public const string Category = "Documentation Rules";
                }
            }
        }

        """;

    [Fact]
    public async Task Every_suppression_is_migrated_when_the_rules_live_in_different_namespaces()
    {
        // The shape a real adoption meets on its first file: this repository ships Sonar, StyleCop and
        // NetAnalyzers as three catalogs in three namespaces, and a file that suppresses rules from two
        // of them needs two imports. Both are appended to the same list, at the same offset.
        string fixedSource = await FixAllHarness.ApplyAsync(
            UseSite,
            new UseCatalogReferenceCodeFixProvider(),
            TwoNamespacedCatalogs + """
                [SuppressMessage("Major Code Smell", "S1144", Justification = "reflection")]
                public sealed class First { }

                [SuppressMessage("Documentation Rules", "SA1600", Justification = "internal")]
                public sealed class Second { }
                """,
            "DiagnosticCatalog.UseCatalogReference");

        Assert.Contains("SonarRules.S1144.Category", fixedSource, StringComparison.Ordinal);
        Assert.Contains("StyleCopRules.SA1600.Category", fixedSource, StringComparison.Ordinal);

        Assert.Contains("using Vendor.Sonar;", fixedSource, StringComparison.Ordinal);
        Assert.Contains("using Vendor.StyleCop;", fixedSource, StringComparison.Ordinal);

        // Neither suppression may be left as it was written.
        Assert.DoesNotContain("\"Major Code Smell\", \"S1144\"", fixedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Documentation Rules\", \"SA1600\"", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_imports_land_in_document_order_whatever_order_the_occurrences_arrive_in()
    {
        // The occurrences arrive in whatever order the analyzer reported them, which nothing
        // promises to be the order they sit in. Appending each import as its occurrence is met lets
        // that order reach the document, so the same file fixed twice can differ in its bytes for
        // no reason a reader could see. Handed the occurrences last-first, the result must not move.
        string fixedSource = await FixAllHarness.ApplyAsync(
            UseSite,
            new UseCatalogReferenceCodeFixProvider(),
            TwoNamespacedCatalogs + """
                [SuppressMessage("Major Code Smell", "S1144", Justification = "reflection")]
                public sealed class First { }

                [SuppressMessage("Documentation Rules", "SA1600", Justification = "internal")]
                public sealed class Second { }
                """,
            "DiagnosticCatalog.UseCatalogReference",
            lastOccurrenceFirst: true);

        int sonar = fixedSource.IndexOf("using Vendor.Sonar;", StringComparison.Ordinal);
        int styleCop = fixedSource.IndexOf("using Vendor.StyleCop;", StringComparison.Ordinal);

        Assert.True(sonar >= 0 && styleCop >= 0, "both imports must be present:\n" + fixedSource);

        // S1144's suppression is the first in the file, so its import is the first appended.
        Assert.True(
            sonar < styleCop,
            "the imports must follow the occurrences' order in the document, not the order they were reported:\n"
            + fixedSource);
    }

    [Fact]
    public async Task Both_constants_are_declared_when_a_rule_declares_neither()
    {
        // DCAT0003 and DCAT0004 are reported together on a rule with an empty body, and both actions
        // carry one equivalence key on purpose — "a rule missing both constants is one thing wrong
        // twice". An empty body puts both insertions at the same offset, just after the open brace.
        string fixedSource = await FixAllHarness.ApplyAsync(
            Definition,
            new AddRuleMemberCodeFixProvider(),
            """
            using DiagnosticCatalog;

            [DiagnosticRule]
            public static class JD0007
            {
            }
            """,
            "DiagnosticCatalog.AddRuleMember");

        Assert.Contains("public const string Id = nameof(JD0007);", fixedSource, StringComparison.Ordinal);
        Assert.Contains("public const string Category = \"TODO\";", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Every_half_migrated_suppression_is_completed()
    {
        // DCAT0007 rewrites one side and imports the rule's namespace for the other's sake, so two
        // occurrences in one file want the same import at the same offset.
        string fixedSource = await FixAllHarness.ApplyAsync(
            UseSite,
            new CompleteCatalogReferenceCodeFixProvider(),
            TwoNamespacedCatalogs + """
                [SuppressMessage(Vendor.Sonar.SonarRules.S1144.Category, "S1144", Justification = "a")]
                public sealed class First { }

                [SuppressMessage(Vendor.StyleCop.StyleCopRules.SA1600.Category, "SA1600", Justification = "b")]
                public sealed class Second { }
                """,
            "DiagnosticCatalog.CompleteCatalogReference");

        Assert.DoesNotContain("\"S1144\"", fixedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SA1600\"", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fix_all_aligned_on_the_category_corrects_every_identifier()
    {
        // The provider offering a choice: the invoked key must decide for every occurrence alike, and
        // aligning on the category corrects the identifier (§12.1). The two pairs cross the two
        // namespaces in opposite directions, so the corrections need one import each — the conflict.
        string fixedSource = await FixAllHarness.ApplyAsync(
            UseSite,
            new AlignIncoherentPairCodeFixProvider(),
            TwoNamespacedCatalogs + """
                [SuppressMessage(Vendor.Sonar.SonarRules.S1144.Category, Vendor.StyleCop.StyleCopRules.SA1600.Id, Justification = "a")]
                public sealed class First { }

                [SuppressMessage(Vendor.StyleCop.StyleCopRules.SA1600.Category, Vendor.Sonar.SonarRules.S1144.Id, Justification = "b")]
                public sealed class Second { }
                """,
            "AlignOnCategory");

        // Each pair now names one rule. The category keeps the spelling the author gave it — aligning on
        // it rewrites the identifier and nothing else (§12.3).
        Assert.Contains(
            "Vendor.Sonar.SonarRules.S1144.Category, SonarRules.S1144.Id",
            fixedSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "Vendor.StyleCop.StyleCopRules.SA1600.Category, StyleCopRules.SA1600.Id",
            fixedSource,
            StringComparison.Ordinal);

        Assert.Contains("using Vendor.Sonar;", fixedSource, StringComparison.Ordinal);
        Assert.Contains("using Vendor.StyleCop;", fixedSource, StringComparison.Ordinal);
    }
}
