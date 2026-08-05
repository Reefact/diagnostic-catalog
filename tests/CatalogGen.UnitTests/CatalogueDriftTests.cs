using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// What the emitter publishes, it must also compare.
/// </summary>
/// <remarks>
/// <para>
/// A run rewrites the file when something moved, and leaves it alone otherwise — that second half is
/// what keeps the scheduled job from opening a pull request every night. The comparison deciding
/// which it is has to cover everything the file states, or the catalogue keeps serving a value
/// upstream no longer has and <c>dcat validate</c>, doing the same comparison, calls it current.
/// </para>
/// <para>
/// The upstream version is what usually saves it: a different release means a different version, so
/// the file is rewritten and the uncompared fields ride along. That does not hold for a source read
/// off disk — <c>--project</c>, <c>--assembly</c>, <c>--solution</c> — where the version is the
/// assembly's own and stays put across every rebuild, which <c>LocalAssemblySource</c> names in as
/// many words. Every test here therefore keeps the version still, because that is the case where the
/// comparison is the only thing standing.
/// </para>
/// </remarks>
public sealed class CatalogueDriftTests : IDisposable
{
    private const string Package = "Vendor.Analyzers";

    /// <summary>Held still on purpose — see the remarks.</summary>
    private const string Version = "1.0.0";

    private readonly string _temp = Directory.CreateTempSubdirectory("cataloggen-drift-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static SortedDictionary<string, RuleInfo> Rules(params RuleInfo[] rules)
    {
        SortedDictionary<string, RuleInfo> map = new(StringComparer.Ordinal);
        for (int index = 0; index < rules.Length; index++) map["X000" + (index + 1)] = rules[index];

        return map;
    }

    private static RuleInfo Rule(string helpLinkUri = "", bool retired = false) =>
        new("Usage", helpLinkUri, retired, "A title.");

    private static Previous Before(SortedDictionary<string, RuleInfo> rules)
    {
        SortedDictionary<string, string> categories = new(StringComparer.Ordinal);
        foreach (RuleInfo info in rules.Values) categories[info.Category] = Naming.ToIdentifier(info.Category);

        return new Previous(Version, rules, categories);
    }

    private GenerateResult Emit(
        SortedDictionary<string, RuleInfo> upstream, Previous previous, out string emitted)
    {
        string output = Path.Combine(_temp, $"{Guid.NewGuid():N}.g.cs");
        Job job = new(Package, Version, "Vendor.Catalog", "VendorRule", output, "cs");

        GenerateResult result = CatalogEmitter.Emit(
            job, Package, Version, upstream, previous, dateOverride: "2026-01-01");

        emitted = File.Exists(output) ? File.ReadAllText(output) : string.Empty;

        return result;
    }

    [Fact]
    public void A_rule_the_vendor_declares_again_stops_being_marked_obsolete()
    {
        // The catalogue says "no longer declared by the vendor — remove your suppression" about a
        // rule the vendor declares. Nothing downstream can contradict it: the platform never
        // validates a suppression's category, so the only thing that could was this comparison.
        Previous carriedForward = Before(Rules(Rule(), Rule(retired: true)));

        GenerateResult result = Emit(Rules(Rule(), Rule()), carriedForward, out string emitted);

        Assert.True(result.Changed, "a rule coming back is a change");
        Assert.Contains("public static class X0002", emitted, StringComparison.Ordinal);
        Assert.DoesNotContain("[Obsolete(", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_help_link_the_vendor_added_reaches_the_catalogue()
    {
        Previous withoutLink = Before(Rules(Rule()));

        GenerateResult result = Emit(Rules(Rule("https://vendor.example/X0001")), withoutLink, out string emitted);

        Assert.True(result.Changed, "a help link appearing is a change");
        Assert.Contains(
            "public const string HelpLinkUri = \"https://vendor.example/X0001\";",
            emitted,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_help_link_the_vendor_moved_reaches_the_catalogue()
    {
        Previous withOldLink = Before(Rules(Rule("https://vendor.example/old")));

        GenerateResult result = Emit(Rules(Rule("https://vendor.example/new")), withOldLink, out string emitted);

        Assert.True(result.Changed, "a help link moving is a change");
        Assert.Contains("https://vendor.example/new", emitted, StringComparison.Ordinal);
        Assert.DoesNotContain("https://vendor.example/old", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_over_a_catalogue_that_did_not_move_still_writes_nothing()
    {
        // The half a wider comparison could break, and the reason this test sits beside the three
        // above rather than being taken for granted: a comparison that reported a change every time
        // would have the scheduled job open a pull request every night whose only content is a date.
        SortedDictionary<string, RuleInfo> settled = Rules(Rule("https://vendor.example/X0001"), Rule(retired: true));

        GenerateResult result = Emit(Rules(Rule("https://vendor.example/X0001")), Before(settled), out string emitted);

        Assert.False(result.Changed);
        Assert.Equal(string.Empty, result.Summary);
        Assert.Equal(string.Empty, emitted);
    }
}
