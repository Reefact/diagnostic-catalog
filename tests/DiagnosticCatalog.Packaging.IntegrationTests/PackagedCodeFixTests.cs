using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using Xunit;

namespace DiagnosticCatalog.Packaging.IntegrationTests;

/// <summary>
/// The code fix, loaded out of the assembly a real restore produced, actually corrects
/// <c>DCAT0006</c> — one occurrence and all of them, at each scope it announces.
/// </summary>
/// <remarks>
/// <para>
/// This is the assertion the packaging checks could not make. <c>@(Analyzer)</c> holding
/// <c>DiagnosticCatalog.CodeFixes.dll</c> says a file was passed to a compiler; the file being in the
/// <c>.nupkg</c> says even less. Neither says the assembly LOADS in a host that carries a different
/// Roslyn from the one it was compiled against, that its provider resolves the diagnostic, that the
/// source it writes compiles, or that <i>Fix all occurrences</i> reaches the second document of the
/// second project. Every one of those is a way the package can be broken while every other test in
/// this repository stays green, because every other test references the projects.
/// </para>
/// <para>
/// Nothing here is constructed by name. The analyzer and the provider are found by asking the restored
/// assemblies which types they carry and which diagnostics those types declare — the same question a
/// compiler host asks. A test naming <c>UseCatalogReferenceCodeFixProvider</c> would keep passing over
/// a package that shipped it and no longer exported it.
/// </para>
/// <para>
/// The catalogue the snippets reference is the RESTORED fixture assembly, taken out of the consumer's
/// own output folder. So the rule types, the marker attribute and the analyzer that reads them all come
/// from packages, and the only thing this repository's build contributes is the Roslyn host — which is
/// exactly what a consumer's compiler contributes.
/// </para>
/// </remarks>
public sealed class PackagedCodeFixTests
{
    private const string ReplaceableStringLiterals = "DCAT0006";

    private static readonly Lazy<RestoredAnalysis> Restored = new(Load, isThreadSafe: true);

    /// <summary>A suppression written in literals that the fixture catalogue can replace.</summary>
    private static string Suppressions(string type, params string[] rules)
    {
        IEnumerable<string> members = rules.Select((rule, index) =>
            $$"""
                  [SuppressMessage("{{Category(rule)}}", "{{rule}}", Justification = "Fixture.")]
                  private static int Member{{index}}() => {{index}};
              """);

        return $$"""
                 using System.Diagnostics.CodeAnalysis;

                 public static class {{type}}
                 {
                 {{string.Join(Environment.NewLine, members)}}
                 }
                 """;
    }

    private static string Category(string rule) =>
        rule == "S1144" ? "Major Code Smell" : "Critical Code Smell";

    [Fact]
    public async Task The_restored_provider_corrects_one_occurrence_and_the_result_compiles()
    {
        RestoredAnalysis analysis = Restored.Value;

        (Document document, ImmutableArray<Diagnostic> reported) =
            await AnalyseAsync(analysis, Suppressions("One", "S1144"));

        Assert.NotEmpty(reported);

        List<CodeAction> actions = [];
        await analysis.Provider.RegisterCodeFixesAsync(new CodeFixContext(
            document,
            reported[0],
            (action, _) => actions.Add(action),
            CancellationToken.None));

        Assert.True(
            actions.Count > 0,
            "the restored provider registered no action for DCAT0006. The assembly loaded and offers "
            + "nothing, which is the failure a package can ship and no in-process test can see.");

        string fixedSource = await AppliedAsync(actions[0], document);

        Assert.Contains($"{PackagedConsumption.Container}.S1144.Id", fixedSource, StringComparison.Ordinal);
        Assert.Contains($"{PackagedConsumption.Container}.S1144.Category", fixedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"S1144\"", fixedSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// <i>Fix all occurrences</i> at each of the three scopes the restored provider announces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The solution has two projects and two documents each, and the fix is always invoked from the
    /// first project. That is what makes a scope mean something: under <c>Project</c> the second
    /// project must come back untouched, and under <c>Solution</c> it must come back fixed. One
    /// solution answers all three, so the scopes cannot quietly stop being comparable.
    /// </para>
    /// <para>
    /// Every document is read back afterwards, fixed or not. What a migration must not do is DROP an
    /// occurrence, and a dropped occurrence is a document that came back exactly as it was — which a
    /// harness returning only what it changed could not tell from a document there was nothing to do to.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(FixAllScope.Document)]
    [InlineData(FixAllScope.Project)]
    [InlineData(FixAllScope.Solution)]
    public async Task The_restored_provider_fixes_all_occurrences_at_every_scope_it_announces(FixAllScope scope)
    {
        RestoredAnalysis analysis = Restored.Value;

        FixAllProvider? fixAll = analysis.Provider.GetFixAllProvider();

        Assert.NotNull(fixAll);
        Assert.Contains(scope, fixAll.GetSupportedFixAllScopes());

        AdhocWorkspace workspace = new();
        Solution solution = workspace.CurrentSolution;

        ProjectId first = ProjectId.CreateNewId("First");
        ProjectId second = ProjectId.CreateNewId("Second");

        DocumentId firstA = DocumentId.CreateNewId(first, "A.cs");
        DocumentId firstB = DocumentId.CreateNewId(first, "B.cs");
        DocumentId secondA = DocumentId.CreateNewId(second, "C.cs");

        solution = WithProject(solution, analysis, first, "First");
        solution = WithProject(solution, analysis, second, "Second");

        solution = solution
            .AddDocument(firstA, "A.cs", Suppressions("A", "S1144", "S2094"))
            .AddDocument(firstB, "B.cs", Suppressions("B", "S1144"))
            .AddDocument(secondA, "C.cs", Suppressions("C", "S2094"));

        ImmutableDictionary<DocumentId, ImmutableArray<Diagnostic>> reported =
            await ReportedPerDocumentAsync(solution, analysis);

        Assert.Equal(3, reported.Count(pair => !pair.Value.IsEmpty));

        string equivalenceKey = await EquivalenceKeyAsync(analysis, solution.GetDocument(firstA)!, reported[firstA][0]);

        // Two constructors, one per starting point: a Document scope is invoked from a document and
        // the wider scopes from a project. Both are the shapes Roslyn's own lightbulb builds.
        FixAllContext context = scope == FixAllScope.Document
            ? new FixAllContext(
                solution.GetDocument(firstA)!,
                analysis.Provider,
                scope,
                equivalenceKey,
                analysis.Provider.FixableDiagnosticIds,
                new ReportedPerDocument(reported),
                CancellationToken.None)
            : new FixAllContext(
                solution.GetProject(first)!,
                analysis.Provider,
                scope,
                equivalenceKey,
                analysis.Provider.FixableDiagnosticIds,
                new ReportedPerDocument(reported),
                CancellationToken.None);

        CodeAction? action = await fixAll.GetFixAsync(context);

        Assert.True(action is not null, $"fix-all offered no action at {scope}");

        Dictionary<string, string> after = await EveryDocumentAsync(action, solution);

        Assert.Equal(3, after.Count);

        // The document the fix was invoked from is always converted.
        Assert.DoesNotContain("\"S1144\"", after["First/A.cs"], StringComparison.Ordinal);

        // The sibling document: reached by Project and Solution, left alone by Document.
        AssertConverted(after["First/B.cs"], "\"S1144\"", scope != FixAllScope.Document, "First/B.cs", scope);

        // The other project: reached by Solution alone.
        AssertConverted(after["Second/C.cs"], "\"S2094\"", scope == FixAllScope.Solution, "Second/C.cs", scope);
    }

    private static void AssertConverted(string source, string literal, bool expected, string name, FixAllScope scope)
    {
        bool converted = !source.Contains(literal, StringComparison.Ordinal);

        Assert.True(
            converted == expected,
            expected
                ? $"{name} still holds {literal} after a {scope} fix-all, so the scope dropped an occurrence."
                : $"{name} was converted by a {scope} fix-all, which must not reach it.");
    }

    // --- driving the restored assemblies -------------------------------------------------------

    private static Solution WithProject(Solution solution, RestoredAnalysis analysis, ProjectId id, string name) =>
        solution
            .AddProject(id, name, name, LanguageNames.CSharp)
            .WithProjectMetadataReferences(id, analysis.References)
            .WithProjectCompilationOptions(id, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    private static async Task<(Document Document, ImmutableArray<Diagnostic> Reported)> AnalyseAsync(
        RestoredAnalysis analysis, string source)
    {
        AdhocWorkspace workspace = new();

        Project project = workspace
            .AddProject("Snippet", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .WithMetadataReferences(analysis.References);

        Document document = project.AddDocument("Suppressions.cs", source);

        Compilation compilation = (await document.Project.GetCompilationAsync())!;

        AssertCompiles(compilation, "the snippet");

        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create(analysis.Analyzer))
            .GetAnalyzerDiagnosticsAsync();

        Diagnostic? crash = diagnostics.FirstOrDefault(diagnostic => diagnostic.Id == "AD0001");
        Assert.True(crash is null, "the restored analyzer threw: " + crash);

        return (document, [.. diagnostics.Where(diagnostic => diagnostic.Id == ReplaceableStringLiterals)]);
    }

    private static async Task<ImmutableDictionary<DocumentId, ImmutableArray<Diagnostic>>>
        ReportedPerDocumentAsync(Solution solution, RestoredAnalysis analysis)
    {
        ImmutableDictionary<DocumentId, ImmutableArray<Diagnostic>>.Builder reported =
            ImmutableDictionary.CreateBuilder<DocumentId, ImmutableArray<Diagnostic>>();

        foreach (Project project in solution.Projects)
        {
            Compilation compilation = (await project.GetCompilationAsync())!;

            AssertCompiles(compilation, project.Name);

            ImmutableArray<Diagnostic> diagnostics = await compilation
                .WithAnalyzers(ImmutableArray.Create(analysis.Analyzer))
                .GetAnalyzerDiagnosticsAsync();

            Diagnostic? crash = diagnostics.FirstOrDefault(diagnostic => diagnostic.Id == "AD0001");
            Assert.True(crash is null, "the restored analyzer threw: " + crash);

            foreach (Document document in project.Documents)
            {
                SyntaxTree? tree = await document.GetSyntaxTreeAsync();

                reported[document.Id] =
                [
                    .. diagnostics.Where(diagnostic =>
                        diagnostic.Id == ReplaceableStringLiterals && diagnostic.Location.SourceTree == tree),
                ];
            }
        }

        return reported.ToImmutable();
    }

    private static async Task<string> EquivalenceKeyAsync(
        RestoredAnalysis analysis, Document document, Diagnostic diagnostic)
    {
        List<CodeAction> actions = [];

        await analysis.Provider.RegisterCodeFixesAsync(new CodeFixContext(
            document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));

        Assert.True(actions.Count > 0, "the restored provider registered no action to take a key from");

        // Read off the action rather than written here. A key spelled in this file would keep passing
        // over a provider that renamed its own, and fix-all would silently apply nothing.
        return actions[0].EquivalenceKey!;
    }

    private static async Task<string> AppliedAsync(CodeAction action, Document document)
    {
        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(CancellationToken.None);

        Solution changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        Document fixedDocument = changed.GetDocument(document.Id)!;

        Compilation compilation = (await fixedDocument.Project.GetCompilationAsync())!;

        AssertCompiles(compilation, "the fixed source");

        SourceText text = await fixedDocument.GetTextAsync();

        return text.ToString();
    }

    private static async Task<Dictionary<string, string>> EveryDocumentAsync(CodeAction action, Solution before)
    {
        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(CancellationToken.None);

        Solution changed = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;

        Dictionary<string, string> texts = new(StringComparer.Ordinal);

        foreach (Project project in changed.Projects)
        {
            AssertCompiles((await project.GetCompilationAsync())!, project.Name);

            foreach (Document document in project.Documents)
            {
                texts[project.Name + "/" + document.Name] = (await document.GetTextAsync()).ToString();
            }
        }

        Assert.Equal(before.Projects.Sum(project => project.Documents.Count()), texts.Count);

        return texts;
    }

    private static void AssertCompiles(Compilation compilation, string what)
    {
        ImmutableArray<Diagnostic> errors =
            [.. compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

        Assert.True(
            errors.IsEmpty,
            $"{what} must compile; it reported: " + string.Join("; ", errors.Select(d => d.ToString())));
    }

    /// <summary>Hands the fix-all engine each document's own diagnostics.</summary>
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
                [.. project.Documents.SelectMany(document =>
                    _byDocument.TryGetValue(document.Id, out ImmutableArray<Diagnostic> found) ? found : [])]);

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(
            Project project, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Diagnostic>>([]);
    }

    /// <summary>The analyzer, the provider and the references, all out of restored packages.</summary>
    private sealed record RestoredAnalysis(
        DiagnosticAnalyzer Analyzer,
        CodeFixProvider Provider,
        ImmutableArray<MetadataReference> References);

    private static RestoredAnalysis Load()
    {
        PackagedConsumption.Consumer consumer = PackagedConsumption.Current.Consumers["Consumer"];

        string analyzerPath = consumer.Single("DiagnosticCatalog.Analyzers.dll");
        string fixesPath = consumer.Single("DiagnosticCatalog.CodeFixes.dll");

        // Loaded by PATH into the default context, which is what a compiler host does with an
        // analyzer it was handed. The dependencies these assemblies name — Microsoft.CodeAnalysis and
        // friends, at $(RoslynFloorVersion) — then resolve against the ones this process already
        // carries, which is the version asymmetry the class comment is about and the reason a load
        // here is worth asserting at all.
        Assembly analyzers = AssemblyLoadContext.Default.LoadFromAssemblyPath(analyzerPath);
        Assembly fixes = AssemblyLoadContext.Default.LoadFromAssemblyPath(fixesPath);

        DiagnosticAnalyzer? analyzer = Exported<DiagnosticAnalyzer>(analyzers)
            .FirstOrDefault(candidate => candidate.SupportedDiagnostics
                .Any(descriptor => descriptor.Id == ReplaceableStringLiterals));

        Assert.True(analyzer is not null, ShipsNothing(analyzerPath, "an analyzer declaring " + ReplaceableStringLiterals));

        CodeFixProvider? provider = Exported<CodeFixProvider>(fixes)
            .FirstOrDefault(candidate => candidate.FixableDiagnosticIds.Contains(ReplaceableStringLiterals));

        Assert.True(provider is not null, ShipsNothing(fixesPath, "a code fix for " + ReplaceableStringLiterals));

        // The attribute assembly sits beside the analyzers inside the same restored package, and the
        // catalogue's own assembly reached the consumer's output folder because a catalogue is
        // constants a consuming application really does carry.
        string packageRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(analyzerPath)!, ".."));
        string attributes = Path.Combine(packageRoot, "lib", "netstandard2.0", "DiagnosticCatalog.dll");

        Assert.True(File.Exists(attributes), $"the restored package carries no lib assembly at {attributes}");

        string catalogue = Path.Combine(consumer.Directory, PackagedConsumption.CatalogueA + ".dll");

        Assert.True(File.Exists(catalogue), $"the consumer's output folder carries no {catalogue}");

        ImmutableArray<MetadataReference> references =
        [
            .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(path => path.Length > 0)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)),
            MetadataReference.CreateFromFile(attributes),
            MetadataReference.CreateFromFile(catalogue),
        ];

        return new RestoredAnalysis(analyzer, provider, references);
    }

    private static IEnumerable<T> Exported<T>(Assembly assembly) where T : class =>
        assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(T).IsAssignableFrom(type))
            .Select(type => Activator.CreateInstance(type) as T)
            .Where(instance => instance is not null)!;

    private static string ShipsNothing(string path, string what) =>
        $"{path} carries no {what}. The package restored, the file reached the compiler, and the "
        + "thing it is there for is not in it.";
}
