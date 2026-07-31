using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CatalogGen;

// The reading stage: analyzer assemblies in, the rules they declare out.
//
// It knows nothing about where the assemblies came from — it is handed paths — and that is the
// point. This is the one stage that must never be duplicated per acquisition strategy: it loads
// third-party code into this process and constructs it, and its failure mode is silence. An
// analyzer that cannot be constructed contributes no descriptor, and a catalogue short of a rule
// is indistinguishable from a catalogue whose vendor removed it. One copy of that risk, exercised
// by every source, is the whole reason the seam sits where it does.
internal static class DescriptorReader
{
    // .NET Core has no binding redirects. Upstream analyzers are compiled against older Roslyn
    // versions, so map every Microsoft.CodeAnalysis request onto the loaded one.
    //
    // Installed once for the process rather than per read: the handler answers from the assemblies
    // already loaded, so a second registration would only add a redundant hop to every resolution.
    // Idempotent because the run is now callable rather than a process entry point, and a caller
    // that runs twice must not stack a second handler onto the first.
    private static bool resolverInstalled;

    internal static void InstallAssemblyResolver()
    {
        if (resolverInstalled) return;
        resolverInstalled = true;

        HashSet<string> resolving = new(StringComparer.Ordinal);
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => ResolveAgainstLoaded(e, resolving);
        _ = typeof(Workspace); // force Workspaces into the load context before the analyzer needs it
    }

    // The handler behind the hook above. Answers with the assembly already loaded, and with null —
    // meaning "not mine" — for anything outside the Microsoft.CodeAnalysis family, which leaves the
    // runtime's own resolution in charge of it.
    private static Assembly? ResolveAgainstLoaded(ResolveEventArgs e, HashSet<string> resolving)
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

    // The rules declared by one acquisition's assemblies, filtered down to the suppressable ones.
    // Null when the read was incomplete — see ReadDescriptors for why that is refused rather than
    // reported. Filtering a shortfall would only make it harder to see.
    internal static SortedDictionary<string, RuleInfo>? Read(AnalyzerAssemblySet source)
    {
        SortedDictionary<string, RuleInfo>? declared = ReadDescriptors(source.AssemblyPaths);

        return declared is null ? null : AcceptSuppressable(declared);
    }

    // Descriptors are instance state, so every analyzer type has to be constructed.
    //
    // Null when anything was dropped along the way, which is the whole contract of this method. A
    // rule the reader failed to reach is absent from the catalogue, and an absent rule is
    // indistinguishable from one the vendor retired — so the emitter carries it forward and states
    // in an [Obsolete] message that the vendor no longer declares it. A partial read does not
    // produce a catalogue that is merely short; it produces one that is wrong about somebody
    // else's product, and says so to that product's users.
    //
    // Nothing downstream can catch it. The platform never validates a suppression's category
    // (specification §3.2), so a catalogue that lost rules produces no symptom in any consumer's
    // build, ever. This is the last place the shortfall is still known, which is why it stops here
    // — the behaviour ADR-0009 calls "a generation that stops rather than guesses".
    private static SortedDictionary<string, RuleInfo>? ReadDescriptors(IReadOnlyList<string> assemblyPaths)
    {
        SortedDictionary<string, RuleInfo> rules = new(StringComparer.Ordinal);
        List<string> dropped = [];
        int analyzerTypes = 0, constructed = 0;

        foreach (string dll in assemblyPaths)
        {
            foreach (Type t in AnalyzerTypesIn(dll, dropped))
            {
                analyzerTypes++;
                if (TryAddDescriptors(t, rules)) constructed++;
                else dropped.Add($"{t.FullName ?? t.Name}: could not be constructed");
            }
        }

        Console.WriteLine($"analyzer types: {analyzerTypes}, constructed: {constructed}, descriptors: {rules.Count}");
        if (dropped.Count == 0) return rules;

        Console.Error.WriteLine($"INCOMPLETE READ: {dropped.Count} item(s) could not be read:");
        foreach (string reason in dropped) Console.Error.WriteLine($"  {reason}");
        Console.Error.WriteLine(
            "Refusing to emit. The rules these declare would be missing from the catalogue, and a " +
            "missing rule is indistinguishable from a retired one: they would be published as " +
            "[Obsolete], telling consumers the vendor no longer declares them. If an upstream " +
            "release changed shape, fix the read rather than accept the shortfall.");

        return null;
    }

    // Appends to <paramref name="dropped"/> whatever this assembly could not yield, so the caller
    // can refuse a read that lost something. An assembly that fails to load entirely is the worst
    // case and the quietest: it contributes no analyzer type at all, so no count anywhere is short.
    private static IEnumerable<Type> AnalyzerTypesIn(string dll, List<string> dropped)
    {
        Assembly asm;
        // S3885 asks for Assembly.Load. It cannot do this: Load resolves an assembly by NAME through
        // the runtime's probing paths, and this path is a file the process extracted moments ago into
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
    private static bool TryAddDescriptors(Type analyzer, SortedDictionary<string, RuleInfo> rules)
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
    private static SortedDictionary<string, RuleInfo> AcceptSuppressable(SortedDictionary<string, RuleInfo> rules)
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
}
