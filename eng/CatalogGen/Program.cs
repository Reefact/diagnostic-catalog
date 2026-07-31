using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CatalogGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

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

// .NET Core has no binding redirects. Upstream analyzers are compiled against older
// Roslyn versions, so map every Microsoft.CodeAnalysis request onto the loaded one.
HashSet<string> resolving = new(StringComparer.Ordinal);
AppDomain.CurrentDomain.AssemblyResolve += (_, e) => ResolveAgainstLoaded(e, resolving);
_ = typeof(Workspace); // force Workspaces into the load context before the analyzer needs it

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
        // S6966 asks for WriteLineAsync here and at the other Console.Error site below, but on
        // none of the ~20 Console.WriteLine calls around them — Console.WriteLine is a static
        // method with no async counterpart, while Console.Error is a TextWriter that has one.
        // Both streams are synchronized writers whose async overloads complete synchronously,
        // so awaiting would yield to nothing and leave this tool's diagnostics half-async on a
        // technicality of where the method happens to be declared.
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

// The handler behind the hook above. Answers with the assembly already loaded, and with null —
// meaning "not mine" — for anything outside the Microsoft.CodeAnalysis family, which leaves the
// runtime's own resolution in charge of it.
static Assembly? ResolveAgainstLoaded(ResolveEventArgs e, HashSet<string> resolving)
{
    string? want = new AssemblyName(e.Name).Name;
    if (want is null) return null;
    Assembly? loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == want);
    if (loaded is not null) return loaded;
    if (!want.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)) return null;

    // Assembly.Load raises AssemblyResolve again when it fails, so without this guard a
    // genuinely missing assembly recurses until the stack overflows.
    lock (resolving)
    {
        if (!resolving.Add(want)) return null;
    }
    try { return Assembly.Load(want); }
    catch { return null; }
    finally { lock (resolving) { resolving.Remove(want); } }
}

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

static async Task<GenerateResult?> GenerateAsync(Job job, string? dateOverride, HttpClient http)
{
    string packageId = job.Package;
    string version = job.Version;

    if (version is "latest" or "latest-any")
    {
        string index = await http.GetStringAsync(
            $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json");
        List<string> all = JsonDocument.Parse(index)
            .RootElement.GetProperty("versions").EnumerateArray()
            .Select(v => v.GetString()!).ToList();

        // "latest" means latest *stable*. A catalogue mirrors a release people actually
        // consume; resolving to a preview would silently pin the catalogue to one.
        List<string> candidates = version == "latest" ? all.Where(v => !v.Contains('-')).ToList() : all;
        if (candidates.Count == 0)
        {
            // Synchronous for the reason given at the other Console.Error site above.
#pragma warning disable S6966 // Awaitable method should be used
            Console.Error.WriteLine($"{packageId} has no stable version; use latest-any or an explicit version");
#pragma warning restore S6966
            return null;
        }
        version = candidates[^1];
        Console.WriteLine($"resolved {packageId} => {version}" +
                          (version == all[^1] ? "" : $" (latest overall is {all[^1]}, a prerelease)"));
    }

    Previous? previous = CatalogParser.ReadPrevious(job.Output);

    DirectoryInfo work = Directory.CreateTempSubdirectory("cataloggen");
    try
    {
        string nupkg = Path.Combine(work.FullName, "package.nupkg");
        string url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/{version}/" +
                  $"{packageId.ToLowerInvariant()}.{version}.nupkg";
        Console.WriteLine($"downloading {url}");
        await using (Stream s = await http.GetStreamAsync(url))
        await using (FileStream f = File.Create(nupkg))
            await s.CopyToAsync(f);

        SortedDictionary<string, RuleInfo>? accepted =
            ExtractRules(nupkg, work.FullName, packageId, version, job.Language, out bool ok);
        if (!ok) return null;

        return CatalogEmitter.Emit(job, packageId, version, accepted, previous, dateOverride);
    }
    finally
    {
        work.Delete(recursive: true);
    }
}

static SortedDictionary<string, RuleInfo>? ExtractRules(
    string nupkg, string workDir, string packageId, string version, string language, out bool ok)
{
    ok = false;
    using ZipArchive zip = ZipFile.OpenRead(nupkg);

    List<ZipArchiveEntry>? entries = SelectAnalyzerAssemblies(zip, packageId, version, language);
    if (entries is null) return null;

    foreach (ZipArchiveEntry e in entries)
        e.ExtractToFile(Path.Combine(workDir, Path.GetFileName(e.FullName)), overwrite: true);

    SortedDictionary<string, RuleInfo> accepted = AcceptSuppressable(ReadDescriptors(workDir));

    ok = true;
    return accepted;
}

// The assemblies in the package that carry this language's descriptors, or null when the package
// carries none at all — which is a failure, not an empty result.
static List<ZipArchiveEntry>? SelectAnalyzerAssemblies(
    ZipArchive zip, string packageId, string version, string language)
{
    // Satellite assemblies hold localized rule text, never descriptors, and they sit in
    // culture-named folders that would otherwise be mistaken for language folders — note
    // that "cs" is both C# and Czech.
    List<ZipArchiveEntry> candidateDlls = zip.Entries
        .Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        .Where(e => !e.FullName.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase))
        .Where(e => e.FullName.StartsWith("analyzers/", StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (candidateDlls.Count == 0)
    {
        Console.Error.WriteLine(
            $"no analyzer assemblies under analyzers/ in {packageId} {version}. " +
            "If this is a metapackage, point --package at the one that actually carries them.");
        return null;
    }

    // Layouts differ, and the difference matters. Sonar ships one assembly straight under
    // analyzers/. StyleCop uses analyzers/dotnet/cs/. Microsoft.CodeAnalysis.NetAnalyzers
    // uses BOTH: the language-specific analyzers live under cs/ and vb/, but the bulk of the
    // rules sit in a language-neutral assembly at analyzers/dotnet/.
    //
    // So the rule is to exclude the OTHER languages, never to keep only the requested one:
    // keeping only .../cs/ would silently drop most of the CA rules, and keeping everything
    // would silently absorb Visual Basic rules into a C# catalogue. Both failures are
    // invisible in the output — you would just get a catalogue with the wrong rules in it.
    string[] knownLanguages = ["cs", "vb", "fs"];
    string[] otherLanguages = knownLanguages
        .Where(l => !string.Equals(l, language, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    List<ZipArchiveEntry> excluded = candidateDlls
        .Where(e => otherLanguages.Contains(Naming.ParentDir(e.FullName), StringComparer.OrdinalIgnoreCase))
        .ToList();
    List<ZipArchiveEntry> entries = candidateDlls.Except(excluded).ToList();

    Console.WriteLine($"analyzer assemblies for language '{language}': {entries.Count}");
    foreach (ZipArchiveEntry e in entries) Console.WriteLine($"  + {e.FullName}");
    foreach (ZipArchiveEntry e in excluded) Console.WriteLine($"  - {e.FullName} (other language)");

    return entries;
}

// Descriptors are instance state, so every analyzer type has to be constructed.
static SortedDictionary<string, RuleInfo> ReadDescriptors(string workDir)
{
    SortedDictionary<string, RuleInfo> rules = new(StringComparer.Ordinal);
    int analyzerTypes = 0, constructed = 0;

    foreach (string dll in Directory.GetFiles(workDir, "*.dll"))
    {
        foreach (Type t in AnalyzerTypesIn(dll))
        {
            analyzerTypes++;
            if (TryAddDescriptors(t, rules)) constructed++;
        }
    }

    Console.WriteLine($"analyzer types: {analyzerTypes}, constructed: {constructed}, descriptors: {rules.Count}");
    if (constructed != analyzerTypes)
        Console.WriteLine($"WARNING: {analyzerTypes - constructed} analyzer type(s) could not be constructed");

    return rules;
}

static IEnumerable<Type> AnalyzerTypesIn(string dll)
{
    Assembly asm;
    // S3885 asks for Assembly.Load. It cannot do this: Load resolves an assembly by NAME through
    // the runtime's probing paths, and this path is a file the process extracted moments ago into
    // its own temp directory, deliberately outside them. LoadFrom is the API that takes a path —
    // the required one here, not a lax alternative. What makes the upstream assembly's older
    // Roslyn references resolve is the AssemblyResolve handler above, not the choice of loader.
#pragma warning disable S3885 // "Assembly.Load" should be used
    try { asm = Assembly.LoadFrom(dll); } catch { return []; }
#pragma warning restore S3885

    Type[] types;
    try { types = asm.GetTypes(); }
    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }

    return types.Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t));
}

// True when the type was constructed, whether or not it declared any descriptor. An analyzer that
// cannot be constructed contributes none; the caller counts the difference so it stays visible
// rather than being silently absorbed.
static bool TryAddDescriptors(Type analyzer, SortedDictionary<string, RuleInfo> rules)
{
    try
    {
        DiagnosticAnalyzer instance = (DiagnosticAnalyzer)Activator.CreateInstance(analyzer)!;
        foreach (DiagnosticDescriptor d in instance.SupportedDiagnostics)
        {
            // A title is a LocalizableString, and the .NET analyzers back theirs with resources.
            // Formatting one against the current culture would make the generated catalogue depend
            // on the machine that produced it, which is the one property a generated file may not
            // have: the same upstream release has to yield the same bytes on a maintainer's laptop
            // and on the nightly runner.
            rules[d.Id] = new RuleInfo(
                d.Category,
                d.HelpLinkUri ?? string.Empty,
                Retired: false,
                Naming.Sentence(d.Title.ToString(CultureInfo.InvariantCulture)));
        }

        return true;
    }
    catch
    {
        return false;
    }
}

// Filtering. Only two things disqualify a descriptor, and both are reported: an empty
// category means the entry is not a suppressable diagnostic (analyzers use such entries
// for internal metrics and telemetry channels), and a non-identifier id would need a
// mangled container name.
static SortedDictionary<string, RuleInfo> AcceptSuppressable(SortedDictionary<string, RuleInfo> rules)
{
    SortedDictionary<string, RuleInfo> accepted = new(StringComparer.Ordinal);
    List<(string Id, string Reason)> skipped = [];
    foreach ((string id, RuleInfo info) in rules)
    {
        if (string.IsNullOrWhiteSpace(info.Category)) { skipped.Add((id, "empty category — not a suppressable diagnostic")); continue; }
        if (!SyntaxFacts.IsValidIdentifier(id)) { skipped.Add((id, "id is not a valid C# identifier")); continue; }
        accepted[id] = info;
    }

    int withHelp = rules.Count(r => !string.IsNullOrEmpty(r.Value.HelpLinkUri));
    Console.WriteLine($"accepted: {accepted.Count}, skipped: {skipped.Count}, HelpLinkUri populated on {withHelp}/{rules.Count}");
    foreach ((string id, string reason) in skipped) Console.WriteLine($"  skipped {id}: {reason}");

    return accepted;
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
