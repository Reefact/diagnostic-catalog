using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Catalogs.UnitTests;

/// <summary>
/// A catalogue's README is its page on nuget.org, and a package page carries no siblings beside it:
/// a reader who lands on one from a search sees that catalogue and nothing else — not the one for
/// the other analyzer they run, and not the foundation they would need to declare a catalogue of
/// their own. So each page points at ONE central index of the catalogues, and at the foundation.
/// </summary>
/// <remarks>
/// <para>
/// One link, not a list. Each README used to name every other catalogue, which with thirteen of them
/// is a hundred and sixty-nine statements maintained by hand: every page carried twelve entries that
/// were about somebody else's analyzer, and adding a catalogue meant editing twenty-six documents to
/// say so. What a reader needs from a package page is one way out to the whole family, and the
/// repository README is where that family is already listed.
/// </para>
/// <para>
/// Each half points at the index in ITS OWN language. A French page whose only way out is an English
/// table sends its reader somewhere they came to this half to avoid, and it is the half where that
/// costs most: the sibling they were not told about has a page they could not read either.
/// </para>
/// <para>
/// The catalogues are discovered from <c>eng/catalogs.json</c> — see <see cref="CatalogueRoster"/>.
/// One added tomorrow enters these theories with its first manifest entry, and the documents that
/// have not heard of it fail. That is the whole point: nothing else would remind anyone, because
/// nothing compiles a README.
/// </para>
/// </remarks>
public sealed class CatalogueIndexTests
{
    private const string NuGetPackage = "https://www.nuget.org/packages/";

    /// <summary>The package every catalogue is built on, and what a reader needs to declare one.</summary>
    private const string Foundation = "DiagnosticCatalog";

    /// <summary>
    /// Where each half of a catalogue's README has to send a reader looking for the others. Absolute,
    /// because nuget.org resolves no relative link (ADR-0034), and to the half in the page's own
    /// language.
    /// </summary>
    private static readonly Dictionary<string, string> Index = new(StringComparer.Ordinal)
    {
        ["README.en.md"] = "https://github.com/Reefact/diagnostic-catalog#-the-ready-made-catalogues",
        ["README.fr.md"] =
            "https://github.com/Reefact/diagnostic-catalog/blob/main/doc/README.fr.md#-les-catalogues-disponibles",
    };

    /// <summary>The two halves of the index itself, as they sit in the repository.</summary>
    private static readonly Dictionary<string, string> IndexDocument = new(StringComparer.Ordinal)
    {
        ["README.en.md"] = "README.md",
        ["README.fr.md"] = "README.fr.md",
    };

    private const string IndexBegin = "<!-- catalogue-index:begin -->";

    private const string IndexEnd = "<!-- catalogue-index:end -->";

    /// <summary>Every catalogue's folder, paired with each half of its README.</summary>
    public static TheoryData<string, string> CatalogueHalves()
    {
        TheoryData<string, string> halves = new();
        foreach (CatalogueEntry catalogue in CatalogueRoster.Vendor)
        {
            halves.Add(catalogue.Folder, "README.en.md");
            halves.Add(catalogue.Folder, "README.fr.md");
        }

        return halves;
    }

    /// <summary>Every catalogue the manifest declares, vendor or not.</summary>
    public static TheoryData<string> Catalogues()
    {
        TheoryData<string> folders = new();
        foreach (CatalogueEntry catalogue in CatalogueRoster.All) folders.Add(catalogue.Folder);

        return folders;
    }

    /// <summary>Every project this repository packs, and each half of its README.</summary>
    public static TheoryData<string, string> PackagedHalves()
    {
        TheoryData<string, string> halves = new();
        foreach (KeyValuePair<string, string> package in CatalogueRoster.PackedAs())
        {
            halves.Add(package.Key, "README.en.md");
            halves.Add(package.Key, "README.fr.md");
        }

        return halves;
    }

    public static TheoryData<string> IndexHalves() => ["README.en.md", "README.fr.md"];

    [Theory]
    [MemberData(nameof(CatalogueHalves))]
    public void A_catalogue_page_points_at_the_index_for_its_own_language(string catalogue, string half)
    {
        Assert.Contains(
            Index[half],
            Readme(catalogue, half),
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(CatalogueHalves))]
    public void A_catalogue_page_names_the_foundation(string catalogue, string half)
    {
        Assert.True(
            Names(Readme(catalogue, half), Foundation),
            $"{catalogue}/{half} never names {Foundation}, so a reader who wants a catalogue of " +
            "their own — for their own analyzer, or an internal ruleset — is told nothing about the " +
            "package that makes one possible.");
    }

    [Theory]
    [MemberData(nameof(Catalogues))]
    public void Every_catalogue_the_manifest_declares_is_indexed(string catalogue)
    {
        CatalogueEntry entry = Entry(catalogue);

        foreach (string half in IndexHalves())
        {
            string index = IndexHalf(half);

            if (entry.MirrorsAVendor)
            {
                Assert.Contains(entry.Namespace, Indexed(index, half), StringComparer.Ordinal);
            }
            else
            {
                // DiagnosticCatalog.Self mirrors this repository's own analyzers rather than a
                // vendor's, so it belongs with the toolkit rather than in the table a reader scans
                // for their analyzer. It still has to be NAMED, or nothing on the front page says
                // that the DCAT rules are themselves catalogued.
                Assert.True(
                    Names(index, entry.Namespace),
                    $"{IndexDocument[half]} never names {entry.Namespace}, which eng/catalogs.json " +
                    "generates. The front page of the project omits a catalogue it publishes.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(IndexHalves))]
    public void The_index_lists_no_catalogue_this_repository_does_not_generate(string half)
    {
        foreach (string package in Indexed(IndexHalf(half), half))
        {
            bool generated = false;
            foreach (CatalogueEntry catalogue in CatalogueRoster.Vendor)
            {
                if (string.Equals(catalogue.Namespace, package, StringComparison.Ordinal)) generated = true;
            }

            Assert.True(
                generated,
                $"{IndexDocument[half]} indexes {package} among the ready-made catalogues, and " +
                "eng/catalogs.json generates no such catalogue. Every catalogue page sends its reader " +
                "to this table, so a row that resolves to nothing is a dead end reached from all of them.");
        }
    }

    [Theory]
    [MemberData(nameof(PackagedHalves))]
    public void A_nuget_address_in_a_readme_resolves_to_a_package_this_repository_publishes(
        string project, string half)
    {
        Dictionary<string, string> packaged = CatalogueRoster.PackedAs();

        // Only addresses claiming to be ours are judged: a README may legitimately link the upstream
        // analyzer it mirrors, and that package's id is not ours to know.
        foreach (Match link in Regex.Matches(
                     Readme(project, half),
                     Regex.Escape(NuGetPackage) + "(?<id>DiagnosticCatalog[^)\\s]*)",
                     RegexOptions.None,
                     TimeSpan.FromSeconds(10)))
        {
            string id = link.Groups["id"].Value;
            bool published = false;
            foreach (KeyValuePair<string, string> package in packaged)
            {
                if (string.Equals(package.Value, id, StringComparison.Ordinal)) published = true;
            }

            Assert.True(
                published,
                $"{project}/{half} links {NuGetPackage}{id}, which this repository publishes no " +
                $"package under. The address of a package is its id, exactly: {NuGetPackage}{{id}}.");
        }
    }

    /// <summary>
    /// Guards the theories above against passing by having nothing to say: were the manifest or the
    /// documents to stop being copied beside the tests, an empty family would assert nothing at all,
    /// which is the one outcome a check written to be a reminder must not allow.
    /// </summary>
    [Fact]
    public void The_catalogues_are_discovered_from_the_manifest_that_generates_them()
    {
        IReadOnlyList<CatalogueEntry> catalogues = CatalogueRoster.All;

        Assert.True(
            catalogues.Count >= 2,
            "Fewer than two catalogues were discovered beside the tests, so these theories would " +
            "assert nothing. Check that eng/catalogs.json is still copied to catalogmanifest/.");

        foreach (CatalogueEntry catalogue in catalogues)
        {
            Assert.True(
                catalogue.PackageId.Length > 0,
                $"{catalogue.Folder} is generated as a catalogue but declares no <ReleaseTrain>, so it " +
                "is not packable and reaches no reader.");

            Assert.True(
                File.Exists(Path.Combine(AppContext.BaseDirectory, "catalogs", catalogue.GeneratedFile)),
                $"{catalogue.Folder} declares {catalogue.GeneratedFile} in the manifest and no such " +
                "generated source was found beside the tests.");
        }
    }

    /// <summary>
    /// What a catalogue's folder has to hold. Stated once, over the whole roster, because every one
    /// of these is a file a reader or a package meets and none of them is produced by compiling.
    /// </summary>
    [Theory]
    [MemberData(nameof(Catalogues))]
    public void A_catalogue_folder_carries_the_documents_and_the_icon_its_kind_requires(string catalogue)
    {
        CatalogueEntry entry = Entry(catalogue);

        Assert.True(File.Exists(Document(catalogue, "README.en.md")), $"{catalogue} carries no README.en.md");
        Assert.True(File.Exists(Document(catalogue, "README.fr.md")), $"{catalogue} carries no README.fr.md");

        if (!entry.MirrorsAVendor) return;

        // A vendor catalogue versions on a train of its own, so it keeps its own changelog; and it
        // sits beside twelve others on nuget.org, so it wears a badge of its own. DiagnosticCatalog
        // .Self ships on the lib train with the analyzers it mirrors and does neither.
        Assert.True(
            File.Exists(Document(catalogue, "CHANGELOG.md")),
            $"{catalogue} rides a train of its own and carries no CHANGELOG.md");

        Assert.True(
            File.Exists(Path.Combine(AppContext.BaseDirectory, "catalogicons", catalogue, "icon.png")),
            $"{catalogue} carries no icon.png of its own");
    }

    private static CatalogueEntry Entry(string folder)
    {
        foreach (CatalogueEntry catalogue in CatalogueRoster.All)
        {
            if (string.Equals(catalogue.Folder, folder, StringComparison.Ordinal)) return catalogue;
        }

        throw new InvalidOperationException("no catalogue named " + folder);
    }

    /// <summary>The package ids listed between the index markers of one half.</summary>
    private static List<string> Indexed(string index, string half)
    {
        int start = index.IndexOf(IndexBegin, StringComparison.Ordinal);
        int end = index.IndexOf(IndexEnd, StringComparison.Ordinal);

        Assert.True(
            start >= 0 && end > start,
            $"{IndexDocument[half]} carries no {IndexBegin} … {IndexEnd} block, so nothing here can " +
            "tell which of its tables claims to list every catalogue.");

        List<string> packages = new();
        foreach (Match row in Regex.Matches(
                     index.Substring(start, end - start),
                     "^\\|\\s*\\*\\*`(?<package>DiagnosticCatalog(?:\\.[A-Za-z]+)?)`\\*\\*",
                     RegexOptions.Multiline,
                     TimeSpan.FromSeconds(10)))
        {
            packages.Add(row.Groups["package"].Value);
        }

        return packages;
    }

    private static string IndexHalf(string half)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "repodocs", IndexDocument[half]);
        Assert.True(File.Exists(path), $"The index was not copied beside the tests: {path}");

        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    /// <summary>
    /// Whether a document names a package. The id must not be followed by an identifier character,
    /// so that <c>DiagnosticCatalog.Sonar</c> — or <c>DiagnosticCatalog.dll</c>, which is a file
    /// rather than a package — cannot stand in for <c>DiagnosticCatalog</c>.
    /// </summary>
    private static bool Names(string document, string packageId) =>
        Regex.IsMatch(
            document,
            Regex.Escape(packageId) + "(?![\\w.])",
            RegexOptions.None,
            TimeSpan.FromSeconds(10));

    private static string Document(string project, string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "catalogdocs", project, fileName);

    private static string Readme(string project, string fileName)
    {
        string path = Document(project, fileName);
        Assert.True(File.Exists(path), $"{fileName} not found beside the tests for {project}: {path}");

        return File.ReadAllText(path).Replace("\r\n", "\n");
    }
}
