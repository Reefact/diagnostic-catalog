using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Catalogs.UnitTests;

/// <summary>
/// A catalogue's README is its page on nuget.org, and a package page carries no siblings beside it:
/// a reader who lands on one from a search sees that catalogue and nothing else — not the one for
/// the other analyzer they run, and not the foundation they would need to declare a catalogue of
/// their own. So each catalogue names the others and the foundation, the foundation names the
/// catalogues, and these assert it.
/// <para>
/// The catalogues are read from <c>eng/catalogs.json</c>, which is what the generator and the
/// nightly workflow already read: a catalogue is declared there before it exists at all. One added
/// tomorrow therefore enters these theories with its first entry, and the READMEs that have not
/// heard of it fail. That is the whole point — nothing else in the repository would remind anyone,
/// because nothing compiles a README.
/// </para>
/// <para>
/// The family is the catalogues and the foundation, not everything this repository publishes.
/// <c>DiagnosticCatalog.Analyzers</c> ships the checking rather than a catalogue, and the CLI and
/// the test-support package will be neither; sending a reader of one vendor's rules to those is
/// noise, and the obligation would grow with the wrong list.
/// </para>
/// <para>
/// What is required is the package <em>id</em>, not a link. A project can be packable long before it
/// is published, and a package cannot be pointed at until a version of it exists (ADR-0007), so
/// demanding an address would eventually put a dead link on pages that are already live. An id is
/// what a reader pastes into a <c>PackageReference</c> or a search box, and it is true from the day
/// the project exists. A README that <em>does</em> link one of our packages is checked separately
/// and offline: the address of a package is <c>https://www.nuget.org/packages/{id}</c>, derived from
/// the id rather than composed, so a mistyped one fails here rather than under a reader's click.
/// </para>
/// </summary>
public sealed class DocumentedSiblingsTests
{
    private const string NuGetPackage = "https://www.nuget.org/packages/";

    /// <summary>
    /// The package every catalogue is built on, and what a reader needs to declare one of their own.
    /// Named rather than discovered: the foundation is the root of the family, not a member of a
    /// list something else maintains.
    /// </summary>
    private const string Foundation = "DiagnosticCatalog";

    /// <summary>
    /// Each catalogue, paired with another catalogue it must point the reader to. Every ordered pair
    /// of distinct catalogues appears, so the obligation runs both ways: a new catalogue must name
    /// the established ones, and they must name it.
    /// </summary>
    public static TheoryData<string, string> Siblings()
    {
        TheoryData<string, string> pairs = new();
        IReadOnlyList<Catalogue> catalogues = Catalogues();
        foreach (Catalogue catalogue in catalogues)
        {
            foreach (Catalogue sibling in catalogues)
            {
                if (!string.Equals(catalogue.PackageId, sibling.PackageId, StringComparison.Ordinal))
                {
                    pairs.Add(catalogue.Folder, sibling.PackageId);
                }
            }
        }

        return pairs;
    }

    /// <summary>The project folder of each catalogue.</summary>
    public static TheoryData<string> CatalogueFolders()
    {
        TheoryData<string> folders = new();
        foreach (Catalogue catalogue in Catalogues())
        {
            folders.Add(catalogue.Folder);
        }

        return folders;
    }

    /// <summary>Each catalogue, by published package id.</summary>
    public static TheoryData<string> CatalogueIds()
    {
        TheoryData<string> ids = new();
        foreach (Catalogue catalogue in Catalogues())
        {
            ids.Add(catalogue.PackageId);
        }

        return ids;
    }

    /// <summary>Every project this repository packs, catalogue or not.</summary>
    public static TheoryData<string> PackagedFolders()
    {
        TheoryData<string> folders = new();
        foreach (KeyValuePair<string, string> package in Packaged())
        {
            folders.Add(package.Key);
        }

        return folders;
    }

    [Theory]
    [MemberData(nameof(Siblings))]
    public void The_readme_of_a_catalogue_names_every_other_catalogue(string catalogue, string sibling)
    {
        Assert.True(
            Names(Readme(catalogue), sibling),
            $"{catalogue}/README.md never names {sibling}, so that catalogue is undiscoverable from " +
            "this one's nuget.org page, where no sibling sits beside it.");
    }

    [Theory]
    [MemberData(nameof(CatalogueFolders))]
    public void The_readme_of_a_catalogue_names_the_foundation(string catalogue)
    {
        Assert.True(
            Names(Readme(catalogue), Foundation),
            $"{catalogue}/README.md never names {Foundation}, so a reader who wants a catalogue of " +
            "their own — for their own analyzer, or an internal ruleset — is told nothing about the " +
            "package that makes one possible.");
    }

    [Theory]
    [MemberData(nameof(CatalogueIds))]
    public void The_foundation_readme_names_every_catalogue(string catalogue)
    {
        Assert.True(
            Names(Readme(Foundation), catalogue),
            $"{Foundation}/README.md never names {catalogue}, so a reader about to declare rules by " +
            "hand is not told that catalogue already exists.");
    }

    [Theory]
    [MemberData(nameof(CatalogueIds))]
    public void The_repository_readme_names_every_catalogue(string catalogue)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "repodocs", "README.md");
        Assert.True(File.Exists(path), $"The repository README was not copied beside the tests: {path}");

        Assert.True(
            Names(File.ReadAllText(path), catalogue),
            $"The repository README never names {catalogue}, so the front door of the project omits a " +
            "catalogue it publishes.");
    }

    [Theory]
    [MemberData(nameof(PackagedFolders))]
    public void A_nuget_address_in_a_readme_resolves_to_a_package_this_repository_publishes(string project)
    {
        IReadOnlyList<string> published = Packaged().Values.ToList();

        // Only addresses claiming to be ours are judged: a README may legitimately link the upstream
        // analyzer it mirrors, and that package's id is not ours to know.
        foreach (Match link in Regex.Matches(
                     Readme(project),
                     Regex.Escape(NuGetPackage) + "(?<id>DiagnosticCatalog[^)\\s]*)",
                     RegexOptions.None,
                     TimeSpan.FromSeconds(10)))
        {
            string id = link.Groups["id"].Value;
            Assert.True(
                published.Contains(id, StringComparer.Ordinal),
                $"{project}/README.md links {NuGetPackage}{id}, which this repository publishes no " +
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
        IReadOnlyList<Catalogue> catalogues = Catalogues();

        Assert.True(
            catalogues.Count >= 2,
            "Fewer than two catalogues were discovered beside the tests, so the sibling theories would " +
            "assert nothing. Check that eng/catalogs.json is still copied to catalogmanifest/.");

        foreach (Catalogue catalogue in catalogues)
        {
            Assert.True(
                Packaged().ContainsKey(catalogue.Folder),
                $"{catalogue.Folder} is generated as a catalogue but declares no <ReleaseTrain>, so it " +
                "is not packable and reaches no reader.");
        }
    }

    /// <summary>
    /// The catalogues, read from the manifest that produces them. The entry gives the project it is
    /// written into; that project gives the name it is published under, so neither is assumed to
    /// match the other.
    /// </summary>
    private static IReadOnlyList<Catalogue> Catalogues()
    {
        string manifest = Path.Combine(AppContext.BaseDirectory, "catalogmanifest", "catalogs.json");
        if (!File.Exists(manifest)) return [];

        IReadOnlyDictionary<string, string> packaged = Packaged();
        List<Catalogue> catalogues = [];
        foreach (Match entry in Regex.Matches(
                     File.ReadAllText(manifest),
                     "\"output\"\\s*:\\s*\"(?<output>[^\"]+)\"",
                     RegexOptions.None,
                     TimeSpan.FromSeconds(10)))
        {
            string[] segments = entry.Groups["output"].Value.Split('/');
            if (segments.Length < 2) continue;

            string folder = segments[segments.Length - 2];
            if (packaged.TryGetValue(folder, out string? packageId))
            {
                catalogues.Add(new Catalogue(folder, packageId));
            }
        }

        return catalogues.OrderBy(catalogue => catalogue.Folder, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// What this repository publishes, by project folder: a project joins a release train, and
    /// becomes packable, by declaring <c>&lt;ReleaseTrain&gt;</c> in its own <c>.csproj</c> and
    /// nowhere else. Wider than the catalogues on purpose — it answers whether a nuget.org address
    /// resolves to something of ours, which the foundation and the analyzers also do.
    /// </summary>
    private static IReadOnlyDictionary<string, string> Packaged()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "catalogprojects");
        if (!Directory.Exists(root)) return new Dictionary<string, string>(StringComparer.Ordinal);

        Dictionary<string, string> packaged = new(StringComparer.Ordinal);

        // project.xml, because a .csproj copied into a build output is picked up by the release
        // tooling's own discovery as a project to pack. See the copy in this project's .csproj.
        foreach (string path in Directory.GetFiles(root, "project.xml", SearchOption.AllDirectories))
        {
            string project = File.ReadAllText(path);
            if (!Declares(project, "ReleaseTrain", out string _)) continue;

            // PackageId, when set, is the published name; the SDK otherwise falls back to the project
            // file name, which is what the folder is named after.
            string folder = Path.GetFileName(Path.GetDirectoryName(path))!;
            packaged[folder] = Declares(project, "PackageId", out string id) ? id : folder;
        }

        return packaged;
    }

    private static bool Declares(string project, string element, out string value)
    {
        Match declaration = Regex.Match(
            project,
            $"<{element}>\\s*(?<value>[^<]+?)\\s*</{element}>",
            RegexOptions.None,
            TimeSpan.FromSeconds(10));

        value = declaration.Success ? declaration.Groups["value"].Value : string.Empty;

        return declaration.Success;
    }

    /// <summary>
    /// Whether a document names a package. The id must not be followed by an identifier character,
    /// so that <c>DiagnosticCatalog.Sonar</c> — or <c>DiagnosticCatalog.dll</c>, which is a file
    /// rather than a package — cannot stand in for <c>DiagnosticCatalog</c>. Where the id sits is
    /// deliberately left open: in prose, in a <c>PackageReference</c> snippet or inside a link, it is
    /// equally findable, and a check that dictated the form would be about formatting instead.
    /// </summary>
    private static bool Names(string document, string packageId) =>
        Regex.IsMatch(
            document,
            Regex.Escape(packageId) + "(?![\\w.])",
            RegexOptions.None,
            TimeSpan.FromSeconds(10));

    private static string Readme(string project)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "catalogdocs", project, "README.md");
        Assert.True(File.Exists(path), $"README.md not found beside the tests for {project}: {path}");

        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    private sealed record Catalogue(string Folder, string PackageId);
}
