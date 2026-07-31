using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Builds the properties declared in <c>FixProperties.Keys.cs</c>. Analyzer side only: it reads symbols
/// the code fix never sees, which is why this half is not linked across.
/// </summary>
internal static partial class FixProperties
{
    /// <summary>
    /// The properties for a match, or none when the choice is not the analyzer's to make.
    /// </summary>
    /// <remarks>
    /// Several rules sharing one <c>(Category, Id)</c> pair get a diagnostic and no automatic fix
    /// (§11.6). Returning empty properties is what makes that happen: the code fix finds nothing to act
    /// on and offers nothing, rather than picking one of them on the author's behalf.
    /// </remarks>
    internal static ImmutableDictionary<string, string?> ForMatches(ImmutableArray<RuleDefinition> matches)
    {
        if (matches.Length != 1) { return ImmutableDictionary<string, string?>.Empty; }

        INamedTypeSymbol ruleType = matches[0].RuleType;

        string @namespace = ruleType.ContainingNamespace is { IsGlobalNamespace: false } containing
            ? containing.ToDisplayString()
            : string.Empty;

        // The qualified name minus its namespace: SonarRules.S1144, not the fully qualified form. That
        // is the shape §12.2 shows, and it is why the fix has a using directive to insert at all.
        string qualified = ruleType.ToDisplayString();
        string reference = @namespace.Length == 0
            ? qualified
            : qualified.Substring(@namespace.Length + 1);

        return ImmutableDictionary<string, string?>.Empty
            .Add(Reference, reference)
            .Add(Namespace, @namespace);
    }
}
