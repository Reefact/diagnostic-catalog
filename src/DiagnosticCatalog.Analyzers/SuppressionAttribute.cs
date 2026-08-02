using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Reads the two positional arguments of a suppression attribute, resolving each to what it references.
/// </summary>
/// <remarks>
/// <para>
/// <b>AttributeData cannot be used, and this is not a preference.</b> By the time constructor arguments
/// are exposed as <c>TypedConstant</c>, constants have been folded: the value is <c>"Usage"</c> and the
/// <see cref="IFieldSymbol"/> is gone. Every check in specification §10 depends on knowing WHICH field
/// was referenced, so the whole section is unimplementable through that API (§10.1). An implementation
/// that reaches for it does not report a wrong answer — DCAT0001 simply can never fire, and every
/// codebase looks coherent.
/// </para>
/// <para>
/// The specified path is a syntax node action on the attribute, then <c>GetSymbolInfo</c> on each
/// argument expression. IAttributeOperation would preserve the field reference but needs Roslyn 4.6 or
/// later, above the load floor this package compiles against.
/// </para>
/// <para>
/// Working on symbols rather than source text is what makes the accepted forms of §10.5 fall out for
/// free: qualified member access, a type alias, and <c>using static</c> all resolve to the same field.
/// </para>
/// </remarks>
internal static class SuppressionAttribute
{
    internal const string SuppressMessageMetadataName =
        "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute";

    internal const string UnconditionalSuppressMessageMetadataName =
        "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute";

    /// <summary>
    /// The attribute's fully qualified name, or null when it is neither suppression attribute.
    /// </summary>
    /// <remarks>
    /// Resolved through the semantic model rather than read from the source text, so that
    /// <c>using Suppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;</c> is recognised
    /// (§9.3). The short name written at the use site is never consulted.
    /// </remarks>
    internal static string? Identify(AttributeSyntax attribute, SemanticModel model)
    {
        if (model.GetSymbolInfo(attribute).Symbol is not IMethodSymbol constructor) { return null; }

        string name = constructor.ContainingType.ToDisplayString();

        return name is SuppressMessageMetadataName or UnconditionalSuppressMessageMetadataName
            ? name
            : null;
    }

    /// <summary>
    /// The first two positional arguments — category then checkId — resolved, or null when the
    /// attribute does not carry two of them.
    /// </summary>
    /// <remarks>
    /// Named arguments are skipped: <c>Justification</c>, <c>Scope</c>, <c>Target</c> and
    /// <c>MessageId</c> may appear in any order and are not part of the pair being checked. The two that
    /// are may themselves be written by parameter name and out of order, which is why the slot comes
    /// from <see cref="SuppressionArgumentOrder"/> rather than from the index in the list.
    /// </remarks>
    internal static (SuppressionArgument Category, SuppressionArgument CheckId)? ReadPair(
        AttributeSyntax attribute,
        SemanticModel model)
    {
        if (attribute.ArgumentList is null) { return null; }

        ExpressionSyntax? category = null;
        ExpressionSyntax? checkId = null;
        int positionalIndex = 0;

        foreach (AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments)
        {
            if (argument.NameEquals is not null) { continue; }

            switch (SuppressionArgumentOrder.SlotOf(argument, positionalIndex))
            {
                case SuppressionArgumentOrder.CategorySlot:
                    category = argument.Expression;

                    break;

                case SuppressionArgumentOrder.CheckIdSlot:
                    checkId = argument.Expression;

                    break;
            }

            positionalIndex++;
        }

        if (category is null || checkId is null) { return null; }

        return (Resolve(category, model), Resolve(checkId, model));
    }

    private static SuppressionArgument Resolve(ExpressionSyntax expression, SemanticModel model)
    {
        // The constant VALUE is available for any constant expression, including a rule member, and is
        // what DCAT0006 and DCAT0007 compare. Read first so it is carried whatever the kind turns out
        // to be.
        Optional<object?> constant = model.GetConstantValue(expression);
        string? value = constant.HasValue ? constant.Value as string : null;

        if (model.GetSymbolInfo(expression).Symbol is IFieldSymbol field
            && field.ContainingType is { } declaringType
            && RuleMarker.IsRule(declaringType))
        {
            // The declaring type is the answer, and the initialiser is NOT followed. A rule may write
            // Category = SonarCategory.MajorCodeSmell (§7.7); walking through to that initialiser would
            // make the comparison see SonarCategory rather than the rule type, and DCAT0001 would fire
            // on every correctly generated catalogue — all three this repository ships.
            return SuppressionArgument.FromRuleMember(declaringType, field, value);
        }

        // §10.6 — an intermediate constant hoisted from a rule member is the form the guide promotes,
        // so it resolves to the RULE and not to its value. Exactly one hop is followed, and only from
        // a declaring type that is NOT itself a rule: following it from a rule member is what the
        // branch above is careful never to do, because Category = SonarCategory.MajorCodeSmell would
        // then read as SonarCategory and fire DCAT0001 on every catalogue this repository generates.
        if (model.GetSymbolInfo(expression).Symbol is IFieldSymbol hoisted
            && hoisted.IsConst
            && ThroughInitialiser(hoisted, model) is { } referenced)
        {
            return referenced;
        }

        // A constant initialised from anything else, and a plain literal, land here: the value is
        // compared exactly as a literal's would be.
        return value is null ? SuppressionArgument.Unresolved : SuppressionArgument.FromConstant(value);
    }

    /// <summary>
    /// The rule member an intermediate constant was initialised from, or null when it was initialised
    /// from anything else.
    /// </summary>
    /// <remarks>
    /// One hop only, and deliberately so: a chain of constants is not a form any document promotes, and
    /// following it would cost a semantic model per link to recognise code nobody writes. A constant
    /// declared in another assembly has no syntax to read and lands on null, which leaves it compared by
    /// value — the behaviour it already had.
    /// </remarks>
    private static SuppressionArgument? ThroughInitialiser(IFieldSymbol hoisted, SemanticModel model)
    {
        foreach (SyntaxReference reference in hoisted.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not VariableDeclaratorSyntax { Initializer.Value: { } initialiser })
            {
                continue;
            }

            // Reusing the caller's model matters: the constant is usually declared in the very file
            // being analysed, and asking the compilation for a second model of that tree throws away
            // everything it has already bound.
            SemanticModel declaring = initialiser.SyntaxTree == model.SyntaxTree
                ? model
                : model.Compilation.GetSemanticModel(initialiser.SyntaxTree);

            if (declaring.GetSymbolInfo(initialiser).Symbol is IFieldSymbol field
                && field.ContainingType is { } declaringType
                && RuleMarker.IsRule(declaringType))
            {
                Optional<object?> constant = declaring.GetConstantValue(initialiser);

                return SuppressionArgument.FromRuleMember(
                    declaringType,
                    field,
                    constant.HasValue ? constant.Value as string : null);
            }
        }

        return null;
    }
}
