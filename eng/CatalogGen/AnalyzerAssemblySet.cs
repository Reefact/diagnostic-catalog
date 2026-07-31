namespace CatalogGen;

/// <summary>
/// The analyzer assemblies one acquisition produced, and what release they came from.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam between <em>acquiring</em> analyzer assemblies and <em>reading</em> the
/// descriptors they declare. Everything upstream of it differs per source — a package downloaded
/// from a feed, a project's build output, a path given on the command line; everything downstream
/// of it is identical, and deliberately so: reading is the stage that loads and runs somebody
/// else's code, and it must exist exactly once no matter where the assemblies came from.
/// </para>
/// <para>
/// It carries nothing but data — paths and two labels — so that the reader can be moved into a
/// separate process later without the contract changing. A worker that must bind to the target's
/// own runtime cannot be handed objects; it can be handed this.
/// </para>
/// <para>
/// <see cref="SourceName"/> and <see cref="SourceVersion"/> are what the emitter writes into the
/// catalogue's <c>CatalogSource</c> attribute, so they are the acquisition's answer to "what did
/// you read, and at which release" — the one question only the acquisition can answer.
/// </para>
/// </remarks>
/// <param name="DependencyContextPath">
/// The <c>.deps.json</c> describing what these assemblies were built against, or null when they
/// came without one. The reader hands it to the worker so the runtime resolves against the
/// TARGET's dependency graph rather than the worker's own — which is how an analyzer compiled
/// against a different Roslyn gets to bring its own along.
/// <para>
/// It is set by the acquisition rather than discovered by the reader, because whether such a graph
/// exists at all is a property of where the assemblies came from: a project's build output has
/// one, and assemblies extracted flat out of a package have none to find.
/// </para>
/// </param>
internal sealed record AnalyzerAssemblySet(
    IReadOnlyList<string> AssemblyPaths, string SourceName, string SourceVersion,
    string? DependencyContextPath = null);
