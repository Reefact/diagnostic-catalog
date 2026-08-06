using Microsoft.CodeAnalysis.Diagnostics;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// What the build told the compiler about how this project is packaged.
/// </summary>
/// <remarks>
/// <para>
/// The only place in this assembly that reads something outside the compilation, and it is worth
/// saying why rather than leaving it to be discovered. Whether a catalogue packs its opt-in
/// (ADR-0038) is a fact about a <c>.csproj</c>, and no syntax tree, symbol or metadata reference
/// carries it. MSBuild computes it in
/// <c>buildTransitive/DiagnosticCatalog.targets</c> and publishes it through
/// <c>CompilerVisibleProperty</c>, which the SDK writes into the generated AnalyzerConfig as
/// <c>build_property.*</c>.
/// </para>
/// <para>
/// <b>Absence is the safe state, deliberately.</b> A project built without those targets — compiled
/// by hand, by another SDK, or by a test harness that says nothing — reports an empty value and
/// <see cref="OptInIsMissing"/> returns false. The alternative reading, "empty means the file is
/// absent", would fire DCAT0015 on every project that never opted into being measured, which is the
/// one failure mode a diagnostic about silence cannot afford.
/// </para>
/// <para>
/// The property is also an ESCAPE. The targets compute it only when it is empty, so a catalogue that
/// arranges the opt-in in a way MSBuild cannot recognise sets it to <c>packed</c> and is believed.
/// That matters because the detection is a match against how the file was declared, and there is more
/// than one way to write the same packaging.
/// </para>
/// </remarks>
internal static class CataloguePackaging
{
    /// <summary>The build's verdict: this project packs a catalogue and no opt-in with it.</summary>
    private const string Missing = "missing";

    private const string OptInKey = "build_property.DiagnosticCatalogAnalyzerOptIn";

    private const string PackageIdKey = "build_property.PackageId";

    internal static bool OptInIsMissing(AnalyzerConfigOptions options) =>
        options.TryGetValue(OptInKey, out string? state)
        && string.Equals(state, Missing, System.StringComparison.Ordinal);

    /// <summary>
    /// The package id to name in the message, or null when the build did not say. Null is reported
    /// rather than guessed from the assembly name: the two differ often enough that a guess would send
    /// a reader looking for <c>build/&lt;assembly&gt;.props</c>, which is not the file NuGet imports.
    /// </summary>
    internal static string? PackageId(AnalyzerConfigOptions options) =>
        options.TryGetValue(PackageIdKey, out string? id) && id.Length > 0 ? id : null;
}
