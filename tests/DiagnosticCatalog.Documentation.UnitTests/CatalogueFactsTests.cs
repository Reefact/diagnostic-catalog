using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The specification's catalogue table is recounted, row by row, from the manifest that produces the
/// catalogues and from the assemblies they compile into.
/// </summary>
/// <remarks>
/// <para>
/// That table is the one place in the documentation that claims to state every catalogue's facts
/// exhaustively — which package it mirrors, at which release, how many rules it publishes, how many
/// of them carry a help link. Every one of those moves without anybody editing prose: the nightly job
/// regenerates a catalogue when upstream moves, and nothing connects the regenerated file to a
/// document. The table had already drifted on two counts at once — a StyleCop row naming the stable
/// line the repository stopped mirroring, and a help-link column comparing published constants
/// against a descriptor count only the vendor knows.
/// </para>
/// <para>
/// <b>Live rules and published constants are counted separately</b>, because they are different
/// numbers and the difference is the whole of §23.1: a rule retired upstream is carried forward and
/// marked <c>[Obsolete]</c> rather than deleted, so the constants a catalogue publishes only ever
/// grow while the rules the mirrored release still declares can shrink. They happen to be equal on
/// every row today, and a table that stated one number would go quietly wrong the first time a vendor
/// retires anything.
/// </para>
/// <para>
/// Read from the compiled assemblies rather than from the generated source, because that is what a
/// consumer's compiler reads. The one thing metadata cannot answer is which rules are retired —
/// <c>[Obsolete]</c> survives, so it can — and which release is mirrored, which the
/// <c>CatalogSource</c> attribute carries. Both are there.
/// </para>
/// <para>
/// The table is delimited by <c>&lt;!-- catalogue-facts:begin --&gt;</c> so this reads a region the
/// document declares rather than guessing which of its tables is the exhaustive one.
/// </para>
/// </remarks>
public sealed class CatalogueFactsTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    private const string FactsBegin = "<!-- catalogue-facts:begin -->";

    private const string FactsEnd = "<!-- catalogue-facts:end -->";

    private static readonly Dictionary<string, string> Specification = new(StringComparer.Ordinal)
    {
        ["en"] = "doc/specification.en.md",
        ["fr"] = "doc/specification.fr.md",
    };

    public static TheoryData<string> Languages() => ["en", "fr"];

    /// <summary>What one row of the table states.</summary>
    private sealed record Row(
        string Catalogue, string Mirrors, int LiveRules, int PublishedConstants, int Categories, int HelpLinks);

    /// <summary>What a catalogue actually is, read from the assembly it compiles into.</summary>
    private sealed record Facts(
        string Mirrors, int LiveRules, int PublishedConstants, int Categories, int HelpLinks);

    [Theory]
    [MemberData(nameof(Languages))]
    public void The_table_names_every_vendor_catalogue_and_only_those(string language)
    {
        IReadOnlyList<string> stated = [.. Stated(language).Select(row => row.Catalogue)];
        IReadOnlyList<string> generated =
            [.. CatalogueManifest.Vendor.Select(catalogue => catalogue.Namespace)];

        List<string> missing = [.. generated.Except(stated, StringComparer.Ordinal)];
        Assert.True(
            missing.Count == 0,
            $"{Specification[language]} states the catalogue table without a row for " +
            $"{string.Join(", ", missing)}. The table claims to be exhaustive, so a catalogue absent " +
            "from it is one the normative document says does not exist.");

        List<string> invented = [.. stated.Except(generated, StringComparer.Ordinal)];
        Assert.True(
            invented.Count == 0,
            $"{Specification[language]} states a row for {string.Join(", ", invented)}, which " +
            "eng/catalogs.json generates no catalogue for.");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_figure_the_table_states_is_the_figure_the_catalogue_carries(string language)
    {
        foreach (Row row in Stated(language))
        {
            Facts facts = Measure(row.Catalogue);

            Assert.True(
                string.Equals(facts.Mirrors, row.Mirrors, StringComparison.Ordinal),
                $"{Specification[language]}: {row.Catalogue} is stated as mirroring `{row.Mirrors}` " +
                $"and its assembly records `{facts.Mirrors}`. A reader takes that version as the one " +
                "the constants come from.");

            Same(language, row.Catalogue, "live rules", row.LiveRules, facts.LiveRules);
            Same(language, row.Catalogue, "published constants", row.PublishedConstants,
                 facts.PublishedConstants);
            Same(language, row.Catalogue, "categories", row.Categories, facts.Categories);
            Same(language, row.Catalogue, "help links", row.HelpLinks, facts.HelpLinks);
        }
    }

    [Fact]
    public void The_two_languages_state_the_same_figures()
    {
        List<Row> english = Stated("en");
        List<Row> french = Stated("fr");

        Assert.True(
            english.SequenceEqual(french),
            "The two halves of the specification state different catalogue tables.\n" +
            $"  en: {string.Join("; ", english)}\n" +
            $"  fr: {string.Join("; ", french)}\n" +
            "A number is the same number in both languages.");
    }

    [Fact]
    public void The_table_is_found_and_the_catalogues_are_loadable()
    {
        foreach (string language in new[] { "en", "fr" })
        {
            Assert.True(
                Stated(language).Count >= 4,
                $"{Specification[language]} yields fewer than four rows this can read between " +
                $"{FactsBegin} and {FactsEnd}. Either the markers moved or the table was written in a " +
                "shape the row pattern does not read, and every figure in it is silently unchecked.");
        }

        foreach (Catalogue catalogue in CatalogueManifest.Vendor)
        {
            Assert.True(
                Load(catalogue.Namespace) is not null,
                $"{catalogue.Namespace} could not be loaded, so its figures cannot be recounted. The " +
                "documentation test project references every catalogue for exactly this reason.");
        }
    }

    private static void Same(string language, string catalogue, string what, int stated, int measured)
        => Assert.True(
            stated == measured,
            $"{Specification[language]}: {catalogue} is stated as carrying {stated} {what} and " +
            $"carries {measured}. The table is recounted from the assembly, so the row is what moved.");

    /// <summary>The rows the specification states, in the order it states them.</summary>
    private static List<Row> Stated(string language)
    {
        MarkdownDocument document = Repository.Require(Specification[language]);

        int start = document.Text.IndexOf(FactsBegin, StringComparison.Ordinal);
        int end = document.Text.IndexOf(FactsEnd, StringComparison.Ordinal);

        Assert.True(
            start >= 0 && end > start,
            $"{Specification[language]} carries no {FactsBegin} … {FactsEnd} block, so nothing here " +
            "can tell which of its tables claims to state every catalogue's figures.");

        List<Row> rows = [];
        foreach (Match row in Regex.Matches(
                     document.Text[start..end],
                     "^\\|\\s*`(?<catalogue>DiagnosticCatalog\\.[A-Za-z]+)`\\s*\\|\\s*`(?<mirrors>[^`]+)`\\s*" +
                     "\\|\\s*(?<live>\\d+)\\s*\\|\\s*(?<published>\\d+)\\s*\\|\\s*(?<categories>\\d+)\\s*" +
                     "\\|\\s*(?<links>\\d+)\\s*\\|",
                     RegexOptions.Multiline,
                     MatchTimeout))
        {
            rows.Add(new Row(
                row.Groups["catalogue"].Value,
                row.Groups["mirrors"].Value,
                int.Parse(row.Groups["live"].Value, System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(row.Groups["published"].Value, System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(row.Groups["categories"].Value, System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(row.Groups["links"].Value, System.Globalization.CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    /// <summary>What the catalogue's own assembly says about itself.</summary>
    private static Facts Measure(string catalogueNamespace)
    {
        Assembly assembly = Load(catalogueNamespace)
                            ?? throw new InvalidOperationException(catalogueNamespace + " is not loadable");

        CustomAttributeData? provenance = assembly.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == "DiagnosticCatalog.CatalogSourceAttribute");

        Assert.NotNull(provenance);

        string mirrors = $"{provenance.ConstructorArguments[0].Value} {provenance.ConstructorArguments[1].Value}";

        List<Type> rules = [.. assembly.GetTypes().Where(IsRule)];
        int retired = rules.Count(rule => rule.GetCustomAttributesData()
                                              .Any(a => a.AttributeType.FullName == "System.ObsoleteAttribute"));

        return new Facts(
            mirrors,
            rules.Count - retired,
            rules.Count,
            rules.Select(Category).Distinct(StringComparer.Ordinal).Count(),
            rules.Count(rule => Constant(rule, "HelpLinkUri") is not null));
    }

    private static bool IsRule(Type type) =>
        type.GetCustomAttributesData()
            .Any(a => a.AttributeType.FullName == "DiagnosticCatalog.DiagnosticRuleAttribute");

    private static string Category(Type rule) => Constant(rule, "Category") ?? string.Empty;

    private static string? Constant(Type rule, string name) =>
        rule.GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string;

    private static Assembly? Load(string name)
    {
        try
        {
            return Assembly.Load(new AssemblyName(name));
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
