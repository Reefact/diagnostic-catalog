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
        List<Job> jobs = [];
        using JsonDocument doc = JsonDocument.Parse(json);
        foreach (JsonElement e in doc.RootElement.GetProperty("catalogs").EnumerateArray())
        {
            // An entry names either a package to fetch or assemblies already on disk. "assemblies"
            // decides, so an entry carrying it needs no "package" — and paths in it are resolved
            // against the manifest, exactly as "output" is.
            IReadOnlyList<string>? assemblies = e.TryGetProperty("assemblies", out JsonElement a)
                ? [.. a.EnumerateArray().Select(x => Path.GetFullPath(Path.Combine(manifestDir, x.GetString()!)))]
                : null;
            string? nupkg = e.TryGetProperty("nupkg", out JsonElement n)
                ? Path.GetFullPath(Path.Combine(manifestDir, n.GetString()!))
                : null;

            jobs.Add(new Job(
                Package: assemblies is null && nupkg is null ? e.GetProperty("package").GetString()! : null,
                Version: assemblies is not null || nupkg is not null ? null
                         : e.TryGetProperty("version", out JsonElement v) ? v.GetString()! : "latest",
                Namespace: e.GetProperty("namespace").GetString()!,
                Container: e.GetProperty("container").GetString()!,
                Output: Path.GetFullPath(Path.Combine(manifestDir, e.GetProperty("output").GetString()!)),
                Language: e.TryGetProperty("language", out JsonElement l) ? l.GetString()! : "cs",
                Assemblies: assemblies,
                SourceName: e.TryGetProperty("sourceName", out JsonElement sn) ? sn.GetString() : null,
                SourceVersion: e.TryGetProperty("sourceVersion", out JsonElement sv) ? sv.GetString() : null,
                Nupkg: nupkg));
        }

        return jobs;
    }

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
    public static async Task<RunOutcome> ExecuteAsync(
        IReadOnlyList<Job> jobs, string? dateOverride, CancellationToken cancellation = default)
    {
        using HttpClient http = new();
        List<string> summaries = [];
        bool changedAny = false;
        int exitCode = 0;

        foreach (Job job in jobs)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {job.Namespace} <- {job.SourceLabel} ===");
            try
            {
                GenerateResult? result = await GenerateAsync(job, dateOverride, http, cancellation);
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
            : "No catalogue changed: every upstream package still resolves to the version already mirrored.";

        return new RunOutcome(exitCode, changedAny, summary);
    }

    // One catalogue, end to end: acquire, read, emit. Only the acquisition differs per source; what
    // follows it is the same two calls either way, which is the property the split exists to give.
    private static async Task<GenerateResult?> GenerateAsync(
        Job job, string? dateOverride, HttpClient http, CancellationToken cancellation)
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
                                                        http, cancellation);

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
                : CatalogEmitter.Emit(job, source.SourceName, source.SourceVersion, rules, previous, dateOverride);
        }
    }
}
