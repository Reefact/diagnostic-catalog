using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The working tree, as the documentation tests see it: where the repository root is, which Markdown
/// documents exist, and how a path written inside one of them resolves against the others.
/// </summary>
/// <remarks>
/// The root is read from assembly metadata stamped by this project's <c>.csproj</c> at build time,
/// rather than discovered by walking up from the output directory. Walking up is a guess about the
/// build layout, and the layout is not what these tests are about; the stamp is exact and fails
/// loudly if it is ever lost.
/// </remarks>
internal static class Repository
{
    /// <summary>
    /// Documents that are checked for links and, where they carry a language suffix, for parity.
    /// Wider than <c>doc/</c> on purpose: the repository README and the contributor instructions
    /// link INTO the documentation set, so a page renamed without them is a dead link on the front
    /// door — which is exactly the failure a link check exists to catch.
    /// </summary>
    private static readonly string[] LinkedRoots =
    [
        "doc",
        "src",
    ];

    /// <summary>
    /// Top-level documents outside any scanned folder that still link into the set.
    /// </summary>
    private static readonly string[] LinkedFiles =
    [
        "README.md",
        "CONTRIBUTING.md",
        "CHANGELOG.md",
        "CLAUDE.md",
        "AGENTS.md",
        "SECURITY.md",
    ];

    private static readonly Lazy<string> RootPath = new(FindRoot);

    private static readonly Lazy<IReadOnlyList<MarkdownDocument>> AllDocuments = new(LoadAll);

    /// <summary>The absolute path of the repository root, with a trailing separator.</summary>
    internal static string Root => RootPath.Value;

    /// <summary>Every Markdown document these tests read, by repository-relative path.</summary>
    internal static IReadOnlyList<MarkdownDocument> Documents => AllDocuments.Value;

    /// <summary>
    /// The documents under <c>doc/guide/</c>, which is the folder that carries a reading order.
    /// </summary>
    internal static IReadOnlyList<MarkdownDocument> Guide =>
        Documents
            .Where(document => document.Path.StartsWith("doc/guide/", StringComparison.Ordinal))
            .ToList();

    /// <summary>
    /// The documents that carry a language suffix, and are therefore bound by the parity rule.
    /// A document without one — a decision record, for now — is simply not in this set; see
    /// <c>doc/CONVENTIONS.en.md</c>, "What the parity check actually sees".
    /// </summary>
    internal static IReadOnlyList<MarkdownDocument> Bilingual =>
        Documents.Where(document => document.Language is not null).ToList();

    /// <summary>Whether a repository-relative path exists as a file on disk.</summary>
    internal static bool Exists(string relativePath) =>
        File.Exists(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>The document at a repository-relative path, or <c>null</c> if it is not Markdown.</summary>
    internal static MarkdownDocument? Find(string relativePath) =>
        Documents.FirstOrDefault(
            document => string.Equals(document.Path, relativePath, StringComparison.Ordinal));

    /// <summary>
    /// The document at a repository-relative path, failing the test when there is none.
    /// </summary>
    /// <remarks>
    /// Every theory here is parameterised by a path this class discovered, so a miss means the set
    /// changed underneath the run rather than that the test was written wrong — worth saying out loud
    /// rather than letting a null reference say it. <see cref="Assert.Fail(string)"/> does not return,
    /// so the compiler narrows the result on its own and no null-forgiving operator is needed.
    /// </remarks>
    internal static MarkdownDocument Require(string relativePath)
    {
        MarkdownDocument? document = Find(relativePath);
        if (document is null)
        {
            Assert.Fail($"{relativePath} was discovered and then could not be read under {Root}.");
        }

        return document;
    }

    /// <summary>
    /// Resolves a link target written inside <paramref name="from"/> to a repository-relative path,
    /// the way a Markdown renderer does: relative to the folder of the document that carries it.
    /// Returns <c>null</c> when the target climbs above the repository root, which is itself a
    /// broken link.
    /// </summary>
    internal static string? Resolve(MarkdownDocument from, string target)
    {
        string folder = from.Path.Contains('/')
            ? from.Path[..from.Path.LastIndexOf('/')]
            : string.Empty;

        List<string> segments = folder.Length == 0
            ? []
            : folder.Split('/').ToList();

        foreach (string segment in target.Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0) return null;

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return string.Join("/", segments);
    }

    private static string FindRoot()
    {
        AssemblyMetadataAttribute? stamp = typeof(Repository).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(metadata =>
                string.Equals(metadata.Key, "DocumentationRepositoryRoot", StringComparison.Ordinal));

        if (stamp is null)
        {
            Assert.Fail(
                "This assembly carries no DocumentationRepositoryRoot metadata, so the documentation " +
                "tests cannot find the tree they read. The stamp is written by this project's .csproj; " +
                "check that its <AssemblyMetadata> item is still there.");
        }

        string root = stamp.Value ?? string.Empty;

        Assert.True(
            Directory.Exists(root),
            $"The stamped repository root does not exist: {root}");

        return root;
    }

    private static IReadOnlyList<MarkdownDocument> LoadAll()
    {
        List<MarkdownDocument> documents = [];

        foreach (string file in LinkedFiles)
        {
            string path = Path.Combine(Root, file);
            if (File.Exists(path))
            {
                documents.Add(MarkdownDocument.Load(file, path));
            }
        }

        foreach (string folder in LinkedRoots)
        {
            string path = Path.Combine(Root, folder);
            if (!Directory.Exists(path)) continue;

            foreach (string file in Directory.EnumerateFiles(path, "*.md", SearchOption.AllDirectories))
            {
                // bin/ and obj/ hold copies of documents other test projects stage beside
                // themselves. Reading those would check the same file twice and report the failure
                // against a path nobody edits.
                string relative = Relative(file);
                if (relative.Contains("/bin/", StringComparison.Ordinal)) continue;
                if (relative.Contains("/obj/", StringComparison.Ordinal)) continue;

                documents.Add(MarkdownDocument.Load(relative, file));
            }
        }

        return documents
            .OrderBy(document => document.Path, StringComparer.Ordinal)
            .ToList();
    }

    private static string Relative(string absolutePath) =>
        absolutePath[Root.Length..].Replace(Path.DirectorySeparatorChar, '/');
}
