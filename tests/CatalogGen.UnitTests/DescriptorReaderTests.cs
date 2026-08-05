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
    public void A_type_no_analyzer_depends_on_does_not_fail_the_read_when_it_cannot_be_loaded()
    {
        // What an analyzer package is mostly made of is not analyzers: code fixes, internal
        // helpers, types the compiler never loads because no attribute points at them. Their rules
        // are not missing when they fail to load, because they declare none — so a read that lost
        // one lost nothing, and refusing it refuses a catalogue that was complete.
        //
        // The compiler never has to make that judgement: it finds analyzers by reading metadata for
        // the types [DiagnosticAnalyzer] names, and loads those. Selecting the same way is what
        // makes the shortfall answerable here too — every analyzer the assembly declares is known
        // by name before anything is loaded, so "did one go missing" stops being a question about
        // types that have no name left to ask about.
        string fixture = Path.Combine(AppContext.BaseDirectory, "partial-load-fixture",
                                      "CatalogGen.PartialLoadFixture.dll");
        Assert.True(File.Exists(fixture),
                    "the partial-load fixture should be built and copied beside the tests by _CopyAnalyzerFixtures");

        // The absence IS the fixture: CatalogGen.PartialLoadFixture implements an interface declared
        // in CatalogGen.AbsentContract, so the helper type cannot be materialised anywhere that
        // assembly is not. Asserted rather than assumed, because a stray copy beside it would leave
        // this test asserting nothing.
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(fixture)!, "CatalogGen.AbsentContract.dll")),
                     "the partial-load fixture should be alone, so its helper type stays unloadable");

        AnalyzerAssemblySet set = new([fixture], "Fixture", "1.0.0");

        SortedDictionary<string, RuleInfo>? read = DescriptorReader.Read(set);

        Assert.NotNull(read);
        Assert.Contains("FIX0002", read.Keys);
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
/// <para>
/// It carries <c>[DiagnosticAnalyzer]</c> because it has to be FOUND before it can fail to be
/// constructed: the reader selects the types the attribute names (ADR-0031), so without it this
/// fixture would be passed over and the test above would assert a refusal that never happened.
/// </para>
/// <para>
/// That reverses an earlier trade. While the reader selected on the base type alone the attribute
/// added nothing here, and leaving it off avoided marking the whole test assembly as a compiler
/// extension — which is what the three rules below then report, every one of them about a fact of
/// this project that is not a defect and cannot be changed: it reaches
/// Microsoft.CodeAnalysis.Workspaces through the generator, it targets net10.0 because that is what
/// a test project targets, and it is a test project rather than a compiler extension, so the
/// authoring rules RS1036 asks it to enforce would be enforced over a hundred and fifty tests that
/// declare no analyzer. Suppressed at the declaration, which is the only place the claim is true.
/// The fixture that really is a compiler extension — CatalogGen.PartialLoadFixture — sets the
/// property instead and suppresses nothing.
/// </para>
/// </remarks>
#pragma warning disable RS1036 // Specify EnforceExtendedAnalyzerRules
#pragma warning disable RS1038 // Compiler extensions should be implemented in assemblies targeting netstandard2.0
#pragma warning disable RS1041 // Compiler extensions should not target a specific compiler host
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnconstructibleAnalyzer : DiagnosticAnalyzer
#pragma warning restore RS1041
#pragma warning restore RS1038
#pragma warning restore RS1036
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
