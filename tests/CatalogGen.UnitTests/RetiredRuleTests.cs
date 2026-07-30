using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// ADR-0010 and specification §23.1: a constant is never deleted. Consumers inline constant values
/// at their own compile time, so removing one breaks their recompilation; a rule the vendor retires
/// is carried forward and marked <c>[Obsolete]</c> instead.
///
/// No shipped catalogue contains a retired rule yet, so until these tests existed that promise had
/// never once been executed. It would have run for the first time unattended, in the nightly job,
/// on the night an upstream release finally dropped a rule.
/// </summary>
public sealed class RetiredRuleTests : IDisposable
{
    private const string Package = "Vendor.Analyzers";
    private readonly string _temp = Directory.CreateTempSubdirectory("cataloggen-retired-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static SortedDictionary<string, RuleInfo> Rules(params (string Id, string Category)[] rules)
    {
        SortedDictionary<string, RuleInfo> map = new(StringComparer.Ordinal);
        foreach ((string id, string category) in rules) map[id] = new RuleInfo(category, string.Empty, Retired: false);
        return map;
    }

    // A Previous as a real prior run would have left it: the rules, plus the identifier each
    // category was published under. Emit reads that second map to keep a published constant's name
    // stable, so a test that omitted it would exercise the first-ever-run path by accident.
    private static Previous Before(string version, SortedDictionary<string, RuleInfo> rules)
    {
        SortedDictionary<string, string> categories = new(StringComparer.Ordinal);
        foreach (RuleInfo info in rules.Values) categories[info.Category] = Naming.ToIdentifier(info.Category);
        return new Previous(version, rules, categories);
    }

    private GenerateResult Emit(
        SortedDictionary<string, RuleInfo> upstream, Previous? previous, out string emitted)
    {
        string output = Path.Combine(_temp, $"{Guid.NewGuid():N}.g.cs");
        Job job = new(Package, "2.0.0", "Vendor.Catalog", "VendorRule", output, "cs");
        GenerateResult result = CatalogEmitter.Emit(
            job, Package, "2.0.0", upstream, previous, dateOverride: "2026-01-01");
        // A run that changes nothing returns before it writes, so an absent file is a result rather
        // than a failure — and the empty string makes that observable to a caller that asserts on it.
        emitted = File.Exists(output) ? File.ReadAllText(output) : string.Empty;
        return result;
    }

    [Fact]
    public void A_rule_dropped_upstream_is_kept_and_marked_obsolete()
    {
        Previous before = Before("1.0.0", Rules(("X0001", "Usage"), ("X0002", "Usage")));

        GenerateResult result = Emit(Rules(("X0001", "Usage")), before, out string emitted);

        Assert.True(result.Changed);
        Assert.Contains("public static class X0002", emitted, StringComparison.Ordinal);
        Assert.Contains(
            "[Obsolete(\"X0002 is no longer declared by Vendor.Analyzers as of 2.0.0.",
            emitted,
            StringComparison.Ordinal);
        Assert.Contains("Retired upstream (1)", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rule_still_declared_upstream_carries_no_obsolete_marker()
    {
        Previous before = Before("1.0.0", Rules(("X0001", "Usage"), ("X0002", "Usage")));

        Emit(Rules(("X0001", "Usage")), before, out string emitted);

        // Counted rather than sliced: the attribute is emitted above the class it marks, after that
        // rule's doc comment, so any slice taken at "public static class X0002" already contains it.
        // Two rules, one retired, therefore exactly one marker in the file.
        Assert.Contains("public static class X0001", emitted, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(emitted, @"\[Obsolete\("));
    }

    [Fact]
    public void A_retirement_survives_being_read_back_by_the_next_run()
    {
        // The night after a retirement, the generator parses its own output again. If the parser
        // missed the [Obsolete] marker the rule would look live, and the run after that would
        // report it as retired all over again — a pull request every night, forever.
        Previous before = Before("1.0.0", Rules(("X0001", "Usage"), ("X0002", "Usage")));

        string output = Path.Combine(_temp, "readback.g.cs");
        Job job = new(Package, "2.0.0", "Vendor.Catalog", "VendorRule", output, "cs");
        CatalogEmitter.Emit(job, Package, "2.0.0", Rules(("X0001", "Usage")), before, dateOverride: "2026-01-01");

        Previous? reparsed = CatalogParser.ReadPrevious(output);

        Assert.NotNull(reparsed);
        Assert.True(reparsed!.Rules["X0002"].Retired, "the retirement should be recoverable from the file");
        Assert.False(reparsed.Rules["X0001"].Retired);
    }

    [Fact]
    public void A_second_run_over_an_unchanged_retirement_writes_nothing()
    {
        // Following on from the test above: once a retirement has been recorded, the state is
        // stable. A night where upstream has not moved must leave the file — and its generatedOn
        // stamp — untouched, or the scheduled job opens a pull request every night whose only
        // content is a new date.
        SortedDictionary<string, RuleInfo> settled = new(StringComparer.Ordinal)
        {
            ["X0001"] = new("Usage", string.Empty, Retired: false),
            ["X0002"] = new("Usage", string.Empty, Retired: true),
        };

        GenerateResult result = Emit(Rules(("X0001", "Usage")), Before("2.0.0", settled), out string emitted);

        Assert.False(result.Changed);
        Assert.Equal(string.Empty, result.Summary);
        Assert.Equal(string.Empty, emitted);
    }
}
