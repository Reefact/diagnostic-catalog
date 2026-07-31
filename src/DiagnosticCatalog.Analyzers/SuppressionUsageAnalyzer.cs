using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Checks the suppressions that reference diagnostic rules. Currently DCAT0001 and DCAT0009.
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
        ImmutableArray.Create(
            Descriptors.MembersFromDifferentRules,
            Descriptors.NonIlUnconditionalSuppression);

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

        if (SuppressionAttribute.Identify(attribute, context.SemanticModel) is not { } attributeName) { return; }

        if (SuppressionAttribute.ReadPair(attribute, context.SemanticModel) is not { } pair) { return; }

        // Independent faults, both reported. Fixing the pairing must not hide the fact that the whole
        // attribute is discarded, and vice versa.
        ReportIncoherentPair(context, attribute, pair.Category, pair.CheckId);
        ReportNonIlIdentifier(context, attribute, attributeName, pair.CheckId);
    }

    private static void ReportIncoherentPair(
        SyntaxNodeAnalysisContext context,
        AttributeSyntax attribute,
        SuppressionArgument category,
        SuppressionArgument checkId)
    {
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

    private static void ReportNonIlIdentifier(
        SyntaxNodeAnalysisContext context,
        AttributeSyntax attribute,
        string attributeName,
        SuppressionArgument checkId)
    {
        // The constraint belongs to ILLink's decoder, so it applies to that attribute alone.
        if (attributeName != SuppressionAttribute.UnconditionalSuppressMessageMetadataName) { return; }

        // A rule, not any constant: §11.9 and §21.2 both name one, and firing on literals would flood
        // every project that hand-writes trim suppressions without ever adopting a catalogue.
        if (checkId.Kind != SuppressionArgumentKind.RuleMember) { return; }

        if (IlWarningId.IsHonoured(checkId.Value)) { return; }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.NonIlUnconditionalSuppression,
            attribute.GetLocation(),
            checkId.Value));
    }
}
