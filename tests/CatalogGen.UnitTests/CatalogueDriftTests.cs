using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// What the emitter publishes, it must also compare.
/// </summary>
/// <remarks>
/// <para>
/// A run rewrites the file when something moved, and leaves it alone otherwise — that second half is
/// what keeps the scheduled job from opening a pull request every night. The comparison deciding
/// which it is has to cover everything the file states, or the catalogue keeps serving a value
/// upstream no longer has and <c>dcat validate</c>, doing the same comparison, calls it current.
/// </para>
/// <para>
/// The upstream version is what usually saves it: a different release means a different version, so
/// the file is rewritten and the uncompared fields ride along. That does not hold for a source read
/// off disk — <c>--project</c>, <c>--assembly</c>, <c>--solution</c> — where the version is the
/// assembly's own and stays put across every rebuild, which <c>LocalAssemblySource</c> names in as
/// many words. Every test here therefore keeps the version still, because that is the case where the
/// comparison is the only thing standing.
/// </para>
/// <para>
/// Rules are only half of what a catalogue publishes. The other half comes from the MANIFEST — the
/// namespace declared, the class the rules sit in, the source recorded, the language read — and a
/// comparison that enumerated fields would keep needing one more the day the emitter states one
/// more. So every previous state here is a file this emitter actually wrote, read back the way the
/// next run reads it, which is what makes the comparison exhaustive rather than merely long.
/// </para>
/// </remarks>
public sealed class CatalogueDriftTests : IDisposable
{
    private const string Package = "Vendor.Analyzers";

    /// <summary>Held still on purpose — see the remarks.</summary>
    private const string Version = "1.0.0";

    private const string Namespace = "Vendor.Catalog";
    private const string Container = "VendorRule";

    private const string FirstRun = "2026-01-01";
    private const string SecondRun = "2026-02-02";

    private readonly string _temp = Directory.CreateTempSubdirectory("cataloggen-drift-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private static SortedDictionary<string, RuleInfo> Rules(params RuleInfo[] rules)
    {
        SortedDictionary<string, RuleInfo> map = new(StringComparer.Ordinal);
        for (int index = 0; index < rules.Length; index++) map["X000" + (index + 1)] = rules[index];

        return map;
    }

    private static RuleInfo Rule(string helpLinkUri = "", string title = "A title.") =>
        new("Usage", helpLinkUri, Retired: false, title);

    private string Output => Path.Combine(_temp, "VendorRules.g.cs");

    private Job JobWith(string ns = Namespace, string container = Container, string language = "cs") =>
        new(Package, Version, ns, container, Output, language);

    /// <summary>
    /// The file a previous run left behind, read back the way the next run reads it.
    /// </summary>
    /// <remarks>
    /// Written rather than constructed. A <see cref="Previous"/> built by hand states what a test
    /// believes the last run published; a file the emitter wrote states what it did publish, and the
    /// difference is exactly the drift these tests are about.
    /// </remarks>
    private Previous Settled(SortedDictionary<string, RuleInfo> rules, Job? job = null, string? sourceName = null)
    {
        Job previousJob = job ?? JobWith();
        CatalogEmitter.Emit(previousJob, sourceName ?? Package, Version, rules, previous: null,
                            dateOverride: FirstRun);

        return CatalogParser.ReadPrevious(previousJob.Output)!;
    }

    /// <summary>The same, for a rule an earlier run carried forward as retired.</summary>
    private Previous SettledWithRetired(
        SortedDictionary<string, RuleInfo> live, SortedDictionary<string, RuleInfo> before)
    {
        Job job = JobWith();
        CatalogEmitter.Emit(job, Package, Version, before, previous: null, dateOverride: FirstRun);
        CatalogEmitter.Emit(job, Package, Version, live, CatalogParser.ReadPrevious(job.Output),
                            dateOverride: FirstRun);

        return CatalogParser.ReadPrevious(job.Output)!;
    }

    private GenerateResult Emit(
        SortedDictionary<string, RuleInfo> upstream, Previous previous, out string emitted,
        Job? job = null, string? sourceName = null)
    {
        GenerateResult result = CatalogEmitter.Emit(
            job ?? JobWith(), sourceName ?? Package, Version, upstream, previous, dateOverride: SecondRun);

        emitted = File.Exists(Output) ? File.ReadAllText(Output) : string.Empty;

        return result;
    }

    [Fact]
    public void A_rule_the_vendor_declares_again_stops_being_marked_obsolete()
    {
        // The catalogue says "no longer declared by the vendor — remove your suppression" about a
        // rule the vendor declares. Nothing downstream can contradict it: the platform never
        // validates a suppression's category, so the only thing that could was this comparison.
        Previous carriedForward = SettledWithRetired(Rules(Rule()), Rules(Rule(), Rule()));

        GenerateResult result = Emit(Rules(Rule(), Rule()), carriedForward, out string emitted);

        Assert.True(result.Changed, "a rule coming back is a change");
        Assert.Contains("public static class X0002", emitted, StringComparison.Ordinal);
        Assert.DoesNotContain("[Obsolete(", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_help_link_the_vendor_added_reaches_the_catalogue()
    {
        Previous withoutLink = Settled(Rules(Rule()));

        GenerateResult result = Emit(Rules(Rule("https://vendor.example/X0001")), withoutLink, out string emitted);

        Assert.True(result.Changed, "a help link appearing is a change");
        Assert.Contains(
            "public const string HelpLinkUri = \"https://vendor.example/X0001\";",
            emitted,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_help_link_the_vendor_moved_reaches_the_catalogue()
    {
        Previous withOldLink = Settled(Rules(Rule("https://vendor.example/old")));

        GenerateResult result = Emit(Rules(Rule("https://vendor.example/new")), withOldLink, out string emitted);

        Assert.True(result.Changed, "a help link moving is a change");
        Assert.Contains("https://vendor.example/new", emitted, StringComparison.Ordinal);
        Assert.DoesNotContain("https://vendor.example/old", emitted, StringComparison.Ordinal);
    }

    // --- what the MANIFEST publishes ------------------------------------------------
    //
    // Each of the four below moves a value that reaches the generated file and no rule at all. Under
    // a comparison made of rules and a version, every one of them was reported "current" while the
    // file on disk said something else — including through `dcat validate`, whose whole answer is
    // this comparison.

    [Fact]
    public void A_namespace_the_manifest_moved_reaches_the_catalogue()
    {
        Previous published = Settled(Rules(Rule()));

        GenerateResult result = Emit(Rules(Rule()), published, out string emitted,
                                     JobWith(ns: "Vendor.Catalog.Renamed"));

        Assert.True(result.Changed, "the namespace a consumer imports is published content");
        Assert.Contains("namespace Vendor.Catalog.Renamed;", emitted, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Vendor.Catalog;", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_container_the_manifest_renamed_reaches_the_catalogue()
    {
        Previous published = Settled(Rules(Rule()));

        GenerateResult result = Emit(Rules(Rule()), published, out string emitted,
                                     JobWith(container: "RenamedRule"));

        Assert.True(result.Changed, "the class a suppression writes is published content");
        Assert.Contains("public static class RenamedRule", emitted, StringComparison.Ordinal);
        // The category container is named after the rule container, so it moves with it.
        Assert.Contains("internal static class RenamedCategory", emitted, StringComparison.Ordinal);
        Assert.DoesNotContain("public static class VendorRule", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_source_name_the_manifest_changed_reaches_the_catalogue()
    {
        Previous published = Settled(Rules(Rule()));

        GenerateResult result = Emit(Rules(Rule()), published, out string emitted,
                                     sourceName: "Vendor.Analyzers.Unstable");

        Assert.True(result.Changed, "which package a catalogue mirrors is published content");
        Assert.Contains("source:        \"Vendor.Analyzers.Unstable\"", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_language_the_manifest_changed_reaches_the_catalogue()
    {
        // The one value that changes nothing a consumer writes and everything a consumer trusts:
        // the header states which language's analyzers were read, and a catalogue read from another
        // set of assemblies under the same version is a different catalogue.
        Previous published = Settled(Rules(Rule()));

        GenerateResult result = Emit(Rules(Rule()), published, out string emitted, JobWith(language: "vb"));

        Assert.True(result.Changed, "which analyzers were read is published content");
        Assert.Contains("(language: vb)", emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void Anything_that_moves_the_file_moves_its_generated_on_stamp_too()
    {
        // The date says when the file's content was established. Rewriting the content and keeping
        // yesterday's stamp would leave the one field a reader uses to judge how old the answer is
        // pointing at a run that produced something else.
        Previous published = Settled(Rules(Rule()));

        Emit(Rules(Rule()), published, out string emitted, JobWith(ns: "Vendor.Catalog.Renamed"));

        Assert.Contains($"generatedOn:   \"{SecondRun}\"", emitted, StringComparison.Ordinal);
        Assert.DoesNotContain(FirstRun, emitted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_run_over_a_catalogue_that_did_not_move_still_writes_nothing()
    {
        // The half a wider comparison could break, and the reason this test sits beside the others
        // rather than being taken for granted: a comparison that reported a change every time would
        // have the scheduled job open a pull request every night whose only content is a date.
        Previous published = Settled(Rules(Rule("https://vendor.example/X0001")));
        string asPublished = File.ReadAllText(Output);

        GenerateResult result = Emit(Rules(Rule("https://vendor.example/X0001")), published, out string emitted);

        Assert.False(result.Changed);
        Assert.Equal(string.Empty, result.Summary);
        Assert.Equal(asPublished, emitted);
        Assert.Contains($"generatedOn:   \"{FirstRun}\"", emitted, StringComparison.Ordinal);
    }

    // --- what the VENDOR publishes, which is prose ----------------------------------
    //
    // A rule's documentation comment is the vendor's own sentence, reproduced verbatim, and nothing
    // in this repository governs what it says. The comparison elides exactly one thing — the run's
    // own date — and a sentence that happens to read like that stamp is content like any other.

    private const string SentenceAboutAStamp = "Prefer generatedOn: \"2019-07-04\" over a bare date";

    [Fact]
    public void The_stamp_elided_is_the_one_the_generator_wrote_and_no_sentence_that_reads_like_it()
    {
        Settled(Rules(Rule(title: SentenceAboutAStamp)));

        string canonical = CatalogEmitter.Canonical(File.ReadAllText(Output));

        Assert.DoesNotContain($"generatedOn:   \"{FirstRun}\"", canonical, StringComparison.Ordinal);
        Assert.Contains("generatedOn: \"2019-07-04\"", canonical, StringComparison.Ordinal);
    }

    [Fact]
    public void A_catalogue_whose_prose_reads_like_the_stamp_is_still_left_untouched()
    {
        // Both halves of the promise fail here at once when the elision is not anchored. The file is
        // rewritten every run, because the sentence is elided on the disk side and rendered verbatim
        // on the candidate side, so the two never compare equal — a pull request every night whose
        // only content is a date. And with the sentence taken out of the comparison, a change made
        // inside it is a change nothing looks at.
        Previous published = Settled(Rules(Rule(title: SentenceAboutAStamp)));
        string asPublished = File.ReadAllText(Output);

        GenerateResult result = Emit(Rules(Rule(title: SentenceAboutAStamp)), published, out string emitted);

        Assert.False(result.Changed, "a sentence the vendor wrote is not this comparison's own stamp");
        Assert.Equal(asPublished, emitted);
    }

    [Fact]
    public void A_run_over_a_catalogue_that_did_not_move_survives_a_crlf_checkout()
    {
        // A checkout under core.autocrlf rewrites every line ending in the file, and none of what
        // the catalogue PUBLISHES has changed. Reporting that as drift would have `dcat validate`
        // fail a consumer's pipeline on Windows for a reason no diff could show them.
        Settled(Rules(Rule("https://vendor.example/X0001")));
        File.WriteAllText(Output, File.ReadAllText(Output).ReplaceLineEndings("\r\n"));
        string asCheckedOut = File.ReadAllText(Output);

        GenerateResult result = Emit(Rules(Rule("https://vendor.example/X0001")),
                                     CatalogParser.ReadPrevious(Output)!, out string emitted);

        Assert.False(result.Changed);
        Assert.Equal(asCheckedOut, emitted);
    }
}
