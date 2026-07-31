using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

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

    /// <summary>Runs <paramref name="analyzer"/> over <paramref name="source"/>.</summary>
    internal static async Task<ImmutableArray<Diagnostic>> RunAsync(DiagnosticAnalyzer analyzer, string source)
    {
        // Self-check one: an analyzer declaring nothing can report nothing, and would make every
        // expectation below vacuous.
        Assert.NotEmpty(analyzer.SupportedDiagnostics);

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Snippet",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: References,
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

        ImmutableArray<Diagnostic> reported = await compilation
            .WithAnalyzers(ImmutableArray.Create(analyzer))
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
    internal static async Task ReportsAsync(DiagnosticAnalyzer analyzer, string source, params string[] expectedIds)
    {
        ImmutableArray<Diagnostic> reported = await RunAsync(analyzer, source).ConfigureAwait(false);

        IEnumerable<string> actual = reported.Select(diagnostic => diagnostic.Id).OrderBy(id => id, StringComparer.Ordinal);
        IEnumerable<string> expected = expectedIds.OrderBy(id => id, StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    /// <summary>Asserts that <paramref name="analyzer"/> reports nothing at all.</summary>
    internal static Task ReportsNothingAsync(DiagnosticAnalyzer analyzer, string source) =>
        ReportsAsync(analyzer, source);
}
