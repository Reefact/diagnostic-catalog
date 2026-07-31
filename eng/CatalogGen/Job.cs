namespace CatalogGen;

/// <summary>
/// One catalogue to generate: where its analyzers come from, and where the result goes.
/// </summary>
/// <remarks>
/// <para>
/// Public because the command-line tool builds these and hands them to <see cref="CatalogRun"/>.
/// It is the engine's whole input surface — nothing else crosses from the shell into the engine —
/// which is what keeps argument parsing on one side of the boundary and generation on the other.
/// </para>
/// <para>
/// <see cref="Package"/>/<see cref="Version"/> and <see cref="Assemblies"/> are the two ways to
/// name a source, and exactly one is set. <see cref="Assemblies"/> is what decides: when it is set
/// the other two are null and never read.
/// </para>
/// </remarks>
public sealed record Job(
    string? Package, string? Version, string Namespace, string Container, string Output, string Language,
    IReadOnlyList<string>? Assemblies = null, string? SourceName = null, string? SourceVersion = null)
{
    /// <summary>
    /// What this job reads from, for the run's header line. The assemblies' file names when they
    /// are what was asked for, because a path list is what the caller will recognise.
    /// </summary>
    public string SourceLabel =>
        Assemblies is null
            ? Package!
            : SourceName ?? string.Join(", ", Assemblies.Select(Path.GetFileName));
}
