using System;
using System.Globalization;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Whether ILLink would honour an identifier written in <c>UnconditionalSuppressMessage</c>.
/// </summary>
/// <remarks>
/// <para>
/// This replicates the decoder quoted in specification §9.1 rather than the tighter
/// <c>^IL\d{4}$</c> that §11.9's wording suggests, and the difference is not cosmetic. DCAT0009 exists
/// because a rejected identifier makes the suppression a <b>silent no-op</b> — ILLink discards the
/// attribute and Roslyn never processes it either. Report anything ILLink actually honours and the
/// diagnostic stops being true: it would tell a developer to change a suppression that works.
/// </para>
/// <para>
/// Two forms reachable in practice separate the two readings. <c>IL2026:FriendlyName</c> is ILLink's own
/// friendly-name syntax and suppresses IL2026; <c>IL20265</c> is honoured as IL2026, because the decoder
/// reads exactly four characters at offset 2 and ignores whatever follows. The strict pattern rejects
/// both.
/// </para>
/// <para>
/// One deliberate departure: the parse is pinned to the invariant culture. ILLink's own call is
/// culture-sensitive through <c>NumberFormatInfo.CurrentInfo</c>, which would make this analyzer report
/// differently depending on the machine's locale — unacceptable in a compiler check, and observable only
/// on sign-and-whitespace forms that no generated catalogue can produce.
/// </para>
/// </remarks>
internal static class IlWarningId
{
    /// <summary>The decoder's length floor: two letters plus the four digits it reads.</summary>
    private const int MinimumLength = 6;

    /// <summary>
    /// True when ILLink's decoder accepts <paramref name="id"/>, so the suppression is registered.
    /// </summary>
    internal static bool IsHonoured(string? id) =>
        id is not null
        && id.Length >= MinimumLength
        && id.StartsWith("IL", StringComparison.Ordinal)
        && int.TryParse(
            id.Substring(2, 4),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int _);
}
