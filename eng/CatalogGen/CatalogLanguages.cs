namespace CatalogGen;

/// <summary>
/// Which languages a catalogue can be generated for.
/// </summary>
/// <remarks>
/// <para>
/// C# only, and the restriction is real rather than a placeholder. Reading descriptors means
/// CONSTRUCTING each analyzer, and a Visual Basic analyzer derives from types in
/// <c>Microsoft.CodeAnalysis.VisualBasic</c>, which the descriptor worker does not carry. Measured
/// against <c>Microsoft.CodeAnalysis.NetAnalyzers</c>, <c>--language vb</c> read 311 descriptors and
/// then refused: three types could not be loaded, and the run correctly declined to emit a catalogue
/// short of the rules they declare.
/// </para>
/// <para>
/// So the option was accepted, documented, and could not succeed — a promise the tool could not
/// keep, discovered only after a package had been downloaded. It is refused up front instead.
/// </para>
/// <para>
/// This is the mechanism, not the whole reason. ADR-0020 records the other half: Visual Basic is
/// closed to new language features, so its analyzer population is small and will not grow, and this
/// project declines to carry a second Roslyn in every install to serve it. That is a settled
/// position rather than a deferred task — which is why this list is not written to be extended
/// lightly.
/// </para>
/// <para>
/// Distinct from the languages a package LAYOUT is known to use, which still includes Visual Basic
/// and F#: a C# read has to recognise a <c>vb/</c> folder in order to exclude it. Knowing about a
/// language and being able to read it are different facts, and conflating them would put Visual
/// Basic rules into a C# catalogue.
/// </para>
/// </remarks>
public static class CatalogLanguages
{
    /// The languages a catalogue can be generated for.
    public static readonly IReadOnlyList<string> Readable = ["cs"];

    /// True when a catalogue can be generated for <paramref name="language"/>.
    public static bool CanRead(string language)
        => Readable.Contains(language, StringComparer.OrdinalIgnoreCase);

    /// What to tell somebody who asked for one this tool cannot read.
    public static string Refusal(string language)
        => $"'{language}' is not a language this tool can read; give {string.Join(" or ", Readable)}. " +
           "Reading descriptors means constructing each analyzer, and the descriptor worker carries " +
           "only C# Roslyn — so another language's analyzers fail to load and the run refuses rather " +
           "than emitting a catalogue short of their rules.";
}
