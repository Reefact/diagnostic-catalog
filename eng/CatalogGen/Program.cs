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
AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
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
};
_ = typeof(Workspace); // force Workspaces into the load context before the analyzer needs it

List<Job> jobs = [];
if (cli.Manifest is not null)
{
    string manifestDir = Path.GetDirectoryName(Path.GetFullPath(cli.Manifest))!;
    using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(cli.Manifest));
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
    Console.WriteLine($"manifest {cli.Manifest}: {jobs.Count} catalogue(s)");
}
else
{
    jobs.Add(new Job(cli.Package!, cli.Version!, cli.Namespace!, cli.Container!,
                     Path.GetFullPath(cli.Output!), cli.Language));
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
        Console.Error.WriteLine($"FAILED {job.Namespace}: {ex.GetType().Name}: {ex.Message}");
        exitCode = 1;
    }
}

if (cli.Summary is not null)
{
    string body = changedAny
        ? string.Join("\n", summaries)
        : "No catalogue changed: every upstream package still resolves to the version already mirrored.";
    File.WriteAllText(Path.GetFullPath(cli.Summary), body.ReplaceLineEndings("\n") + "\n",
                      new UTF8Encoding(false));
    Console.WriteLine();
    Console.WriteLine($"summary written to {cli.Summary}");
}

Console.WriteLine();
Console.WriteLine(changedAny ? "RESULT: catalogues changed" : "RESULT: no change");
return exitCode;

// ---------------------------------------------------------------------------

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
            Console.Error.WriteLine($"{packageId} has no stable version; use latest-any or an explicit version");
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

    foreach (ZipArchiveEntry e in entries)
        e.ExtractToFile(Path.Combine(workDir, Path.GetFileName(e.FullName)), overwrite: true);

    // Descriptors are instance state, so every analyzer type has to be constructed.
    SortedDictionary<string, RuleInfo> rules = new(StringComparer.Ordinal);
    int analyzerTypes = 0, constructed = 0;

    foreach (string dll in Directory.GetFiles(workDir, "*.dll"))
    {
        Assembly asm;
        try { asm = Assembly.LoadFrom(dll); } catch { continue; }

        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).ToArray()!; }

        foreach (Type t in types)
        {
            if (t.IsAbstract || !typeof(DiagnosticAnalyzer).IsAssignableFrom(t)) continue;
            analyzerTypes++;
            try
            {
                DiagnosticAnalyzer instance = (DiagnosticAnalyzer)Activator.CreateInstance(t)!;
                foreach (DiagnosticDescriptor d in instance.SupportedDiagnostics)
                    rules[d.Id] = new RuleInfo(d.Category, d.HelpLinkUri ?? string.Empty, Retired: false);
                constructed++;
            }
            catch
            {
                // An analyzer that cannot be constructed contributes no descriptors. Counted
                // below so the difference is visible rather than silently absorbed.
            }
        }
    }

    Console.WriteLine($"analyzer types: {analyzerTypes}, constructed: {constructed}, descriptors: {rules.Count}");
    if (constructed != analyzerTypes)
        Console.WriteLine($"WARNING: {analyzerTypes - constructed} analyzer type(s) could not be constructed");

    // Filtering. Only two things disqualify a descriptor, and both are reported: an empty
    // category means the entry is not a suppressable diagnostic (analyzers use such entries
    // for internal metrics and telemetry channels), and a non-identifier id would need a
    // mangled container name.
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

    ok = true;
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

    internal sealed record RuleInfo(string Category, string HelpLinkUri, bool Retired);

    // CategoryNames maps a category's LITERAL to the identifier it was published under — the
    // direction the emitter needs to keep an already-published constant's name stable.
    internal sealed record Previous(
        string SourceVersion,
        SortedDictionary<string, RuleInfo> Rules,
        SortedDictionary<string, string> CategoryNames);

    internal sealed record GenerateResult(bool Changed, string Summary);
}
