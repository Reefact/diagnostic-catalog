using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Xunit;

namespace DiagnosticCatalog.Catalogs.UnitTests;

/// <summary>
/// Invariants every generated catalog must keep, checked against the metadata a consumer's
/// compiler actually reads.
/// <para>
/// These run over whatever the catalogs currently contain rather than over a recorded snapshot, so
/// they stay true across an upstream bump instead of failing on every legitimate regeneration.
/// Rule counts are deliberately not asserted for the same reason.
/// </para>
/// </summary>
public sealed class GeneratedCatalogTests
{
    public static TheoryData<string> Catalogs() =>
    [
        "DiagnosticCatalog.Sonar",
        "DiagnosticCatalog.NetAnalyzers",
        "DiagnosticCatalog.StyleCop",
        "DiagnosticCatalog.CodeStyle",
        "DiagnosticCatalog.Xunit",
        "DiagnosticCatalog.NUnit",
        "DiagnosticCatalog.MSTest",
        "DiagnosticCatalog.Trimming",
    ];

    private static Assembly Load(string name) =>
        // Referenced at compile time, so a plain Load resolves against the copied output.
        Assembly.Load(new AssemblyName(name));

    private static List<Type> RuleTypesOf(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t.GetCustomAttributes().Any(a =>
                a.GetType().FullName == "DiagnosticCatalog.DiagnosticRuleAttribute"))
            .ToList();

    private static string ConstantValue(Type rule, string member) =>
        (string)rule.GetField(member, BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;

    [Theory]
    [MemberData(nameof(Catalogs))]
    public void Every_rule_exposes_a_non_empty_id_and_category(string assemblyName)
    {
        List<Type> rules = RuleTypesOf(Load(assemblyName));

        Assert.NotEmpty(rules);
        foreach (Type rule in rules)
        {
            Assert.False(string.IsNullOrWhiteSpace(ConstantValue(rule, "Id")), $"{rule.FullName}.Id");
            Assert.False(string.IsNullOrWhiteSpace(ConstantValue(rule, "Category")), $"{rule.FullName}.Category");
        }
    }

    /// <summary>
    /// Both members must be genuine constants, not static readonly fields: only a constant can be
    /// an attribute argument, so this is the difference between a catalog that works and one that
    /// does not compile at any use site.
    /// </summary>
    [Theory]
    [MemberData(nameof(Catalogs))]
    public void Every_rule_member_is_a_literal_constant(string assemblyName)
    {
        foreach (Type rule in RuleTypesOf(Load(assemblyName)))
        {
            foreach (string member in new[] { "Id", "Category" })
            {
                FieldInfo field = rule.GetField(member, BindingFlags.Public | BindingFlags.Static)!;
                Assert.True(field.IsLiteral, $"{rule.FullName}.{member} must be const");
                Assert.False(field.IsInitOnly, $"{rule.FullName}.{member} must not be static readonly");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Catalogs))]
    public void Rule_identifiers_are_unique_and_match_their_type_name(string assemblyName)
    {
        List<Type> rules = RuleTypesOf(Load(assemblyName));

        foreach (Type rule in rules)
        {
            Assert.Equal(rule.Name, ConstantValue(rule, "Id"));
        }

        List<string> duplicates = rules
            .GroupBy(r => ConstantValue(r, "Id"), StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.Empty(duplicates);
    }

    /// <summary>
    /// Every category a rule carries is one the catalog's category class declares — the two are
    /// consistent, and the class is complete.
    /// <para>
    /// It deliberately does NOT establish that the rule reached its category through that class:
    /// constant folding erases the difference, so a rule repeating the literal produces byte-identical
    /// metadata. That half is checked on the source, in <see cref="GeneratedCatalogSourceTests"/>.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Catalogs))]
    public void Every_category_is_declared_by_the_catalogs_category_class(string assemblyName)
    {
        Assembly assembly = Load(assemblyName);

        Type categoryClass = assembly.GetTypes().Single(t => t.GetCustomAttributes().Any(a =>
            a.GetType().FullName == "DiagnosticCatalog.DiagnosticCategoryAttribute"));

        // ToHashSet does not exist on .NET Framework; the constructor overload does.
        HashSet<string> declared = new(
            categoryClass
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!),
            StringComparer.Ordinal);

        Assert.NotEmpty(declared);
        foreach (Type rule in RuleTypesOf(assembly))
        {
            Assert.Contains(ConstantValue(rule, "Category"), declared);
        }
    }

    /// <summary>
    /// A container named after the first segment of its own namespace is unusable: at a consumer's
    /// use site the namespace is a member of the global namespace and is found before any type a
    /// using-directive imported, so every reference fails with CS0234 and the consumer has no way
    /// around it. This shipped once and is exactly the kind of defect only a use site reveals.
    /// </summary>
    [Theory]
    [MemberData(nameof(Catalogs))]
    public void No_container_is_shadowed_by_the_first_segment_of_its_namespace(string assemblyName)
    {
        Assembly assembly = Load(assemblyName);

        IEnumerable<Type> containers = RuleTypesOf(assembly)
            .Select(r => r.DeclaringType)
            .Where(t => t is not null)
            .Distinct()!;

        foreach (Type container in containers)
        {
            string firstSegment = container.Namespace!.Split('.')[0];
            Assert.NotEqual(firstSegment, container.Name);
        }
    }

    /// <summary>
    /// A mirrored catalog is a snapshot, and the attribute is the only thing in the compiled
    /// assembly that says which snapshot. Tooling that flags a stale catalog reads exactly this.
    /// </summary>
    [Theory]
    [MemberData(nameof(Catalogs))]
    public void The_catalog_records_the_upstream_release_it_mirrors(string assemblyName)
    {
        Attribute source = Load(assemblyName).GetCustomAttributes()
            .Single(a => a.GetType().FullName == "DiagnosticCatalog.CatalogSourceAttribute");

        Type type = source.GetType();
        string Read(string property) => (string)type.GetProperty(property)!.GetValue(source)!;

        Assert.False(string.IsNullOrWhiteSpace(Read("Source")));
        Assert.False(string.IsNullOrWhiteSpace(Read("SourceVersion")));
        Assert.True(
            DateTime.TryParseExact(Read("GeneratedOn"), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            $"{assemblyName} records a generation date that is not an ISO 8601 calendar date.");
    }

    /// <summary>
    /// A rule the upstream package retired is kept and marked obsolete rather than deleted, so a
    /// consumer that still references it gets a warning naming the rule instead of a compile error
    /// about a member that vanished. It therefore has to stay fully formed, not become a husk.
    /// </summary>
    [Theory]
    [MemberData(nameof(Catalogs))]
    public void A_retired_rule_stays_usable(string assemblyName)
    {
        IEnumerable<Type> retired = RuleTypesOf(Load(assemblyName))
            .Where(r => r.GetCustomAttribute<ObsoleteAttribute>() is not null);

        foreach (Type rule in retired)
        {
            Assert.Equal(rule.Name, ConstantValue(rule, "Id"));
            Assert.False(string.IsNullOrWhiteSpace(ConstantValue(rule, "Category")));
        }
    }
}
