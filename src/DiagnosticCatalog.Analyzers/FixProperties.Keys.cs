namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// What a reported diagnostic hands to the code fix that repairs it.
/// </summary>
/// <remarks>
/// <para>
/// The two assemblies do not reference each other, so a diagnostic's properties are the whole channel
/// between them — the seam Roslyn provides for exactly this. Passing rendered strings rather than a
/// symbol reference also means the fix does no resolution of its own: it cannot disagree with the
/// analyzer about which rule was matched, because it never looks the rule up.
/// </para>
/// <para>
/// This half is <b>linked into the code-fix assembly</b>; the half that builds the dictionary stays with
/// the analyzer, since it reads symbols the fix never sees. The keys are an internal protocol between
/// one analyzer and one code fix shipped and versioned together — not a published contract the way a
/// DCAT id is.
/// </para>
/// </remarks>
internal static partial class FixProperties
{
    /// <summary>The reference to write, already relative to <see cref="Namespace"/>.</summary>
    internal const string Reference = "Reference";

    /// <summary>The namespace the reference needs imported, empty when it needs none.</summary>
    internal const string Namespace = "Namespace";

    /// <summary>
    /// Which argument the fix must rewrite, as the slot number: absent means both.
    /// </summary>
    /// <remarks>
    /// DCAT0007 rewrites one side and must leave the other exactly as written — it is already a
    /// reference. Which side that is cannot be re-derived from syntax alone, since a constant declared
    /// outside any rule looks like a member access too, so the analyzer says.
    /// </remarks>
    internal const string Slot = "Slot";
}
