using System;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Decides whether a type carries one of the foundation's marker attributes, matched by fully
/// qualified metadata name.
/// </summary>
/// <remarks>
/// Shared by <see cref="RuleMarker"/> and <see cref="CategoryMarker"/>, and shared deliberately: the
/// matching is subtle enough that two copies would drift, and a marker that stops being recognised
/// reports nothing rather than reporting something wrong. Each caller keeps its own metadata name and
/// its own reasoning; only the comparison lives here.
/// </remarks>
internal static class AttributeMarker
{
    /// <summary>
    /// True when <paramref name="type"/> carries an attribute whose fully qualified metadata name is
    /// <paramref name="attributeMetadataName"/>, whichever assembly declares it.
    /// </summary>
    internal static bool Carries(INamedTypeSymbol type, string attributeMetadataName) =>
        type.GetAttributes().Any(attribute => IsMarker(attribute, attributeMetadataName));

    private static bool IsMarker(AttributeData attribute, string attributeMetadataName)
    {
        INamedTypeSymbol? attributeClass = attribute.AttributeClass;

        return attributeClass is not null
            && string.Equals(FullMetadataName(attributeClass), attributeMetadataName, StringComparison.Ordinal);
    }

    // ToDisplayString would spell a nested or generic type differently from the metadata name, and
    // MetadataName alone drops the namespace. Building it explicitly keeps the comparison exact for the
    // shape that matters here: a top-level, non-generic attribute class.
    private static string FullMetadataName(INamedTypeSymbol type)
    {
        string @namespace = type.ContainingNamespace is { IsGlobalNamespace: false } containing
            ? containing.ToDisplayString()
            : string.Empty;

        return @namespace.Length == 0 ? type.MetadataName : @namespace + "." + type.MetadataName;
    }
}
