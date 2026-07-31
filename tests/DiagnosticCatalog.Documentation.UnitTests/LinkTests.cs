using System;
using System.IO;
using System.Text.RegularExpressions;
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
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The HTML attributes a figure can be shown through.</summary>
    private static readonly string[] ImageAttributes = ["src", "srcset"];

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
        MarkdownDocument document = Repository.Require(path);

        foreach (MarkdownLink link in document.Links)
        {
            if (link.IsExternal || link.IsLocalAnchor || link.PathPart.Length == 0) continue;

            string? target = Repository.Resolve(document, link.PathPart);

            if (target is null)
            {
                Assert.Fail(
                    $"{path}: the link \"{link.Text}\" climbs above the repository root ({link.Target}).");
            }

            Assert.True(
                Repository.Exists(target) || IsDirectory(target),
                $"{path}: the link \"{link.Text}\" points at {link.Target}, which resolves to " +
                $"{target} — and nothing is there.");
        }
    }

    [Theory]
    [MemberData(nameof(LinkedDocuments))]
    public void Every_anchor_resolves(string path)
    {
        MarkdownDocument document = Repository.Require(path);

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
        MarkdownDocument document = Repository.Require(path);

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
        string images = Path.Combine(Repository.Root, "doc", "images");
        if (!Directory.Exists(images)) return;

        IReadOnlySet<string> referenced = EverythingReferenced();

        foreach (string file in Directory.EnumerateFiles(images, "*", SearchOption.AllDirectories))
        {
            string relative = file[Repository.Root.Length..].Replace(Path.DirectorySeparatorChar, '/');

            Assert.True(
                referenced.Contains(relative),
                $"{relative} is committed but no document displays it. Either a page lost its figure " +
                "or the figure outlived the page.");
        }
    }

    /// <summary>
    /// Every repository path any document points at, whether through a Markdown link or through the
    /// HTML a figure needs — <c>&lt;img src&gt;</c>, and the <c>&lt;source srcset&gt;</c> of a
    /// <c>&lt;picture&gt;</c> offering a light and a dark rendering.
    /// </summary>
    private static IReadOnlySet<string> EverythingReferenced()
    {
        HashSet<string> referenced = new(StringComparer.Ordinal);

        foreach (MarkdownDocument document in Repository.Documents)
        {
            foreach (MarkdownLink link in document.Links)
            {
                Remember(referenced, document, link.IsExternal ? string.Empty : link.PathPart);
            }

            foreach (string attribute in ImageAttributes)
            {
                foreach (Match match in Regex.Matches(
                             document.Text,
                             attribute + "=\"(?<target>[^\"]+)\"",
                             RegexOptions.None,
                             MatchTimeout))
                {
                    Remember(referenced, document, match.Groups["target"].Value);
                }
            }
        }

        return referenced;
    }

    private static void Remember(HashSet<string> referenced, MarkdownDocument document, string target)
    {
        if (target.Length == 0) return;

        string? resolved = Repository.Resolve(document, target);
        if (resolved is not null) referenced.Add(resolved);
    }

    [Fact]
    public void The_documents_are_discovered()
    {
        Assert.True(
            Repository.Documents.Count >= 20,
            $"Only {Repository.Documents.Count} Markdown documents were found under " +
            $"{Repository.Root}. These theories would assert almost nothing.");
    }

    private static bool IsDirectory(string relativePath) =>
        Directory.Exists(
            Path.Combine(Repository.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

}
