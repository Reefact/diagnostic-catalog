using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// A catalogue's README and changelog state which upstream release it mirrors, and nothing
/// compiles either of them. Left to a maintainer's attention, that statement is wrong the first
/// night a regeneration moves the mirrored release — and wrong silently, in the place a consumer
/// looks first. The generator therefore rewrites it, and these tests are what say it does.
/// <para>
/// Their counterpart lives next door: <c>DocumentedMirrorTests</c> asserts that the shipped
/// documents agree with the shipped catalogues. Together they close the loop — this one keeps the
/// writer working, that one catches anything else that moved the text.
/// </para>
/// </summary>
public sealed class MirrorBannerTests : IDisposable
{
    private const string Package = "Vendor.Analyzers";
    private readonly string _temp = Directory.CreateTempSubdirectory("cataloggen-banner-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void Regenerating_restates_the_mirrored_release_in_the_readme()
    {
        WriteDoc("README.md", "> ## Mirrors `Vendor.Analyzers 1.0.0`");

        Emit();

        Assert.Contains("Mirrors `Vendor.Analyzers 2.0.0`", ReadDoc("README.md"), StringComparison.Ordinal);
        Assert.DoesNotContain("1.0.0", ReadDoc("README.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void Regenerating_restates_the_mirrored_release_in_the_changelog()
    {
        WriteDoc("CHANGELOG.md", "**Mirrors `Vendor.Analyzers 1.0.0`** — unchanged upstream.");

        Emit();

        Assert.Contains("**Mirrors `Vendor.Analyzers 2.0.0`**", ReadDoc("CHANGELOG.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_moved_upstream_release_says_what_it_moved_from()
    {
        // The question a reader of a new version actually has. Saying only where it landed leaves
        // them to diff two changelog entries to find out whether upstream moved at all.
        WriteDoc("CHANGELOG.md", "**Mirrors `Vendor.Analyzers 1.0.0`** — unchanged upstream.");

        Emit();

        Assert.Contains("upstream moved from `1.0.0`", ReadDoc("CHANGELOG.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void Prose_around_the_block_survives_being_rewritten()
    {
        // Whatever a catalogue has to explain about its upstream — a vendor mirrored on its
        // prerelease line, analyzers that ship inside the SDK — is authored prose that lives
        // outside the markers. Rewriting the block must not be able to take it with it.
        WriteDoc("README.md", "> ## Mirrors `Vendor.Analyzers 1.0.0`",
                 before: "# Vendor catalogue\n\nA tagline nobody generated.\n\n",
                 after: "\nMirrored on its prerelease line, deliberately.\n");

        Emit();

        string readme = ReadDoc("README.md");
        Assert.Contains("A tagline nobody generated.", readme, StringComparison.Ordinal);
        Assert.Contains("Mirrored on its prerelease line, deliberately.", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void A_document_carrying_no_block_is_left_alone()
    {
        // Where a banner belongs is an editorial choice the generator cannot make, so it reports
        // rather than guesses — and DocumentedMirrorTests fails the build over the absence, which
        // is the half of the loop that makes reporting enough.
        string path = Path.Combine(_temp, "README.md");
        File.WriteAllText(path, "# Vendor catalogue\n\nNo markers here.\n");

        Emit();

        Assert.Equal("# Vendor catalogue\n\nNo markers here.\n", File.ReadAllText(path).Replace("\r\n", "\n"));
    }

    [Fact]
    public void A_document_carrying_no_block_is_noted_without_claiming_anything_about_the_reader()
    {
        // This line now ships inside `dcat`, so it reaches repositories that have none of our
        // tests. Announcing to a stranger that "the tests will say so" is wrong twice over: nothing
        // failed, and the tests it names are this repository's, not theirs. Saying it at WARNING
        // compounds it — a document with no markers has asked for no banner, which is not a fault.
        File.WriteAllText(Path.Combine(_temp, "README.md"), "# Vendor catalogue\n\nNo markers here.\n");

        string said = Capture(Emit);

        Assert.Contains("README.md", said, StringComparison.Ordinal);
        Assert.DoesNotContain("the tests will say so", said, StringComparison.Ordinal);
        Assert.DoesNotContain($"WARNING: no {MirrorBegin}", said, StringComparison.Ordinal);
    }

    // Matches the marker the emitter opens a banner with, so the assertion above names the exact
    // line rather than any word that happens to read WARNING.
    private const string MirrorBegin = "<!-- mirror:begin -->";

    private static string Capture(Action action)
    {
        TextWriter original = Console.Out;
        using StringWriter captured = new();
        Console.SetOut(captured);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(original);
        }

        return captured.ToString();
    }

    private void Emit()
    {
        SortedDictionary<string, RuleInfo> before = new(StringComparer.Ordinal)
        {
            ["X0001"] = new("Usage", string.Empty, Retired: false, "Fields should be private."),
        };
        SortedDictionary<string, RuleInfo> now = new(StringComparer.Ordinal)
        {
            ["X0001"] = new("Usage", string.Empty, Retired: false, "Fields should be private."),
        };
        SortedDictionary<string, string> categories = new(StringComparer.Ordinal) { ["Usage"] = "Usage" };

        Job job = new(Package, "2.0.0", "Vendor.Catalog", "VendorRule",
                      Path.Combine(_temp, "VendorRules.g.cs"), "cs");
        CatalogEmitter.Emit(job, Package, "2.0.0", now, new Previous("1.0.0", before, categories),
                            dateOverride: "2026-01-01");
    }

    private void WriteDoc(string fileName, string block, string before = "", string after = "")
        => File.WriteAllText(
            Path.Combine(_temp, fileName),
            $"{before}<!-- mirror:begin -->\n{block}\n<!-- mirror:end -->\n{after}");

    private string ReadDoc(string fileName)
        => File.ReadAllText(Path.Combine(_temp, fileName)).Replace("\r\n", "\n");
}
