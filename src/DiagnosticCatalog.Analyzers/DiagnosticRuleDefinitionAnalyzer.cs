using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Checks that a type declared as a diagnostic rule satisfies the structural contract of §8:
/// DCAT0002–DCAT0005 and DCAT0011–DCAT0013.
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
            Descriptors.InvalidRuleCategory,
            Descriptors.UnreferencedRuleCategory,
            Descriptors.RuleTypeNameDiffersFromId,
            Descriptors.IdNotWrittenAsNameOf,
            Descriptors.RuleTypeNameDoesNotSayId);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();

        // Analyze AND ReportDiagnostics, not None: a generated catalogue is the main thing this has to
        // check. The two flags are separate and both are needed — Analyze alone runs the callbacks over
        // generated trees and then discards everything they report, which costs nothing visible and
        // leaves the analyzer quiet on exactly the files it exists for. GeneratedCodeTests holds both
        // halves, here and on the use-site analyzer, whose None is the deliberate opposite.
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);

        // DCAT0011 is the one definition check that cannot be answered from symbols. Whether a category
        // was REACHED through a named constant lives in the initialiser, and IFieldSymbol.ConstantValue
        // has already folded it away — SonarRule.S1144.Category and "Major Code Smell" are the same
        // symbol by then. So it registers separately, on syntax, rather than joining RuleContract:
        // that contract is symbol-only on purpose, because DCAT0010 replays it against metadata symbols
        // that have no syntax at all (§11 preamble).
        //
        // Being syntax-bound also settles the scope for free. DCAT0011 can only ever fire on source,
        // which is what §11 requires of every definition diagnostic anyway.
        context.RegisterSyntaxNodeAction(AnalyzeCategoryInitialiser, SyntaxKind.FieldDeclaration);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol type) { return; }
        if (!RuleMarker.IsRule(type)) { return; }

        RuleContractResult result = RuleContract.Check(type);

        // Every applicable violation is reported, rather than the first. A type that is neither static
        // nor carries an Id has two separate things to fix, and hiding one behind the other means
        // fixing the first only reveals the second on the next build.
        Report(context, type, RuleContractViolations.NotAStaticNonGenericClass, Descriptors.InvalidRuleType, result);
        Report(context, type, RuleContractViolations.InvalidId, Descriptors.InvalidRuleId, result);
        Report(context, type, RuleContractViolations.InvalidCategory, Descriptors.InvalidRuleCategory, result);

        // The naming diagnostics all read what Id HOLDS, so none of them has anything to say until §8.2
        // holds. A type failing that already carries DCAT0003, which is the thing to fix first.
        if (result.Id is null || result.IdField is null) { return; }

        ReportNaming(context, type, result.Id, result.IdField);
    }

    /// <summary>§8.5 — the category must be reached through a marked category constant (DCAT0011).</summary>
    private static void AnalyzeCategoryInitialiser(SyntaxNodeAnalysisContext context)
    {
        FieldDeclarationSyntax declaration = (FieldDeclarationSyntax)context.Node;

        foreach (VariableDeclaratorSyntax declarator in declaration.Declaration.Variables)
        {
            // The member name is the cheap filter, and it runs before any symbol is bound: this action
            // sees every field declaration in the compilation, of which almost none are a rule's.
            if (!string.Equals(declarator.Identifier.ValueText, "Category", StringComparison.Ordinal)) { continue; }

            if (context.SemanticModel.GetDeclaredSymbol(declarator, context.CancellationToken) is not IFieldSymbol field)
            {
                continue;
            }

            if (!RuleMarker.IsRule(field.ContainingType)) { continue; }

            // §8.3 first. A rule with no usable Category is already DCAT0004, and reporting both would
            // name two problems where the author has one — fix the constant and this check applies to
            // whatever they wrote. Checking the CONTRACT rather than this field also keeps the two
            // diagnostics reading the same member: a rule declaring Category twice fails §8.3, and
            // neither declaration is "the" category to judge.
            RuleContractResult contract = RuleContract.Check(field.ContainingType);
            if ((contract.Violations & RuleContractViolations.InvalidCategory) != 0) { continue; }
            if (!SymbolEqualityComparer.Default.Equals(contract.CategoryField, field)) { continue; }

            // A const field without an initialiser is CS0145 and never reaches an analyzer, but reading
            // the value of a null one would be a crash reported as AD0001 — which is to say, as silence.
            if (declarator.Initializer is not { } initialiser) { continue; }

            if (ReachesADeclaredCategory(context.SemanticModel, initialiser.Value, context.CancellationToken))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                Descriptors.UnreferencedRuleCategory,
                initialiser.Value.GetLocation(),
                field.ContainingType.Name));
        }
    }

    /// <summary>DCAT0005, DCAT0012 and DCAT0013: what the type's NAME says about the id it declares.</summary>
    private static void ReportNaming(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        string id,
        IFieldSymbol idField)
    {
        switch (RuleNaming.Classify(id, type.Name))
        {
            case RuleNameVerdict.Matches:
                ReportLiteralIdentifier(context, type, idField);

                break;

            case RuleNameVerdict.Forced:
                ReportOnType(context, type, Descriptors.RuleTypeNameDiffersFromId, type.Name, id);

                break;

            case RuleNameVerdict.Arbitrary:
                ReportOnType(context, type, Descriptors.RuleTypeNameDoesNotSayId, type.Name, id);

                break;
        }
    }

    /// <summary>
    /// True when <paramref name="expression"/> is a reference to a constant declared in a
    /// <c>[DiagnosticCategory]</c> class.
    /// </summary>
    /// <remarks>
    /// Resolved through the semantic model rather than matched on the source text, so every spelling
    /// that binds to the same field is accepted alike: <c>ContosoCategory.Usage</c>, an aliased
    /// container, a <c>using static</c>, a fully qualified name. What is rejected is anything that is
    /// not one field reference — a literal, a concatenation of two constants, a constant borrowed from
    /// an unmarked class — because none of them gives the catalogue a single spelling per value.
    ///
    /// The container may live in another assembly. A generated one is internal (ADR-0026) and so out of
    /// reach in practice, but a hand-written catalogue may publish one, and nothing about the guarantee
    /// depends on which assembly declares it.
    /// </remarks>
    private static bool ReachesADeclaredCategory(
        SemanticModel model,
        ExpressionSyntax expression,
        System.Threading.CancellationToken cancellationToken) =>
        model.GetSymbolInfo(expression, cancellationToken).Symbol is IFieldSymbol { IsConst: true } source
        && CategoryMarker.IsCategoryContainer(source.ContainingType);

    /// <summary>
    /// DCAT0012 — the name and the id agree, and only the source says whether they are held together.
    /// </summary>
    /// <remarks>
    /// The one place this analyzer reads syntax, and the one place it can. <c>nameof(JD0007)</c> and
    /// <c>"JD0007"</c> fold to the same constant, so <see cref="IFieldSymbol.ConstantValue"/> cannot tell
    /// them apart and a field with no syntax at all — the metadata case — carries no answer to give. That
    /// is not a gap: in a referenced assembly there is no longer a form to recommend.
    ///
    /// Reported on the INITIALISER rather than on the type's identifier, which is where every other
    /// definition diagnostic points. The fault is the expression, and the fix rewrites the expression;
    /// underlining the type name instead would leave the line that changes unmarked.
    /// </remarks>
    private static void ReportLiteralIdentifier(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        IFieldSymbol idField)
    {
        foreach (SyntaxReference reference in idField.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(context.CancellationToken) is not VariableDeclaratorSyntax declarator)
            {
                continue;
            }

            ExpressionSyntax? value = declarator.Initializer?.Value;

            // Any nameof, not only nameof(ThisType). A qualified argument spells the same operator, and
            // the value has already been checked to be the type's own name — an initialiser that both
            // reads nameof and folds to the right string is holding the two together, however it is
            // written.
            if (value is null || IsNameOf(value)) { continue; }

            context.ReportDiagnostic(
                Diagnostic.Create(Descriptors.IdNotWrittenAsNameOf, value.GetLocation(), type.Name));
        }
    }

    /// <remarks>
    /// Matched on the token's text rather than its contextual kind. A constant initialiser that reads
    /// <c>nameof(...)</c> and compiles cannot be anything else: an ordinary invocation is not a constant
    /// expression, so the compiler would have rejected the field long before this ran.
    /// </remarks>
    private static bool IsNameOf(ExpressionSyntax value) =>
        value is InvocationExpressionSyntax invocation
        && invocation.Expression is IdentifierNameSyntax name
        && name.Identifier.ValueText == "nameof";

    private static void Report(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        RuleContractViolations violation,
        DiagnosticDescriptor descriptor,
        RuleContractResult result)
    {
        if ((result.Violations & violation) == 0) { return; }

        ReportOnType(context, type, descriptor, type.Name);
    }

    private static void ReportOnType(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        DiagnosticDescriptor descriptor,
        params object[] messageArguments)
    {
        foreach (Location location in type.Locations)
        {
            // A partial type has one location per part, and only the ones in source can carry a
            // diagnostic. In practice a rule is declared once, but a partial declaration is legal and
            // reporting on a metadata location throws.
            if (location.IsInSource)
            {
                context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArguments));
            }
        }
    }
}
