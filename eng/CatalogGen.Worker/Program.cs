using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CatalogGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

// ---------------------------------------------------------------------------
// CatalogGen.Worker — reads the DiagnosticDescriptor instances declared by a set of analyzer
// assemblies, and reports what it could not read.
//
// It is a separate process, and that is the whole point of it. Reading descriptors means loading
// somebody else's analyzer assemblies and CONSTRUCTING them: running third-party code that was
// compiled against a Roslyn this repository does not choose, on a runtime it does not choose
// either. Two things follow, and neither is available in-process:
//
//   * The runtime can follow the assemblies rather than the tool. `dcat` is floored at net8.0 so
//     it installs widely (ADR-0017), and a net8.0 process cannot load an analyzer built for a
//     newer target. This worker rolls forward to the LATEST installed major instead, so the floor
//     that makes the tool installable stops deciding what it can read.
//
//   * A failure stays inside it. An analyzer whose construction overflows the stack or kills the
//     process takes this worker down and leaves `dcat` to report it, rather than disappearing
//     mid-run with no output.
//
// stdout and stderr are the run's diagnostics and are relayed verbatim by the caller, so the log
// reads exactly as it did when this ran in-process. The rules themselves travel as JSON, because a
// console is not a data channel.
//
// Usage: CatalogGen.Worker <request.json> <response.json>
// ---------------------------------------------------------------------------

if (args.Length != 2)
{
    Console.Error.WriteLine("usage: CatalogGen.Worker <request.json> <response.json>");

    return WorkerExitCodes.UsageError;
}

// .NET Core has no binding redirects. Upstream analyzers are compiled against older Roslyn
// versions, so map every Microsoft.CodeAnalysis request onto the loaded one.
HashSet<string> resolving = new(StringComparer.Ordinal);
AppDomain.CurrentDomain.AssemblyResolve += (_, e) => ResolveAgainstLoaded(e, resolving);
_ = typeof(Workspace); // force Workspaces into the load context before the analyzer needs it

DescriptorReadRequest? request =
    JsonSerializer.Deserialize<DescriptorReadRequest>(await File.ReadAllTextAsync(args[0]));
if (request is null)
{
    Console.Error.WriteLine($"unreadable request: {args[0]}");

    return WorkerExitCodes.UsageError;
}

SortedDictionary<string, ReadRule> rules = new(StringComparer.Ordinal);
List<string> dropped = [];
int analyzerTypes = 0, constructed = 0;

foreach (string dll in request.AssemblyPaths)
{
    foreach (Type t in AnalyzerTypesIn(dll, dropped))
    {
        analyzerTypes++;
        if (TryAddDescriptors(t, rules)) constructed++;
        else dropped.Add($"{t.FullName ?? t.Name}: could not be constructed");
    }
}

Console.WriteLine($"analyzer types: {analyzerTypes}, constructed: {constructed}, descriptors: {rules.Count}");

// A rule the reader failed to reach is absent from the catalogue, and an absent rule is
// indistinguishable from one the vendor retired — so the emitter carries it forward and states in
// an [Obsolete] message that the vendor no longer declares it. A partial read does not produce a
// catalogue that is merely short; it produces one that is wrong about somebody else's product, and
// says so to that product's users.
//
// Nothing downstream can catch it. The platform never validates a suppression's category
// (specification §3.2), so a catalogue that lost rules produces no symptom in any consumer's build,
// ever. This is the last place the shortfall is still known, which is why it stops here — the
// behaviour ADR-0009 calls "a generation that stops rather than guesses".
if (dropped.Count > 0)
{
    Console.Error.WriteLine($"INCOMPLETE READ: {dropped.Count} item(s) could not be read:");
    foreach (string reason in dropped) Console.Error.WriteLine($"  {reason}");
    Console.Error.WriteLine(
        "Refusing to emit. The rules these declare would be missing from the catalogue, and a " +
        "missing rule is indistinguishable from a retired one: they would be published as " +
        "[Obsolete], telling consumers the vendor no longer declares them. If an upstream " +
        "release changed shape, fix the read rather than accept the shortfall.");

    return WorkerExitCodes.IncompleteRead;
}

DescriptorReadResponse response = new() { Rules = AcceptSuppressable(rules) };
await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(response));

return WorkerExitCodes.Complete;

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

// Appends to dropped whatever this assembly could not yield, so the run can refuse a read that lost
// something. An assembly that fails to load entirely is the worst case and the quietest: it
// contributes no analyzer type at all, so no count anywhere is short.
static IEnumerable<Type> AnalyzerTypesIn(string dll, List<string> dropped)
{
    Assembly asm;
    // S3885 asks for Assembly.Load. It cannot do this: Load resolves an assembly by NAME through
    // the runtime's probing paths, and this path is a file the caller extracted moments ago into
    // its own temp directory, deliberately outside them. LoadFrom is the API that takes a path —
    // the required one here, not a lax alternative. What makes the upstream assembly's older
    // Roslyn references resolve is the AssemblyResolve handler above, not the choice of loader.
    //
    // The catch is broad because the failure set is: LoadFrom answers a malformed file, an
    // unreadable one and a refused one with three different exception types, and every one of
    // them means the same thing here — this assembly's rules did not arrive.
#pragma warning disable S3885 // "Assembly.Load" should be used
    try { asm = Assembly.LoadFrom(dll); }
#pragma warning restore S3885
    catch (Exception ex)
    {
        dropped.Add($"{Path.GetFileName(dll)}: could not be loaded ({ex.GetType().Name}: {ex.Message})");

        return [];
    }

    Type[] types;
    try { types = asm.GetTypes(); }
    catch (ReflectionTypeLoadException ex)
    {
        // The types that did load are still worth reading; the ones that did not are the reason
        // the run will refuse. An analyzer among them would otherwise vanish without a trace.
        types = ex.Types.Where(t => t is not null).ToArray()!;
        dropped.Add($"{Path.GetFileName(dll)}: {ex.LoaderExceptions.Length} type(s) could not be loaded " +
                    $"({ex.LoaderExceptions.FirstOrDefault()?.Message ?? "no detail given"})");
    }

    return types.Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t));
}

// True when the type was constructed, whether or not it declared any descriptor. An analyzer that
// cannot be constructed contributes none; the caller counts the difference so it stays visible
// rather than being silently absorbed.
static bool TryAddDescriptors(Type analyzer, SortedDictionary<string, ReadRule> rules)
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
            rules[d.Id] = new ReadRule
            {
                Category = d.Category,
                HelpLinkUri = d.HelpLinkUri ?? string.Empty,
                Title = Naming.Sentence(d.Title.ToString(CultureInfo.InvariantCulture)),
            };
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
static Dictionary<string, ReadRule> AcceptSuppressable(SortedDictionary<string, ReadRule> rules)
{
    Dictionary<string, ReadRule> accepted = new(StringComparer.Ordinal);
    List<(string Id, string Reason)> skipped = [];
    foreach ((string id, ReadRule info) in rules)
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
