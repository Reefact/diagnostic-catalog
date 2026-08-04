using System;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis.CSharp;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// How a rule type's NAME relates to the identifier it declares.
/// </summary>
internal enum RuleNameVerdict
{
    /// <summary>The name is the identifier. There is nothing to say.</summary>
    Matches,

    /// <summary>
    /// The identifier cannot be spelled as a type name, and the name is the identifier legalised.
    /// The divergence was imposed, and no better spelling exists — reported as DCAT0005.
    /// </summary>
    Forced,

    /// <summary>
    /// The name does not say the identifier, and nothing forced that — reported as DCAT0012.
    /// </summary>
    Arbitrary,
}

/// <summary>
/// The relationship §8.2 recommends between a rule type's name and its <c>Id</c>, evaluated over two
/// strings.
/// </summary>
/// <remarks>
/// <para>
/// Two strings, deliberately: the same verdict has to hold for a rule read from source and for one read
/// from metadata, and a metadata symbol has no syntax to consult. The one question that DOES need syntax
/// — whether the author wrote <c>nameof</c> or a literal — is asked separately, and only of source.
/// </para>
/// <para>
/// The order of the two questions is the whole design, and reversing it is the mistake the
/// specification's own trigger condition made. §11.5 said to report only when
/// <c>SyntaxFacts.IsValidIdentifier(Id)</c> holds, using it as a SILENCER: an identifier that cannot be a
/// C# identifier was taken as proof that the name was forced. It is not. <c>RULE42</c> declaring
/// <c>"RULE-0001"</c> passed that guard silently, and it is the most misleading declaration of the lot.
/// Here the same predicate is a DISCRIMINATOR — it separates a divergence the author CHOSE from one C#
/// imposed — and both branches report.
/// </para>
/// </remarks>
internal static class RuleNaming
{
    /// <summary>Classifies <paramref name="typeName"/> against the <c>Id</c> it declares.</summary>
    internal static RuleNameVerdict Classify(string id, string typeName)
    {
        if (string.Equals(id, typeName, StringComparison.Ordinal)) { return RuleNameVerdict.Matches; }

        // The exact name was available — C# would have accepted a type spelled like the identifier —
        // and the author spelled it otherwise. Nothing imposed the divergence, so nothing excuses it:
        // every use site now reads a name that does not say which diagnostic it suppresses.
        //
        // Asked of the WHOLE identifier, before any truncation. Truncating first would make
        // "IL2026:Members annotated with RequiresUnreferencedCode" answer yes — its head alone is a
        // legal identifier — and route the trimmer's own friendly-name form here, where the repair on
        // offer is to name a type something C# will not accept.
        if (SyntaxFacts.IsValidIdentifier(id)) { return RuleNameVerdict.Arbitrary; }

        string core = Core(id);

        // A zero-length core carries nothing to recognise, so nothing can be said to lead with it.
        if (core.Length == 0) { return RuleNameVerdict.Arbitrary; }

        // Equal after legalisation, or the name LEADS with the identifier. Both spellings of
        // "RULE-0001" — RULE_0001 and RULE0001 — land on the first; IL2026Annotated lands on the
        // second. Demanding equality would reject the one shape the friendly-name form leaves open,
        // and there is no ground to prefer either legalisation over the other.
        return Alphanumerics(typeName).StartsWith(core, StringComparison.Ordinal)
            ? RuleNameVerdict.Forced
            : RuleNameVerdict.Arbitrary;
    }

    /// <summary>
    /// The part of an identifier a type name can be expected to carry.
    /// </summary>
    /// <remarks>
    /// Truncated at the first colon, exactly as §11.6 truncates a suppression's identifier and for the
    /// same reason: Roslyn and ILLink both read the head and treat the rest as a friendly name. A type
    /// named after the head has said everything the identifier identifies.
    ///
    /// Through <see cref="CheckId.Normalise"/> rather than a second hand-written truncation. The two
    /// were written separately and drifted apart at the other end — the index kept the raw declared
    /// value while every lookup was normalised — so one spelling of the operation is the point, not a
    /// tidiness.
    /// </remarks>
    private static string Core(string id) => Alphanumerics(CheckId.Normalise(id));

    /// <summary>
    /// The letters and digits, in order.
    /// </summary>
    /// <remarks>
    /// Everything C# forbids in an identifier is dropped rather than mapped, because the two legalisations
    /// an author reaches for — drop the character, or replace it with an underscore — differ only in what
    /// they leave behind, and this library has no ground to elect one. Dropping compares them equal.
    /// </remarks>
    private static string Alphanumerics(string text)
    {
        StringBuilder kept = new(text.Length);

        foreach (char character in text.Where(char.IsLetterOrDigit))
        {
            kept.Append(character);
        }

        return kept.ToString();
    }
}
