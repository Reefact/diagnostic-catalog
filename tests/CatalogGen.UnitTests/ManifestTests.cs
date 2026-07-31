using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// What a manifest is allowed to say, and what a manifest that says it wrong is told.
/// </summary>
/// <remarks>
/// A manifest is edited by hand, so the likeliest fault in one is a mistyped key — and the answer to
/// that used to be <c>The given key was not present in the dictionary</c>, which named neither the
/// key, nor the file, nor which of several entries carried it. Every assertion below is on the text
/// of the message, because the message is the whole feature.
/// </remarks>
public sealed class ManifestTests
{
    [Fact]
    public void An_entry_that_omits_a_required_key_names_the_key_the_file_and_the_entry()
    {
        string message = Refused("""
            { "catalogs": [
                { "package": "P", "container": "C", "output": "o.g.cs" }
            ] }
            """);

        Assert.Equal("catalogs.json: catalogs[0]: \"namespace\" is missing.", message);
    }

    [Fact]
    public void The_entry_named_is_the_one_at_fault_not_the_first()
    {
        // The reason the index is in the message at all: a manifest of three catalogues reporting
        // "namespace is missing" says nothing about which of the three to open.
        string message = Refused("""
            { "catalogs": [
                { "package": "A", "namespace": "N", "container": "C", "output": "a.g.cs" },
                { "package": "B", "namespace": "N", "container": "C", "output": "b.g.cs" },
                { "package": "C", "namespace": "N", "container": "C" }
            ] }
            """);

        Assert.Equal("catalogs.json: catalogs[2]: \"output\" is missing.", message);
    }

    [Fact]
    public void A_key_of_the_wrong_type_says_what_it_should_have_been_and_what_it_was()
    {
        string message = Refused("""
            { "catalogs": [
                { "package": "P", "namespace": 3, "container": "C", "output": "o.g.cs" }
            ] }
            """);

        Assert.Equal("catalogs.json: catalogs[0]: \"namespace\" should be a string, not Number.", message);
    }

    [Fact]
    public void An_entry_naming_two_sources_is_refused_rather_than_resolved_by_precedence()
    {
        // Each names a source. Picking one silently would generate a catalogue from something the
        // manifest's author did not ask for, and nothing downstream would report it.
        string message = Refused("""
            { "catalogs": [
                { "nupkg": "p.nupkg", "assemblies": ["a.dll"],
                  "namespace": "N", "container": "C", "output": "o.g.cs" }
            ] }
            """);

        Assert.Equal(
            "catalogs.json: catalogs[0]: names more than one source; give one of " +
            "\"package\", \"nupkg\", \"projects\" or \"assemblies\".",
            message);
    }

    [Fact]
    public void A_manifest_with_no_catalogs_array_is_refused()
        => Assert.Equal("catalogs.json: no \"catalogs\" array.", Refused("""{ "catalog": [] }"""));

    [Fact]
    public void A_manifest_declaring_no_entry_is_refused_rather_than_read_as_nothing_to_do()
    {
        // A scheduled job branches on the exit code. An empty manifest that generated nothing and
        // succeeded would report "no catalogue changed" forever, which is what it also reports when
        // every catalogue is current.
        Assert.Equal("catalogs.json: \"catalogs\" declares no entry.", Refused("""{ "catalogs": [] }"""));
    }

    [Theory]
    [InlineData("package", "\"P\"")]
    [InlineData("nupkg", "\"p.nupkg\"")]
    [InlineData("projects", "[\"p.csproj\"]")]
    [InlineData("assemblies", "[\"a.dll\"]")]
    public void Every_source_a_manifest_can_name_is_read(string key, string value)
    {
        IReadOnlyList<Job> jobs = CatalogRun.JobsFromManifest($$"""
            { "catalogs": [
                { "{{key}}": {{value}}, "namespace": "N", "container": "C", "output": "o.g.cs" }
            ] }
            """, Manifest);

        Job job = Assert.Single(jobs);
        Assert.Equal("N", job.Namespace);
        Assert.True(job.Package is not null || job.Nupkg is not null
                    || job.Projects is not null || job.Assemblies is not null);
    }

    [Fact]
    public void Paths_are_resolved_against_the_manifest_so_it_works_from_any_directory()
    {
        IReadOnlyList<Job> jobs = CatalogRun.JobsFromManifest("""
            { "catalogs": [
                { "projects": ["../src/My.Analyzers/My.Analyzers.csproj"],
                  "namespace": "N", "container": "C", "output": "../src/My/Rules.g.cs" }
            ] }
            """, Manifest);

        Job job = Assert.Single(jobs);
        Assert.True(Path.IsPathRooted(job.Output));
        Assert.True(Path.IsPathRooted(job.Projects![0]));
        Assert.EndsWith(Path.Combine("src", "My.Analyzers", "My.Analyzers.csproj"), job.Projects[0]);
    }

    [Theory]
    [InlineData("vb")]
    [InlineData("fs")]
    public void A_language_the_tool_cannot_read_is_refused_by_the_manifest_too(string language)
    {
        // An entry reaches the run without passing through any option parsing, so refusing the flag
        // and not the file would leave the same request true of one and false of the other.
        string message = Refused($$"""
            { "catalogs": [
                { "package": "P", "language": "{{language}}",
                  "namespace": "N", "container": "C", "output": "o.g.cs" }
            ] }
            """);

        Assert.StartsWith($"catalogs.json: catalogs[0]: '{language}' is not a language this tool can read",
                          message);
    }

    [Fact]
    public void The_defaults_are_the_ones_the_schema_documents()
    {
        IReadOnlyList<Job> jobs = CatalogRun.JobsFromManifest("""
            { "catalogs": [
                { "package": "P", "namespace": "N", "container": "C", "output": "o.g.cs" }
            ] }
            """, Manifest);

        Job job = Assert.Single(jobs);
        Assert.Equal("latest", job.Version);
        Assert.Equal("cs", job.Language);
        Assert.Equal("Release", job.Configuration);
    }

    /// <summary>
    /// The schema beside the manifest describes exactly the keys the reader reads.
    /// </summary>
    /// <remarks>
    /// Two statements of one contract, and drift between them has the quiet shape this repository
    /// exists to refuse: a key added to the reader alone is squiggled in every editor that is doing
    /// what it was told, and a key removed from the reader alone is offered by completion and
    /// silently ignored. Neither produces a failure anywhere.
    /// <para>
    /// <see cref="Job"/> is the link rather than a second hand-written list, because it is what the
    /// reader fills: a key can only reach the run through one of its parameters.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_schema_describes_exactly_the_keys_the_reader_reads()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(SchemaPath));
        HashSet<string> described =
        [
            .. schema.RootElement.GetProperty("$defs").GetProperty("catalogue").GetProperty("properties")
                     .EnumerateObject().Select(p => p.Name).Where(n => !n.StartsWith('$')),
        ];

        // Job's parameters, camel-cased: Package -> package, SourceName -> sourceName. Output and
        // the rest are named for their key on purpose, which is what makes this mapping total.
        HashSet<string> read =
        [
            .. typeof(Job).GetConstructors().Single().GetParameters()
                          .Select(p => char.ToLowerInvariant(p.Name![0]) + p.Name[1..]),
        ];

        Assert.Equal(read.OrderBy(k => k, StringComparer.Ordinal),
                     described.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void The_repositorys_own_manifest_is_the_one_the_schema_points_at()
    {
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(RealManifestPath));

        Assert.Equal("./catalogs.schema.json", manifest.RootElement.GetProperty("$schema").GetString());
    }

    // Named without a directory so the message it produces is stable wherever the tests run: the
    // reader reports the file name, and the assertions above are on the whole message.
    private const string Manifest = "catalogs.json";

    private static string SchemaPath => Path.Combine(AppContext.BaseDirectory, "manifest", "catalogs.schema.json");

    private static string RealManifestPath => Path.Combine(AppContext.BaseDirectory, "manifest", "catalogs.json");

    private static string Refused(string json)
        => Assert.Throws<ManifestException>(() => CatalogRun.JobsFromManifest(json, Manifest)).Message;
}
