using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every rule reference a document shows — <c>SonarRule.S1144.Id</c> and its like — names a type
/// that the catalogue it belongs to actually publishes.
/// </summary>
/// <remarks>
/// <para>
/// This is the check whose absence had already cost something. Three documents, the guides and the
/// analyzers' own package README, spelled the Sonar container <c>SonarRules</c> — plural — across
/// sixteen samples. The catalogue publishes <c>SonarRule</c>. Every one of those samples was
/// uncompilable, on the page whose entire subject is that a reference the compiler checks beats a
/// string it does not, and nothing in the repository could say so: a code fence is prose.
/// </para>
/// <para>
/// Which containers exist is read from <c>eng/catalogs.json</c>, the manifest the generator and the
/// nightly workflow already read — a catalogue is declared there before it exists — and each one is
/// then resolved against the compiled assembly. Neither half is another document.
/// </para>
/// </remarks>
public sealed class CatalogueSampleTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A catalogue as the manifest declares it: the namespace the rules land in, and the container
    /// type they are nested under.
    /// </summary>
    private sealed record Catalogue(string Namespace, string Container);

    private static readonly Lazy<IReadOnlyList<Catalogue>> Declared = new(ReadManifest);

    public static TheoryData<string> DocumentsShowingRules()
    {
        TheoryData<string> paths = new();
        foreach (MarkdownDocument document in Repository.Documents)
        {
            if (Declared.Value.Any(catalogue =>
                    document.Text.Contains(catalogue.Container + ".", StringComparison.Ordinal)))
            {
                paths.Add(document.Path);
            }
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(DocumentsShowingRules))]
    public void Every_rule_a_document_shows_is_published_by_its_catalogue(string path)
    {
        MarkdownDocument document = Repository.Require(path);

        foreach (Catalogue catalogue in Declared.Value)
        {
            Assembly? assembly = Load(catalogue.Namespace);
            if (assembly is null) continue;   // The_catalogues_are_loadable reports the absence.

            Type? container = assembly.GetType($"{catalogue.Namespace}.{catalogue.Container}", throwOnError: false);

            if (container is null)
            {
                Assert.Fail(
                    $"{catalogue.Namespace}.{catalogue.Container} is declared in eng/catalogs.json and " +
                    "is not in the assembly it generates into.");
            }

            IReadOnlyDictionary<string, string> deliberate = DeliberatelyMissing(document);

            foreach (Match reference in Regex.Matches(
                         document.Text,
                         Regex.Escape(catalogue.Container) + "\\.(?<rule>[A-Z][A-Za-z0-9]*)",
                         RegexOptions.None,
                         MatchTimeout))
            {
                string rule = reference.Groups["rule"].Value;
                if (deliberate.ContainsKey($"{catalogue.Container}.{rule}")) continue;

                Assert.True(
                    container.GetNestedType(rule, BindingFlags.Public) is not null,
                    $"{path} shows {catalogue.Container}.{rule}, which " +
                    $"{catalogue.Namespace} does not publish. A reader who copies that sample gets a " +
                    "compile error on the page telling them a checked reference beats a string.\n" +
                    "If the page shows it BECAUSE it does not exist -- a deliberate mistake, a " +
                    "rejected naming shape -- declare it at the top of the document:\n" +
                    $"  <!-- dcat-doc:missing {catalogue.Container}.{rule} why the page shows it -->");
            }
        }
    }

    /// <summary>
    /// No document pluralises a container. This is the exact defect that went unnoticed, and it is
    /// invisible to the theory above — that one resolves the rules under containers it knows, and a
    /// misspelled container is simply not one of them.
    /// </summary>
    /// <remarks>
    /// Narrow on purpose. A check that flagged every unknown <c>SomethingRules.X</c> would also
    /// flag the specification's invented catalogues, which exist to illustrate a contract and are
    /// meant to name nothing real. The plural of a container this repository actually publishes is
    /// never an illustration: it is the real name, typed the way English wants to type it.
    /// </remarks>
    [Theory]
    [MemberData(nameof(DocumentsShowingRules))]
    public void A_document_never_pluralises_a_container(string path)
    {
        MarkdownDocument document = Repository.Require(path);

        foreach (Catalogue catalogue in Declared.Value)
        {
            Match plural = Regex.Match(
                document.Text,
                "(?<!\\w)" + Regex.Escape(catalogue.Container) + "s\\.(?<rule>[A-Z][A-Za-z0-9]*)",
                RegexOptions.None,
                MatchTimeout);

            if (!plural.Success) continue;

            Assert.Fail(
                $"{path} shows {catalogue.Container}s.{plural.Groups["rule"].Value}. The container " +
                $"is {catalogue.Container}, in the singular: the use site reads " +
                $"{catalogue.Container}.{plural.Groups["rule"].Value} — one rule, named. As written " +
                "the sample does not compile.");
        }
    }

    /// <summary>
    /// A declaration that nothing needs is a hole left open. The page that showed the counter-example
    /// was rewritten, the reference went away, and the exemption stayed -- covering whatever is
    /// written there next.
    /// </summary>
    [Theory]
    [MemberData(nameof(DocumentsShowingRules))]
    public void A_deliberately_missing_reference_is_actually_shown(string path)
    {
        MarkdownDocument document = Repository.Require(path);

        foreach (KeyValuePair<string, string> declaration in DeliberatelyMissing(document))
        {
            Assert.True(
                Regex.IsMatch(
                    document.Text,
                    "(?<!\\w)" + Regex.Escape(declaration.Key) + "(?!\\w)",
                    RegexOptions.None,
                    MatchTimeout),
                $"{path} declares {declaration.Key} as deliberately missing and never shows it. " +
                "Remove the declaration: an exemption nothing uses covers whatever is written there " +
                "next.");
        }
    }

    /// <summary>
    /// Guards both theories against passing on an empty world: a manifest that could not be read, or
    /// catalogues that did not land beside the tests, would let any sample through.
    /// </summary>
    [Fact]
    public void The_catalogues_are_loadable()
    {
        IReadOnlyList<Catalogue> declared = Declared.Value;

        Assert.True(
            declared.Count >= 3,
            $"Only {declared.Count} catalogues were read from eng/catalogs.json, so a sample naming " +
            "a rule would be checked against almost nothing.");

        foreach (Catalogue catalogue in declared)
        {
            Assert.True(
                Load(catalogue.Namespace) is not null,
                $"{catalogue.Namespace} could not be loaded beside the tests. Check that this test " +
                "project still references it.");
        }
    }

    /// <summary>
    /// The references a document shows ON PURPOSE although no catalogue publishes them, declared in
    /// the document itself as <c>&lt;!-- dcat-doc:missing SonarRule.S1145 why --&gt;</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Some pages have to show a name that does not resolve. The tutorial's third step asks the
    /// reader to break a reference and read the <c>CS0117</c> it produces -- that is the moment the
    /// whole library is demonstrated in, and a rule that existed would ruin it. The concepts page
    /// shows <c>SonarRule.S1144Id</c> as the shape it declined to adopt.
    /// </para>
    /// <para>
    /// Declared in the document rather than listed in this file, and per document rather than
    /// globally: the intent belongs beside the prose that carries it, a reader of the source meets
    /// the reason where the exemption is, and the same misspelling on any other page still fails.
    /// The reason is required, so an exemption is always a sentence somebody wrote.
    /// </para>
    /// </remarks>
    private static IReadOnlyDictionary<string, string> DeliberatelyMissing(MarkdownDocument document)
    {
        Dictionary<string, string> declared = new(StringComparer.Ordinal);

        foreach (Match declaration in Regex.Matches(
                     document.Text,
                     "<!--\\s*dcat-doc:missing\\s+(?<reference>[A-Za-z0-9_.]+)\\s+(?<reason>[^>]*?)\\s*-->",
                     RegexOptions.None,
                     MatchTimeout))
        {
            string reason = declaration.Groups["reason"].Value;

            Assert.True(
                reason.Length > 0,
                $"{document.Path} declares {declaration.Groups["reference"].Value} as deliberately " +
                "missing and gives no reason. An exemption without one is a hole nobody can judge.");

            declared[declaration.Groups["reference"].Value] = reason;
        }

        return declared;
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

    /// <summary>
    /// The catalogues, read from the manifest that generates them. Read with a regex rather than a
    /// JSON deserialiser for the same reason <c>DocumentedSiblingsTests</c> does: the two keys this
    /// needs are stable, and a model of the whole schema would be a third place the schema is
    /// written down.
    /// </summary>
    private static IReadOnlyList<Catalogue> ReadManifest()
    {
        string path = Path.Combine(Repository.Root, "eng", "catalogs.json");
        if (!File.Exists(path)) return [];

        List<Catalogue> catalogues = [];
        foreach (Match entry in Regex.Matches(
                     File.ReadAllText(path),
                     "\"namespace\"\\s*:\\s*\"(?<namespace>[^\"]+)\"\\s*,\\s*\"container\"\\s*:\\s*\"(?<container>[^\"]+)\"",
                     RegexOptions.None,
                     MatchTimeout))
        {
            catalogues.Add(new Catalogue(entry.Groups["namespace"].Value, entry.Groups["container"].Value));
        }

        return catalogues;
    }

}
