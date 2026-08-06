using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// Compiles a snippet with an analyzer and returns what it reported.
/// </summary>
/// <remarks>
/// Hand-written rather than Microsoft.CodeAnalysis.*.Testing, following the precedent in the sibling
/// first-class-errors repository: those packages would be three new central pins for a harness this
/// small, and CLAUDE.md asks for a clear reason before a dependency.
///
/// The self-checks below are the point of it. An analyzer test suite fails in one characteristic way:
/// the verifier never actually runs the analyzer — it was not registered, its SupportedDiagnostics is
/// empty, or it threw and Roslyn swallowed the throw as AD0001 — and every "no diagnostics expected"
/// test then passes forever, growing more reassuring with each one added. All three are asserted on
/// every single run, so the harness cannot report silence it did not verify.
/// </remarks>
internal static class AnalyzerHarness
{
    // The test host's own assemblies. Using them rather than a reference-assemblies package keeps the
    // dependency list short, and gives the snippets the foundation this project already references —
    // which is how [DiagnosticRule] resolves from real metadata in the fixtures that want it.
    private static readonly ImmutableArray<MetadataReference> References = ImmutableArray.CreateRange(
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => path.Length > 0)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)));

    /// <summary>The same references, for the code-fix harness's workspace.</summary>
    internal static ImmutableArray<MetadataReference> PlatformReferences => References;

    /// <summary>Runs <paramref name="analyzer"/> over <paramref name="source"/>.</summary>
    /// <param name="referencedSource">
    /// Optional source compiled into a separate assembly and referenced by the snippet. The analyzer
    /// then meets its rules as METADATA symbols with no syntax at all — the §21.2 case, and the only way
    /// to exercise the paths that exist for referenced catalogues.
    /// </param>
    /// <param name="referenceMayUseFoundation">
    /// When false, the referenced assembly is compiled without DiagnosticCatalog.dll, so it can only
    /// carry rules by declaring the marker itself (§7.2). That is what makes the second clause of the
    /// §13.1 pre-filter observable: without it, such an assembly is skipped and its rules vanish.
    /// </param>
    /// <param name="consumerMayUseFoundation">
    /// When false, the SNIPPET is compiled without DiagnosticCatalog.dll while the referenced assembly
    /// keeps it. That is what a consumer sees when a catalogue hides the foundation behind
    /// PrivateAssets="all": the marker is unresolvable in their compilation, although the catalogue's
    /// metadata still carries it.
    /// </param>
    /// <param name="buildProperties">
    /// MSBuild properties the build would have published through CompilerVisibleProperty, keyed WITHOUT
    /// the build_property. prefix. Null means a build that said nothing, which is what every other test
    /// here compiles under and what DCAT0015 must stay silent for.
    /// </param>
    internal static async Task<ImmutableArray<Diagnostic>> RunAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        string? referencedSource = null,
        bool referenceMayUseFoundation = true,
        bool consumerMayUseFoundation = true,
        IReadOnlyDictionary<string, string>? buildProperties = null)
    {
        // Self-check one: an analyzer declaring nothing can report nothing, and would make every
        // expectation below vacuous.
        Assert.NotEmpty(analyzer.SupportedDiagnostics);

        ImmutableArray<MetadataReference> available = consumerMayUseFoundation
            ? References
            : WithoutFoundation(References);

        ImmutableArray<MetadataReference> references = referencedSource is null
            ? available
            : available.Add(CompileToReference(referencedSource, referenceMayUseFoundation));

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Snippet",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Self-check two: a snippet that does not compile makes any result meaningless — the symbols
        // the analyzer inspects would be error types. Reported here rather than as a puzzling absence
        // of diagnostics later.
        ImmutableArray<Diagnostic> compileErrors = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        Assert.True(
            compileErrors.IsEmpty,
            "the snippet must compile; it reported: " + string.Join("; ", compileErrors.Select(d => d.ToString())));

        AnalyzerOptions options = new(
            ImmutableArray<AdditionalText>.Empty,
            new BuildPropertyProvider(buildProperties ?? new Dictionary<string, string>()));

        ImmutableArray<Diagnostic> reported = await compilation
            .WithAnalyzers(ImmutableArray.Create(analyzer), options)
            .GetAnalyzerDiagnosticsAsync()
            .ConfigureAwait(false);

        // Self-check three: AD0001 is how Roslyn reports that an analyzer threw. It arrives as a
        // diagnostic like any other, so without this a crashing analyzer reads as "reported nothing
        // unexpected" and the suite stays green.
        Diagnostic? crash = reported.FirstOrDefault(diagnostic => diagnostic.Id == "AD0001");
        Assert.True(crash is null, "the analyzer threw: " + crash);

        return reported;
    }

    /// <summary>Asserts that running <paramref name="analyzer"/> reports exactly <paramref name="expectedIds"/>.</summary>
    internal static Task ReportsAsync(DiagnosticAnalyzer analyzer, string source, params string[] expectedIds) =>
        AssertReportsAsync(analyzer, source, null, true, expectedIds);

    /// <summary>Asserts that <paramref name="analyzer"/> reports nothing at all.</summary>
    internal static Task ReportsNothingAsync(DiagnosticAnalyzer analyzer, string source) =>
        ReportsAsync(analyzer, source);

    /// <summary>As <see cref="ReportsAsync"/>, with the rules living in a referenced assembly.</summary>
    internal static Task ReportsAgainstReferenceAsync(
        DiagnosticAnalyzer analyzer,
        string referencedSource,
        string source,
        params string[] expectedIds) =>
        AssertReportsAsync(analyzer, source, referencedSource, true, expectedIds);

    /// <summary>
    /// As <see cref="ReportsAgainstReferenceAsync"/>, with the foundation missing from the CONSUMER's
    /// compilation rather than from the catalogue's — the PrivateAssets="all" case.
    /// </summary>
    internal static Task ReportsAgainstReferenceWithoutFoundationAsync(
        DiagnosticAnalyzer analyzer,
        string referencedSource,
        string source,
        params string[] expectedIds) =>
        AssertReportsAsync(analyzer, source, referencedSource, true, false, expectedIds);

    /// <summary>As above, but the referenced assembly cannot use the foundation and must embed the marker.</summary>
    internal static Task ReportsAgainstSelfContainedReferenceAsync(
        DiagnosticAnalyzer analyzer,
        string referencedSource,
        string source,
        params string[] expectedIds) =>
        AssertReportsAsync(analyzer, source, referencedSource, false, expectedIds);

    private static async Task AssertReportsAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        string? referencedSource,
        bool referenceMayUseFoundation,
        string[] expectedIds) =>
        await AssertReportsAsync(analyzer, source, referencedSource, referenceMayUseFoundation, true, expectedIds)
            .ConfigureAwait(false);

    private static async Task AssertReportsAsync(
        DiagnosticAnalyzer analyzer,
        string source,
        string? referencedSource,
        bool referenceMayUseFoundation,
        bool consumerMayUseFoundation,
        string[] expectedIds)
    {
        ImmutableArray<Diagnostic> reported = await RunAsync(
            analyzer,
            source,
            referencedSource,
            referenceMayUseFoundation,
            consumerMayUseFoundation).ConfigureAwait(false);

        IEnumerable<string> actual = reported.Select(diagnostic => diagnostic.Id).OrderBy(id => id, StringComparer.Ordinal);
        IEnumerable<string> expected = expectedIds.OrderBy(id => id, StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    private static ImmutableArray<MetadataReference> WithoutFoundation(
        ImmutableArray<MetadataReference> references) =>
        references
            .Where(reference =>
                !string.Equals(
                    Path.GetFileName(reference.Display),
                    FoundationAssemblyFileName,
                    StringComparison.OrdinalIgnoreCase))
            .ToImmutableArray();

    private static PortableExecutableReference CompileToReference(string source, bool mayUseFoundation)
    {
        IEnumerable<MetadataReference> references = mayUseFoundation
            ? References
            : WithoutFoundation(References);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "ReferencedCatalog",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using MemoryStream stream = new();

        EmitResult result = compilation.Emit(stream);

        // Same reasoning as self-check two: a fixture assembly that failed to build would hand the
        // analyzer error types and turn every expectation below into a study of nothing.
        Assert.True(
            result.Success,
            "the referenced fixture must compile; it reported: "
            + string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private const string FoundationAssemblyFileName = "DiagnosticCatalog.dll";

    /// <summary>
    /// Stands in for what the SDK writes into the generated AnalyzerConfig from
    /// <c>CompilerVisibleProperty</c>. The <c>build_property.</c> prefix is added here rather than in
    /// every test, so a test states the MSBuild property name a reader would recognise.
    /// </summary>
    private sealed class BuildPropertyProvider : AnalyzerConfigOptionsProvider
    {
        private readonly Options _global;

        internal BuildPropertyProvider(IReadOnlyDictionary<string, string> properties)
        {
            Dictionary<string, string> prefixed = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> property in properties)
            {
                prefixed["build_property." + property.Key] = property.Value;
            }

            _global = new Options(prefixed);
        }

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Options.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Options.Empty;

        private sealed class Options : AnalyzerConfigOptions
        {
            internal static readonly Options Empty = new(new Dictionary<string, string>(StringComparer.Ordinal));

            private readonly Dictionary<string, string> _values;

            internal Options(Dictionary<string, string> values) => _values = values;

            public override bool TryGetValue(string key, out string value) =>
                _values.TryGetValue(key, out value!);
        }
    }
}
