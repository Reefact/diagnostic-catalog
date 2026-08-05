using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Catalogs.UnitTests;

/// <summary>
/// Which upstream release a catalogue mirrors is the first thing a consumer needs, and the
/// documents they read it in are the two things nothing compiles. A regeneration that moved the
/// mirrored release and left a README saying the old one would be wrong, published, and silent —
/// the same shape of failure as a wrong category, in the place a consumer looks first.
/// <para>
/// So the generator writes that statement into a marked block in both documents, and these tests
/// assert it against the <c>[assembly: CatalogSource]</c> attribute it wrote beside them. The
/// generator keeps the banner current; this keeps anything else from moving it.
/// </para>
/// </summary>
public sealed class DocumentedMirrorTests
{
    /// <summary>
    /// A catalogue's project folder and the generated source inside it. Listed rather than
    /// discovered: a new catalogue whose documents were never marked would otherwise be absent from
    /// the theory and pass by not being tested, which is the one outcome these must not allow.
    /// </summary>
    public static TheoryData<string, string> Catalogues() =>
        new()
        {
            { "DiagnosticCatalog.Sonar", "SonarRules.g.cs" },
            { "DiagnosticCatalog.NetAnalyzers", "NetAnalyzersRules.g.cs" },
            { "DiagnosticCatalog.StyleCop", "StyleCopRules.g.cs" },
            { "DiagnosticCatalog.CodeStyle", "CodeStyleRules.g.cs" },
            { "DiagnosticCatalog.Xunit", "XunitRules.g.cs" },
            { "DiagnosticCatalog.NUnit", "NUnitRules.g.cs" },
            { "DiagnosticCatalog.MSTest", "MSTestRules.g.cs" },
            { "DiagnosticCatalog.Trimming", "TrimRules.g.cs" },
            { "DiagnosticCatalog.AspNetCore", "AspNetCoreRules.g.cs" },
            { "DiagnosticCatalog.Syslib", "SyslibRules.g.cs" },
            { "DiagnosticCatalog.Roslyn", "RoslynRules.g.cs" },
            { "DiagnosticCatalog.PublicApi", "PublicApiRules.g.cs" },
            { "DiagnosticCatalog.BannedApi", "BannedApiRules.g.cs" },
        };

    [Theory]
    [MemberData(nameof(Catalogues))]
    public void The_readme_states_the_release_the_catalogue_actually_mirrors(string project, string source)
    {
        string expected = MirroredBy(source);

        Assert.Contains($"`{expected}`", MirrorBlock(project, "README.en.md"), StringComparison.Ordinal);
    }

    /// <summary>
    /// And so does its translation. A catalogue's README is a pair (ADR-0034), and a stale banner in
    /// the French half is the same wrong statement in the place a French reader looks first — with
    /// the added difficulty that neither the assembly attribute it contradicts nor the guides that
    /// would correct it are the document they opened.
    /// </summary>
    [Theory]
    [MemberData(nameof(Catalogues))]
    public void The_translated_readme_states_the_release_the_catalogue_actually_mirrors(
        string project, string source)
    {
        string expected = MirroredBy(source);

        Assert.Contains($"`{expected}`", MirrorBlock(project, "README.fr.md"), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Catalogues))]
    public void The_changelog_states_the_release_the_catalogue_actually_mirrors(string project, string source)
    {
        // Under Unreleased, so that cutting a release promotes the statement into that version's
        // section: every published entry then names what it mirrored, including the ones where
        // nothing upstream moved.
        string expected = MirroredBy(source);

        Assert.Contains($"`{expected}`", MirrorBlock(project, "CHANGELOG.md"), StringComparison.Ordinal);
    }

    /// <summary>
    /// The source of truth: what the generator recorded in the catalogue's own assembly-level
    /// attribute, read from the generated file rather than from either document.
    /// </summary>
    private static string MirroredBy(string generatedSource)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "catalogs", generatedSource);
        Assert.True(File.Exists(path), $"Generated source not found beside the tests: {path}");

        Match provenance = Regex.Match(
            File.ReadAllText(path).Replace("\r\n", "\n"),
            "\\[assembly: CatalogSource\\(\\s*source:\\s*\"(?<source>[^\"]*)\",\\s*" +
            "sourceVersion:\\s*\"(?<version>[^\"]*)\"",
            RegexOptions.None,
            TimeSpan.FromSeconds(10));
        Assert.True(provenance.Success, $"{generatedSource} declares no CatalogSource attribute");

        return $"{provenance.Groups["source"].Value} {provenance.Groups["version"].Value}";
    }

    /// <summary>
    /// What sits between the generator's markers in one of a catalogue's documents. An absent block
    /// fails rather than passing vacuously: a document with no block states nothing, which is the
    /// condition these tests exist to forbid.
    /// </summary>
    private static string MirrorBlock(string project, string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "catalogdocs", project, fileName);
        Assert.True(File.Exists(path), $"{fileName} not found beside the tests for {project}: {path}");

        string text = File.ReadAllText(path).Replace("\r\n", "\n");
        int start = text.IndexOf("<!-- mirror:begin -->", StringComparison.Ordinal);
        int end = text.IndexOf("<!-- mirror:end -->", StringComparison.Ordinal);
        Assert.True(
            start >= 0 && end > start,
            $"{project}/{fileName} carries no <!-- mirror:begin --> … <!-- mirror:end --> block, so it " +
            "does not state which upstream release the catalogue mirrors, and the generator cannot " +
            "keep it current.");

        return text.Substring(start, end - start);
    }
}
