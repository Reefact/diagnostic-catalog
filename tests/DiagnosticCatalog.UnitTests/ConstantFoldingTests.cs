using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Xunit;

namespace DiagnosticCatalog.UnitTests;

/// <summary>
/// The premise the whole library rests on: a rule's members must remain usable as attribute
/// arguments, and must fold to the literals the platform will actually match on.
/// <para>
/// This file compiling at all is half the assertion. If <c>Id</c> or <c>Category</c> ever stopped
/// being a compile-time constant, the suppressions below would not build — which is exactly the
/// failure the design exists to make loud.
/// </para>
/// </summary>
public sealed class ConstantFoldingTests
{
    [DiagnosticCategory]
    private static class TestCategory
    {
        public const string Usage = "Usage";
    }

    private static class TestRules
    {
        [DiagnosticRule]
        [SuppressMessage(
            "Major Code Smell",
            "S101:Types should be named in PascalCase",
            Justification =
                "A fixture rule's name IS its identifier: it is read back through nameof and "
                + "asserted as such. Real rule ids are shouty by nature — S1144, CA1822, SA1000 — "
                + "so PascalCase here would change the constant under test.")]
        public static class TEST0001
        {
            public const string Id = nameof(TEST0001);
            public const string Category = "Usage";
        }

        /// <summary>
        /// The category arrives through a second constant rather than a literal, which is how a
        /// generated catalog declares each category once instead of repeating it per rule.
        /// </summary>
        [DiagnosticRule]
        [SuppressMessage(
            "Major Code Smell",
            "S101:Types should be named in PascalCase",
            Justification =
                "A fixture rule's name IS its identifier: it is read back through nameof and "
                + "asserted as such. Real rule ids are shouty by nature — S1144, CA1822, SA1000 — "
                + "so PascalCase here would change the constant under test.")]
        public static class TEST0002
        {
            public const string Id = nameof(TEST0002);
            public const string Category = TestCategory.Usage;
        }
    }

    [SuppressMessage(
        TestRules.TEST0001.Category,
        TestRules.TEST0001.Id,
        Justification = "Subject of the constant-folding test.")]
    private sealed class DirectlyDeclaredSubject
    {
    }

    [SuppressMessage(
        TestRules.TEST0002.Category,
        TestRules.TEST0002.Id,
        Justification = "Subject of the constant-chain test.")]
    private sealed class ChainedCategorySubject
    {
    }

    /// <summary>
    /// What the compiler wrote into metadata is what Roslyn will read, so the folded values are
    /// the ones that decide whether a suppression matches anything.
    /// </summary>
    [Fact]
    public void Rule_members_fold_to_their_literals_in_metadata()
    {
        SuppressMessageAttribute? attribute = typeof(DirectlyDeclaredSubject)
            .GetCustomAttribute<SuppressMessageAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("TEST0001", attribute.CheckId);
        Assert.Equal("Usage", attribute.Category);
    }

    /// <summary>
    /// A const initialised from another const is still a compile-time constant, so declaring each
    /// category once costs nothing at the use site. Were that not so, a catalog would have to
    /// repeat every category literal per rule — 456 declarations for 13 values in the Sonar case.
    /// </summary>
    [Fact]
    public void A_category_reached_through_a_second_constant_folds_the_same_way()
    {
        SuppressMessageAttribute? attribute = typeof(ChainedCategorySubject)
            .GetCustomAttribute<SuppressMessageAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("TEST0002", attribute.CheckId);
        Assert.Equal("Usage", attribute.Category);
    }

    /// <summary>
    /// nameof keeps the class name and the identifier in step by construction, which is the whole
    /// reason it is the recommended form.
    /// </summary>
    [Fact]
    public void Nameof_keeps_the_identifier_equal_to_the_type_name()
    {
        Assert.Equal(TestRules.TEST0001.Id, typeof(TestRules.TEST0001).Name);
        Assert.Equal(TestRules.TEST0002.Id, typeof(TestRules.TEST0002).Name);
    }

    /// <summary>
    /// A rule type is a static class, which the CLR represents as abstract and sealed. The
    /// analyzer's structural check reads exactly this shape out of referenced metadata.
    /// </summary>
    [Fact]
    public void A_rule_type_is_static_in_metadata()
    {
        Type rule = typeof(TestRules.TEST0001);

        Assert.True(rule.IsAbstract);
        Assert.True(rule.IsSealed);
        Assert.NotNull(rule.GetCustomAttribute<DiagnosticRuleAttribute>());
    }

    /// <summary>
    /// The marker has to be readable by its fully qualified name alone, because that is how the
    /// analyzers find it when a catalog embeds its own copy rather than referencing this package.
    /// </summary>
    [Fact]
    public void The_marker_is_discoverable_by_metadata_name_alone()
    {
        bool marked = typeof(TestRules.TEST0001)
            .GetCustomAttributes()
            .Any(a => a.GetType().FullName == "DiagnosticCatalog.DiagnosticRuleAttribute");

        Assert.True(marked);
    }
}

/// <summary>
/// Provenance is recorded so tooling can act on it without the catalog's source.
/// </summary>
public sealed class CatalogSourceTests
{
    [Fact]
    public void It_round_trips_the_upstream_release_it_mirrors()
    {
        CatalogSourceAttribute source = new("SonarAnalyzer.CSharp", "10.31.0.145097", "2026-07-30");

        Assert.Equal("SonarAnalyzer.CSharp", source.Source);
        Assert.Equal("10.31.0.145097", source.SourceVersion);
        Assert.Equal("2026-07-30", source.GeneratedOn);
    }

    /// <summary>
    /// The date is a string because no date type can be a compile-time constant, so the format is
    /// the only thing keeping it machine-readable. A catalog whose date did not parse would defeat
    /// the staleness check the attribute exists to enable.
    /// </summary>
    [Fact]
    public void Its_date_parses_as_an_ISO_8601_calendar_date()
    {
        CatalogSourceAttribute source = new("Acme.Analyzers", "1.2.3", "2026-07-30");

        bool parsed = DateTime.TryParseExact(
            source.GeneratedOn,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime date);

        Assert.True(parsed);

        // Unspecified, stated rather than defaulted into: DateTimeStyles.None is what makes the
        // parse yield it, and a catalogue stamps a calendar DATE — the day the generator ran — not
        // an instant anybody could place on a timeline. Naming the kind here says that on purpose.
        // Leaving it off would say the same thing by accident.
        Assert.Equal(new DateTime(2026, 7, 30, 0, 0, 0, DateTimeKind.Unspecified), date);
        Assert.Equal(DateTimeKind.Unspecified, date.Kind);
    }
}
