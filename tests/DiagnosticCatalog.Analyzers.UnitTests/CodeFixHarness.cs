using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// Runs an analyzer, then applies a code fix to what it reported, and returns the resulting source.
/// </summary>
/// <remarks>
/// A code fix acts on a <see cref="Document"/>, so this needs a workspace where the diagnostic harness
/// needed only a compilation. The self-checks are the same in spirit: a fix that registered no action,
/// or one whose result no longer compiles, is a silent failure that a naive comparison would not catch —
/// the "expected" text would simply be whatever the broken fix produced.
/// </remarks>
internal static class CodeFixHarness
{
    /// <summary>Applies the single offered fix and returns the rewritten source.</summary>
    internal static Task<string> ApplyAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string source) =>
        ApplyCoreAsync(analyzer, provider, source, equivalenceKey: null);

    /// <summary>
    /// Applies the fix carrying <paramref name="equivalenceKey"/>, when several are offered.
    /// </summary>
    /// <remarks>
    /// Selecting by key rather than by position is the point: a diagnostic offering a choice must not
    /// have that choice pinned to the order the provider happened to register them in, which is exactly
    /// the ranking §12.1 forbids.
    /// </remarks>
    internal static Task<string> ApplyAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string source,
        string equivalenceKey) =>
        ApplyCoreAsync(analyzer, provider, source, equivalenceKey);

    /// <summary>Asserts that the fix offers nothing for the diagnostic it reported.</summary>
    internal static async Task OffersNothingAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string source)
    {
        (Document document, ImmutableArray<Diagnostic> reported) = await AnalyseAsync(analyzer, source)
            .ConfigureAwait(false);

        // The diagnostic must still have been reported: a fix offering nothing because nothing was
        // found would pass this vacuously, and would be the opposite of what it claims to show.
        Assert.NotEmpty(reported);

        Assert.Empty(await OfferedAsync(provider, document, reported).ConfigureAwait(false));
    }

    private static async Task<string> ApplyCoreAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string source,
        string? equivalenceKey)
    {
        (Document document, ImmutableArray<Diagnostic> reported) = await AnalyseAsync(analyzer, source)
            .ConfigureAwait(false);

        Assert.NotEmpty(reported);

        List<CodeAction> actions = await OfferedAsync(provider, document, reported).ConfigureAwait(false);

        CodeAction chosen;

        if (equivalenceKey is null)
        {
            Assert.Single(actions);

            chosen = actions[0];
        }
        else
        {
            chosen = Assert.Single(actions, action => action.EquivalenceKey == equivalenceKey);
        }

        ImmutableArray<CodeActionOperation> operations = await chosen
            .GetOperationsAsync(CancellationToken.None)
            .ConfigureAwait(false);

        Solution changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        Document fixedDocument = changed.GetDocument(document.Id)!;

        SourceText text = await fixedDocument.GetTextAsync().ConfigureAwait(false);

        // Self-check: the rewritten document must still compile. A fix that produced a syntactically
        // valid but unbindable reference — the wrong name, or the right name with no using — would
        // otherwise be recorded as the expected output of a passing test.
        Compilation compilation = (await fixedDocument.Project.GetCompilationAsync().ConfigureAwait(false))!;

        ImmutableArray<Diagnostic> errors = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        Assert.True(
            errors.IsEmpty,
            "the fixed source must compile; it reported: " + string.Join("; ", errors.Select(d => d.ToString()))
            + Environment.NewLine + text);

        return text.ToString();
    }

    /// <summary>The equivalence keys of every action the provider offers, in order.</summary>
    internal static async Task<ImmutableArray<string?>> EquivalenceKeysAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string source)
    {
        (Document document, ImmutableArray<Diagnostic> reported) = await AnalyseAsync(analyzer, source)
            .ConfigureAwait(false);

        List<CodeAction> actions = await OfferedAsync(provider, document, reported).ConfigureAwait(false);

        return actions.Select(action => action.EquivalenceKey).ToImmutableArray();
    }

    private static async Task<List<CodeAction>> OfferedAsync(
        CodeFixProvider provider,
        Document document,
        ImmutableArray<Diagnostic> reported)
    {
        List<CodeAction> actions = new();

        foreach (Diagnostic diagnostic in reported.Where(d => provider.FixableDiagnosticIds.Contains(d.Id)))
        {
            CodeFixContext context = new(
                document,
                diagnostic,
                (action, _) => actions.Add(action),
                CancellationToken.None);

            await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
        }

        return actions;
    }

    private static async Task<(Document Document, ImmutableArray<Diagnostic> Reported)> AnalyseAsync(
        DiagnosticAnalyzer analyzer,
        string source)
    {
        AdhocWorkspace workspace = new();

        Project project = workspace
            .AddProject("Snippet", LanguageNames.CSharp)
            .WithCompilationOptions(new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(AnalyzerHarness.PlatformReferences);

        Document document = project.AddDocument("Suppressions.cs", source);

        Compilation compilation = (await document.Project.GetCompilationAsync().ConfigureAwait(false))!;

        ImmutableArray<Diagnostic> compileErrors = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        Assert.True(
            compileErrors.IsEmpty,
            "the snippet must compile; it reported: " + string.Join("; ", compileErrors.Select(d => d.ToString())));

        ImmutableArray<Diagnostic> reported = await compilation
            .WithAnalyzers(ImmutableArray.Create(analyzer))
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);

        Diagnostic? crash = reported.FirstOrDefault(diagnostic => diagnostic.Id == "AD0001");
        Assert.True(crash is null, "the analyzer threw: " + crash);

        return (document, reported);
    }
}
