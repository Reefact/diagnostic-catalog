using System;
using System.Collections.Immutable;
using System.Globalization;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Checks the suppressions that reference diagnostic rules: DCAT0001, DCAT0006, DCAT0007 and DCAT0009.
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
            Descriptors.ReplaceableStringLiterals,
            Descriptors.MixedReferenceAndLiteral,
            Descriptors.NonIlUnconditionalSuppression);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // None, the opposite of the definition analyzer's flag. Getting this one backwards is loud
        // rather than silent — every generated file lights up — but it is still wrong.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(start =>
        {
            // Lazy, and §13.1 requires it. Building the index sweeps the metadata of every referenced
            // assembly that could hold a rule; DCAT0001, DCAT0007 and DCAT0009 resolve everything from
            // the attribute itself, so a project whose suppressions are already catalogue references —
            // or half migrated — never pays for the sweep at all. Lazy<T>'s default mode is thread-safe,
            // which matters under the concurrent execution enabled above.
            Lazy<RuleIndex> index = new(() => RuleIndex.Build(start.Compilation));

            // A syntax node action, because AttributeData folds the constants away and takes the field
            // symbol with them. See SuppressionAttribute for why that closes off the whole of §10.
            start.RegisterSyntaxNodeAction(node => Analyze(node, index), SyntaxKind.Attribute);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, Lazy<RuleIndex> index)
    {
        AttributeSyntax attribute = (AttributeSyntax)context.Node;

        if (SuppressionAttribute.Identify(attribute, context.SemanticModel) is not { } attributeName) { return; }

        if (SuppressionAttribute.ReadPair(attribute, context.SemanticModel) is not { } pair) { return; }

        // Independent faults, all reported. Fixing the pairing must not hide the fact that the whole
        // attribute is discarded, and vice versa.
        //
        // The first three partition the pair by what its two halves are, so at most one can fire:
        // two references is DCAT0001's, two values is DCAT0006's, one of each is DCAT0007's. Only
        // DCAT0009 can accompany another, because it asks a different question entirely.
        ReportIncoherentPair(context, attribute, pair.Category, pair.CheckId);
        ReportReplaceableLiterals(context, attribute, pair.Category, pair.CheckId, index);
        ReportMixedPair(context, attribute, pair.Category, pair.CheckId);
        ReportNonIlIdentifier(context, attribute, attributeName, pair.CheckId);
    }

    private static void ReportMixedPair(
        SyntaxNodeAnalysisContext context,
        AttributeSyntax attribute,
        SuppressionArgument category,
        SuppressionArgument checkId)
    {
        bool categoryIsReference = category.Kind == SuppressionArgumentKind.RuleMember;
        bool checkIdIsReference = checkId.Kind == SuppressionArgumentKind.RuleMember;

        // Exactly one side migrated. Neither, or both, belongs to a different diagnostic.
        if (categoryIsReference == checkIdIsReference) { return; }

        SuppressionArgument reference = categoryIsReference ? category : checkId;
        SuppressionArgument literal = categoryIsReference ? checkId : category;

        // An unresolved argument is nobody's business: nothing can be said about what it meant.
        if (literal.Kind != SuppressionArgumentKind.ConstantValue) { return; }

        INamedTypeSymbol ruleType = reference.RuleType!;

        // No index lookup anywhere here — the rule is named by the migrated argument, which is exactly
        // what §11.7 means by the only fully deterministic case. A malformed rule is left to the
        // definition diagnostics rather than half-reported by this one.
        RuleContractResult contract = RuleContract.Check(ruleType);

        if (!contract.IsSatisfied) { return; }

        // The literal stands in for the member the OTHER side did not migrate.
        int slot = categoryIsReference
            ? SuppressionArgumentOrder.CheckIdSlot
            : SuppressionArgumentOrder.CategorySlot;

        string declared = categoryIsReference ? contract.Id! : contract.Category!;

        // Normalised on the identifier side only, so "S1144:Unused private members should be removed"
        // is recognised as naming S1144 — and replaced by the reference, dropping the suffix (§11.6).
        string written = categoryIsReference ? CheckId.Normalise(literal.Value!) : literal.Value!;

        bool agrees = string.Equals(declared, written, StringComparison.Ordinal);

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.MixedReferenceAndLiteral,
            attribute.GetLocation(),
            // A fix only when completing it cannot change meaning. When the literal names something
            // else the suppression is reported and left alone: rewriting it would silence a different
            // diagnostic than the one silenced today, which no migration should decide by itself.
            agrees ? FixProperties.ForCompletion(ruleType, slot) : ImmutableDictionary<string, string?>.Empty,
            ruleType.Name,
            literal.Value,
            agrees
                ? "complete it from that rule"
                : "that value names something else, so completing it would change what is suppressed"));
    }

    private static void ReportReplaceableLiterals(
        SyntaxNodeAnalysisContext context,
        AttributeSyntax attribute,
        SuppressionArgument category,
        SuppressionArgument checkId,
        Lazy<RuleIndex> index)
    {
        // Both halves must be plain values. One reference and one literal is DCAT0007's business, and a
        // pair that already references the catalogue has nothing to migrate.
        if (category.Kind != SuppressionArgumentKind.ConstantValue) { return; }
        if (checkId.Kind != SuppressionArgumentKind.ConstantValue) { return; }

        // Mandatory before the lookup: the suffixed form is what Visual Studio generates, so skipping
        // this finds nothing in the codebases most worth migrating (§11.6).
        string normalised = CheckId.Normalise(checkId.Value!);

        ImmutableArray<RuleDefinition> matches = index.Value.Find(category.Value!, normalised);

        // Nothing known matches: the literals may name a rule from a vendor with no catalogue, or
        // nothing at all. Reporting here would fire on every codebase that has not adopted one, and
        // saying which identifiers exist at all is DCAT0008's opt-in job.
        if (matches.IsEmpty) { return; }

        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.ReplaceableStringLiterals,
            attribute.GetLocation(),
            FixProperties.ForMatches(matches),
            category.Value,
            checkId.Value,
            Describe(matches)));
    }

    /// <summary>Names the rule when it is unambiguous, and says so when it is not.</summary>
    /// <remarks>
    /// Several rules sharing one <c>(Category, Id)</c> pair is legitimate — two catalogues may both
    /// describe the same vendor rule — and §11.6 gives that case a diagnostic without an automatic fix.
    /// </remarks>
    private static string Describe(ImmutableArray<RuleDefinition> matches) =>
        matches.Length == 1
            ? "'" + matches[0].RuleType.ToDisplayString() + "'"
            : "one of the " + matches.Length.ToString(CultureInfo.InvariantCulture)
              + " matching diagnostic rules";

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
            // Both corrections, always. §12.1 forbids the fix from guessing which rule was intended,
            // and sending one of them would be exactly that guess made a step earlier.
            FixProperties.ForIncoherentPair(category.RuleType!, checkId.RuleType!),
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
