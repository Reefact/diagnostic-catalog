using System;

namespace DiagnosticCatalog;

/// <summary>
/// Identifies a static class that holds the set of diagnostic categories used by a
/// catalogue, as compile-time constants.
/// </summary>
/// <remarks>
/// <para>
/// A catalogue of any size repeats very few distinct category strings across very many
/// rules — the Sonar catalogue spends 456 declarations on 13 distinct values. Declaring
/// each category once and referring to it keeps a single source per value:
/// </para>
/// <example>
/// <code>
/// [DiagnosticCategory]
/// public static class SonarCategory
/// {
///     public const string MajorCodeSmell = "Major Code Smell";
///     public const string MinorCodeSmell = "Minor Code Smell";
/// }
///
/// public static class SonarRule
/// {
///     [DiagnosticRule]
///     public static class S1144
///     {
///         public const string Id = nameof(S1144);
///         public const string Category = SonarCategory.MajorCodeSmell;
///     }
/// }
/// </code>
/// </example>
/// <para>
/// A <c>const</c> initialised from another <c>const</c> is still a compile-time constant,
/// so <c>SonarRule.S1144.Category</c> remains usable as an attribute argument and still
/// folds to the literal <c>"Major Code Smell"</c> in metadata. The indirection is free.
/// </para>
///
/// <para><b>What the marker is for</b></para>
/// <para>
/// The categories would work as plain constants without it — nothing in the structural
/// contract of a rule (see <see cref="DiagnosticRuleAttribute"/>) requires that
/// <c>Category</c> be initialised from a named constant rather than a literal. The marker
/// exists because without it an analyzer cannot tell a category constant from any other
/// string constant in the assembly. With it, tooling can offer the named constant when
/// replacing a literal category, and can validate that the class holds nothing but
/// non-empty public <c>const string</c> members.
/// </para>
/// <para>
/// Like <see cref="DiagnosticRuleAttribute"/>, this attribute must never be made
/// <c>[Conditional]</c>: it is read from the metadata of referenced assemblies, so a
/// conditional marker would make every catalogue shipped as a package invisible.
/// </para>
/// <para>
/// Applying this attribute is optional. A catalogue that repeats its category literals
/// remains perfectly valid; it simply gives up the single source of truth.
/// </para>
/// </remarks>
/// <seealso cref="DiagnosticRuleAttribute"/>
[AttributeUsage(
    AttributeTargets.Class,
    AllowMultiple = false,
    Inherited = false)]
public sealed class DiagnosticCategoryAttribute : Attribute
{
}
