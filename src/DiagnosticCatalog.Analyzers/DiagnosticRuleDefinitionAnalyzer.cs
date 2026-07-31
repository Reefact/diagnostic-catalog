using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Checks that a type declared as a diagnostic rule satisfies the structural contract of §8:
/// DCAT0002, DCAT0003 and DCAT0004.
/// </summary>
/// <remarks>
/// Split from the use-site analyzer for a reason that is mechanical rather than stylistic (§18):
/// ConfigureGeneratedCodeAnalysis is per-ANALYZER, not per-diagnostic, and the two groups need opposite
/// settings. Definition diagnostics must run on generated code — the catalogues this repository ships
/// are generated, and they are precisely what this checks — while use-site diagnostics must not, or
/// every generated file drowns in them.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiagnosticRuleDefinitionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            Descriptors.InvalidRuleType,
            Descriptors.InvalidRuleId,
            Descriptors.InvalidRuleCategory);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Analyze, not None: a generated catalogue is the main thing this has to check. Getting this
        // flag backwards costs nothing visible — the analyzer simply goes quiet on exactly the files it
        // exists for.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);

        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol type) { return; }
        if (!RuleMarker.IsRule(type)) { return; }

        RuleContractResult result = RuleContract.Check(type);
        if (result.IsSatisfied) { return; }

        // Every applicable violation is reported, rather than the first. A type that is neither static
        // nor carries an Id has two separate things to fix, and hiding one behind the other means
        // fixing the first only reveals the second on the next build.
        Report(context, type, RuleContractViolations.NotAStaticNonGenericClass, Descriptors.InvalidRuleType, result);
        Report(context, type, RuleContractViolations.InvalidId, Descriptors.InvalidRuleId, result);
        Report(context, type, RuleContractViolations.InvalidCategory, Descriptors.InvalidRuleCategory, result);
    }

    private static void Report(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        RuleContractViolations violation,
        DiagnosticDescriptor descriptor,
        RuleContractResult result)
    {
        if ((result.Violations & violation) == 0) { return; }

        foreach (Location location in type.Locations)
        {
            // A partial type has one location per part, and only the ones in source can carry a
            // diagnostic. In practice a rule is declared once, but a partial declaration is legal and
            // reporting on a metadata location throws.
            if (location.IsInSource)
            {
                context.ReportDiagnostic(Diagnostic.Create(descriptor, location, type.Name));
            }
        }
    }
}
