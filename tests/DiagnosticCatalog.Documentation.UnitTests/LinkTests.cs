using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every relative link in the documentation resolves to a file that exists, and every anchor to a
/// heading that exists.
/// </summary>
/// <remarks>
/// <para>
/// Nothing compiles a Markdown link. A page renamed leaves every reference to it pointing at a 404,
/// and the only signal is a reader who follows one — which is to say, the reader least able to work
/// out what the page was called before. This repository renames documents deliberately and often
/// enough for that to be a real cost.
/// </para>
/// <para>
/// The package READMEs are checked the other way round. They are shipped inside the <c>.nupkg</c>
/// and rendered by nuget.org, which resolves no relative link at all: there a relative link is
/// always broken, however carefully it was written, so the requirement is that they carry none.
/// </para>
/// </remarks>
public sealed class LinkTests
{
    public static TheoryData<string> LinkedDocuments()
    {
        TheoryData<string> paths = new();
        foreach (MarkdownDocument document in Repository.Documents)
        {
            paths.Add(document.Path);
        }

        return paths;
    }

    public static TheoryData<string> PackageReadmes()
    {
        TheoryData<string> paths = new();
        foreach (MarkdownDocument document in Repository.Documents)
        {
            if (document.Path.StartsWith("src/", StringComparison.Ordinal) &&
                document.Path.EndsWith("/README.md", StringComparison.Ordinal))
            {
                paths.Add(document.Path);
            }
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(LinkedDocuments))]
    public void Every_relative_link_resolves(string path)
    {
        MarkdownDocument document = Document(path);

        foreach (MarkdownLink link in document.Links)
        {
            if (link.IsExternal || link.IsLocalAnchor || link.PathPart.Length == 0) continue;

            string? target = Repository.Resolve(document, link.PathPart);

            Assert.True(
                target is not null,
                $"{path}: the link \"{link.Text}\" climbs above the repository root ({link.Target}).");

            Assert.True(
                Repository.Exists(target!) || Directory(target!),
                $"{path}: the link \"{link.Text}\" points at {link.Target}, which resolves to " +
                $"{target} — and nothing is there.");
        }
    }

    [Theory]
    [MemberData(nameof(LinkedDocuments))]
    public void Every_anchor_resolves(string path)
    {
        MarkdownDocument document = Document(path);

        foreach (MarkdownLink link in document.Links)
        {
            if (link.IsExternal || link.Anchor.Length == 0) continue;

            MarkdownDocument? target = link.IsLocalAnchor
                ? document
                : Repository.Find(Repository.Resolve(document, link.PathPart) ?? string.Empty);

            // A link into a file this test does not read — a source file, say — carries no heading
            // to check. Every_relative_link_resolves has already established that the file is there.
            if (target is null) continue;

            Assert.True(
                target.HasAnchor(link.Anchor),
                $"{path}: the link \"{link.Text}\" points at #{link.Anchor} in {target.Path}, which " +
                "declares no heading with that anchor. GitHub silently lands the reader at the top " +
                "of the page instead.");
        }
    }

    [Theory]
    [MemberData(nameof(PackageReadmes))]
    public void A_package_readme_carries_no_relative_link(string path)
    {
        MarkdownDocument document = Document(path);

        IReadOnlyList<MarkdownLink> relative = document.Links
            .Where(link => !link.IsExternal && !link.IsLocalAnchor && link.PathPart.Length > 0)
            .ToList();

        Assert.True(
            relative.Count == 0,
            $"{path} carries relative links: " +
            $"{string.Join(", ", relative.Select(link => link.Target))}. This file is shipped inside " +
            "the package and rendered by nuget.org, which resolves none of them — the reader gets a " +
            "dead link. Use an absolute https://github.com/Reefact/diagnostic-catalog/blob/main/… " +
            "address instead.");
    }

    /// <summary>
    /// Every image a page shows exists, and every image committed under <c>doc/images/</c> is shown
    /// by a page. The second direction matters because an unused figure is a file that still has to
    /// be kept correct, by someone who has no way of telling that nothing displays it.
    /// </summary>
    [Fact]
    public void Every_committed_image_is_displayed()
    {
        string images = System.IO.Path.Combine(Repository.Root, "doc", "images");
        if (!System.IO.Directory.Exists(images)) return;

        HashSet<string> referenced = new(StringComparer.Ordinal);
        foreach (MarkdownDocument document in Repository.Documents)
        {
            foreach (MarkdownLink link in document.Links)
            {
                if (link.IsExternal || link.PathPart.Length == 0) continue;

                string? target = Repository.Resolve(document, link.PathPart);
                if (target is not null) referenced.Add(target);
            }

            // <img src="..."> and <source srcset="..."> inside a <picture>, which is how a figure
            // offers a light and a dark rendering.
            foreach (string attribute in new[] { "src", "srcset" })
            {
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                             document.Text,
                             attribute + "=\"(?<target>[^\"]+)\"",
                             System.Text.RegularExpressions.RegexOptions.None,
                             TimeSpan.FromSeconds(10)))
                {
                    string? target = Repository.Resolve(document, match.Groups["target"].Value);
                    if (target is not null) referenced.Add(target);
                }
            }
        }

        foreach (string file in System.IO.Directory.EnumerateFiles(images, "*", System.IO.SearchOption.AllDirectories))
        {
            string relative = file[Repository.Root.Length..].Replace(System.IO.Path.DirectorySeparatorChar, '/');

            Assert.True(
                referenced.Contains(relative),
                $"{relative} is committed but no document displays it. Either a page lost its figure " +
                "or the figure outlived the page.");
        }
    }

    [Fact]
    public void The_documents_are_discovered()
    {
        Assert.True(
            Repository.Documents.Count >= 20,
            $"Only {Repository.Documents.Count} Markdown documents were found under " +
            $"{Repository.Root}. These theories would assert almost nothing.");
    }

    private static bool Directory(string relativePath) =>
        System.IO.Directory.Exists(
            System.IO.Path.Combine(
                Repository.Root,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

    private static MarkdownDocument Document(string path)
    {
        MarkdownDocument? document = Repository.Find(path);
        Assert.True(document is not null, $"{path} was discovered and then could not be read.");

        return document!;
    }
}
