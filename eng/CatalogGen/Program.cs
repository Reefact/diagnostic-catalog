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
//   acquire   NuGetPackageSource  → AnalyzerAssemblySet   (where the assemblies come from)
//   read      DescriptorReader    → the rules they declare (loads and constructs third-party code)
//   emit      CatalogEmitter      → the catalogue as C# source
//
// Only the first differs per source. A second way of obtaining analyzer assemblies is a new
// acquisition next to NuGetPackageSource; it is never a second reader.
//
// Usage:
//   dotnet run --project eng/CatalogGen -- --manifest eng/catalogs.json
//   dotnet run --project eng/CatalogGen -- \
//       --package SonarAnalyzer.CSharp --version latest \
//       --namespace DiagnosticCatalog.Sonar --container SonarRule \
//       --output src/DiagnosticCatalog.Sonar/SonarRules.g.cs \
//       [--date 2026-07-30] [--language cs] [--summary out.md]
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
    jobs = [new Job(cli.Package!, cli.Version!, cli.Namespace!, cli.Container!,
                    Path.GetFullPath(cli.Output!), cli.Language)];
}

using HttpClient http = new();
List<string> summaries = [];
bool changedAny = false;
int exitCode = 0;

foreach (Job job in jobs)
{
    Console.WriteLine();
    Console.WriteLine($"=== {job.Namespace} <- {job.Package} ===");
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
        jobs.Add(new Job(
            Package: e.GetProperty("package").GetString()!,
            Version: e.TryGetProperty("version", out JsonElement v) ? v.GetString()! : "latest",
            Namespace: e.GetProperty("namespace").GetString()!,
            Container: e.GetProperty("container").GetString()!,
            // Manifest paths are relative to the manifest, so the tool can be run from
            // anywhere without the paths depending on the caller's working directory.
            Output: Path.GetFullPath(Path.Combine(manifestDir, e.GetProperty("output").GetString()!)),
            Language: e.TryGetProperty("language", out JsonElement l) ? l.GetString()! : "cs"));
    }

    return jobs;
}

// One catalogue, end to end: acquire, read, emit. The temp directory belongs here rather than to
// the acquisition, because it is the run's scratch space and has to be cleaned up whether the
// acquisition succeeded, returned nothing, or threw.
static async Task<GenerateResult?> GenerateAsync(Job job, string? dateOverride, HttpClient http)
{
    Previous? previous = CatalogParser.ReadPrevious(job.Output);

    DirectoryInfo work = Directory.CreateTempSubdirectory("cataloggen");
    try
    {
        AnalyzerAssemblySet? source =
            await NuGetPackageSource.AcquireAsync(job.Package, job.Version, job.Language, work.FullName, http);
        if (source is null) return null;

        SortedDictionary<string, RuleInfo> accepted = DescriptorReader.Read(source);

        return CatalogEmitter.Emit(job, source.SourceName, source.SourceVersion, accepted, previous, dateOverride);
    }
    finally
    {
        work.Delete(recursive: true);
    }
}


// Top-level statements place every type declared after them in the global namespace, where
// nothing can reference them explicitly and anything the build pulls in can collide with
// them. A named namespace costs one indent and settles the question.
namespace CatalogGen
{
    internal sealed record Cli(
        string? Package, string? Version, string? Namespace, string? Container, string? Output,
        string? Date, string Language, string? Manifest, string? Summary);

    internal sealed record Job(
        string Package, string Version, string Namespace, string Container, string Output, string Language);

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
