using System.Collections.Immutable;
using System.Globalization;

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
    internal static ImmutableDictionary<string, string?> ForMatches(ImmutableArray<RuleDefinition> matches) =>
        matches.Length == 1 ? Render(matches[0].RuleType) : ImmutableDictionary<string, string?>.Empty;

    /// <summary>
    /// The properties for completing a half-migrated suppression, rewriting <paramref name="slot"/>.
    /// </summary>
    internal static ImmutableDictionary<string, string?> ForCompletion(INamedTypeSymbol ruleType, int slot) =>
        Render(ruleType).Add(Slot, slot.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// The properties for an incoherent pair: one reference per correction, both always present.
    /// </summary>
    /// <remarks>
    /// §12.1 requires two fixes and forbids guessing which rule was intended, so sending both is the
    /// contract rather than a convenience. Which slot each rewrites follows from its own name and is
    /// not carried: aligning on the category corrects the identifier, and the reverse.
    /// </remarks>
    internal static ImmutableDictionary<string, string?> ForIncoherentPair(
        INamedTypeSymbol categoryRule,
        INamedTypeSymbol checkIdRule)
    {
        (string Reference, string Namespace) category = Describe(categoryRule);
        (string Reference, string Namespace) checkId = Describe(checkIdRule);

        return ImmutableDictionary<string, string?>.Empty
            .Add(ReferenceKey(AlignOnCategory), category.Reference)
            .Add(NamespaceKey(AlignOnCategory), category.Namespace)
            .Add(ReferenceKey(AlignOnId), checkId.Reference)
            .Add(NamespaceKey(AlignOnId), checkId.Namespace);
    }

    private static ImmutableDictionary<string, string?> Render(INamedTypeSymbol ruleType)
    {
        (string Reference, string Namespace) described = Describe(ruleType);

        return ImmutableDictionary<string, string?>.Empty
            .Add(Reference, described.Reference)
            .Add(Namespace, described.Namespace);
    }

    private static (string Reference, string Namespace) Describe(INamedTypeSymbol ruleType)
    {
        string @namespace = ruleType.ContainingNamespace is { IsGlobalNamespace: false } containing
            ? containing.ToDisplayString()
            : string.Empty;

        // The qualified name minus its namespace: SonarRules.S1144, not the fully qualified form. That
        // is the shape §12.2 shows, and it is why the fix has a using directive to insert at all.
        string qualified = ruleType.ToDisplayString();

        return (@namespace.Length == 0 ? qualified : qualified.Substring(@namespace.Length + 1), @namespace);
    }
}
