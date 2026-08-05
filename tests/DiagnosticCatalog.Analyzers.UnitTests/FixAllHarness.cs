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
/// Runs <i>Fix all occurrences</i> over a document and returns the resulting source.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="CodeFixHarness"/> because it exercises a different object: that one applies
/// ONE registered <see cref="CodeAction"/>, this one drives the provider's
/// <see cref="CodeFixProvider.GetFixAllProvider"/> through a <see cref="FixAllContext"/>. Every fix here
/// passed the single-occurrence tests while fix-all silently dropped occurrences, which is exactly the
/// gap one harness could not see and the other could.
/// </para>
/// <para>
/// The scope is the document. It is the one a developer reaches for first, the one the providers' own
/// remarks promise ("so <i>Fix all occurrences</i> applies one consistent choice across a document,
/// project or solution"), and the smallest one that reproduces a conflict between two occurrences.
/// </para>
/// </remarks>
internal static class FixAllHarness
{
    /// <summary>Applies the fix carrying <paramref name="equivalenceKey"/> to every occurrence.</summary>
    /// <param name="lastOccurrenceFirst">
    /// Hands the engine the occurrences sorted by DESCENDING position — the last one in the document
    /// first. Nothing promises the order they arrive in: the analyzer reports them as it finds them,
    /// and these run concurrently, so the order varies between runs of the same input. Measured, it
    /// did: eight runs of one file produced its imports in two different orders.
    /// <para>
    /// Sorted rather than reversed, and that is what makes a test out of it. Reversing an order that
    /// is itself arbitrary is still arbitrary — a test written that way failed twice in eight runs
    /// against the unfixed code and passed the other six. Descending is a known order, so a fix that
    /// honours the document fails it every time and one that does not, never.
    /// </para>
    /// </param>
    internal static async Task<string> ApplyAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        string source,
        string equivalenceKey,
        bool lastOccurrenceFirst = false)
    {
        (Document document, ImmutableArray<Diagnostic> reported) =
            await AnalyseAsync(analyzer, provider, source).ConfigureAwait(false);

        // Fixing one occurrence proves nothing about fixing all of them, so a snippet that raised a
        // single diagnostic would pass this vacuously.
        Assert.True(
            reported.Length > 1,
            "fix-all is only meaningful over several occurrences; this snippet raised " + reported.Length);

        FixAllProvider? fixAll = provider.GetFixAllProvider();

        Assert.NotNull(fixAll);

        FixAllContext context = new(
            document,
            provider,
            FixAllScope.Document,
            equivalenceKey,
            provider.FixableDiagnosticIds,
            new Reported(lastOccurrenceFirst
                ? [.. reported.OrderByDescending(d => d.Location.SourceSpan.Start)]
                : reported),
            CancellationToken.None);

        CodeAction? action = await fixAll.GetFixAsync(context).ConfigureAwait(false);

        Assert.True(action is not null, "fix-all offered no action for '" + equivalenceKey + "'");

        return await AppliedAsync(action, document).ConfigureAwait(false);
    }

    private static async Task<string> AppliedAsync(CodeAction action, Document document)
    {
        ImmutableArray<CodeActionOperation> operations = await action
            .GetOperationsAsync(CancellationToken.None)
            .ConfigureAwait(false);

        Solution changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        Document fixedDocument = changed.GetDocument(document.Id)!;

        SourceText text = await fixedDocument.GetTextAsync().ConfigureAwait(false);

        // The same self-check CodeFixHarness makes, and it earns its place here twice over: a fix-all
        // that rewrote a reference without importing its namespace would leave source that binds to
        // nothing, and a naive comparison would record that as the expected output.
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

    private static async Task<(Document Document, ImmutableArray<Diagnostic> Reported)> AnalyseAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
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

        return (document, reported.Where(d => provider.FixableDiagnosticIds.Contains(d.Id)).ToImmutableArray());
    }

    /// <summary>Hands the fix-all engine the diagnostics the harness already ran the analyzer for.</summary>
    /// <remarks>
    /// The engine asks for them per document and per project; a snippet is one document in one project,
    /// so both answers are the same set. Project-level diagnostics — those with no document — are none.
    /// </remarks>
    private sealed class Reported : FixAllContext.DiagnosticProvider
    {
        private readonly ImmutableArray<Diagnostic> _diagnostics;

        internal Reported(ImmutableArray<Diagnostic> diagnostics)
        {
            _diagnostics = diagnostics;
        }

        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(_diagnostics);

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
            Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(_diagnostics);

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>([]);
    }
}
