using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using DiagnosticCatalog.CodeFixes;

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// <i>Fix all occurrences</i> at project and solution scope — the ones a migration actually invokes.
/// </summary>
/// <remarks>
/// <para>
/// The provider announces three scopes. Only one of them was ever built a context for, so the other two
/// were a promise the suite restated and never tested: a fix-all that stopped after the document it was
/// invoked from would have passed every test there was, while a team running <i>Fix all in solution</i>
/// over a codebase adopting a catalogue got a fraction of their suppressions migrated and no word about
/// the rest. Suppressions that stay as they were written are the failure with no symptom — the platform
/// never validates a suppression's category (§3.2), which is the whole reason this library exists.
/// </para>
/// <para>
/// Every solution here carries a second C# project holding the same occurrences, so that the two scopes
/// differ in their answer rather than only in their name: a project scope must leave it alone, a
/// solution scope must fix it. A third project carrying no occurrence at all rides along in the
/// solution-scope tests: the scope reaches it, and it has to come back byte for byte as it went in.
/// </para>
/// </remarks>
public sealed class FixAllScopeTests
{
    private static readonly DiagnosticAnalyzer UseSite = new SuppressionUsageAnalyzer();

    /// <summary>The catalogue the suppressions below name, in a namespace no document imports.</summary>
    private const string Catalogue = """
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

                [DiagnosticCatalog.DiagnosticRule]
                public static class S3776
                {
                    public const string Id = nameof(S3776);
                    public const string Category = "Major Code Smell";
                }
            }
        }
        """;

    /// <summary>A document suppressing two rules with literals, under a name of its own.</summary>
    private static (string Name, string Source) Suppressing(string type) => (
        type + ".cs",
        $$"""
        using System.Diagnostics.CodeAnalysis;

        [SuppressMessage("Major Code Smell", "S1144", Justification = "reflection")]
        public sealed class {{type}}First { }

        [SuppressMessage("Major Code Smell", "S3776", Justification = "measured")]
        public sealed class {{type}}Second { }
        """);

    /// <summary>A C# project of two documents, each suppressing two rules, plus the catalogue.</summary>
    private static FixAllHarness.ProjectFixture Adopting(string project) =>
        new(project,
            [
                ("Catalogue.cs", Catalogue),
                Suppressing(project + "Alpha"),
                Suppressing(project + "Beta"),
            ]);

    /// <summary>A C# project the fix has nothing to do in.</summary>
    private static FixAllHarness.ProjectFixture Untouched(string project) =>
        new(project, [("Plain.cs", "public sealed class " + project + "Plain { }")]);

    private const string MigrateSuppression = "DiagnosticCatalog.UseCatalogReference";

    /// <summary>Asserts that both suppressions in <paramref name="document"/> now name the catalogue.</summary>
    private static void Migrated(IReadOnlyDictionary<string, string> documents, string document)
    {
        string text = documents[document];

        Assert.Contains("SonarRules.S1144.Category", text, StringComparison.Ordinal);
        Assert.Contains("SonarRules.S3776.Category", text, StringComparison.Ordinal);
        Assert.Contains("using Vendor.Sonar;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Major Code Smell\"", text, StringComparison.Ordinal);
    }

    /// <summary>Asserts that <paramref name="document"/> still carries the literals it was written with.</summary>
    private static void LeftAlone(IReadOnlyDictionary<string, string> documents, string document)
    {
        string text = documents[document];

        Assert.Contains("\"Major Code Smell\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SonarRules.S1144.Category", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_project_scope_migrates_every_document_of_that_project()
    {
        IReadOnlyDictionary<string, string> documents = await FixAllHarness.ApplyAcrossAsync(
            UseSite,
            new UseCatalogReferenceCodeFixProvider(),
            FixAllScope.Project,
            [Adopting("Web"), Adopting("Api")],
            MigrateSuppression);

        // Both documents of the invoked project, not just the one that happened to be reached first.
        Migrated(documents, "Web/WebAlpha.cs");
        Migrated(documents, "Web/WebBeta.cs");
    }

    [Fact]
    public async Task A_project_scope_leaves_the_other_project_exactly_as_it_was()
    {
        IReadOnlyDictionary<string, string> documents = await FixAllHarness.ApplyAcrossAsync(
            UseSite,
            new UseCatalogReferenceCodeFixProvider(),
            FixAllScope.Project,
            [Adopting("Web"), Adopting("Api")],
            MigrateSuppression);

        // The half that makes "project" a scope rather than a synonym for "solution".
        LeftAlone(documents, "Api/ApiAlpha.cs");
        LeftAlone(documents, "Api/ApiBeta.cs");
    }

    [Fact]
    public async Task A_solution_scope_migrates_every_c_sharp_project()
    {
        IReadOnlyDictionary<string, string> documents = await FixAllHarness.ApplyAcrossAsync(
            UseSite,
            new UseCatalogReferenceCodeFixProvider(),
            FixAllScope.Solution,
            [Adopting("Web"), Adopting("Api"), Untouched("Tools")],
            MigrateSuppression);

        Migrated(documents, "Web/WebAlpha.cs");
        Migrated(documents, "Web/WebBeta.cs");
        Migrated(documents, "Api/ApiAlpha.cs");
        Migrated(documents, "Api/ApiBeta.cs");
    }

    [Fact]
    public async Task A_solution_scope_leaves_a_project_it_has_nothing_to_do_in_untouched()
    {
        IReadOnlyDictionary<string, string> documents = await FixAllHarness.ApplyAcrossAsync(
            UseSite,
            new UseCatalogReferenceCodeFixProvider(),
            FixAllScope.Solution,
            [Adopting("Web"), Adopting("Api"), Untouched("Tools")],
            MigrateSuppression);

        Assert.Equal("public sealed class ToolsPlain { }", documents["Tools/Plain.cs"]);
    }

    [Fact]
    public async Task No_occurrence_is_dropped_anywhere_a_solution_scope_reached()
    {
        // What a migration promises, stated once over the whole solution rather than document by
        // document: after a solution-wide fix-all, not one literal suppression is left. A fix that
        // silently dropped an occurrence would leave a suppression that compiles, suppresses nothing,
        // and reports nothing.
        IReadOnlyDictionary<string, string> documents = await FixAllHarness.ApplyAcrossAsync(
            UseSite,
            new UseCatalogReferenceCodeFixProvider(),
            FixAllScope.Solution,
            [Adopting("Web"), Adopting("Api"), Untouched("Tools")],
            MigrateSuppression);

        foreach ((string name, string text) in documents)
        {
            // The literal FIRST ARGUMENT, which is what a suppression written as magic strings looks
            // like. The catalogue document declares "Major Code Smell" too — as the constant that
            // replaces it — so the category alone would flag the very file doing the replacing.
            Assert.False(text.Contains("SuppressMessage(\"", StringComparison.Ordinal),
                         name + " still carries a literal suppression:" + Environment.NewLine + text);
            Assert.DoesNotContain("\"S1144\"", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\"S3776\"", text, StringComparison.Ordinal);
        }
    }
}
