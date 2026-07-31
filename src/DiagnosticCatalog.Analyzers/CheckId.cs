namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// Normalises a <c>checkId</c> written in a suppression before it is compared with a rule's Id.
/// </summary>
/// <remarks>
/// <para>
/// Roslyn truncates <c>checkId</c> at the first colon, so
/// <c>"S1144:Unused private members should be removed"</c> suppresses <c>S1144</c> (§3.3). Reproducing
/// that is <b>mandatory</b>, not a nicety: the suffixed form is what Visual Studio's built-in
/// <i>Suppress → In Source</i> fix generates, so it dominates existing codebases — precisely the code
/// this library exists to migrate. An analyzer skipping the step passes every hand-written fixture and
/// finds nothing in the wild.
/// </para>
/// <para>
/// It applies to <c>SuppressMessage</c> only. ILLink's decoder has no truncation step (§9.1), which is
/// why <c>IL2026:FriendlyName</c> is honoured there as written — see <see cref="IlWarningId"/>.
/// </para>
/// </remarks>
internal static class CheckId
{
    /// <summary>Everything before the first colon, or the whole string when there is none.</summary>
    internal static string Normalise(string checkId)
    {
        int separator = checkId.IndexOf(':');

        return separator < 0 ? checkId : checkId.Substring(0, separator);
    }
}
