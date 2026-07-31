using Microsoft.CodeAnalysis;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// A rule the analyzer discovered, in the current compilation or in a referenced assembly.
/// </summary>
/// <remarks>
/// <para>
/// Specification §13 shows this as a positional record. It is a plain sealed class here because a record
/// in a shipped <c>netstandard2.0</c> assembly needs <c>IsExternalInit</c>, and the polyfill under
/// <c>build/</c> is a test-only concession — a consumer compiling against .NET Framework would otherwise
/// have to supply the marker themselves (CLAUDE.md, ADR-0001).
/// </para>
/// <para>
/// It carries the two keys §13 distinguishes. The <b>functional</b> key is <c>Category + Id</c>, used by
/// the value-based lookups; the <b>structural</b> key is <see cref="RuleType"/>, the symbol itself. That
/// separation is why DCAT0001 needs no index at all — it compares symbols resolved from the attribute.
/// </para>
/// </remarks>
internal sealed class RuleDefinition
{
    internal RuleDefinition(
        INamedTypeSymbol ruleType,
        IFieldSymbol idField,
        IFieldSymbol categoryField,
        string id,
        string category)
    {
        RuleType = ruleType;
        IdField = idField;
        CategoryField = categoryField;
        Id = id;
        Category = category;
    }

    internal INamedTypeSymbol RuleType { get; }

    internal IFieldSymbol IdField { get; }

    internal IFieldSymbol CategoryField { get; }

    internal string Id { get; }

    internal string Category { get; }
}
