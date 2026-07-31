using System.Text;
using System.Text.Json;
using CatalogGen;

// ---------------------------------------------------------------------------
// CatalogGen — generates a DiagnosticCatalog catalogue from an upstream
// analyzer package, by reading the DiagnosticDescriptor instances the analyzers
// actually declare.
//
// Reading the descriptors is the whole point. Rule metadata published as JSON or
// as documentation drifts from what the analyzer declares, and because the .NET
// platform never validates a suppression's category (specification §3.2), such a
// divergence produces no symptom anywhere. The descriptors are the only source
// that cannot be wrong.
//
// The run is three stages, and they are separate files for a reason:
//
//   acquire   NuGetPackageSource   → AnalyzerAssemblySet   (where the assemblies come from)
//             LocalAssemblySource  →
//   read      DescriptorReader     → the rules they declare (loads and constructs third-party code)
//   emit      CatalogEmitter       → the catalogue as C# source
//
// Only the first differs per source. A further way of obtaining analyzer assemblies is a new
// acquisition beside those two; it is never a second reader.
//
// Usage:
//   dotnet run --project eng/CatalogGen -- --manifest eng/catalogs.json
//   dotnet run --project eng/CatalogGen -- \
//       --package SonarAnalyzer.CSharp --version latest \
//       --namespace DiagnosticCatalog.Sonar --container SonarRule \
//       --output src/DiagnosticCatalog.Sonar/SonarRules.g.cs \
//       [--date 2026-07-30] [--language cs] [--summary out.md]
//   dotnet run --project eng/CatalogGen -- \
//       --assembly bin/Release/net10.0/My.Analyzers.dll \
//       --namespace My.Catalog --container MyRule --output src/My.Catalog/MyRules.g.cs \
//       [--source-name My.Analyzers] [--source-version 1.4.0] [--date 2026-07-30] [--summary out.md]
// ---------------------------------------------------------------------------

Cli? cli = CommandLine.ParseArgs(args);
if (cli is null) return 2;

DescriptorReader.InstallAssemblyResolver();

List<Job> jobs;
if (cli.Manifest is not null)
{
    jobs = JobsFromManifest(await File.ReadAllTextAsync(cli.Manifest), Path.GetFullPath(cli.Manifest));
    Console.WriteLine($"manifest {cli.Manifest}: {jobs.Count} catalogue(s)");
}
else
{
    bool fromAssemblies = cli.Assemblies.Count > 0;
    jobs = [new Job(fromAssemblies ? null : cli.Package!, fromAssemblies ? null : cli.Version!,
                    cli.Namespace!, cli.Container!, Path.GetFullPath(cli.Output!), cli.Language,
                    Assemblies: fromAssemblies ? cli.Assemblies : null,
                    SourceName: cli.SourceName, SourceVersion: cli.SourceVersion)];
}

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
        GenerateResult? result = await GenerateAsync(job, cli.Date, http);
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
        // S6966 asks for WriteLineAsync here, but on none of the ~20 Console.WriteLine calls around
        // it — Console.WriteLine is a static method with no async counterpart, while Console.Error is
        // a TextWriter that has one. Both streams are synchronized writers whose async overloads
        // complete synchronously, so awaiting would yield to nothing and leave this tool's
        // diagnostics half-async on a technicality of where the method happens to be declared.
#pragma warning disable S6966 // Awaitable method should be used
        Console.Error.WriteLine($"FAILED {job.Namespace}: {ex.GetType().Name}: {ex.Message}");
#pragma warning restore S6966
        exitCode = 1;
    }
}

if (cli.Summary is not null)
{
    string body = changedAny
        ? string.Join("\n", summaries)
        : "No catalogue changed: every upstream package still resolves to the version already mirrored.";
    await File.WriteAllTextAsync(Path.GetFullPath(cli.Summary), body.ReplaceLineEndings("\n") + "\n",
                                 new UTF8Encoding(false));
    Console.WriteLine();
    Console.WriteLine($"summary written to {cli.Summary}");
}

Console.WriteLine();
Console.WriteLine(changedAny ? "RESULT: catalogues changed" : "RESULT: no change");
return exitCode;

// ---------------------------------------------------------------------------

static List<Job> JobsFromManifest(string json, string manifestPath)
{
    string manifestDir = Path.GetDirectoryName(manifestPath)!;
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

        jobs.Add(new Job(
            Package: assemblies is null ? e.GetProperty("package").GetString()! : null,
            Version: assemblies is not null ? null
                     : e.TryGetProperty("version", out JsonElement v) ? v.GetString()! : "latest",
            Namespace: e.GetProperty("namespace").GetString()!,
            Container: e.GetProperty("container").GetString()!,
            // Manifest paths are relative to the manifest, so the tool can be run from
            // anywhere without the paths depending on the caller's working directory.
            Output: Path.GetFullPath(Path.Combine(manifestDir, e.GetProperty("output").GetString()!)),
            Language: e.TryGetProperty("language", out JsonElement l) ? l.GetString()! : "cs",
            Assemblies: assemblies,
            SourceName: e.TryGetProperty("sourceName", out JsonElement sn) ? sn.GetString() : null,
            SourceVersion: e.TryGetProperty("sourceVersion", out JsonElement sv) ? sv.GetString() : null));
    }

    return jobs;
}

// One catalogue, end to end: acquire, read, emit. Only the acquisition differs per source; what
// follows it is the same two calls either way, which is the property the split exists to give.
static async Task<GenerateResult?> GenerateAsync(Job job, string? dateOverride, HttpClient http)
{
    Previous? previous = CatalogParser.ReadPrevious(job.Output);

    if (job.Assemblies is not null)
    {
        AnalyzerAssemblySet? local = LocalAssemblySource.Acquire(job.Assemblies, job.SourceName, job.SourceVersion);

        return local is null ? null : EmitFrom(local);
    }

    // Only a package needs scratch space: it has to be downloaded and unzipped before it can be
    // read, and the directory has to go whether that succeeded, returned nothing, or threw.
    DirectoryInfo work = Directory.CreateTempSubdirectory("cataloggen");
    try
    {
        AnalyzerAssemblySet? fetched =
            await NuGetPackageSource.AcquireAsync(job.Package!, job.Version!, job.Language, work.FullName, http);

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


// Top-level statements place every type declared after them in the global namespace, where
// nothing can reference them explicitly and anything the build pulls in can collide with
// them. A named namespace costs one indent and settles the question.
namespace CatalogGen
{
    internal sealed record Cli(
        string? Package, string? Version, string? Namespace, string? Container, string? Output,
        string? Date, string Language, string? Manifest, string? Summary,
        IReadOnlyList<string> Assemblies, string? SourceName, string? SourceVersion);

    // Package/Version and Assemblies are the two ways to name a source, and exactly one is set —
    // the same shape Cli already uses for its mutually exclusive modes. Assemblies is what decides:
    // when it is set the other two are null and never read.
    internal sealed record Job(
        string? Package, string? Version, string Namespace, string Container, string Output, string Language,
        IReadOnlyList<string>? Assemblies = null, string? SourceName = null, string? SourceVersion = null)
    {
        // What this job reads from, for the run's header line. The assemblies' file names when they
        // are what was asked for, because a path list is what the caller will recognise.
        internal string SourceLabel =>
            Assemblies is null
                ? Package!
                : SourceName ?? string.Join(", ", Assemblies.Select(Path.GetFileName));
    }

    // Title defaults to empty because a rule can genuinely have none to state: one the vendor
    // retired before this generator emitted titles at all is carried forward from a file that
    // never recorded one, and no later run can recover it — the descriptor it came from is gone.
    // The emitter falls back to the identifier and category for those, which is what every rule
    // carried before.
    internal sealed record RuleInfo(string Category, string HelpLinkUri, bool Retired, string Title = "");

    // CategoryNames maps a category's LITERAL to the identifier it was published under — the
    // direction the emitter needs to keep an already-published constant's name stable.
    internal sealed record Previous(
        string SourceVersion,
        SortedDictionary<string, RuleInfo> Rules,
        SortedDictionary<string, string> CategoryNames);

    internal sealed record GenerateResult(bool Changed, string Summary);
}
