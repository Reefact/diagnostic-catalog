using System.Collections.Generic;

using Xunit;

namespace DiagnosticCatalog.Packaging.IntegrationTests;

/// <summary>
/// What a restore actually hands the compiler, and what it keeps out of the application.
/// </summary>
/// <remarks>
/// <para>
/// Every assertion is made against <c>@(Analyzer)</c> — the item list the compiler is invoked with —
/// rather than against a build log. A log cannot answer the question: MSBuild echoes each warning
/// again in its summary, so a raw count reads two for one analyzer, and Roslyn collapses two identical
/// diagnostics into one, so a count can read one for two.
/// </para>
/// <para>
/// The CODE FIX assembly is counted here beside the analyzer, and that is the half nothing measured
/// before. <c>DiagnosticCatalog.CodeFixes.dll</c> being present in the package is not the same claim
/// as it being handed to a compiler, which is not the same claim as it working —
/// <see cref="PackagedCodeFixTests"/> is the third.
/// </para>
/// </remarks>
public sealed class PackagedDeliveryTests
{
    private const string Analyzers = "DiagnosticCatalog.Analyzers.dll";

    private const string CodeFixes = "DiagnosticCatalog.CodeFixes.dll";

    private const string Attributes = "DiagnosticCatalog.dll";

    private static PackagedConsumption.Consumer Consumer(string name) =>
        PackagedConsumption.Current.Consumers[name];

    [Fact]
    public void One_catalogue_reference_delivers_the_analyzer_and_the_code_fixes()
    {
        PackagedConsumption.Consumer consumer = Consumer("Consumer");

        Assert.Equal(1, consumer.Instances(Analyzers));
        Assert.Equal(1, consumer.Instances(CodeFixes));
    }

    /// <summary>
    /// Two catalogues, one analyzer, one code-fix assembly. Both reach the compiler through the SAME
    /// package identity, so NuGet unifies it.
    /// </summary>
    /// <remarks>
    /// This is the check that would fail if the analyzers were ever folded into the catalogue packages
    /// instead — the alternative ADR-0037 rejected and ADR-0038 had to reject again. There the
    /// assemblies arrive from packages that version independently, added by path, with no identity for
    /// MSBuild to unify: which one runs is settled by whichever catalogue happens to carry the highest.
    /// </remarks>
    [Fact]
    public void Two_catalogues_still_deliver_exactly_one_of_each()
    {
        PackagedConsumption.Consumer consumer = Consumer("TwoCatalogues");

        Assert.Equal(1, consumer.Instances(Analyzers));
        Assert.Equal(1, consumer.Instances(CodeFixes));
    }

    /// <summary>
    /// A consumer two hops out receives neither. It chose neither the catalogue nor the library's
    /// reasons for taking one, and ADR-0038 is the whole of that.
    /// </summary>
    [Fact]
    public void A_consumer_two_hops_out_receives_neither()
    {
        PackagedConsumption.Consumer consumer = Consumer("TwoHops");

        Assert.Equal(0, consumer.Instances(Analyzers));
        Assert.Equal(0, consumer.Instances(CodeFixes));
    }

    [Fact]
    public void A_consumer_two_hops_out_can_ask_for_both()
    {
        PackagedConsumption.Consumer consumer = Consumer("TwoHopsOptIn");

        Assert.Equal(1, consumer.Instances(Analyzers));
        Assert.Equal(1, consumer.Instances(CodeFixes));
    }

    [Fact]
    public void A_direct_consumer_can_decline_both()
    {
        PackagedConsumption.Consumer consumer = Consumer("DirectOptOut");

        Assert.Equal(0, consumer.Instances(Analyzers));
        Assert.Equal(0, consumer.Instances(CodeFixes));
    }

    /// <summary>
    /// Neither analysis assembly becomes a runtime dependency of the consuming application, and the
    /// attribute assembly does.
    /// </summary>
    /// <remarks>
    /// One package now carries assemblies that must land on OPPOSITE sides of that line, so a packaging
    /// slip moving an analyzer into <c>lib/</c> is invisible to every in-process test and caught only
    /// from the far side of a restore. Asked of applications: a class library never copies package
    /// assemblies at all, so the same question put to one measures the SDK's copy rules.
    /// </remarks>
    [Theory]
    [InlineData("Consumer")]
    [InlineData("TwoHops")]
    [InlineData("DirectOptOut")]
    public void No_analysis_assembly_reaches_the_runtime_folder(string name)
    {
        PackagedConsumption.Consumer consumer = Consumer(name);

        Assert.False(consumer.Reached(Analyzers), $"{name} shipped {Analyzers} to its output folder");
        Assert.False(consumer.Reached(CodeFixes), $"{name} shipped {CodeFixes} to its output folder");
        Assert.True(consumer.Reached(Attributes), $"{name} did not receive {Attributes}");
    }

    /// <summary>
    /// Guards every count above against passing on an empty world: a consumer whose restore produced
    /// nothing would report zero analyzers, which three of these theories expect.
    /// </summary>
    [Fact]
    public void Every_consumer_was_really_built()
    {
        IReadOnlyDictionary<string, PackagedConsumption.Consumer> consumers =
            PackagedConsumption.Current.Consumers;

        Assert.Equal(5, consumers.Count);

        foreach (KeyValuePair<string, PackagedConsumption.Consumer> consumer in consumers)
        {
            Assert.True(
                consumer.Value.Output.Count > 0,
                $"{consumer.Key} produced no output folder, so nothing read from it means anything.");
        }
    }
}
