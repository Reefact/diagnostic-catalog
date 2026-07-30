using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

Cli? cli = ParseArgs(args);
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

    Previous? previous = ReadPrevious(job.Output);

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

        return Emit(job, packageId, version, accepted, previous, dateOverride);
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
        .Where(e => otherLanguages.Contains(ParentDir(e.FullName), StringComparer.OrdinalIgnoreCase))
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

static GenerateResult Emit(
    Job job, string packageId, string version,
    SortedDictionary<string, RuleInfo>? upstream, Previous? previous, string? dateOverride)
{
    SortedDictionary<string, RuleInfo> accepted = new(upstream!, StringComparer.Ordinal);

    // §23.1: a constant is never deleted. Consumers inline const values at their own
    // compile time, so removing one breaks their recompilation. A rule that upstream has
    // retired is carried forward and marked [Obsolete] instead — a warning they can act
    // on, rather than a missing member they cannot.
    List<string> retired = [];
    if (previous is not null)
    {
        foreach ((string id, RuleInfo info) in previous.Rules)
        {
            if (accepted.ContainsKey(id)) continue;
            accepted[id] = info with { Retired = true };
            if (!info.Retired) retired.Add(id);
        }
    }

    List<KeyValuePair<string, RuleInfo>> live = accepted.Where(r => !r.Value.Retired).ToList();
    List<string> categories = accepted.Values.Select(v => v.Category).Distinct()
        .OrderBy(c => c, StringComparer.Ordinal).ToList();
    Console.WriteLine($"distinct categories ({categories.Count}): {string.Join(", ", categories)}");

    // A catalogue repeats very few distinct categories across very many rules — Sonar spends
    // 456 declarations on 13 values. Declare each once and have the rules refer to it: a
    // const initialised from another const is still a compile-time constant, so the rules
    // stay usable as attribute arguments and still fold to the literal in metadata.
    string categoryContainer = job.Container.EndsWith("Rule", StringComparison.Ordinal)
        ? job.Container[..^"Rule".Length] + "Category"
        : job.Container + "Category";

    Dictionary<string, string> categoryNames = new(StringComparer.Ordinal);
    HashSet<string> usedNames = new(StringComparer.Ordinal);
    foreach (string c in categories)
    {
        string baseName = ToIdentifier(c);
        string name = baseName;
        // Deterministic disambiguation: two categories differing only in punctuation would
        // otherwise silently collapse onto one constant.
        for (int n = 2; !usedNames.Add(name); n++) name = baseName + n.ToString(CultureInfo.InvariantCulture);
        if (name != baseName) Console.WriteLine($"  note: category '{c}' renamed to {name} to avoid a collision");
        categoryNames[c] = name;
    }

    // --- what actually changed -------------------------------------------------
    List<string> added = accepted.Keys.Where(id => previous is null || !previous.Rules.ContainsKey(id)).ToList();
    List<(string Id, string From, string To)> recategorised = previous is null
        ? []
        : accepted.Where(r => previous.Rules.TryGetValue(r.Key, out RuleInfo? old)
                              && !string.Equals(old.Category, r.Value.Category, StringComparison.Ordinal))
                  .Select(r => (Id: r.Key, From: previous.Rules[r.Key].Category, To: r.Value.Category))
                  .ToList();

    bool versionChanged = previous is null || !string.Equals(previous.SourceVersion, version, StringComparison.Ordinal);
    bool rulesChanged = added.Count > 0 || retired.Count > 0 || recategorised.Count > 0;

    // The date only moves when something else did. Bumping it on every run would make the
    // scheduled job open a pull request every night whose only content was a new date.
    if (previous is not null && !versionChanged && !rulesChanged)
    {
        Console.WriteLine($"unchanged: {packageId} {version}, {live.Count} rules — file left untouched");
        return new GenerateResult(Changed: false, Summary: string.Empty);
    }

    string date = dateOverride ?? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ---------------------------------------------------------------------------
    // Emit. Output is ordered deterministically so a regeneration produces a diff that
    // shows only genuine upstream change.
    // ---------------------------------------------------------------------------
    StringBuilder sb = new();
    sb.AppendLine("// <auto-generated>");
    sb.AppendLine("//     Generated by eng/CatalogGen from the DiagnosticDescriptor instances declared by");
    sb.AppendLine($"//     {packageId} {version} (language: {job.Language}).");
    sb.AppendLine("//     Do not edit by hand: rerun the generator.");
    sb.AppendLine("//");
    sb.AppendLine("//     Only Id, Category and HelpLinkUri are emitted, and only when the descriptor");
    sb.AppendLine("//     actually supplies them. All are facts read from the descriptors. Rule titles");
    sb.AppendLine("//     and descriptions are the upstream vendor's authored content and are");
    sb.AppendLine("//     deliberately not redistributed here.");
    sb.AppendLine("// </auto-generated>");
    sb.AppendLine();
    sb.AppendLine("using System;");
    sb.AppendLine("using DiagnosticCatalog;");
    sb.AppendLine();
    sb.AppendLine("[assembly: CatalogSource(");
    sb.AppendLine($"    source:        \"{packageId}\",");
    sb.AppendLine($"    sourceVersion: \"{version}\",");
    sb.AppendLine($"    generatedOn:   \"{date}\")]");
    sb.AppendLine();
    sb.AppendLine($"namespace {job.Namespace};");
    sb.AppendLine();
    sb.AppendLine("/// <summary>");
    sb.AppendLine($"/// The diagnostic categories used by {packageId}, declared once each.");
    sb.AppendLine("/// </summary>");
    sb.AppendLine("[DiagnosticCategory]");
    sb.AppendLine($"public static class {categoryContainer}");
    sb.AppendLine("{");
    bool firstCategory = true;
    foreach (string c in categories)
    {
        if (!firstCategory) sb.AppendLine();
        firstCategory = false;
        sb.AppendLine($"    /// <summary>The <c>{Escape(c)}</c> category.</summary>");
        sb.AppendLine($"    public const string {categoryNames[c]} = \"{Escape(c)}\";");
    }
    sb.AppendLine("}");
    sb.AppendLine();
    sb.AppendLine("/// <summary>");
    sb.AppendLine($"/// The {packageId} diagnostic rules, as declared by that package's analyzers.");
    sb.AppendLine("/// </summary>");
    sb.AppendLine($"public static class {job.Container}");
    sb.AppendLine("{");

    bool first = true;
    foreach ((string id, RuleInfo info) in accepted)
    {
        if (!first) sb.AppendLine();
        first = false;
        bool hasHelp = !string.IsNullOrWhiteSpace(info.HelpLinkUri);
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Rule <c>{id}</c>, category <c>{Escape(info.Category)}</c>.");
        if (info.Retired)
            sb.AppendLine($"    /// No longer declared by {packageId} as of {version}.");
        if (hasHelp) sb.AppendLine($"    /// See <see href=\"{Escape(info.HelpLinkUri)}\"/>.");
        sb.AppendLine("    /// </summary>");
        if (info.Retired)
            sb.AppendLine($"    [Obsolete(\"{Escape(id)} is no longer declared by {packageId} as of {version}. " +
                          "Kept so that removing it does not break recompilation; remove your suppression.\")]");
        sb.AppendLine("    [DiagnosticRule]");
        sb.AppendLine($"    public static class {id}");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>The canonical identifier of this diagnostic.</summary>");
        sb.AppendLine($"        public const string Id = nameof({id});");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>");
        sb.AppendLine($"        public const string Category = {categoryContainer}.{categoryNames[info.Category]};");
        if (hasHelp)
        {
            sb.AppendLine();
            sb.AppendLine("        /// <summary>The help link declared by the analyzer's DiagnosticDescriptor.</summary>");
            sb.AppendLine($"        public const string HelpLinkUri = \"{Escape(info.HelpLinkUri)}\";");
        }
        sb.AppendLine("    }");
    }

    sb.AppendLine("}");

    Directory.CreateDirectory(Path.GetDirectoryName(job.Output)!);
    File.WriteAllText(job.Output, sb.ToString().ReplaceLineEndings("\n"), new UTF8Encoding(false));
    Console.WriteLine($"wrote {live.Count} live rules " +
                      $"({accepted.Count - live.Count} retired) to {job.Output}");

    // --- human-readable summary for the pull request ---------------------------
    StringBuilder md = new();
    string fromTo = previous is null
        ? version
        : versionChanged ? $"{previous.SourceVersion} → {version}" : version;
    md.AppendLine($"#### {job.Namespace} — {packageId} {fromTo}");
    md.AppendLine();
    if (!rulesChanged)
    {
        md.AppendLine("No rule changes. Only the mirrored upstream version moved.");
    }
    else
    {
        if (added.Count > 0)
        {
            md.AppendLine($"**Added ({added.Count}):**");
            foreach (string id in added.Take(50))
                md.AppendLine($"- `{id}` — {accepted[id].Category}");
            if (added.Count > 50) md.AppendLine($"- …and {added.Count - 50} more");
            md.AppendLine();
        }
        if (recategorised.Count > 0)
        {
            md.AppendLine($"**Recategorised ({recategorised.Count}):**");
            foreach ((string Id, string From, string To) r in recategorised)
                md.AppendLine($"- `{r.Id}` — {r.From} → {r.To}");
            md.AppendLine();
        }
        if (retired.Count > 0)
        {
            md.AppendLine($"**Retired upstream ({retired.Count}) — kept and marked `[Obsolete]`:**");
            foreach (string id in retired)
                md.AppendLine($"- `{id}`");
            md.AppendLine();
            md.AppendLine("> Constants are never deleted: consumers inline them, so removing one breaks " +
                          "their recompilation. Deleting these is a major version (§23.1).");
            md.AppendLine();
        }
    }
    md.AppendLine($"{live.Count} live rules, {categories.Count} categories.");

    return new GenerateResult(Changed: true, Summary: md.ToString().TrimEnd() + "\n");
}

// Parses a previously generated file back into rules. The format is fixed and emitted by
// this same tool, so the parse is reliable — and it avoids carrying a second artefact
// next to the .g.cs purely to remember what the last run produced.
static Previous? ReadPrevious(string path)
{
    if (!File.Exists(path)) return null;
    string text = File.ReadAllText(path);

    string sourceVersion = Regex.Match(text, @"sourceVersion:\s*""([^""]*)""").Groups[1].Value;

    // 4-space indent is the category class; rule members sit at 8.
    Dictionary<string, string> categoryLiterals = Regex.Matches(text, @"^    public const string (\w+) = ""((?:[^""\\]|\\.)*)"";$",
                                         RegexOptions.Multiline)
        .ToDictionary(m => m.Groups[1].Value, m => Unescape(m.Groups[2].Value), StringComparer.Ordinal);

    SortedDictionary<string, RuleInfo> rules = new(StringComparer.Ordinal);
    MatchCollection blocks = Regex.Matches(
        text,
        @"^(?<obsolete>    \[Obsolete\([^\n]*\)\]\n)?    \[DiagnosticRule\]\n    public static class (?<id>\w+)\n    \{\n(?<body>(?:.*\n)*?)    \}$",
        RegexOptions.Multiline);

    foreach (Match b in blocks)
    {
        string id = b.Groups["id"].Value;
        string body = b.Groups["body"].Value;
        Match catRef = Regex.Match(body, @"public const string Category = \w+\.(\w+);");
        if (!catRef.Success || !categoryLiterals.TryGetValue(catRef.Groups[1].Value, out string? category))
            continue;
        Match help = Regex.Match(body, @"public const string HelpLinkUri = ""((?:[^""\\]|\\.)*)"";");
        rules[id] = new RuleInfo(category, help.Success ? Unescape(help.Groups[1].Value) : string.Empty,
                                 Retired: b.Groups["obsolete"].Success);
    }

    Console.WriteLine($"previous: {packageOrEmpty(sourceVersion)}{rules.Count} rules " +
                      $"({rules.Count(r => r.Value.Retired)} already retired)");
    return new Previous(sourceVersion, rules);

    static string packageOrEmpty(string v) => string.IsNullOrEmpty(v) ? "" : $"{v}, ";
}

static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
static string Unescape(string s) => s.Replace("\\\"", "\"").Replace("\\\\", "\\");

static string ParentDir(string path)
{
    int i = path.LastIndexOf('/');
    if (i < 0) return string.Empty;
    int j = path.LastIndexOf('/', i - 1);
    return j < 0 ? path[..i] : path[(j + 1)..i];
}

// Mechanical, and deliberately not clever. Stripping a common prefix would read better —
// StyleCop's categories all start with "StyleCop.CSharp." — but the common prefix changes
// the moment upstream adds a category outside it, which would rename every existing
// constant and break every consumer that referenced one (§23.1).
static string ToIdentifier(string value)
{
    StringBuilder sb = new();
    bool upperNext = true;
    foreach (char ch in value)
    {
        if (char.IsLetterOrDigit(ch))
        {
            sb.Append(upperNext ? char.ToUpperInvariant(ch) : ch);
            upperNext = false;
        }
        else
        {
            upperNext = true;
        }
    }
    string result = sb.ToString();
    if (result.Length == 0) return "Unnamed";
    return char.IsDigit(result[0]) ? "_" + result : result;
}

static Cli? ParseArgs(string[] args)
{
    string? package = null, version = null, ns = null, container = null, output = null,
            date = null, language = null, manifest = null, summary = null;
    for (int i = 0; i + 1 < args.Length; i += 2)
    {
        switch (args[i])
        {
            case "--package": package = args[i + 1]; break;
            case "--version": version = args[i + 1]; break;
            case "--namespace": ns = args[i + 1]; break;
            case "--container": container = args[i + 1]; break;
            case "--output": output = args[i + 1]; break;
            case "--date": date = args[i + 1]; break;
            case "--language": language = args[i + 1]; break;
            case "--manifest": manifest = args[i + 1]; break;
            case "--summary": summary = args[i + 1]; break;
            default:
                Console.Error.WriteLine($"unknown argument: {args[i]}");
                return null;
        }
    }

    bool singleComplete = package is not null && version is not null && ns is not null
                         && container is not null && output is not null;
    if (manifest is null && !singleComplete)
    {
        Console.Error.WriteLine(
            "usage: --manifest <catalogs.json> [--date yyyy-MM-dd] [--summary out.md]\n" +
            "   or: --package <id> --version <v|latest|latest-any> --namespace <ns> " +
            "--container <name> --output <path.g.cs> [--date yyyy-MM-dd] [--language cs] [--summary out.md]");
        return null;
    }

    // The date may be pinned so that regenerating the same inputs twice is byte-identical.
    // Left unset it defaults to today, which only ever reaches the file when something
    // else changed too.
    return new Cli(package, version, ns, container, output, date, language ?? "cs", manifest, summary);
}

internal sealed record Cli(
    string? Package, string? Version, string? Namespace, string? Container, string? Output,
    string? Date, string Language, string? Manifest, string? Summary);

internal sealed record Job(
    string Package, string Version, string Namespace, string Container, string Output, string Language);

internal sealed record RuleInfo(string Category, string HelpLinkUri, bool Retired);

internal sealed record Previous(string SourceVersion, SortedDictionary<string, RuleInfo> Rules);

internal sealed record GenerateResult(bool Changed, string Summary);
