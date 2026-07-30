using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// Two vendor categories that differ only in punctuation flatten to one identifier. Left alone that
/// would emit the same <c>public const</c> name twice in one class, and the generated catalogue
/// would simply not compile — a nightly pull request that fails CI with a duplicate-member error and
/// no hint as to which upstream category caused it. The emitter disambiguates instead.
/// </summary>
public sealed class CategoryCollisionTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("cataloggen-collision-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void Colliding_categories_are_given_distinct_constants()
    {
        SortedDictionary<string, RuleInfo> upstream = new(StringComparer.Ordinal)
        {
            ["X0001"] = new("Major Code Smell", string.Empty, Retired: false),
            ["X0002"] = new("Major-Code-Smell", string.Empty, Retired: false),
        };

        string output = Path.Combine(_temp, "collision.g.cs");
        Job job = new("Vendor.Analyzers", "1.0.0", "Vendor.Catalog", "VendorRule", output, "cs");
        CatalogEmitter.Emit(job, "Vendor.Analyzers", "1.0.0", upstream, previous: null, dateOverride: "2026-01-01");

        string emitted = File.ReadAllText(output);

        // Both literals survive, under names that differ.
        Assert.Contains("public const string MajorCodeSmell = \"Major Code Smell\";", emitted, StringComparison.Ordinal);
        Assert.Contains("public const string MajorCodeSmell2 = \"Major-Code-Smell\";", emitted, StringComparison.Ordinal);

        // And each rule points at the one that carries its own category.
        Assert.Contains("public const string Category = VendorCategory.MajorCodeSmell;", emitted, StringComparison.Ordinal);
        Assert.Contains("public const string Category = VendorCategory.MajorCodeSmell2;", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_collision_still_round_trips_through_the_parser()
    {
        // The suffix is only useful if the next run can still map each rule back to the category
        // text it actually carries. Recovering the identifier is not enough — it is the literal
        // that reaches a consumer's SuppressMessage call.
        SortedDictionary<string, RuleInfo> upstream = new(StringComparer.Ordinal)
        {
            ["X0001"] = new("Major Code Smell", string.Empty, Retired: false),
            ["X0002"] = new("Major-Code-Smell", string.Empty, Retired: false),
        };

        string output = Path.Combine(_temp, "collision-roundtrip.g.cs");
        Job job = new("Vendor.Analyzers", "1.0.0", "Vendor.Catalog", "VendorRule", output, "cs");
        CatalogEmitter.Emit(job, "Vendor.Analyzers", "1.0.0", upstream, previous: null, dateOverride: "2026-01-01");

        Previous? reparsed = CatalogParser.ReadPrevious(output);

        Assert.NotNull(reparsed);
        Assert.Equal("Major Code Smell", reparsed!.Rules["X0001"].Category);
        Assert.Equal("Major-Code-Smell", reparsed.Rules["X0002"].Category);
    }
}
