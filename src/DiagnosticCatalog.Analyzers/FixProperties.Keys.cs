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
    /// Which argument the fix must rewrite, as the slot number.
    /// </summary>
    /// <remarks>
    /// Written on DCAT0007 alone, which rewrites one side and must leave the other exactly as written
    /// — it is already a reference. Which side that is cannot be re-derived from syntax alone, since a
    /// constant declared outside any rule looks like a member access too, so the analyzer says.
    ///
    /// Absent everywhere else, and nothing derives a default from that absence: the DCAT0006 fix
    /// rewrites both arguments by construction, because both of them are values.
    /// </remarks>
    internal const string Slot = "Slot";

    /// <summary>Keep the category's rule and correct the identifier (§12.1).</summary>
    internal const string AlignOnCategory = "AlignOnCategory";

    /// <summary>Keep the identifier's rule and correct the category (§12.1).</summary>
    internal const string AlignOnId = "AlignOnId";

    /// <summary>
    /// An incoherent pair carries one reference per correction, so the keys are prefixed by which.
    /// </summary>
    /// <remarks>
    /// Both are always present. §12.1 forbids the fix from guessing which rule was intended, so the
    /// analyzer cannot send one of them and call it the answer.
    /// </remarks>
    internal static string ReferenceKey(string alignment) => alignment + "." + Reference;

    /// <summary>The namespace half of <see cref="ReferenceKey"/>.</summary>
    internal static string NamespaceKey(string alignment) => alignment + "." + Namespace;
}
