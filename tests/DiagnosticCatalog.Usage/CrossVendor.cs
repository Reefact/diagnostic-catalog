// Cross-vendor suppressions: four catalogues, two suppression attributes, one compilation.
//
// Every pair below takes its Category and its Id from the SAME rule. What varies is the vendor, the
// attribute, the scope, and the path the constant is reached by.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using DiagnosticCatalog;
using DiagnosticCatalog.NetAnalyzers;
using DiagnosticCatalog.Self;
using DiagnosticCatalog.Sonar;
using DiagnosticCatalog.StyleCop;

using static DiagnosticCatalog.Sonar.SonarRule;
using static DiagnosticCatalog.StyleCop.StyleCopRule;

using CatchAll = DiagnosticCatalog.NetAnalyzers.NetAnalyzersRule.CA1031;
using Suppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;
using UnderscorePrefix = DiagnosticCatalog.StyleCop.StyleCopRule.SA1309;
using UnusedParameter = DiagnosticCatalog.Sonar.SonarRule.S1172;

// The shape a real GlobalSuppressions.cs has, except that it is kept beside the code it covers:
// assembly and module scope, three vendors, and the Scope/Target/MessageId properties Visual Studio
// writes when it suppresses "in suppression file".
[assembly: SuppressMessage(
    StyleCopRule.SA1600.Category,
    StyleCopRule.SA1600.Id,
    Justification = "The usage suite documents its types with one line each, not with XML.",
    Scope = "namespaceanddescendants",
    Target = "~N:DiagnosticCatalog.Usage")]

[assembly: SuppressMessage(
    NetAnalyzersRule.CA1707.Category,
    NetAnalyzersRule.CA1707.Id,
    Justification = "The name mirrors the C header's field, underscore included.",
    Scope = "member",
    Target = "~M:DiagnosticCatalog.Usage.CrossVendorBridge.Translate_Header(System.String)",
    MessageId = "Member")]

[assembly: SuppressMessage(
    SonarRule.S1104.Category,
    SonarRule.S1104.Id,
    Justification = "The bridge's constants are part of its published surface.",
    Scope = "type",
    Target = "~T:DiagnosticCatalog.Usage.CrossVendorBridge")]

[module: SuppressMessage(
    SonarRule.S1075.Category,
    SonarRule.S1075.Id,
    Justification = "The loopback endpoint is fixed by the frame protocol.")]

[module: SuppressMessage(
    StyleCopRule.SA1633.Category,
    StyleCopRule.SA1633.Id,
    Justification = "File headers are applied by the repository template, not per file.")]

namespace DiagnosticCatalog.Usage;

/// <summary>Three vendors on one member, a fourth on the type, and the pair written by parameter name.</summary>
[Suppress(
    StyleCopRule.SA1402.Category,
    StyleCopRule.SA1402.Id,
    Justification = "The bridge and its helpers belong to one file.")]
internal static class CrossVendorBridge
{
    internal const string FallbackEndpoint = "http://localhost:9111/frames";

    [SuppressMessage(
        SonarRule.S107.Category,
        SonarRule.S107.Id,
        Justification = "The native entry point takes every field of the frame header.")]
    [SuppressMessage(
        NetAnalyzersRule.CA1062.Category,
        NetAnalyzersRule.CA1062.Id,
        Justification = "Internal surface; the caller has already validated the frame.")]
    [SuppressMessage(
        StyleCopRule.SA1503.Category,
        StyleCopRule.SA1503.Id,
        Justification = "The guard reads better on one line.")]
    internal static string Translate(
        string header,
        int version,
        int flags,
        int length,
        int offset,
        int checksum,
        string payload)
    {
        if (header.Length == 0) return payload;

        return string.Join(
            "/",
            header,
            version.ToString(CultureInfo.InvariantCulture),
            flags.ToString(CultureInfo.InvariantCulture),
            length.ToString(CultureInfo.InvariantCulture),
            offset.ToString(CultureInfo.InvariantCulture),
            checksum.ToString(CultureInfo.InvariantCulture),
            payload);
    }

    // The two constructor arguments written by name and in reverse order, beside a second vendor's
    // suppression on the same member.
    [SuppressMessage(
        checkId: NetAnalyzersRule.CA1305.Id,
        category: NetAnalyzersRule.CA1305.Category,
        Justification = "The frame protocol is ASCII and culture-independent by definition.")]
    [SuppressMessage(
        SonarRule.S3776.Category,
        SonarRule.S3776.Id,
        Justification = "One branch per header state; splitting it would hide the mapping.")]
    internal static string Translate_Header(string header) =>
        Translate(header, 1, 0, header.Length, 0, 0, string.Empty);
}

/// <summary>One type carrying suppressions from all four catalogues this project references.</summary>
internal sealed class MigrationLedger
{
    [Suppress(
        UnderscorePrefix.Category,
        UnderscorePrefix.Id,
        Justification = "The team's field convention predates StyleCop.")]
    private readonly List<string> _entries = new List<string>();

    [SuppressMessage(
        NetAnalyzersRule.CA1002.Category,
        NetAnalyzersRule.CA1002.Id,
        Justification = "The ledger is internal and the list is its natural shape.")]
    internal List<string> Entries => _entries;

    [SuppressMessage(
        SonarRule.S3776.Category,
        SonarRule.S3776.Id,
        Justification = "The branches mirror the migration states one to one.")]
    internal void Record(string stage, bool migrated, bool skipped)
    {
        if (migrated)
        {
            _entries.Add(stage + ":done");
        }
        else if (skipped)
        {
            _entries.Add(stage + ":skipped");
        }
        else
        {
            _entries.Add(stage + ":pending");
        }
    }

    // The fourth catalogue is DiagnosticCatalog's own. Scoped to this member on purpose: the literal
    // beside it names a vendor no catalogue here covers, so DCAT0006 has nothing to match and this
    // suppression can hide nothing.
    [SuppressMessage(
        DcatRule.DCAT0006.Category,
        DcatRule.DCAT0006.Id,
        Justification = "Roslynator has no catalogue yet, so this pair stays a literal until it does.")]
    [SuppressMessage(
        "Redundancy",
        "RCS1163",
        Justification = "The signature is fixed by the delegate this implements.")]
    internal void Discard(string stage)
    {
    }
}

/// <summary>Aliases naming two vendors' rules, under an alias on the suppression attribute itself.</summary>
internal sealed class ReplayBuffer
{
    private readonly int _capacity;

    internal ReplayBuffer(int capacity)
    {
        _capacity = capacity;
    }

    [Suppress(
        UnusedParameter.Category,
        UnusedParameter.Id,
        Justification = "The signature is fixed by the replay delegate.")]
    [Suppress(
        CatchAll.Category,
        CatchAll.Id,
        Justification = "A corrupt frame must not take the replay down.")]
    internal bool TryReplay(string frame, int attempt)
    {
        try
        {
            return frame.Length <= _capacity;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>Bare rule names from two catalogues imported with <c>using static</c>, in one file.</summary>
internal static class LegacyImport
{
    [SuppressMessage(
        S1144.Category,
        S1144.Id,
        Justification = "Called by the serializer through reflection.")]
    private static string Rebuild(string payload) => payload.Trim();

    [SuppressMessage(
        SA1101.Category,
        SA1101.Id,
        Justification = "The house style omits the 'this.' prefix.")]
    internal static string Describe(string payload) => Rebuild(payload);
}

/// <summary>The categories the in-house trimmer rules are declared under.</summary>
[DiagnosticCategory]
internal static class TrimmerCategory
{
    /// <summary>The category ILLink's own descriptors declare.</summary>
    public const string Trimming = "Trimming";
}

/// <summary>An in-house rule for a trimmer identifier — the only shape UnconditionalSuppressMessage honours.</summary>
internal static class TrimmerRule
{
    /// <summary>Members annotated with RequiresUnreferencedCode may break when trimming.</summary>
    [DiagnosticRule]
    internal static class IL2026
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(IL2026);

        /// <summary>The category ILLink's own descriptor declares.</summary>
        public const string Category = TrimmerCategory.Trimming;
    }
}

/// <summary>Both suppression attributes on one member: the trimmer's rule beside a vendor's.</summary>
internal static class PluginLoader
{
    [UnconditionalSuppressMessage(
        TrimmerRule.IL2026.Category,
        TrimmerRule.IL2026.Id,
        Justification = "The plugin surface is rooted by a trimmer descriptor.")]
    [SuppressMessage(
        CatchAll.Category,
        CatchAll.Id,
        Justification = "A plugin that fails to load must not take the host down.")]
    internal static string? Load(string name)
    {
        try
        {
            return Type.GetType(name)?.FullName;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>One Sonar rule suppressed twice with different justifications, each beside another vendor's.</summary>
internal static class SampleIds
{
    [SuppressMessage(
        SonarRule.S2245.Category,
        SonarRule.S2245.Id,
        Justification = "Jitter for a retry backoff, not a secret.")]
    [SuppressMessage(
        NetAnalyzersRule.CA5394.Category,
        NetAnalyzersRule.CA5394.Id,
        Justification = "Jitter for a retry backoff, not a secret.")]
    internal static int Jitter(int ceiling) => new Random().Next(ceiling);

    [SuppressMessage(
        SonarRule.S2245.Category,
        SonarRule.S2245.Id,
        Justification = "Telemetry sampling; unpredictability is explicitly not required here.")]
    [SuppressMessage(
        StyleCopRule.SA1201.Category,
        StyleCopRule.SA1201.Id,
        Justification = "Kept next to the method it samples for.")]
    internal static bool Sample(int oneIn) => new Random().Next(oneIn) == 0;
}

// ---------------------------------------------------------------------------------------------
// SETTLED. This block was an OPEN FINDING: the shape below is listed under **Accepted syntactic
// forms at a use site** in `doc/guide/rule-contract.en.md`,
//
//     private const string RuleId = SonarRule.S1144.Id;
//     [SuppressMessage(SonarRule.S1144.Category, RuleId)]
//
// and the analyzer reported DCAT0007 on it anyway. The suite kept it because it was reported, and
// carried a pragma so the branch would still build.
//
// SuppressionAttribute.Resolve now follows one hop into a constant's initialiser, so a rule member
// hoisted into a named constant resolves to its rule. The pragma is gone and this file compiles
// clean — which is the assertion. What settled it, and why the hop is exactly one and only from a
// declaring type that is not itself a rule, is in the fix's own commit; the reasoning that made it
// urgent is ADR-0027, since as an error the false positive would have failed a consumer's build.
//
// The third point that block made also landed: the message said "writes the literal", where there
// need be no literal. It now says "the string value".
// ---------------------------------------------------------------------------------------------

/// <summary>Ids shared by several suppressions, each reached from its rule (doc/guide/rule-contract.en.md, §10.6).</summary>
internal static class SharedRules
{
    internal const string UnusedPrivateMember = SonarRule.S1144.Id;
}

/// <summary>A category reached from the rule and an id reached through an intermediate constant.</summary>
internal static class ScheduledJobs
{
    internal static string Name => nameof(ScheduledJobs);

    [SuppressMessage(
        SonarRule.S1144.Category,
        SharedRules.UnusedPrivateMember,
        Justification = "Bound by the scheduler through reflection.")]
    private static void Tick()
    {
    }
}

