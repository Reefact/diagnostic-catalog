using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// What the generator reads and writes must not depend on how git checked the file out.
/// </summary>
/// <remarks>
/// <para>
/// The emitter writes LF, always. What puts CRLF on disk is the round trip through git:
/// <c>core.autocrlf=true</c> converts on checkout, and that is the default of the Git for Windows
/// installer and of the <c>windows-latest</c> runners. This repository is immune because
/// <c>.gitattributes</c> pins <c>*.g.cs text eol=lf</c> — but <c>dcat</c> ships, a consumer's
/// repository carries no such rule, and nothing in the published documentation asks them to add one.
/// </para>
/// <para>
/// Every other test that reads a generated catalogue back reads one the emitter has just written, so
/// it is LF by construction and the CRLF path is never taken. That is precisely why these are here
/// rather than folded into the round-trip tests: the fixture has to be spelled with CRLF on purpose.
/// </para>
/// </remarks>
public sealed class LineEndingTests : IDisposable
{
    private const string Package = "Vendor.Analyzers";

    private readonly string _temp = Directory.CreateTempSubdirectory("cataloggen-eol-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static SortedDictionary<string, RuleInfo> Rules(params string[] ids)
    {
        SortedDictionary<string, RuleInfo> map = new(StringComparer.Ordinal);
        foreach (string id in ids) map[id] = new RuleInfo("Usage", string.Empty, Retired: false);

        return map;
    }

    private static Previous Before(string version, SortedDictionary<string, RuleInfo> rules)
    {
        SortedDictionary<string, string> categories = new(StringComparer.Ordinal);
        foreach (RuleInfo info in rules.Values) categories[info.Category] = Naming.ToIdentifier(info.Category);

        return new Previous(version, rules, categories);
    }

    /// <summary>Rewrites the file with CRLF endings, as a checkout under core.autocrlf would.</summary>
    private static void CheckOutAsCrlf(string path) =>
        File.WriteAllText(path, File.ReadAllText(path).ReplaceLineEndings("\r\n"));

    [Fact]
    public void A_catalogue_checked_out_with_crlf_is_read_back_in_full()
    {
        string output = Path.Combine(_temp, "VendorRules.g.cs");
        Job job = new(Package, "1.0.0", "Vendor.Catalog", "VendorRule", output, "cs");
        CatalogEmitter.Emit(job, Package, "1.0.0", Rules("X0001", "X0002"), previous: null,
                            dateOverride: "2026-01-01");

        Previous? asWritten = CatalogParser.ReadPrevious(output);
        Assert.NotNull(asWritten);
        Assert.Equal(2, asWritten.Rules.Count);

        CheckOutAsCrlf(output);

        Previous? asCheckedOut = CatalogParser.ReadPrevious(output);

        // The version is recovered either way — its pattern is not anchored to the end of a line —
        // so a parse that lost every rule still comes back looking like a valid previous run.
        Assert.NotNull(asCheckedOut);
        Assert.Equal(asWritten.SourceVersion, asCheckedOut.SourceVersion);

        Assert.Equal(asWritten.Rules.Count, asCheckedOut.Rules.Count);
        Assert.Equal(asWritten.CategoryNames.Count, asCheckedOut.CategoryNames.Count);
    }

    [Fact]
    public void A_retirement_survives_a_regeneration_from_a_crlf_checkout()
    {
        // The consequence that costs something. An earlier run carried X0002 forward and marked it
        // obsolete. Read back as zero rules, CarryForwardRetired has nothing to carry, so the
        // constant is deleted — the one thing §23.1 says a catalogue may never do to a consumer.
        string output = Path.Combine(_temp, "VendorRules.g.cs");
        Job job = new(Package, "2.0.0", "Vendor.Catalog", "VendorRule", output, "cs");

        CatalogEmitter.Emit(job, Package, "2.0.0", Rules("X0001"), Before("1.0.0", Rules("X0001", "X0002")),
                            dateOverride: "2026-01-01");

        Assert.Contains("public static class X0002", File.ReadAllText(output), StringComparison.Ordinal);

        CheckOutAsCrlf(output);

        Job next = new(Package, "3.0.0", "Vendor.Catalog", "VendorRule", output, "cs");
        CatalogEmitter.Emit(next, Package, "3.0.0", Rules("X0001"), CatalogParser.ReadPrevious(output),
                            dateOverride: "2026-01-02");

        string regenerated = File.ReadAllText(output);

        Assert.Contains("public static class X0002", regenerated, StringComparison.Ordinal);
        Assert.Contains("[Obsolete(", regenerated, StringComparison.Ordinal);
    }

    [Fact]
    public void A_banner_written_into_a_crlf_document_keeps_its_line_endings()
    {
        // The mirror banner is rewritten in place with hard "\n", so a document a consumer keeps in
        // CRLF comes back with a handful of LF lines in the middle of it — a diff on lines nobody
        // edited, in the file a consumer reads first.
        string readme = Path.Combine(_temp, "README.md");
        File.WriteAllText(
            readme,
            "# Vendor catalogue\r\n\r\n<!-- mirror:begin -->\r\n> ## Mirrors `Vendor.Analyzers 1.0.0`\r\n<!-- mirror:end -->\r\n");

        Job job = new(Package, "2.0.0", "Vendor.Catalog", "VendorRule",
                      Path.Combine(_temp, "VendorRules.g.cs"), "cs");
        CatalogEmitter.Emit(job, Package, "2.0.0", Rules("X0001"), Before("1.0.0", Rules("X0001")),
                            dateOverride: "2026-01-01");

        string written = File.ReadAllText(readme);

        Assert.Contains("Mirrors `Vendor.Analyzers 2.0.0`", written, StringComparison.Ordinal);

        // Every line ending is still CRLF: no LF stands alone.
        Assert.Equal(
            written.Split('\n').Length - 1,
            written.Split("\r\n", StringSplitOptions.None).Length - 1);
    }
}
