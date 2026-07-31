using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// Which dependency graph the worker is run against, and when handing one over would take more away
/// than it gives.
/// </summary>
/// <remarks>
/// <para>
/// An analyzer built by the SDK has a <c>.deps.json</c> beside it, and running the worker against it
/// is what lets an analyzer compiled against a different Roslyn be read through its own. But
/// <c>--depsfile</c> <em>replaces</em> the worker's graph rather than adding to it, so a graph that
/// does not carry Roslyn does not leave the worker with its own — it leaves it with none.
/// </para>
/// <para>
/// A <c>netstandard2.0</c> library's <c>.deps.json</c> is exactly that: its runtime target lists one
/// library, the assembly itself, and no framework at all. Handing it over used to make the worker's
/// own <c>Microsoft.CodeAnalysis</c> reference unresolvable, and the failure surfaced as an
/// unhandled <c>FileNotFoundException</c> and <c>the descriptor worker exited with 134</c> — a
/// stack trace where a diagnosis belonged.
/// </para>
/// </remarks>
public sealed class DependencyGraphHandoffTests : IDisposable
{
    private readonly DirectoryInfo _work = Directory.CreateTempSubdirectory("depsfile");

    public void Dispose() => _work.Delete(recursive: true);

    [Fact]
    public void An_assembly_whose_graph_carries_no_Roslyn_is_read_through_the_workers_own()
    {
        // The engine's own assembly declares no analyzer, so a complete read of it is an empty one —
        // which is the outcome asserted here, as distinct from the null that means "refused" and
        // from the crash this used to be.
        //
        // Read where it stands, with only the graph synthesised: moving an assembly away from its
        // own dependencies would fail the read for an unrelated reason and prove nothing about the
        // graph.
        string graph = Path.Combine(_work.FullName, "library.deps.json");
        File.WriteAllText(graph, LibraryGraph("CatalogGen"));
        AnalyzerAssemblySet set = new([typeof(DescriptorReader).Assembly.Location], "Fixture", "1.0.0", graph);

        Assert.Empty(Assert.IsType<SortedDictionary<string, RuleInfo>>(DescriptorReader.Read(set)));
    }

    [Fact]
    public void A_graph_that_carries_no_Roslyn_is_not_handed_to_the_worker_at_all()
    {
        // The acquisition is where the decision belongs: a graph that cannot supply what the worker
        // needs is worse than no graph, because --depsfile replaces rather than extends. Asserting
        // it here as well as through a read keeps the reason visible when the read is only slow.
        string assembly = Beside(typeof(DescriptorReader).Assembly.Location, LibraryGraph("CatalogGen"));

        AnalyzerAssemblySet? acquired = LocalAssemblySource.Acquire([assembly], "Fixture", "1.0.0");

        Assert.NotNull(acquired);
        Assert.Null(acquired.DependencyContextPath);
    }

    // Copies an assembly somewhere of its own and writes the given graph beside it, which is the
    // layout the SDK produces and the one the acquisition looks for.
    private string Beside(string assembly, string graph)
    {
        string copy = Path.Combine(_work.FullName, Path.GetFileName(assembly));
        File.Copy(assembly, copy, overwrite: true);
        File.WriteAllText(Path.ChangeExtension(copy, ".deps.json"), graph);

        return copy;
    }

    // What `dotnet build` writes beside a netstandard2.0 library: a runtime target naming the
    // assembly and nothing else. Reproduced verbatim rather than referenced from a sibling project's
    // output, so the fixture stays the thing under test rather than a build artefact that could
    // change shape.
    private static string LibraryGraph(string name) => $$"""
        {
          "runtimeTarget": { "name": ".NETStandard,Version=v2.0/", "signature": "" },
          "compilationOptions": {},
          "targets": {
            ".NETStandard,Version=v2.0": {},
            ".NETStandard,Version=v2.0/": {
              "{{name}}/1.0.0": { "runtime": { "{{name}}.dll": {} } }
            }
          },
          "libraries": {
            "{{name}}/1.0.0": { "type": "project", "serviceable": false, "sha512": "" }
          }
        }
        """;
}
