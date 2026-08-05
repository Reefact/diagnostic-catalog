using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every upstream version a document quotes — <c>SonarAnalyzer.CSharp</c> at
/// <c>10.31.0.145097</c> and its like — is the version the catalogue mirroring that package
/// actually records, read from the catalogue's own <see cref="CatalogSourceAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// A quoted version is the one documentation claim that goes stale on its own. The nightly job
/// regenerates a catalogue when upstream moves and rewrites the <c>.g.cs</c>; no page follows,
/// because nothing connects the two. What is left is a document telling a reader which upstream
/// release the catalogue mirrors, and being wrong about it — on pages that nuget.org renders,
/// where the version is most of the reason to read the section at all.
/// </para>
/// <para>
/// The check keys off the package name, so it never has an opinion about an invented catalogue: the
/// authoring and versioning guides illustrate with <c>Contoso.Analyzers</c> at <c>4.2.1</c>, which
/// no catalogue mirrors and this therefore never sees. That is also the escape hatch, and the reason
/// there is no other one — a page that needs to show a version which is deliberately not the real
/// one shows it against an invented package, the way those two guides already do.
/// </para>
/// <para>
/// The recorded side is the compiled attribute rather than the generated source, so this reads what
/// a consumer's tooling reads. The quoted side is every document under <c>doc/</c> and <c>src/</c>,
/// which is what puts the shipped package READMEs in scope.
/// </para>
/// </remarks>
public sealed class CatalogueProvenanceTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The floor the guard holds the citation count to. Set below what the tree carries so that
    /// rewriting a page does not fail it, and far enough above zero that a pattern which stopped
    /// matching cannot pass for a documentation set that quotes nothing.
    /// </summary>
    private const int FewestCitationsWorthChecking = 6;

    /// <summary>What a catalogue records about the package it mirrors.</summary>
    private sealed record Provenance(string Package, string Version, string Catalogue);

    /// <summary>Where a document quotes a version, and which version it quotes there.</summary>
    private sealed record Citation(string Package, string Version, string Quoted);

    private static readonly Lazy<IReadOnlyList<Provenance>> Recorded = new(ReadRecorded);

    public static TheoryData<string> DocumentsQuotingAVersion()
    {
        TheoryData<string> paths = [];
        foreach (MarkdownDocument document in Repository.Documents)
        {
            if (Citations(document).Count > 0)
            {
                paths.Add(document.Path);
            }
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(DocumentsQuotingAVersion))]
    public void Every_quoted_version_is_the_one_its_catalogue_records(string path)
    {
        MarkdownDocument document = Repository.Require(path);

        foreach (Citation citation in Citations(document))
        {
            List<Provenance> mirroring = Recorded.Value
                .Where(provenance => string.Equals(provenance.Package, citation.Package, StringComparison.Ordinal))
                .ToList();

            if (mirroring.Any(provenance =>
                    string.Equals(provenance.Version, citation.Version, StringComparison.Ordinal)))
            {
                continue;
            }

            string records = string.Join(
                ", ",
                mirroring.Select(provenance => $"{provenance.Version} ({provenance.Catalogue})"));

            Assert.Fail(
                $"{path} says {citation.Package} is at {citation.Version}, written as " +
                $"\"{citation.Quoted}\". The catalogue records {records}.\n" +
                "The recorded value is the one a consumer reads out of the assembly, so the page is " +
                "the half that is wrong: quote what CatalogSource says, or regenerate the catalogue " +
                "if the page is ahead of it.");
        }
    }

    /// <summary>
    /// Guards the theory against passing on an empty world. Catalogues that did not load beside the
    /// tests, or patterns that stopped matching the way a page writes a version, would leave every
    /// document quoting nothing and every quotation unchecked — which reads exactly like success.
    /// </summary>
    [Fact]
    public void The_documentation_quotes_versions_this_can_check()
    {
        IReadOnlyList<Provenance> recorded = Recorded.Value;

        foreach (string catalogueNamespace in ManifestNamespaces())
        {
            Assert.True(
                recorded.Any(provenance =>
                    string.Equals(provenance.Catalogue, catalogueNamespace, StringComparison.Ordinal)),
                $"{catalogueNamespace} is declared in eng/catalogs.json and records no CatalogSource " +
                "beside the tests. Either the assembly did not load — check that this test project " +
                "still references it — or the catalogue was generated without its provenance stamp, " +
                "and nothing else in the repository would say so.");
        }

        int quoted = Repository.Documents.Sum(document => Citations(document).Count);

        Assert.True(
            quoted >= FewestCitationsWorthChecking,
            $"Only {quoted} upstream versions are quoted across the documentation, which is below " +
            $"the {FewestCitationsWorthChecking} this expects. Either the pages stopped quoting one, " +
            "or they now write it in a shape the patterns here do not read — and in that second case " +
            "every quotation is silently unchecked.");
    }

    /// <summary>
    /// The versions a document quotes, in the two shapes the documentation uses: the
    /// <c>[assembly: CatalogSource(...)]</c> sample that shows what a catalogue stamps, and a
    /// package named in code style followed by its version, which is how prose and tables cite one.
    /// </summary>
    private static List<Citation> Citations(MarkdownDocument document)
    {
        List<Citation> citations = [];

        foreach (Provenance provenance in Recorded.Value)
        {
            string package = Regex.Escape(provenance.Package);

            foreach (Match sample in Regex.Matches(
                         document.Text,
                         "source:\\s*\"" + package + "\"\\s*,\\s*sourceVersion:\\s*\"(?<version>[^\"]*)\"",
                         RegexOptions.None,
                         MatchTimeout))
            {
                citations.Add(new Citation(
                    provenance.Package,
                    sample.Groups["version"].Value,
                    sample.Value.Replace("\r", string.Empty).Replace('\n', ' ')));
            }

            foreach (Match prose in Regex.Matches(
                         document.Text,
                         "`" + package + "`\\s+(?<version>[0-9][0-9A-Za-z.+-]*)",
                         RegexOptions.None,
                         MatchTimeout))
            {
                citations.Add(new Citation(
                    provenance.Package,
                    prose.Groups["version"].Value,
                    prose.Value));
            }
        }

        return citations;
    }

    private static List<Provenance> ReadRecorded()
    {
        List<Provenance> recorded = [];

        foreach (string catalogueNamespace in ManifestNamespaces())
        {
            Assembly? assembly = Load(catalogueNamespace);

            // A catalogue that did not load is reported by The_documentation_quotes_versions_this_can_check,
            // which is the test that exists to notice an empty world rather than read one.
            if (assembly is null) continue;

            foreach (CatalogSourceAttribute stamp in assembly.GetCustomAttributes<CatalogSourceAttribute>())
            {
                recorded.Add(new Provenance(stamp.Source, stamp.SourceVersion, catalogueNamespace));
            }
        }

        return recorded;
    }

    /// <summary>
    /// The catalogues, read from the manifest that generates them, with a regex for the reason
    /// <see cref="CatalogueSampleTests"/> gives: the one key this needs is stable, and a model of the
    /// whole schema would be another place the schema is written down.
    /// </summary>
    private static List<string> ManifestNamespaces()
    {
        string path = Path.Combine(Repository.Root, "eng", "catalogs.json");
        if (!File.Exists(path)) return [];

        List<string> namespaces = [];
        foreach (Match entry in Regex.Matches(
                     File.ReadAllText(path),
                     "\"namespace\"\\s*:\\s*\"(?<namespace>[^\"]+)\"",
                     RegexOptions.None,
                     MatchTimeout))
        {
            namespaces.Add(entry.Groups["namespace"].Value);
        }

        return namespaces;
    }

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
