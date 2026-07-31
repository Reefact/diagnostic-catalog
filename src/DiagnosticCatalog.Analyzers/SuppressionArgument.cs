using Microsoft.CodeAnalysis;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// What one argument of a suppression attribute turned out to be.
/// </summary>
internal enum SuppressionArgumentKind
{
    /// <summary>Nothing usable: an unresolved symbol, or an expression that is not constant.</summary>
    Unresolved,

    /// <summary>A field declared on a type carrying the rule marker — the canonical form.</summary>
    RuleMember,

    /// <summary>
    /// A constant that is not a rule member: a literal, or a constant declared elsewhere (§10.6).
    /// Its value is known and comparable; where it was declared is not interesting.
    /// </summary>
    ConstantValue,
}

/// <summary>
/// One resolved argument of a <c>SuppressMessage</c> attribute.
/// </summary>
internal sealed class SuppressionArgument
{
    private SuppressionArgument(
        SuppressionArgumentKind kind,
        INamedTypeSymbol? ruleType,
        IFieldSymbol? field,
        string? value)
    {
        Kind = kind;
        RuleType = ruleType;
        Field = field;
        Value = value;
    }

    internal SuppressionArgumentKind Kind { get; }

    /// <summary>The rule type declaring <see cref="Field"/>, when this is a rule member.</summary>
    internal INamedTypeSymbol? RuleType { get; }

    /// <summary>The referenced field, when this is a rule member.</summary>
    internal IFieldSymbol? Field { get; }

    /// <summary>The constant value, known for both a rule member and a plain constant.</summary>
    internal string? Value { get; }

    internal static SuppressionArgument Unresolved { get; } =
        new(SuppressionArgumentKind.Unresolved, null, null, null);

    internal static SuppressionArgument FromRuleMember(INamedTypeSymbol ruleType, IFieldSymbol field, string? value) =>
        new(SuppressionArgumentKind.RuleMember, ruleType, field, value);

    internal static SuppressionArgument FromConstant(string value) =>
        new(SuppressionArgumentKind.ConstantValue, null, null, value);
}
