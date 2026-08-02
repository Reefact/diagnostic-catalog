using Microsoft.CodeAnalysis;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Decides whether a type is declared as the class holding a catalogue's categories.
/// </summary>
/// <remarks>
/// Matched by fully qualified metadata name for the same two reasons <see cref="RuleMarker"/> is, and
/// they are correctness requirements rather than optimisations (§7.2): a catalogue may declare the
/// marker itself instead of taking a package dependency, and an unresolvable attribute degrades to an
/// error type that still carries its name. A symbol comparison would silently match neither, and a
/// category container that stops being recognised turns every rule in the catalogue into a DCAT0011.
/// </remarks>
internal static class CategoryMarker
{
    /// <summary>The marker's fully qualified metadata name (§7.7).</summary>
    internal const string AttributeMetadataName = "DiagnosticCatalog.DiagnosticCategoryAttribute";

    /// <summary>True when <paramref name="type"/> carries the marker, whichever assembly declares it.</summary>
    internal static bool IsCategoryContainer(INamedTypeSymbol type) =>
        AttributeMarker.Carries(type, AttributeMetadataName);
}
