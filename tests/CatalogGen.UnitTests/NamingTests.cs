using System;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// Turning a vendor's category string into a C# identifier is where a catalogue's public surface is
/// decided. Every name these produce becomes a <c>public const</c> a consumer references by hand, so
/// the rules below are a published contract rather than an implementation detail.
/// </summary>
public sealed class NamingTests
{
    [Theory]
    // The two that actually ship, spelled out so a change to either is visible in the diff.
    [InlineData("Major Code Smell", "MajorCodeSmell")]
    [InlineData("StyleCop.CSharp.SpacingRules", "StyleCopCSharpSpacingRules")]
    // Any run of non-alphanumerics is a word break, whatever it is made of.
    [InlineData("a-b", "AB")]
    [InlineData("a   b", "AB")]
    [InlineData("Usage", "Usage")]
    public void A_category_becomes_its_punctuation_stripped_pascal_case(string category, string expected)
        => Assert.Equal(expected, Naming.ToIdentifier(category));

    [Fact]
    public void An_identifier_that_would_start_with_a_digit_gains_a_leading_underscore()
        => Assert.Equal("_1Rule", Naming.ToIdentifier("1 rule"));

    [Fact]
    public void A_category_with_nothing_usable_in_it_falls_back_to_a_fixed_name()
    {
        Assert.Equal("Unnamed", Naming.ToIdentifier(string.Empty));
        Assert.Equal("Unnamed", Naming.ToIdentifier("   "));
        Assert.Equal("Unnamed", Naming.ToIdentifier("-.-"));
    }

    [Fact]
    public void Two_categories_differing_only_in_punctuation_produce_the_same_identifier()
    {
        // Documented, not desired. Collapsing them here is exactly why the emitter carries its own
        // disambiguation pass (see CategoryCollisionTests) — this function cannot see the other
        // categories, so it cannot resolve the clash by itself.
        Assert.Equal(
            Naming.ToIdentifier("Major Code Smell"),
            Naming.ToIdentifier("Major-Code-Smell"));
    }

    [Fact]
    public void The_common_prefix_is_deliberately_not_stripped()
    {
        // StyleCop's categories all begin with "StyleCop.CSharp.", and dropping that prefix would
        // read better. It is left in on purpose: the common prefix changes the day upstream adds a
        // category outside it, which would rename every existing constant at once and break every
        // consumer that referenced one (specification §23.1).
        Assert.StartsWith("StyleCop", Naming.ToIdentifier("StyleCop.CSharp.OrderingRules"), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("a \"quoted\" value")]
    [InlineData(@"a back\slash")]
    [InlineData(@"both \ and """)]
    public void Escaping_a_value_for_a_c_sharp_literal_is_reversible(string value)
        => Assert.Equal(value, Naming.Unescape(Naming.Escape(value)));

    [Fact]
    public void Escaping_produces_the_literal_body_a_compiler_would_accept()
    {
        Assert.Equal("a \\\"quoted\\\" value", Naming.Escape("a \"quoted\" value"));
        Assert.Equal("a back\\\\slash", Naming.Escape(@"a back\slash"));
    }

    [Theory]
    // Despite the name, this yields the parent directory's NAME, not a path to it: it is what the
    // generator prints when reporting which package folder an assembly was loaded from.
    [InlineData("a/b/c.dll", "b")]
    [InlineData("b/c.dll", "b")]
    [InlineData("c.dll", "")]
    public void The_parent_directory_helper_returns_a_name_rather_than_a_path(string path, string expected)
        => Assert.Equal(expected, Naming.ParentDir(path));
}
