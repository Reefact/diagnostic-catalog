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
/// The document scope is the one a developer reaches for first, and the smallest one that reproduces a
/// conflict between two occurrences. It is not the only one offered: <see cref="ApplyAcrossAsync"/>
/// drives the two wider scopes the provider announces, over a solution of several projects. Announcing
/// a scope no test ever built a context for is a promise nothing kept — and the wider scopes are the
/// ones a migration actually invokes, since nobody adopts a catalogue one file at a time.
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

    // --- the wider scopes -----------------------------------------------------------------

    /// <summary>One project of the solution a wider fix-all runs over.</summary>
    /// <param name="Name">The project's name, and the first half of the key its documents come back under.</param>
    /// <param name="Documents">Each document's name and its source.</param>
    internal sealed record ProjectFixture(string Name, IReadOnlyList<(string Name, string Source)> Documents);

    /// <summary>
    /// Applies the fix carrying <paramref name="equivalenceKey"/> across a whole project or solution,
    /// and returns every document in the solution afterwards, keyed <c>project/document</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fix is invoked from the FIRST project, which is what makes a project scope mean anything: a
    /// second project carrying the same diagnostics must come back untouched, and under a solution scope
    /// the same second project must come back fixed. One harness answers both, so the two cannot be
    /// tested against different solutions and quietly stop being comparable.
    /// </para>
    /// <para>
    /// Every document is returned, fixed or not, because what a migration must not do is drop an
    /// occurrence — and an occurrence dropped is a document that came back exactly as it was. A harness
    /// returning only what it changed could not tell that from a document there was nothing to do to.
    /// </para>
    /// </remarks>
    internal static async Task<IReadOnlyDictionary<string, string>> ApplyAcrossAsync(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        FixAllScope scope,
        IReadOnlyList<ProjectFixture> projects,
        string equivalenceKey)
    {
        Assert.True(scope is FixAllScope.Project or FixAllScope.Solution,
                    "ApplyAcrossAsync drives the wider scopes; use ApplyAsync for a document");

        AdhocWorkspace workspace = new();
        Solution solution = workspace.CurrentSolution;
        List<ProjectId> order = [];

        foreach (ProjectFixture fixture in projects)
        {
            ProjectId projectId = ProjectId.CreateNewId(fixture.Name);
            order.Add(projectId);

            solution = solution
                .AddProject(projectId, fixture.Name, fixture.Name, LanguageNames.CSharp)
                .WithProjectMetadataReferences(projectId, AnalyzerHarness.PlatformReferences)
                .WithProjectCompilationOptions(
                    projectId,
                    new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            foreach ((string name, string source) in fixture.Documents)
            {
                solution = solution.AddDocument(DocumentId.CreateNewId(projectId, name), name, source);
            }
        }

        ImmutableDictionary<DocumentId, ImmutableArray<Diagnostic>> reported =
            await AnalyseEveryDocumentAsync(solution, analyzer, provider).ConfigureAwait(false);

        // Several occurrences, in several documents. A solution whose diagnostics all sat in one file
        // would pass a fix-all that never looked past the first document it was given.
        Assert.True(
            reported.Values.Sum(d => d.Length) > 1 && reported.Count(pair => !pair.Value.IsEmpty) > 1,
            "a wider fix-all is only meaningful over several documents; this solution reported "
            + string.Join(", ", reported.Select(pair => pair.Key + "=" + pair.Value.Length)));

        FixAllProvider? fixAll = provider.GetFixAllProvider();

        Assert.NotNull(fixAll);
        Assert.Contains(scope, fixAll.GetSupportedFixAllScopes());

        FixAllContext context = new(
            solution.GetProject(order[0])!,
            provider,
            scope,
            equivalenceKey,
            provider.FixableDiagnosticIds,
            new ReportedPerDocument(reported),
            CancellationToken.None);

        CodeAction? action = await fixAll.GetFixAsync(context).ConfigureAwait(false);

        Assert.True(action is not null, "fix-all offered no action for '" + equivalenceKey + "'");

        return await EveryDocumentAsync(action, solution).ConfigureAwait(false);
    }

    /// <summary>Runs the analyzer over every C# project and keeps what it reported, per document.</summary>
    private static async Task<ImmutableDictionary<DocumentId, ImmutableArray<Diagnostic>>>
        AnalyseEveryDocumentAsync(Solution solution, DiagnosticAnalyzer analyzer, CodeFixProvider provider)
    {
        ImmutableDictionary<DocumentId, ImmutableArray<Diagnostic>>.Builder reported =
            ImmutableDictionary.CreateBuilder<DocumentId, ImmutableArray<Diagnostic>>();

        foreach (Project project in solution.Projects)
        {
            Compilation compilation = (await project.GetCompilationAsync().ConfigureAwait(false))!;

            await AssertCompilesAsync(project, compilation).ConfigureAwait(false);

            ImmutableArray<Diagnostic> diagnostics = await compilation
                .WithAnalyzers(ImmutableArray.Create(analyzer))
                .GetAnalyzerDiagnosticsAsync()
                .ConfigureAwait(false);

            Diagnostic? crash = diagnostics.FirstOrDefault(diagnostic => diagnostic.Id == "AD0001");
            Assert.True(crash is null, "the analyzer threw: " + crash);

            foreach (Document document in project.Documents)
            {
                SyntaxTree? tree = await document.GetSyntaxTreeAsync().ConfigureAwait(false);

                reported[document.Id] =
                [
                    .. diagnostics.Where(diagnostic => provider.FixableDiagnosticIds.Contains(diagnostic.Id)
                                                       && diagnostic.Location.SourceTree == tree),
                ];
            }
        }

        return reported.ToImmutable();
    }

    /// <summary>Every document of the changed solution, keyed <c>project/document</c>.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> EveryDocumentAsync(
        CodeAction action, Solution before)
    {
        ImmutableArray<CodeActionOperation> operations = await action
            .GetOperationsAsync(CancellationToken.None)
            .ConfigureAwait(false);

        Solution changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        Dictionary<string, string> texts = new(StringComparer.Ordinal);

        foreach (Project project in changed.Projects)
        {
            // The same self-check the document harness makes, and it earns its place here for a wider
            // reason: a fix-all that rewrote a reference in one project without importing its namespace
            // would leave source that binds to nothing, in a file nobody opened.
            await AssertCompilesAsync(project, (await project.GetCompilationAsync().ConfigureAwait(false))!)
                .ConfigureAwait(false);

            foreach (Document document in project.Documents)
            {
                SourceText text = await document.GetTextAsync().ConfigureAwait(false);
                texts[project.Name + "/" + document.Name] = text.ToString();
            }
        }

        // Nothing may VANISH either: a solution that came back with fewer documents than it went in
        // with would satisfy every assertion made about the ones that remain.
        Assert.Equal(before.Projects.Sum(project => project.Documents.Count()), texts.Count);

        return texts;
    }

    private static async Task AssertCompilesAsync(Project project, Compilation compilation)
    {
        ImmutableArray<Diagnostic> errors =
            [.. compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

        if (errors.IsEmpty) { return; }

        List<string> sources = [];
        foreach (Document document in project.Documents)
        {
            sources.Add(document.Name + ":" + Environment.NewLine
                        + (await document.GetTextAsync().ConfigureAwait(false)));
        }

        Assert.Fail(project.Name + " must compile; it reported: "
                    + string.Join("; ", errors.Select(diagnostic => diagnostic.ToString()))
                    + Environment.NewLine + string.Join(Environment.NewLine, sources));
    }

    /// <summary>Hands the engine each document's own diagnostics.</summary>
    /// <remarks>
    /// Unlike <see cref="Reported"/>, which answers one snippet's set to every question asked. Over
    /// several documents that shortcut is not a simplification but a different behaviour: every
    /// document would be handed every other document's diagnostics, and a fix that never looked past
    /// the first would still appear to work.
    /// </remarks>
    private sealed class ReportedPerDocument : FixAllContext.DiagnosticProvider
    {
        private readonly ImmutableDictionary<DocumentId, ImmutableArray<Diagnostic>> _byDocument;

        internal ReportedPerDocument(ImmutableDictionary<DocumentId, ImmutableArray<Diagnostic>> byDocument)
        {
            _byDocument = byDocument;
        }

        public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(
            Document document, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(
                _byDocument.TryGetValue(document.Id, out ImmutableArray<Diagnostic> found) ? found : []);

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(
            Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>(
                [.. project.Documents.SelectMany(
                    document => _byDocument.TryGetValue(document.Id, out ImmutableArray<Diagnostic> found)
                        ? found
                        : [])]);

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>([]);
    }
}
