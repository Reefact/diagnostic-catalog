using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// What the reader does when it could not read everything it was given.
/// </summary>
/// <remarks>
/// <para>
/// This is the one failure the generator cannot afford to absorb. A rule the reader dropped is
/// absent from the catalogue, and an absent rule is indistinguishable from one the vendor retired
/// — so the emitter carries it forward and states, in an <c>[Obsolete]</c> message a consumer
/// reads, that the vendor no longer declares it. A partial read does not merely produce a
/// catalogue that is short; it produces one that is wrong about somebody else's product.
/// </para>
/// <para>
/// Nothing downstream can catch it, for the reason ADR-0009 gives: the platform never validates a
/// suppression's category, so a catalogue that has silently lost rules produces no symptom in any
/// consumer's build. The reader is the last place the truth is still known.
/// </para>
/// </remarks>
public sealed class DescriptorReaderTests
{
    [Fact]
    public void The_worker_is_deployed_beside_whatever_reads()
    {
        // Named rather than inferred, because its absence is invisible in the tests that matter:
        // reading with no worker returns null, which is exactly what the two refusal tests below
        // assert. Without this they would keep passing while nothing was ever read.
        Assert.True(
            File.Exists(Path.Combine(AppContext.BaseDirectory, "CatalogGen.Worker.dll")),
            "the descriptor worker should be bundled beside its caller by build/BundleDescriptorWorker.props");
    }

    [Fact]
    public void An_analyzer_that_cannot_be_constructed_fails_the_read()
    {
        // The fixture below lives in this assembly, so reading this assembly reaches an analyzer
        // whose construction throws — which is what the generator meets when an upstream analyzer
        // was compiled against a Roslyn it does not tolerate.
        AnalyzerAssemblySet set = new([typeof(UnconstructibleAnalyzer).Assembly.Location], "Fixture", "1.0.0");

        Assert.Null(DescriptorReader.Read(set));
    }

    [Fact]
    public void An_assembly_that_cannot_be_loaded_fails_the_read()
    {
        // Worse than a construction failure, because the assembly contributes no analyzer type at
        // all: there is nothing to count, so the run cannot even report a shortfall.
        string notAnAssembly = Path.Combine(Path.GetTempPath(), $"cataloggen-not-an-assembly-{Guid.NewGuid():N}.dll");
        File.WriteAllText(notAnAssembly, "this is not a portable executable");
        try
        {
            AnalyzerAssemblySet set = new([notAnAssembly], "Fixture", "1.0.0");

            Assert.Null(DescriptorReader.Read(set));
        }
        finally
        {
            File.Delete(notAnAssembly);
        }
    }

    [Fact]
    public void An_analyzer_compiled_against_the_async_interfaces_facade_is_read_rather_than_refused()
    {
        // The worker HOSTS analyzers, and hosting them means supplying what the runtime has under a
        // name it does not answer to. A netstandard2.0 analyzer reaches IAsyncEnumerable<T> through
        // Microsoft.Bcl.AsyncInterfaces; the shared framework carries the types and not that
        // identity, and an analyzer package carries neither — it ships its own assembly and declares
        // no dependencies. So the identity can only come from the host, and when it does not,
        // Assembly.GetTypes() loses every type that mentions it.
        //
        // What that costs is the whole point. The analyzer itself still loads and its descriptor is
        // still read, so the refusal is not protecting a rule that went missing — it is protecting
        // against the possibility, which the reader cannot rule out because a type that failed to
        // load has no name to ask about. Measured against the real packages: reading
        // Microsoft.CodeAnalysis.CSharp.CodeStyle, Meziantou.Analyzer and
        // Microsoft.CodeAnalysis.PublicApiAnalyzers yields exactly the same descriptors before and
        // after the facade is deployed — 121, 210 and 23 — and all three are refused without it.
        string fixture = Path.Combine(AppContext.BaseDirectory, "analyzer-fixture", "CatalogGen.AnalyzerFixture.dll");
        Assert.True(File.Exists(fixture),
                    "the analyzer fixture should be built and copied beside the tests by _CopyAnalyzerFixture");

        // Named, because its presence would make this test pass without the worker deploying
        // anything: the worker probes the directories of the assemblies it is given, so a facade
        // sitting beside the fixture resolves the identity there and the read succeeds for a reason
        // that has nothing to do with the fix.
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(fixture)!, "Microsoft.Bcl.AsyncInterfaces.dll")),
                     "the fixture should be alone in its folder, as an analyzer is alone in its package");

        AnalyzerAssemblySet set = new([fixture], "Fixture", "1.0.0");

        SortedDictionary<string, RuleInfo>? read = DescriptorReader.Read(set);

        Assert.NotNull(read);
        Assert.Contains("FIX0001", read.Keys);
    }

    [Fact]
    public void An_assembly_declaring_no_analyzer_is_read_without_failing()
    {
        // The guard is against an INCOMPLETE read, not an empty one. The generator's own assembly
        // declares no analyzer, and reading it drops nothing — so it must not be refused, or the
        // guard would fail runs that read exactly what they were given.
        AnalyzerAssemblySet set = new([typeof(DescriptorReader).Assembly.Location], "Fixture", "1.0.0");

        Assert.Empty(Assert.IsType<SortedDictionary<string, RuleInfo>>(DescriptorReader.Read(set)));
    }
}

/// <summary>
/// An analyzer that cannot be constructed. Real rather than mocked: the reader constructs every
/// analyzer type it finds, so the only way to exercise a construction failure is to give it one.
/// </summary>
/// <remarks>
/// Deliberately carries no <c>[DiagnosticAnalyzer]</c> attribute. The reader selects on the base
/// type alone — every non-abstract <see cref="DiagnosticAnalyzer"/> it finds — so the attribute
/// would add nothing here, while declaring it marks the whole test assembly as a compiler
/// extension: RS1038 and RS1041 then fire on facts about this project that cannot be changed (it
/// reaches Microsoft.CodeAnalysis.Workspaces through the generator, and targets net10.0), and CI's
/// warning ratchet turns both into errors. Leaving it off costs one suppression instead of three.
/// </remarks>
// RS1001 is the mirror image of that trade: it wants the attribute on a DiagnosticAnalyzer
// subclass, which is right for an analyzer somebody ships and wrong for a fixture that exists to
// fail construction. It is never registered with a compilation and never runs.
#pragma warning disable RS1001 // Missing 'DiagnosticAnalyzerAttribute' attribute
public sealed class UnconstructibleAnalyzer : DiagnosticAnalyzer
#pragma warning restore RS1001
{
    public UnconstructibleAnalyzer()
        => throw new InvalidOperationException("deliberately unconstructible, see DescriptorReaderTests");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [];

    // Unreachable — the constructor above throws before any of this can be registered. Written out
    // rather than left empty because RS1025 and RS1026 are static checks on the override, and
    // satisfying them costs two calls where suppressing them would cost two more pragmas.
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}
