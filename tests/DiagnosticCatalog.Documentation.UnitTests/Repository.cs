using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The working tree, as the documentation tests see it: where the repository root is, which Markdown
/// documents exist, how a path written inside one of them resolves against the others, and which
/// shipped C# sources carry documentation of their own.
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
    /// The address a document writes when a relative link would not work. The package READMEs are
    /// shipped inside the <c>.nupkg</c> and rendered by nuget.org, which resolves no relative link,
    /// so they reach the rest of the repository in full — the language banner that offers the other
    /// half included (ADR-0034). An address naming a path inside this repository is still a path
    /// inside it, so it is checked rather than waved through as external.
    /// </summary>
    private const string BlobAddress = "https://github.com/Reefact/diagnostic-catalog/blob/main/";

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

    private static readonly Lazy<IReadOnlyList<string>> AllSources = new(LoadSources);

    /// <summary>The absolute path of the repository root, with a trailing separator.</summary>
    internal static string Root => RootPath.Value;

    /// <summary>Every Markdown document these tests read, by repository-relative path.</summary>
    internal static IReadOnlyList<MarkdownDocument> Documents => AllDocuments.Value;

    /// <summary>
    /// The hand-written C# files under <c>src/</c>, by repository-relative path. These carry the XML
    /// documentation that ships inside the packages and that an IDE shows on hover, which is
    /// documentation the Markdown checks never see.
    /// </summary>
    internal static IReadOnlyList<string> Sources => AllSources.Value;

    /// <summary>The text of a source file discovered by <see cref="Sources"/>.</summary>
    internal static string ReadSource(string relativePath) =>
        File.ReadAllText(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

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

    /// <summary>
    /// The package READMEs: the pages shipped inside a <c>.nupkg</c> and rendered by nuget.org.
    /// That renderer is what makes their rules differ from every other page's — it resolves no
    /// relative link — so which documents it governs is answered here rather than in each test that
    /// has to ask.
    /// </summary>
    internal static IReadOnlyList<MarkdownDocument> PackageReadmes =>
        Documents
            .Where(document =>
                document.Path.StartsWith("src/", StringComparison.Ordinal) &&
                document.FileName.StartsWith("README.", StringComparison.Ordinal))
            .ToList();

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
    /// <remarks>
    /// An address that names this repository in full resolves to the path it names, whatever
    /// document carries it. That is what keeps a package README checkable: nuget.org resolves no
    /// relative link, so those pages write every address out (<see cref="BlobAddress"/>), and a
    /// check that read them as external would verify nothing about the one link that has to work.
    /// </remarks>
    internal static string? Resolve(MarkdownDocument from, string target)
    {
        if (target.StartsWith(BlobAddress, StringComparison.Ordinal))
        {
            return target[BlobAddress.Length..];
        }

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

    private static List<MarkdownDocument> LoadAll()
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

    /// <summary>
    /// The hand-written sources under <c>src/</c>. Generated files are left out: a catalogue's
    /// <c>.g.cs</c> is written by the generator from the analyzer's own descriptors, so a sample
    /// cannot be wrong in it, and nobody edits it to fix one.
    /// </summary>
    private static List<string> LoadSources()
    {
        List<string> sources = [];

        string root = Path.Combine(Root, "src");
        if (!Directory.Exists(root)) return sources;

        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Relative(file);
            if (relative.Contains("/bin/", StringComparison.Ordinal)) continue;
            if (relative.Contains("/obj/", StringComparison.Ordinal)) continue;
            if (relative.EndsWith(".g.cs", StringComparison.Ordinal)) continue;

            sources.Add(relative);
        }

        return sources.OrderBy(path => path, StringComparer.Ordinal).ToList();
    }

    private static string Relative(string absolutePath) =>
        absolutePath[Root.Length..].Replace(Path.DirectorySeparatorChar, '/');
}
