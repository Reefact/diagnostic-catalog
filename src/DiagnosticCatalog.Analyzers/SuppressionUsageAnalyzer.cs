using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Checks the suppressions that reference diagnostic rules. Currently DCAT0001.
/// </summary>
/// <remarks>
/// Separate from the definition analyzer because ConfigureGeneratedCodeAnalysis is per-ANALYZER and the
/// two groups need opposite settings (§18). Use-site diagnostics must NOT run on generated code: a
/// generated file's suppressions are not the author's to fix, and reporting them floods every one.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SuppressionUsageAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Descriptors.MembersFromDifferentRules);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // None, the opposite of the definition analyzer's flag. Getting this one backwards is loud
        // rather than silent — every generated file lights up — but it is still wrong.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        // A syntax node action, because AttributeData folds the constants away and takes the field
        // symbol with them. See SuppressionAttribute for why that closes off the whole of §10.
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        AttributeSyntax attribute = (AttributeSyntax)context.Node;

        if (SuppressionAttribute.Identify(attribute, context.SemanticModel) is null) { return; }

        if (SuppressionAttribute.ReadPair(attribute, context.SemanticModel) is not { } pair) { return; }

        SuppressionArgument category = pair.Category;
        SuppressionArgument checkId = pair.CheckId;

        // Both halves must be rule members for the question to arise at all. A literal on either side
        // is DCAT0006 or DCAT0007's business, and an unresolved argument is nobody's.
        if (category.Kind != SuppressionArgumentKind.RuleMember) { return; }
        if (checkId.Kind != SuppressionArgumentKind.RuleMember) { return; }

        // The comparison is between the DECLARING TYPES, never between the values. Two rules that
        // happen to share a category still produce a diagnostic: the pairing is a copy-paste error
        // whose consequence is deferred to the day one of them is recategorised, at which point the
        // suppression silently carries the wrong category.
        if (SymbolEqualityComparer.Default.Equals(category.RuleType, checkId.RuleType)) { return; }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.MembersFromDifferentRules,
            attribute.GetLocation(),
            category.RuleType!.Name,
            checkId.RuleType!.Name));
    }
}
