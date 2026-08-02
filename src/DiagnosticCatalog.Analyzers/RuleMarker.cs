using Microsoft.CodeAnalysis;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Decides whether a type is declared as a diagnostic rule.
/// </summary>
internal static class RuleMarker
{
    /// <summary>
    /// The marker's fully qualified metadata name. Matching on this string rather than on a resolved
    /// symbol is a correctness requirement, not an optimisation (specification §7.2).
    /// </summary>
    internal const string AttributeMetadataName = "DiagnosticCatalog.DiagnosticRuleAttribute";

    /// <summary>
    /// True when <paramref name="type"/> carries the marker, whichever assembly declares it.
    /// </summary>
    /// <remarks>
    /// Never resolve the attribute to one <see cref="INamedTypeSymbol"/> and compare with
    /// SymbolEqualityComparer. Two things break, and both break silently:
    ///
    /// A catalogue is allowed to declare its own <c>internal sealed class DiagnosticRuleAttribute</c>
    /// in the DiagnosticCatalog namespace rather than take a package dependency — the pattern PolySharp
    /// uses for IsExternalInit (§7.2). Its attribute is a different symbol and would never match.
    ///
    /// And when a consumer cannot resolve DiagnosticCatalog.dll, <c>[DiagnosticRule]</c> degrades to an
    /// error type. Symbol comparison then finds nothing, every check reports nothing, and the output is
    /// indistinguishable from a codebase with no problems — which is the exact failure this library
    /// exists to eliminate, reproduced inside the tool meant to detect it.
    ///
    /// An error type still carries its name, so the comparison below survives that case.
    /// </remarks>
    internal static bool IsRule(INamedTypeSymbol type) =>
        AttributeMarker.Carries(type, AttributeMetadataName);
}
