using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// A rule's documentation comment carries the title its <c>DiagnosticDescriptor</c> declares, so that
/// hovering a constant says what the rule is about rather than restating the identifier already under
/// the cursor. Three things have to hold for that to be safe: the sentence goes onto exactly one line,
/// it is escaped for XML rather than for a C# string literal, and the next run can read it back —
/// a title the parser could not recover would be reported as changed every night, forever.
/// </summary>
public sealed class RuleTitleTests : IDisposable
{
    private const string Package = "Vendor.Analyzers";
    private readonly string _temp = Directory.CreateTempSubdirectory("cataloggen-title-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void A_rules_summary_is_the_title_the_descriptor_declares()
    {
        string emitted = Emit(("X0001", "Usage", "Unused private members should be removed", ""));

        Assert.Contains(
            "    /// <summary>Unused private members should be removed.</summary>\n    [DiagnosticRule]",
            emitted.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    [Theory]
    // The vendors are not consistent: 964 of the 967 titles across the three mirrored packages
    // carry no final stop and three do. A catalogue whose sentences end two different ways shows
    // the reader an inconsistency that is upstream's, not its own.
    [InlineData("Unused private members should be removed", "Unused private members should be removed.")]
    [InlineData("Using static directives should be placed correctly.",
                "Using static directives should be placed correctly.")]
    [InlineData("Something ended oddly..", "Something ended oddly.")]
    [InlineData("Trailing space is not a sentence ", "Trailing space is not a sentence.")]
    public void A_summary_ends_on_exactly_one_full_stop(string title, string expected)
    {
        string emitted = Emit(("X0001", "Usage", title, ""));

        Assert.Contains($"/// <summary>{expected}</summary>", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_categorys_own_value_is_not_restated_in_its_documentation()
    {
        // The category constant names what it is, not what it holds: the value sits on the very
        // next line, and a comment repeating it is a second copy to keep in step for no gain.
        string emitted = Emit(("X0001", "Usage", "Unused private members should be removed", ""));

        Assert.Contains(
            "        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>\n" +
            "        public const string Category = VendorCategory.Usage;",
            emitted.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_title_carrying_angle_brackets_is_escaped_for_xml()
    {
        // Five SonarAnalyzer titles and five .NET analyzer ones look like this. Escaped for a C#
        // string literal instead — which is what every other value in the file needs — the emitted
        // file does not compile: the compiler reports CS1570, promoted to an error here.
        string emitted = Emit(("X0001", "Usage", "Value types should implement \"IEquatable<T>\"", ""));

        Assert.Contains(
            "/// <summary>Value types should implement \"IEquatable&lt;T&gt;\".</summary>",
            emitted,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_help_link_sits_beside_the_title_rather_than_inside_it()
    {
        // Both on one line reaches 282 characters on the .NET analyzers, and a regeneration is
        // reviewed as a diff (ADR-0009). The link stays visible on hover: Roslyn's quick info
        // renders remarks by default.
        string emitted = Emit(("X0001", "Usage", "Types should be defined in named namespaces",
                               "https://example.test/rules/x0001"));

        Assert.Contains(
            "    /// <summary>Types should be defined in named namespaces.</summary>\n" +
            "    /// <remarks>See <see href=\"https://example.test/rules/x0001\"/>.</remarks>\n",
            emitted.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_rule_whose_descriptor_declares_no_title_keeps_the_identifier_and_category()
    {
        // The case that cannot be repaired: a rule retired before this generator emitted titles is
        // carried forward from a file that never recorded one, and the descriptor it came from is
        // gone. It must still document itself.
        string emitted = Emit(("X0001", "Usage", "", ""));

        Assert.Contains(
            "/// <summary>Rule <c>X0001</c>, category <c>Usage</c>.</summary>",
            emitted,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Unused private members should be removed.")]
    [InlineData("Value types should implement \"IEquatable<T>\".")]
    [InlineData("Rule <c>X0001</c>, category <c>Usage</c>.")]
    public void A_title_survives_being_read_back_by_the_next_run(string title)
    {
        // The third case is the trap: a title that happens to read exactly like the sentence used
        // when there is no title. The parser distinguishes them by comparing against the escaped
        // form the emitter would have written, so this one comes back as a title, not as nothing.
        //
        // The titles here already end on a full stop, as the descriptor reader leaves them: what
        // is written and what is read back have to be the same string, or a rule would be reported
        // as retitled on every run.
        string output = Path.Combine(_temp, "readback.g.cs");
        EmitTo(output, ("X0001", "Usage", title, ""));

        Previous? reparsed = CatalogParser.ReadPrevious(output);

        Assert.NotNull(reparsed);
        Assert.Equal(title, reparsed!.Rules["X0001"].Title);
    }

    [Fact]
    public void A_title_reworded_upstream_rewrites_the_file_and_is_reported()
    {
        // Nothing else about the rule moved: same version, same category, same identifier. Before
        // titles were emitted this run wrote nothing, which is now the wrong answer — the catalogue
        // would keep serving the previous sentence.
        SortedDictionary<string, RuleInfo> before = new(StringComparer.Ordinal)
        {
            ["X0001"] = new("Usage", string.Empty, Retired: false, "Fields should be private"),
        };
        SortedDictionary<string, string> categories = new(StringComparer.Ordinal) { ["Usage"] = "Usage" };

        GenerateResult result = CatalogEmitter.Emit(
            Job(Path.Combine(_temp, "retitled.g.cs")), Package, "2.0.0",
            Rules(("X0001", "Usage", "Fields should not have public accessibility", "")),
            new Previous("2.0.0", before, categories), dateOverride: "2026-01-01");

        Assert.True(result.Changed);
        Assert.Contains("**Retitled upstream (1):**", result.Summary, StringComparison.Ordinal);
        Assert.Contains(
            "- `X0001` — \"Fields should be private\" → \"Fields should not have public accessibility\"",
            result.Summary,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_that_changes_no_title_still_writes_nothing()
    {
        // The other half of the test above: reporting a retitle must not become a nightly pull
        // request whose only content is the same sentence written again.
        SortedDictionary<string, RuleInfo> settled = new(StringComparer.Ordinal)
        {
            ["X0001"] = new("Usage", string.Empty, Retired: false, "Fields should be private"),
        };
        SortedDictionary<string, string> categories = new(StringComparer.Ordinal) { ["Usage"] = "Usage" };
        string output = Path.Combine(_temp, "settled.g.cs");

        GenerateResult result = CatalogEmitter.Emit(
            Job(output), Package, "2.0.0",
            Rules(("X0001", "Usage", "Fields should be private", "")),
            new Previous("2.0.0", settled, categories), dateOverride: "2026-01-01");

        Assert.False(result.Changed);
        Assert.False(File.Exists(output));
    }

    private static Job Job(string output) => new(Package, "2.0.0", "Vendor.Catalog", "VendorRule", output, "cs");

    private static SortedDictionary<string, RuleInfo> Rules(
        params (string Id, string Category, string Title, string Help)[] rules)
    {
        SortedDictionary<string, RuleInfo> map = new(StringComparer.Ordinal);
        foreach ((string id, string category, string title, string help) in rules)
            map[id] = new RuleInfo(category, help, Retired: false, title);
        return map;
    }

    private string Emit((string Id, string Category, string Title, string Help) rule)
    {
        string output = Path.Combine(_temp, $"{Guid.NewGuid():N}.g.cs");
        EmitTo(output, rule);
        return File.ReadAllText(output);
    }

    private static void EmitTo(string output, (string Id, string Category, string Title, string Help) rule)
        => CatalogEmitter.Emit(
            Job(output), Package, "2.0.0", Rules(rule), previous: null, dateOverride: "2026-01-01");
}
