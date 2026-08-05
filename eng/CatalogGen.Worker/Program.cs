using System.Globalization;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CatalogGen.Worker;

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

/// <summary>The worker's entry point.</summary>
/// <remarks>
/// Written as a class with a Main rather than as top-level statements, and not for style: the
/// compiler puts top-level statements in a synthesised <c>Program</c> carrying
/// <c>[CompilerGenerated]</c>, which coverage.runsettings excludes — so this file, the busiest
/// code in the generator, was absent from every coverage report and counted as entirely
/// uncovered. Naming the class is what makes the measurement describe it.
/// </remarks>
internal static class Program
{
    // This file's diagnostics stay synchronous, and the disable is never restored because the reason
    // covers every one of them. Console.Out and Console.Error are synchronized writers whose async
    // overloads complete synchronously, and Console.WriteLine — used all around them — is a static
    // method with no async counterpart at all. Awaiting the few calls S6966 can see would yield to
    // nothing and leave this worker's log half-async on a technicality of where the method happens to
    // be declared. The same call is made, and the same reasoning written out, in CatalogRun.ExecuteAsync.
#pragma warning disable S6966 // Awaitable method should be used

    private static async Task<int> Main(string[] args)
    {
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
    }
    // ---------------------------------------------------------------------------

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

    // Appends to dropped whatever this assembly could not yield, so the run can refuse a read that lost
    // something. An assembly that fails to load entirely is the worst case and the quietest: it
    // contributes no analyzer type at all, so no count anywhere is short.
    //
    // The analyzers are found the way the COMPILER finds them: by reading metadata for the types
    // [DiagnosticAnalyzer] names, and loading those (ADR-0031). The difference from asking the assembly
    // for every type it declares is not a matter of taste, and it is not about speed either.
    //
    // An analyzer package is mostly not analyzers. It carries code fixes, internal helpers, and types
    // compiled against a Roslyn or a facade that this process has no reason to hold — and materialising
    // a type means resolving its base type and its interfaces, so those fail. They failed as a group,
    // through one ReflectionTypeLoadException, and a type that failed to load has no name left to ask
    // about: the run could not tell whether what it lost was a code fix or an analyzer whose rule would
    // be published as retired, so it refused everything. That refusal was right on the evidence
    // available and wrong about the packages it turned away — Microsoft.CodeAnalysis.CSharp.CodeStyle,
    // Meziantou.Analyzer and Microsoft.CodeAnalysis.PublicApiAnalyzers each yielded their full set of
    // descriptors while being refused for helpers that declare none.
    //
    // Reading the attribute first makes the question answerable. Every analyzer the assembly declares is
    // known BY NAME before anything is loaded, so "did one go missing" is asked of a list rather than of
    // a hole. And it is the same list the compiler works from: an analyzer without the attribute is
    // never loaded by any host, reports no diagnostic in any build, and a catalogue that published it
    // would describe rules no consumer can ever receive.
    // The return type is the concrete list rather than IEnumerable, which CA1859 asks for and which
    // a local function was never checked against: every return here is already materialised, so the
    // interface only added a dispatch. Materialised on purpose, too — the sequence is walked while
    // `dropped` is being appended to, and a lazy one would interleave the two.
    private static List<Type> AnalyzerTypesIn(string dll, List<string> dropped)
    {
        // Null means the metadata itself could not be read, which is already recorded as a shortfall:
        // an assembly nobody can enumerate may declare analyzers nobody will ever see.
        List<string>? declared = DeclaredAnalyzerNames(dll, dropped);
        if (declared is null || declared.Count == 0) return [];

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

        // The attribute is matched on its name, as the compiler matches it, so a same-named attribute
        // from somewhere else reaches this point. The base type is what settles it, and a type that is
        // not a DiagnosticAnalyzer is not a shortfall — it declares no rule, so nothing is missing when
        // it is passed over. Abstract likewise: nothing constructs one.
        return declared.Select(name => LoadDeclaredAnalyzer(asm, name, dropped))
                       .OfType<Type>()
                       .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
                       .ToList();
    }

    // One named analyzer, loaded. Null when it could not be, which IS a shortfall and is recorded as
    // one: the attribute said this type is an analyzer, so whatever stopped it from loading stopped a
    // rule from arriving.
    private static Type? LoadDeclaredAnalyzer(Assembly asm, string name, List<string> dropped)
    {
        Type? type;
        // Broad for the same reason the load above is, and throwOnError stays false because it does not
        // cover the interesting failures anyway — a base type in an assembly that is not there surfaces
        // as a FileNotFoundException either way.
        try { type = asm.GetType(name, throwOnError: false); }
        catch (Exception ex)
        {
            dropped.Add($"{name}: declares [DiagnosticAnalyzer] but could not be loaded " +
                        $"({ex.GetType().Name}: {ex.Message})");

            return null;
        }

        if (type is null)
            dropped.Add($"{name}: declares [DiagnosticAnalyzer] but no such type could be loaded");

        return type;
    }

    // The names of the types this assembly marks with [DiagnosticAnalyzer], read from metadata without
    // loading anything. Null when the metadata could not be read at all, which the caller treats as a
    // shortfall; an empty list when the assembly simply declares no analyzer, which is not one — half
    // the assemblies in an analyzer package are dependencies that were never going to declare any.
    private static List<string>? DeclaredAnalyzerNames(string dll, List<string> dropped)
    {
        try
        {
            using FileStream file = File.OpenRead(dll);
            using PEReader pe = new(file);

            // A resource-only or native file declares no type at all. Nothing is lost by saying so, and
            // it is not the same event as metadata that is present and unreadable.
            if (!pe.HasMetadata) return [];

            MetadataReader metadata = pe.GetMetadataReader();

            return metadata.TypeDefinitions
                           .Select(metadata.GetTypeDefinition)
                           .Where(type => MarksAnAnalyzer(metadata, type))
                           .Select(type => ReflectionName(metadata, type))
                           .ToList();
        }
        catch (Exception ex)
        {
            dropped.Add($"{Path.GetFileName(dll)}: its metadata could not be read " +
                        $"({ex.GetType().Name}: {ex.Message})");

            return null;
        }
    }

    // True when the type carries [DiagnosticAnalyzer]. Matched on the attribute's simple name, which is
    // what the compiler matches on: the attribute reaches this assembly as a TypeReference into whatever
    // Microsoft.CodeAnalysis it was compiled against, and pinning the namespace would make the match
    // depend on which of those it was. The caller confirms the base type afterwards, so a coincidence
    // costs a type that is skipped rather than a rule that is invented.
    private static bool MarksAnAnalyzer(MetadataReader metadata, TypeDefinition type)
        => type.GetCustomAttributes()
               .Select(metadata.GetCustomAttribute)
               .Any(attribute => AttributeName(metadata, attribute) == "DiagnosticAnalyzerAttribute");

    // The simple name of an attribute's own type. An attribute declared in another assembly — which
    // [DiagnosticAnalyzer] always is — arrives as a MemberReference to a TypeReference; the
    // MethodDefinition case is the same attribute declared in the assembly being read, and costs one
    // branch rather than an assumption about who compiled what.
    private static string? AttributeName(MetadataReader metadata, CustomAttribute attribute)
    {
        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MemberReference:
                MemberReference reference =
                    metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);

                return reference.Parent.Kind == HandleKind.TypeReference
                           ? metadata.GetString(
                               metadata.GetTypeReference((TypeReferenceHandle)reference.Parent).Name)
                           : null;

            case HandleKind.MethodDefinition:
                MethodDefinition definition =
                    metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);

                return metadata.GetString(metadata.GetTypeDefinition(definition.GetDeclaringType()).Name);

            default:
                return null;
        }
    }

    // The name Assembly.GetType answers to, which is not the name metadata stores. A nested type is
    // reached through its declaring type with a '+', and carries no namespace of its own — the
    // outermost one holds it. Analyzers are routinely nested inside the service that owns them, so this
    // is the ordinary case rather than a corner of it.
    private static string ReflectionName(MetadataReader metadata, TypeDefinition type)
    {
        string name = metadata.GetString(type.Name);
        if (type.IsNested)
            return ReflectionName(metadata, metadata.GetTypeDefinition(type.GetDeclaringType())) + "+" + name;

        string @namespace = metadata.GetString(type.Namespace);

        return string.IsNullOrEmpty(@namespace) ? name : @namespace + "." + name;
    }

    // True when the type was constructed, whether or not it declared any descriptor. An analyzer that
    // cannot be constructed contributes none; the caller counts the difference so it stays visible
    // rather than being silently absorbed.
    private static bool TryAddDescriptors(Type analyzer, SortedDictionary<string, ReadRule> rules)
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
    private static Dictionary<string, ReadRule> AcceptSuppressable(SortedDictionary<string, ReadRule> rules)
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
}
