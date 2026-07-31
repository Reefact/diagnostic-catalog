using System.Text.Json;

namespace CatalogGen;

/// <summary>
/// What one run over a set of catalogues produced.
/// </summary>
/// <param name="ExitCode">Zero when every catalogue was generated; one when any of them failed.</param>
/// <param name="ChangedAny">Whether any catalogue's file was rewritten.</param>
/// <param name="Summary">
/// The run's report, in Markdown, ready to be written where a pull request body can read it. Its
/// destination is the caller's business: the engine says what happened, the shell decides where
/// that goes.
/// </param>
public sealed record RunOutcome(int ExitCode, bool ChangedAny, string Summary);

/// <summary>
/// The generator's entry point, as the command-line tool calls it.
/// </summary>
/// <remarks>
/// This is the whole boundary between the shell and the engine. Everything above it — parsing a
/// command line, reading a configuration file, deciding where output goes — belongs to the tool;
/// everything below it is acquiring analyzer assemblies, reading their descriptors and emitting
/// catalogues. Keeping the boundary this narrow is what lets the command line be replaced without
/// the generator noticing, which is exactly what happened when it was.
/// </remarks>
public static class CatalogRun
{
    /// <summary>
    /// Reads a manifest into the catalogues it declares.
    /// </summary>
    /// <param name="json">The manifest's content.</param>
    /// <param name="manifestPath">
    /// Where it was read from. Paths inside a manifest are relative to the manifest itself, so the
    /// tool can be run from anywhere without those paths depending on the caller's directory.
    /// </param>
    public static IReadOnlyList<Job> JobsFromManifest(string json, string manifestPath)
    {
        string manifestDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        string file = Path.GetFileName(manifestPath);
        List<Job> jobs = [];
        using JsonDocument doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("catalogs", out JsonElement catalogs))
            throw new ManifestException($"{file}: no \"catalogs\" array.");

        int index = 0;
        foreach (JsonElement e in catalogs.EnumerateArray())
        {
            // Every entry names itself in its own errors. A manifest is edited by hand, so the
            // likeliest fault is a mistyped key — and the answer to one used to be "The given key
            // was not present in the dictionary", which named neither the key, nor the file, nor
            // which of several entries carried it.
            string where = $"{file}: catalogs[{index}]";
            index++;

            // An entry names a package to fetch, a .nupkg on disk, or assemblies on disk. Paths in
            // any of them are resolved against the manifest, exactly as "output" is.
            IReadOnlyList<string>? assemblies = e.TryGetProperty("assemblies", out JsonElement a)
                ? [.. a.EnumerateArray().Select(x => Path.GetFullPath(Path.Combine(manifestDir, Text(x, where, "assemblies"))))]
                : null;
            string? nupkg = e.TryGetProperty("nupkg", out JsonElement n)
                ? Path.GetFullPath(Path.Combine(manifestDir, Text(n, where, "nupkg")))
                : null;
            string? project = e.TryGetProperty("project", out JsonElement pr)
                ? Path.GetFullPath(Path.Combine(manifestDir, Text(pr, where, "project")))
                : null;

            int named = (assemblies is not null ? 1 : 0) + (nupkg is not null ? 1 : 0) + (project is not null ? 1 : 0);
            if (named > 1)
                throw new ManifestException($"{where}: names more than one source; give one of " +
                                            "\"package\", \"nupkg\", \"project\" or \"assemblies\".");

            bool fromFeed = named == 0;

            jobs.Add(new Job(
                Package: fromFeed ? Required(e, "package", where) : null,
                Version: fromFeed ? Optional(e, "version", where) ?? "latest" : null,
                Namespace: Required(e, "namespace", where),
                Container: Required(e, "container", where),
                Output: Path.GetFullPath(Path.Combine(manifestDir, Required(e, "output", where))),
                Language: Optional(e, "language", where) ?? "cs",
                Assemblies: assemblies,
                SourceName: Optional(e, "sourceName", where),
                SourceVersion: Optional(e, "sourceVersion", where),
                Nupkg: nupkg,
                Source: Optional(e, "source", where),
                Project: project));
        }

        if (jobs.Count == 0) throw new ManifestException($"{file}: \"catalogs\" declares no entry.");

        return jobs;
    }

    private static string Required(JsonElement entry, string name, string where)
        => entry.TryGetProperty(name, out JsonElement value)
               ? Text(value, where, name)
               : throw new ManifestException($"{where}: \"{name}\" is missing.");

    private static string? Optional(JsonElement entry, string name, string where)
        => entry.TryGetProperty(name, out JsonElement value) ? Text(value, where, name) : null;

    private static string Text(JsonElement value, string where, string name)
        => value.ValueKind == JsonValueKind.String
               ? value.GetString()!
               : throw new ManifestException($"{where}: \"{name}\" should be a string, not {value.ValueKind}.");

    /// <summary>
    /// Generates every catalogue in <paramref name="jobs"/>.
    /// </summary>
    /// <param name="jobs">The catalogues to generate.</param>
    /// <param name="dateOverride">
    /// The generation date to stamp, or null for today. Pinning it is what makes regenerating the
    /// same inputs twice produce the same bytes.
    /// </param>
    /// <param name="cancellation">
    /// Observed while a package is being resolved and downloaded, which is where a run spends
    /// almost all of its time and the only place interrupting it has anything to interrupt.
    /// </param>
    /// <param name="writeChanges">
    /// False to compare without touching anything, which is what asking "is this catalogue still
    /// true?" means. <see cref="RunOutcome.ChangedAny"/> then reports drift rather than work done.
    /// </param>
    public static async Task<RunOutcome> ExecuteAsync(
        IReadOnlyList<Job> jobs, string? dateOverride, CancellationToken cancellation = default,
        bool writeChanges = true)
    {
        List<string> summaries = [];
        bool changedAny = false;
        int exitCode = 0;

        foreach (Job job in jobs)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {job.Namespace} <- {job.SourceLabel} ===");
            try
            {
                GenerateResult? result = await GenerateAsync(job, dateOverride, cancellation, writeChanges);
                if (result is null) { exitCode = 1; continue; }
                if (result.Changed)
                {
                    changedAny = true;
                    summaries.Add(result.Summary);
                }
            }
            catch (Exception ex)
            {
                // One unreachable or restructured upstream package must not silently take the
                // whole run down: report it, keep going, and fail the process at the end.
                //
                // S6966 asks for WriteLineAsync here, but on none of the ~20 Console.WriteLine calls
                // around it — Console.WriteLine is a static method with no async counterpart, while
                // Console.Error is a TextWriter that has one. Both streams are synchronized writers
                // whose async overloads complete synchronously, so awaiting would yield to nothing
                // and leave this tool's diagnostics half-async on a technicality of where the method
                // happens to be declared.
#pragma warning disable S6966 // Awaitable method should be used
                Console.Error.WriteLine($"FAILED {job.Namespace}: {ex.GetType().Name}: {ex.Message}");
#pragma warning restore S6966
                exitCode = 1;
            }
        }

        string summary = changedAny
            ? string.Join("\n", summaries)
            : writeChanges
                ? "No catalogue changed: every upstream package still resolves to the version already mirrored."
                : "Every catalogue is current with its source.";

        return new RunOutcome(exitCode, changedAny, summary);
    }

    // One catalogue, end to end: acquire, read, emit. Only the acquisition differs per source; what
    // follows it is the same two calls either way, which is the property the split exists to give.
    private static async Task<GenerateResult?> GenerateAsync(
        Job job, string? dateOverride, CancellationToken cancellation, bool writeChanges)
    {
        Previous? previous = CatalogParser.ReadPrevious(job.Output);

        if (job.Assemblies is not null)
        {
            AnalyzerAssemblySet? local = LocalAssemblySource.Acquire(job.Assemblies, job.SourceName, job.SourceVersion);

            return local is null ? null : EmitFrom(local);
        }

        // Only a package needs scratch space: it has to be unzipped — and, from a feed, downloaded
        // first — before it can be read, and the directory has to go whether that succeeded,
        // returned nothing, or threw.
        DirectoryInfo work = Directory.CreateTempSubdirectory("cataloggen");
        try
        {
            AnalyzerAssemblySet? fetched = job.Nupkg is not null
                ? LocalPackageSource.Acquire(job.Nupkg, job.SourceName, job.SourceVersion, job.Language,
                                             work.FullName)
                : await NuGetPackageSource.AcquireAsync(job.Package!, job.Version!, job.Language, work.FullName,
                                                        job.Source, cancellation);

            return fetched is null ? null : EmitFrom(fetched);
        }
        finally
        {
            work.Delete(recursive: true);
        }

        GenerateResult? EmitFrom(AnalyzerAssemblySet source)
        {
            SortedDictionary<string, RuleInfo>? rules = DescriptorReader.Read(source);

            return rules is null
                ? null
                : CatalogEmitter.Emit(job, source.SourceName, source.SourceVersion, rules, previous, dateOverride,
                                      writeChanges);
        }
    }
}

/// <summary>
/// A manifest the tool cannot act on, described in terms of the manifest rather than of the parser.
/// </summary>
/// <remarks>
/// It exists so the shell can tell a file it was handed from a defect in the tool: the first is the
/// caller's to fix and deserves one legible line, the second is not and deserves a stack trace.
/// </remarks>
public sealed class ManifestException : Exception
{
    public ManifestException(string message) : base(message)
    {
    }

    public ManifestException()
    {
    }

    public ManifestException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
