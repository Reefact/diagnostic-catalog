using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Which of a suppression's two constructor parameters an argument fills.
/// </summary>
/// <remarks>
/// <para>
/// Position alone is not enough. C# allows the parameters to be named, and named ones may be written in
/// any order: <c>[SuppressMessage(checkId: "S1144", category: "Major Code Smell")]</c> is legal and
/// reversed. Reading it by position swaps the pair — the analyzer then looks up a rule under
/// <c>("S1144", "Major Code Smell")</c>, finds nothing, and reports nothing; the code fix would write
/// the category where the identifier belongs.
/// </para>
/// <para>
/// This file is <b>linked into the code-fix assembly</b>, so the analyzer and the fix cannot disagree
/// about which argument is which. Matching on the parameter names is safe: they belong to the public API
/// of two BCL attributes, and both spell them the same way.
/// </para>
/// </remarks>
internal static class SuppressionArgumentOrder
{
    /// <summary>The slot holding the category.</summary>
    internal const int CategorySlot = 0;

    /// <summary>The slot holding the identifier.</summary>
    internal const int CheckIdSlot = 1;

    /// <summary>Nothing this pair cares about — a named argument, or a third parameter.</summary>
    internal const int NoSlot = -1;

    /// <summary>
    /// The slot <paramref name="argument"/> fills, given how many positional arguments preceded it.
    /// </summary>
    internal static int SlotOf(AttributeArgumentSyntax argument, int positionalIndex)
    {
        // Justification, Scope, Target and MessageId are properties, not constructor parameters. They
        // may appear in any order and are never part of the pair.
        if (argument.NameEquals is not null) { return NoSlot; }

        if (argument.NameColon is not null)
        {
            return argument.NameColon.Name.Identifier.ValueText switch
            {
                "category" => CategorySlot,
                "checkId" => CheckIdSlot,
                _ => NoSlot,
            };
        }

        return positionalIndex is CategorySlot or CheckIdSlot ? positionalIndex : NoSlot;
    }
}
