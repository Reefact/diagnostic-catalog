using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// The acquisition that reads analyzer assemblies already on disk. What it hands the reader is the
/// contract the reader shares with every other source, so the two things worth pinning down are
/// that it refuses rather than shortens — a set missing an assembly emits a catalogue missing
/// rules, and nothing downstream can tell that from a vendor retiring them — and that the set it
/// produces does not depend on the order the caller happened to name them in.
/// </summary>
public sealed class LocalAssemblySourceTests
{
    // Two assemblies that certainly exist while this test runs, and whose names differ.
    private static readonly string Generator = typeof(LocalAssemblySource).Assembly.Location;
    private static readonly string Tests = typeof(LocalAssemblySourceTests).Assembly.Location;

    [Fact]
    public void A_path_that_does_not_resolve_is_refused_rather_than_skipped()
    {
        AnalyzerAssemblySet? set = LocalAssemblySource.Acquire(
            [Generator, Path.Combine(Path.GetTempPath(), "no-such-analyzer.dll")], null, null);

        Assert.Null(set);
    }

    [Fact]
    public void No_assembly_at_all_is_refused() => Assert.Null(LocalAssemblySource.Acquire([], null, null));

    [Fact]
    public void The_source_is_named_after_the_first_assembly_when_no_name_is_given()
    {
        AnalyzerAssemblySet? set = LocalAssemblySource.Acquire([Generator], null, null);

        Assert.NotNull(set);
        Assert.Equal(AssemblyName.GetAssemblyName(Generator).Name, set.SourceName);
    }

    [Fact]
    public void The_source_version_falls_back_to_the_assembly_version()
    {
        AnalyzerAssemblySet? set = LocalAssemblySource.Acquire([Generator], null, null);

        Assert.NotNull(set);
        Assert.Equal(AssemblyName.GetAssemblyName(Generator).Version!.ToString(), set.SourceVersion);
    }

    [Fact]
    public void A_given_name_and_version_win_over_what_the_assembly_declares()
    {
        // The reason the overrides exist: an assembly built out of a working copy carries whatever
        // its project last set, typically unchanged across every rebuild, while its rules move. The
        // caller that knows the meaningful release says so.
        AnalyzerAssemblySet? set = LocalAssemblySource.Acquire([Generator], "My.Analyzers", "1.4.0");

        Assert.NotNull(set);
        Assert.Equal("My.Analyzers", set.SourceName);
        Assert.Equal("1.4.0", set.SourceVersion);
    }

    [Fact]
    public void The_same_assemblies_named_in_either_order_produce_the_same_set()
    {
        // When two assemblies declare the same rule id the last one read wins, so an order that
        // followed the caller's would let the same two files yield two different catalogues.
        AnalyzerAssemblySet? one = LocalAssemblySource.Acquire([Generator, Tests], "n", "v");
        AnalyzerAssemblySet? other = LocalAssemblySource.Acquire([Tests, Generator], "n", "v");

        Assert.NotNull(one);
        Assert.NotNull(other);
        Assert.Equal(one.AssemblyPaths, other.AssemblyPaths);
    }

    [Fact]
    public void The_same_assembly_named_twice_is_read_once()
    {
        // Otherwise its analyzer types are counted twice, and the run reports a total that does not
        // match what it read.
        AnalyzerAssemblySet? set = LocalAssemblySource.Acquire([Generator, Generator], "n", "v");

        Assert.NotNull(set);
        Assert.Single(set.AssemblyPaths);
    }

    [Fact]
    public void An_assembly_that_came_with_a_dependency_graph_carries_it_forward()
    {
        // What the graph buys: the worker is run against the TARGET's dependencies, so an analyzer
        // compiled against another Roslyn resolves its own instead of being read through this
        // tool's. The worker's own assembly is used as the fixture because it is the one beside
        // these tests that ships a .deps.json.
        string withGraph = Path.Combine(AppContext.BaseDirectory, "CatalogGen.Worker.dll");

        AnalyzerAssemblySet? set = LocalAssemblySource.Acquire([withGraph], "n", "v");

        Assert.NotNull(set);
        Assert.Equal(Path.ChangeExtension(withGraph, ".deps.json"), set.DependencyContextPath);
    }

    [Fact]
    public void An_assembly_without_one_carries_nothing_rather_than_a_path_that_is_not_there()
    {
        // Assemblies extracted flat out of a package are the common case and have no graph at all.
        // Naming a file that does not exist would make the worker fail to start rather than fall
        // back to reading them through its own Roslyn, which is what it did before graphs existed.
        AnalyzerAssemblySet? set = LocalAssemblySource.Acquire([Generator], "n", "v");

        Assert.NotNull(set);
        Assert.False(File.Exists(Path.ChangeExtension(Generator, ".deps.json")),
                     "the fixture only means anything while the generator has no graph beside it");
        Assert.Null(set.DependencyContextPath);
    }

    [Fact]
    public void A_relative_path_is_resolved_before_it_reaches_the_reader()
    {
        // The reader is handed paths and nothing else — it has no working directory of its own, and
        // will not have one at all once it runs in a separate process.
        string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), Generator);

        AnalyzerAssemblySet? set = LocalAssemblySource.Acquire([relative], "n", "v");

        Assert.NotNull(set);
        Assert.Equal(Generator, Assert.Single(set.AssemblyPaths));
    }
}
