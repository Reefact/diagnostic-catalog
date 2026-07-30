using System;

namespace DiagnosticCatalog;

/// <summary>
/// Records which upstream analyzer release a catalogue assembly mirrors, and when that
/// catalogue was last generated from it.
/// </summary>
/// <remarks>
/// <para>
/// A catalogue that mirrors an analyzer someone else ships is a snapshot: rules get
/// added, retired and recategorised with every upstream release. Nothing in the compiled
/// catalogue would otherwise say which release it reflects, or how old the snapshot is —
/// and because the platform never validates a category (see the specification, §3.2), a
/// catalogue that has silently drifted from its source produces no symptom at all.
/// </para>
/// <para>
/// Apply this attribute at assembly level in the generated catalogue:
/// </para>
/// <example>
/// <code>
/// [assembly: CatalogSource(
///     source:        "SonarAnalyzer.CSharp",
///     sourceVersion: "10.31.0.145097",
///     generatedOn:   "2026-07-30")]
/// </code>
/// </example>
/// <para>
/// <b>Why the date is a string.</b> Attribute arguments must be compile-time constants,
/// and neither <see cref="DateTime"/> nor <c>DateOnly</c> can be one. The value is
/// therefore an ISO 8601 calendar date, <c>yyyy-MM-dd</c>, exactly as
/// <c>AssemblyMetadataAttribute</c> is used for the same purpose. A consumer that wants a
/// real date can round-trip it with
/// <c>DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture)</c>.
/// </para>
/// <para>
/// The attribute is deliberately readable from metadata, so tooling can act on it without
/// the catalogue's source: a future analyzer can report a catalogue whose snapshot has
/// aged past a configured threshold, or whose <see cref="SourceVersion"/> no longer
/// matches the analyzer package actually referenced by the project.
/// </para>
/// <para>
/// <see cref="AttributeUsageAttribute.AllowMultiple"/> is enabled because one catalogue
/// assembly may legitimately
/// mirror several upstream packages — a C# and a Visual Basic analyzer from the same
/// vendor, for instance. It is meant for generated catalogues; a first-party catalogue
/// maintained by hand alongside its own analyzer needs no provenance record, since the
/// two ship from the same repository at the same version.
/// </para>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Assembly,
    AllowMultiple = true,
    Inherited = false)]
public sealed class CatalogSourceAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogSourceAttribute"/> class.
    /// </summary>
    /// <param name="source">
    /// The upstream artefact this catalogue mirrors, normally a NuGet package id such as
    /// <c>SonarAnalyzer.CSharp</c>.
    /// </param>
    /// <param name="sourceVersion">
    /// The exact version of <paramref name="source"/> the rules were read from.
    /// </param>
    /// <param name="generatedOn">
    /// The date the catalogue was generated, as an ISO 8601 calendar date
    /// (<c>yyyy-MM-dd</c>).
    /// </param>
    public CatalogSourceAttribute(string source, string sourceVersion, string generatedOn)
    {
        Source = source;
        SourceVersion = sourceVersion;
        GeneratedOn = generatedOn;
    }

    /// <summary>
    /// Gets the upstream artefact this catalogue mirrors.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets the exact version of <see cref="Source"/> the rules were read from.
    /// </summary>
    public string SourceVersion { get; }

    /// <summary>
    /// Gets the generation date as an ISO 8601 calendar date (<c>yyyy-MM-dd</c>).
    /// </summary>
    public string GeneratedOn { get; }
}
