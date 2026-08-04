using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// The rules visible to a compilation, keyed on the functional <c>(Category, Id)</c> pair.
/// </summary>
/// <remarks>
/// <para>
/// Built once per compilation and consulted by the value-based diagnostics — DCAT0006 today, DCAT0007
/// and DCAT0008 later. DCAT0001 and DCAT0009 never touch it: they resolve everything from the attribute
/// itself, which is why construction sits behind a <c>Lazy</c> at the call site (§13.1).
/// </para>
/// <para>
/// A referenced type that carries the marker but fails the structural contract is skipped rather than
/// recorded. It cannot be matched, so skipping is the correct behaviour here; DCAT0010, whose whole
/// subject is reporting those types, will need them kept and is the point at which to add it.
/// </para>
/// </remarks>
internal sealed class RuleIndex
{
    private readonly Dictionary<FunctionalKey, ImmutableArray<RuleDefinition>> _rules;

    private RuleIndex(Dictionary<FunctionalKey, ImmutableArray<RuleDefinition>> rules)
    {
        _rules = rules;
    }

    /// <summary>Sweeps <paramref name="compilation"/> and the assemblies it references.</summary>
    internal static RuleIndex Build(Compilation compilation)
    {
        Dictionary<FunctionalKey, List<RuleDefinition>> collected = [];

        foreach (IAssemblySymbol assembly in Candidates(compilation))
        {
            Collect(assembly.GlobalNamespace, collected);
        }

        return new RuleIndex(collected.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToImmutableArray()));
    }

    /// <summary>
    /// The rules matching a suppression's arguments — empty, one, or several.
    /// </summary>
    /// <remarks>
    /// <paramref name="checkId"/> must already be normalised through <see cref="CheckId.Normalise"/>.
    /// </remarks>
    internal ImmutableArray<RuleDefinition> Find(string category, string checkId) =>
        _rules.TryGetValue(new FunctionalKey(category, checkId), out ImmutableArray<RuleDefinition> found)
            ? found
            : ImmutableArray<RuleDefinition>.Empty;

    /// <summary>
    /// The assemblies that could possibly hold a rule.
    /// </summary>
    /// <remarks>
    /// Walking every type of every referenced assembly is an expensive metadata sweep, so §13.1 makes
    /// this pre-filter mandatory. Both clauses are: dropping the second — the assembly declaring the
    /// marker itself — makes every catalogue that embeds its own <c>internal DiagnosticRuleAttribute</c>
    /// rather than take a package dependency invisible (§7.2). The result is zero rules and zero
    /// diagnostics, which reads exactly like a clean codebase.
    /// </remarks>
    private static IEnumerable<IAssemblySymbol> Candidates(Compilation compilation)
    {
        yield return compilation.Assembly;

        foreach (MetadataReference reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) { continue; }

            if (MayContainRules(assembly)) { yield return assembly; }
        }
    }

    private static bool MayContainRules(IAssemblySymbol assembly)
    {
        // Clause two, and not optional — see the remarks above.
        if (assembly.GetTypeByMetadataName(RuleMarker.AttributeMetadataName) is not null) { return true; }

        IModuleSymbol? module = assembly.Modules.FirstOrDefault();

        if (module is null) { return false; }

        return module.ReferencedAssemblies.Any(
            identity => string.Equals(identity.Name, FoundationAssemblyName, StringComparison.Ordinal));
    }

    private static void Collect(
        INamespaceOrTypeSymbol container,
        Dictionary<FunctionalKey, List<RuleDefinition>> collected)
    {
        foreach (INamespaceOrTypeSymbol member in container.GetMembers().OfType<INamespaceOrTypeSymbol>())
        {
            if (member is INamedTypeSymbol type)
            {
                if (RuleMarker.IsRule(type)) { Record(type, collected); }

                // Rules are normally nested inside a container type — SonarRules.S1144 — so the walk
                // must descend through types, not only through namespaces.
                Collect(type, collected);

                continue;
            }

            Collect(member, collected);
        }
    }

    private static void Record(
        INamedTypeSymbol type,
        Dictionary<FunctionalKey, List<RuleDefinition>> collected)
    {
        RuleContractResult contract = RuleContract.Check(type);

        // The same predicate the definition diagnostics use, over symbols alone — which is what lets it
        // run here against a metadata symbol that has no syntax at all.
        if (!contract.IsSatisfied) { return; }

        RuleDefinition definition = new(
            type,
            contract.IdField!,
            contract.CategoryField!,
            contract.Id!,
            contract.Category!);

        // Normalised on the way IN, because every lookup is normalised on the way out: Find contracts
        // for it, so no query can ever produce a key containing a colon. Keying on the raw value made
        // a rule whose declared Id carries a friendly-name suffix — a form §8.2 blesses and the usage
        // corpus ships — unreachable by any suppression at all, including one writing it verbatim.
        FunctionalKey key = new(definition.Category, CheckId.Normalise(definition.Id));

        if (!collected.TryGetValue(key, out List<RuleDefinition>? bucket))
        {
            bucket = [];
            collected.Add(key, bucket);
        }

        bucket.Add(definition);
    }

    /// <summary>The assembly a catalogue references when it does not embed the marker itself.</summary>
    private const string FoundationAssemblyName = "DiagnosticCatalog";

    /// <summary>
    /// The functional key of §13: the category and identifier a suppression actually writes.
    /// </summary>
    /// <remarks>
    /// Ordinal comparison throughout. A category differing only by case is a different category as far
    /// as Roslyn's own suppression matching is concerned, and guessing otherwise would silently pair a
    /// suppression with a rule it does not name.
    /// </remarks>
    private readonly struct FunctionalKey : IEquatable<FunctionalKey>
    {
        private readonly string _category;
        private readonly string _id;

        internal FunctionalKey(string category, string id)
        {
            _category = category;
            _id = id;
        }

        public bool Equals(FunctionalKey other) =>
            string.Equals(_category, other._category, StringComparison.Ordinal)
            && string.Equals(_id, other._id, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is FunctionalKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(_category) * 397)
                    ^ StringComparer.Ordinal.GetHashCode(_id);
            }
        }
    }
}
