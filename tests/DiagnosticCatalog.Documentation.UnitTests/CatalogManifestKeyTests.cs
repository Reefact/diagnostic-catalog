using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every key the catalogue manifest accepts is described in
/// <c>doc/guide/catalogs-manifest</c>, and every key that page describes is one the manifest
/// accepts.
/// </summary>
/// <remarks>
/// <para>
/// The last surface this repository can enumerate and did not check, named as the outstanding
/// follow-up of ADR-0025. Every key the schema declares happens to be on the page today; nothing
/// held it there. A key added tomorrow would reach a release with the page unchanged — and a page
/// that is merely out of date reads exactly like a complete one, which is the failure the whole
/// documentation project exists to refuse.
/// </para>
/// <para>
/// The truth is <c>eng/catalogs.schema.json</c> rather than <c>eng/catalogs.json</c>. The manifest
/// is one instance: it names four catalogues and reaches for eight of the fifteen keys, so a check
/// against it would quietly stop asking about <c>nupkg</c>, <c>solution</c> and the rest — the keys
/// a reader is most likely to need the page for, because this repository does not use them.
/// </para>
/// <para>
/// And the schema is not another document. <c>ManifestTests.The_schema_describes_exactly_the_keys_the_reader_reads</c>
/// holds it to the reader's own parameters, so it is a statement of the set that something else is
/// keeping true — the standard
/// <see href="https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md">ADR-0009</see>
/// sets, and the same move <see cref="DiagnosticCoverageTests"/> makes with the
/// <c>AnalyzerReleases</c> files.
/// </para>
/// </remarks>
public sealed class CatalogManifestKeyTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    private const string Reference = "doc/guide/catalogs-manifest.{0}.md";

    private static readonly Lazy<SortedSet<string>> Declared = new(ReadSchema);

    public static TheoryData<string, string> DeclaredByLanguage()
    {
        TheoryData<string, string> data = [];
        foreach (string key in Declared.Value)
        {
            data.Add(key, "en");
            data.Add(key, "fr");
        }

        return data;
    }

    public static TheoryData<string> Languages() => new("en", "fr");

    [Theory]
    [MemberData(nameof(DeclaredByLanguage))]
    public void Every_key_the_manifest_accepts_is_documented(string key, string language)
    {
        MarkdownDocument reference = Repository.Require(string.Format(Reference, language));

        Assert.True(
            DocumentedKeys(reference).Contains(key),
            $"the manifest schema declares '{key}' and {reference.Path} lists it in no key table. " +
            "A key nobody wrote down is one a reader cannot know exists, and the page still reads " +
            "as though it were the whole list.");
    }

    /// <summary>
    /// The converse, which catches the key renamed rather than the key added. A reader who copies a
    /// key the schema no longer accepts gets it rejected by the manifest reader, and the page is the
    /// last thing they will suspect.
    /// </summary>
    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_key_the_page_lists_is_one_the_manifest_accepts(string language)
    {
        MarkdownDocument reference = Repository.Require(string.Format(Reference, language));
        SortedSet<string> declared = Declared.Value;

        foreach (string key in DocumentedKeys(reference))
        {
            Assert.True(
                declared.Contains(key),
                $"{reference.Path} documents the manifest key '{key}', which the schema does not " +
                "declare. Either it was renamed and the page outlived it, or it never existed — and " +
                "a reader cannot tell either from a page that is right. Declared: " +
                $"{string.Join(", ", declared)}.");
        }
    }

    /// <summary>
    /// Guards both theories against an empty world. A schema that moved, or a key table rewritten in
    /// a shape the reader below cannot see, would leave one theory asserting nothing and the other
    /// passing on nothing — and both would look exactly like a documentation set that is complete.
    /// </summary>
    [Fact]
    public void The_manifest_keys_are_discovered()
    {
        SortedSet<string> declared = Declared.Value;

        Assert.True(
            declared.Count >= 10,
            $"Only {declared.Count} keys were read from eng/catalogs.schema.json. Check that the " +
            "schema still declares them under $defs.catalogue.properties.");

        Assert.Contains("package", declared);
        Assert.Contains("container", declared);

        foreach (string language in new[] { "en", "fr" })
        {
            SortedSet<string> documented = DocumentedKeys(Repository.Require(string.Format(Reference, language)));

            Assert.True(
                documented.Count >= 10,
                $"Only {documented.Count} keys were read out of the tables in " +
                $"doc/guide/catalogs-manifest.{language}.md. The converse theory would assert almost " +
                "nothing — check that the key tables still open with a backticked key in the first " +
                "column.");
        }
    }

    /// <summary>
    /// The keys a page lists: the first cell of every table row that opens with a backticked
    /// identifier.
    /// </summary>
    /// <remarks>
    /// Reading the tables rather than searching the prose is what makes the converse direction
    /// possible. A page-wide search for backticked words would find <c>dcat</c>, <c>Release</c> and
    /// every type name the page mentions, and the converse theory would fail on a page that is
    /// entirely right. The table is where the page makes the claim "this is a key"; everywhere else
    /// it is discussing one.
    /// </remarks>
    private static SortedSet<string> DocumentedKeys(MarkdownDocument document)
    {
        SortedSet<string> keys = new(StringComparer.Ordinal);

        foreach (Match row in Regex.Matches(
                     document.Text,
                     "^\\|\\s*`(?<key>[a-z][A-Za-z0-9]*)`\\s*\\|",
                     RegexOptions.Multiline,
                     MatchTimeout))
        {
            keys.Add(row.Groups["key"].Value);
        }

        return keys;
    }

    /// <summary>
    /// The keys a catalogue entry may carry, read from the schema. <c>$</c>-prefixed names are
    /// skipped: <c>$comment</c> is JSON Schema's own escape hatch rather than a key the tool reads,
    /// and the reader test beside the schema skips it for the same reason.
    /// </summary>
    private static SortedSet<string> ReadSchema()
    {
        SortedSet<string> keys = new(StringComparer.Ordinal);

        string path = Path.Combine(Repository.Root, "eng", "catalogs.schema.json");
        if (!File.Exists(path)) return keys;

        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(path));

        foreach (JsonProperty property in schema.RootElement
                                                .GetProperty("$defs")
                                                .GetProperty("catalogue")
                                                .GetProperty("properties")
                                                .EnumerateObject())
        {
            if (property.Name.StartsWith('$')) continue;

            keys.Add(property.Name);
        }

        return keys;
    }

}
