using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Xunit;

namespace DiagnosticCatalog.UnitTests;

/// <summary>
/// The invariants the foundation's attributes have to keep for anything downstream to work.
/// Each one is a constraint that a plausible edit would break silently.
/// </summary>
public sealed class AttributeContractTests
{
    /// <summary>
    /// The single most destructive edit available to a future maintainer. Rule discovery reads
    /// these markers out of the metadata of *referenced* assemblies; a conditional attribute is
    /// not emitted there unless the declaring assembly was compiled with the symbol defined. Every
    /// catalog shipped as a package would become invisible — no rules found, no diagnostics
    /// reported, no error anywhere to explain it.
    /// </summary>
    [Theory]
    [InlineData(typeof(DiagnosticRuleAttribute))]
    [InlineData(typeof(DiagnosticCategoryAttribute))]
    [InlineData(typeof(CatalogSourceAttribute))]
    public void Attribute_is_never_conditional(Type attributeType)
    {
        ConditionalAttribute? conditional = attributeType.GetCustomAttribute<ConditionalAttribute>();

        Assert.Null(conditional);
    }

    /// <summary>
    /// The platform fact the zero-footprint promise rests on, asserted rather than recalled:
    /// SuppressMessageAttribute carries [Conditional("CODE_ANALYSIS")], so it reaches metadata
    /// only where that symbol is defined. If a future .NET release dropped it, the claim that a
    /// catalog-based suppression leaves no trace would quietly stop being true.
    /// </summary>
    [Fact]
    public void SuppressMessageAttribute_is_conditional_on_CODE_ANALYSIS()
    {
        ConditionalAttribute[] conditionals = typeof(SuppressMessageAttribute)
            .GetCustomAttributes<ConditionalAttribute>()
            .ToArray();

        Assert.Contains(conditionals, c => c.ConditionString == "CODE_ANALYSIS");
    }

    /// <summary>
    /// UnconditionalSuppressMessageAttribute is the deliberate counterpart: it carries no
    /// [Conditional] precisely so trimming tools can read it back out of the compiled assembly.
    /// The asymmetry is why the two attributes are not interchangeable.
    /// <para>
    /// Excluded from the .NET Framework 4.7.2 leg because the type is internal there — it is a
    /// trimming concept, and .NET Framework has no trimmer. The catalogs' own rules never rely on
    /// it, so the floor loses nothing by not asserting it.
    /// </para>
    /// </summary>
#if NET5_0_OR_GREATER
    [Fact]
    public void UnconditionalSuppressMessageAttribute_is_not_conditional()
    {
        ConditionalAttribute? conditional = typeof(UnconditionalSuppressMessageAttribute)
            .GetCustomAttribute<ConditionalAttribute>();

        Assert.Null(conditional);
    }
#endif

    [Fact]
    public void DiagnosticRuleAttribute_targets_a_class_once()
    {
        AttributeUsageAttribute? usage = typeof(DiagnosticRuleAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Fact]
    public void DiagnosticCategoryAttribute_targets_a_class_once()
    {
        AttributeUsageAttribute? usage = typeof(DiagnosticCategoryAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    /// <summary>
    /// CatalogSource allows multiple because one catalog assembly may legitimately mirror several
    /// upstream packages — a C# and a Visual Basic analyzer from the same vendor, say.
    /// </summary>
    [Fact]
    public void CatalogSourceAttribute_targets_an_assembly_and_allows_several()
    {
        AttributeUsageAttribute? usage = typeof(CatalogSourceAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Assembly, usage.ValidOn);
        Assert.True(usage.AllowMultiple);
    }

    /// <summary>
    /// The analyzers match the marker by its fully qualified metadata name rather than by symbol
    /// identity, so that a catalog may embed its own copy and carry no package dependency. Moving
    /// or renaming the type would break that matching for every such catalog at once, which is why
    /// it is a major version — and why the name is asserted here rather than assumed.
    /// </summary>
    [Theory]
    [InlineData(typeof(DiagnosticRuleAttribute), "DiagnosticCatalog.DiagnosticRuleAttribute")]
    [InlineData(typeof(DiagnosticCategoryAttribute), "DiagnosticCatalog.DiagnosticCategoryAttribute")]
    [InlineData(typeof(CatalogSourceAttribute), "DiagnosticCatalog.CatalogSourceAttribute")]
    public void Attribute_keeps_its_fully_qualified_metadata_name(Type attributeType, string expected)
    {
        Assert.Equal(expected, attributeType.FullName);
    }
}
