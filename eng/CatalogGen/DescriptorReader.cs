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
    internal static void InstallAssemblyResolver()
    {
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
    internal static SortedDictionary<string, RuleInfo> Read(AnalyzerAssemblySet source) =>
        AcceptSuppressable(ReadDescriptors(source.AssemblyPaths));

    // Descriptors are instance state, so every analyzer type has to be constructed.
    private static SortedDictionary<string, RuleInfo> ReadDescriptors(IReadOnlyList<string> assemblyPaths)
    {
        SortedDictionary<string, RuleInfo> rules = new(StringComparer.Ordinal);
        int analyzerTypes = 0, constructed = 0;

        foreach (string dll in assemblyPaths)
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

    private static IEnumerable<Type> AnalyzerTypesIn(string dll)
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
