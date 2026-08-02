using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// A category constant is <c>internal</c> in a generated catalogue
/// (ADR-0026), so its name is no longer something a consumer writes and a rename no longer breaks
/// their build. That was this test's original reason to exist, and it is gone; what remains is
/// narrower and still worth holding.
///
/// Names are assigned in ordinal order, which makes them unstable without care: two categories
/// differing only in punctuation flatten to one identifier, and whichever sorts first takes the base
/// name. The day upstream adds a category that both collides with an existing one and sorts before
/// it, the newcomer would claim the base name and push the EXISTING constant onto a numbered suffix.
/// Nothing outside the assembly notices now — but every rule that names the moved constant is
/// rewritten, so an unattended nightly run would open a pull request whose diff is hundreds of lines
/// of churn with no upstream change behind it, and the real change in that run would be invisible
/// inside it.
/// </summary>
public sealed class CategoryNameStabilityTests : IDisposable
{
    private const string Package = "Vendor.Analyzers";
    private readonly string _temp = Directory.CreateTempSubdirectory("cataloggen-stability-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static SortedDictionary<string, RuleInfo> Rules(params (string Id, string Category)[] rules)
    {
        SortedDictionary<string, RuleInfo> map = new(StringComparer.Ordinal);
        foreach ((string id, string category) in rules) map[id] = new RuleInfo(category, string.Empty, Retired: false);
        return map;
    }

    private static SortedDictionary<string, string> Published(params (string Literal, string Name)[] categories)
    {
        SortedDictionary<string, string> map = new(StringComparer.Ordinal);
        foreach ((string literal, string name) in categories) map[literal] = name;
        return map;
    }

    private string Emit(SortedDictionary<string, RuleInfo> upstream, Previous? previous)
    {
        string output = Path.Combine(_temp, $"{Guid.NewGuid():N}.g.cs");
        Job job = new(Package, "2.0.0", "Vendor.Catalog", "VendorRule", output, "cs");
        CatalogEmitter.Emit(job, Package, "2.0.0", upstream, previous, dateOverride: "2026-01-01");
        return File.Exists(output) ? File.ReadAllText(output) : string.Empty;
    }

    private static string NameOf(string emitted, string literal)
    {
        Match m = Regex.Match(emitted, $@"public const string (\w+) = ""{Regex.Escape(literal)}"";");
        return m.Success ? m.Groups[1].Value : "<absent>";
    }

    [Fact]
    public void A_published_category_keeps_its_name_when_a_newcomer_would_have_taken_it()
    {
        // "Code Smell" sorts before "Code-Smell" — space is 0x20, hyphen 0x2D — and both flatten to
        // CodeSmell. So the newcomer is exactly the case that used to displace the incumbent.
        Previous before = new(
            "1.0.0",
            Rules(("X0001", "Code-Smell")),
            Published(("Code-Smell", "CodeSmell")));

        string emitted = Emit(Rules(("X0001", "Code-Smell"), ("X0002", "Code Smell")), before);

        Assert.Equal("CodeSmell", NameOf(emitted, "Code-Smell"));   // the incumbent is untouched
        Assert.Equal("CodeSmell2", NameOf(emitted, "Code Smell"));  // the newcomer takes the suffix
    }

    [Fact]
    public void A_suffixed_category_keeps_its_suffix_after_the_collision_clears()
    {
        // Stability beats prettiness. Renaming CodeSmell2 back to CodeSmell once the other category
        // is gone would break precisely the consumers this exists to protect.
        Previous before = new(
            "1.0.0",
            Rules(("X0001", "Code-Smell")),
            Published(("Code Smell", "CodeSmell"), ("Code-Smell", "CodeSmell2")));

        string emitted = Emit(Rules(("X0001", "Code-Smell")), before);

        Assert.Equal("CodeSmell2", NameOf(emitted, "Code-Smell"));
    }

    [Fact]
    public void A_first_run_still_disambiguates_in_ordinal_order()
    {
        // Nothing published yet, so there is no incumbent to protect and the original rule applies.
        string emitted = Emit(Rules(("X0001", "Code-Smell"), ("X0002", "Code Smell")), previous: null);

        Assert.Equal("CodeSmell", NameOf(emitted, "Code Smell"));
        Assert.Equal("CodeSmell2", NameOf(emitted, "Code-Smell"));
    }

    [Fact]
    public void A_new_category_that_collides_with_nothing_gets_its_plain_name()
    {
        Previous before = new(
            "1.0.0",
            Rules(("X0001", "Usage")),
            Published(("Usage", "Usage")));

        string emitted = Emit(Rules(("X0001", "Usage"), ("X0002", "Design")), before);

        Assert.Equal("Usage", NameOf(emitted, "Usage"));
        Assert.Equal("Design", NameOf(emitted, "Design"));
    }

    [Fact]
    public void The_names_a_run_publishes_are_recoverable_by_the_next_one()
    {
        // The guarantee is only as good as the parser's ability to read the mapping back: if
        // ReadPrevious lost it, every run would look like a first run and the protection would
        // silently stop applying.
        string output = Path.Combine(_temp, "readback.g.cs");
        Job job = new(Package, "1.0.0", "Vendor.Catalog", "VendorRule", output, "cs");
        CatalogEmitter.Emit(
            job, Package, "1.0.0",
            Rules(("X0001", "Code-Smell"), ("X0002", "Code Smell")),
            previous: null, dateOverride: "2026-01-01");

        Previous? reparsed = CatalogParser.ReadPrevious(output);

        Assert.NotNull(reparsed);
        Assert.Equal("CodeSmell", reparsed.CategoryNames["Code Smell"]);
        Assert.Equal("CodeSmell2", reparsed.CategoryNames["Code-Smell"]);
    }
}
