using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The figures that carry the case for <c>ProducesDiagnosticRules</c> — how many projects each
/// rejected discovery heuristic would select in this repository, and how many of those really
/// produce analyzers — are recounted from the tree and held to what the pages state.
/// </summary>
/// <remarks>
/// <para>
/// These numbers are the whole argument of ADR-0023. A reader meeting the property for the first
/// time is asked to accept a declaration where discovery would seem to do, and what persuades them
/// is the gap: a heuristic selects many projects here and is right about one. If the gap narrows
/// while the pages keep claiming it, the argument is being made on evidence that has stopped
/// existing — and a stale figure reads exactly like a measured one.
/// </para>
/// <para>
/// They had already drifted once, silently and in opposite directions on the two pages, which is
/// what this test exists to stop. Nothing else can: the counts are prose, they change when a project
/// is added rather than when either page is edited, and no reviewer of an unrelated pull request has
/// a reason to recount them.
/// </para>
/// <para>
/// <b>The heuristics are defined here, in code, and that is deliberate.</b> A count is only as
/// honest as the rule that produced it, and "references <c>Microsoft.CodeAnalysis</c>" has more than
/// one defensible reading — a hand-count that greps project files for the name also selects
/// <c>DiagnosticCatalog.CodeStyle</c>, which mentions Roslyn in an XML comment and in its package
/// description while referencing none of it. A stricter reading than the one below, counting only
/// the Roslyn <i>API</i> packages and not the analyzer packages consumed with
/// <c>PrivateAssets="all"</c>, selects one project fewer. The looser reading is kept because it is
/// the one somebody writing a discovery heuristic would actually write, and because over-selection
/// is the very failure the pages are describing: the number has to be the one the heuristic really
/// produces, not the one that flatters the argument.
/// </para>
/// <para>
/// <c>doc/adr/0023-*</c> states the figures of its own day and is deliberately left out. An accepted
/// decision record is dated evidence — it says what was measured when the decision was taken, and
/// recounting it later would rewrite the record rather than correct it. The living pages describe
/// the repository as it is now; the ADR describes the moment it was decided, and only the first kind
/// of claim can go stale.
/// </para>
/// </remarks>
public sealed class DiscoveryHeuristicTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The pages carrying the figures as a table. Both languages share the shape, and the cell that
    /// names the heuristic is a backticked type or package name, so one reader serves both.
    /// </summary>
    private const string Tour = "doc/guide/dcat.{0}.md";

    /// <summary>
    /// The package README, which makes the same claim in a sentence because nuget.org renders it to
    /// a reader who arrived for the tool rather than for the reasoning.
    /// </summary>
    private const string PackageReadme = "src/DiagnosticCatalog.Cli/README.md";

    /// <summary>
    /// The small vocabulary the README spells its numbers in. Anything outside it fails loudly rather
    /// than being read as zero: a sentence rewritten past this map is a sentence this test can no
    /// longer check, which is worth a failure and not a silent pass.
    /// </summary>
    private static readonly Dictionary<string, int> Numbers = new(StringComparer.Ordinal)
    {
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
        ["six"] = 6,
        ["seven"] = 7,
        ["eight"] = 8,
        ["nine"] = 9,
        ["ten"] = 10,
        ["eleven"] = 11,
        ["twelve"] = 12,
    };

    private static readonly Lazy<IReadOnlyList<ProjectFile>> AllProjects = new(LoadProjects);

    public static TheoryData<string> Languages() => new("en", "fr");

    /// <summary>
    /// Projects a reference-based heuristic would select: those declaring a
    /// <c>PackageReference</c> to any <c>Microsoft.CodeAnalysis*</c> package.
    /// </summary>
    private static List<ProjectFile> ReferencingRoslyn =>
        AllProjects.Value.Where(project => project.ReferencesRoslyn).ToList();

    /// <summary>
    /// Projects a type-based heuristic would select: those declaring a type deriving from
    /// <c>DiagnosticAnalyzer</c>.
    /// </summary>
    private static List<ProjectFile> DeclaringAnalyzer =>
        AllProjects.Value.Where(project => project.DeclaresAnalyzer).ToList();

    /// <summary>
    /// The projects that really produce catalogued analyzers, measured by the declaration the pages
    /// argue for. Using <c>ProducesDiagnosticRules</c> as the truth is the point rather than a
    /// shortcut: the third column of each table asks "and how many of those were right", and the
    /// answer is by construction whatever the property says.
    /// </summary>
    private static List<ProjectFile> Producing =>
        AllProjects.Value.Where(project => project.ProducesDiagnosticRules).ToList();

    [Theory]
    [MemberData(nameof(Languages))]
    public void The_tour_states_the_measured_figures(string language)
    {
        MarkdownDocument tour = Repository.Require(string.Format(Tour, language));

        AssertRow(tour, "Microsoft.CodeAnalysis", ReferencingRoslyn);
        AssertRow(tour, "DiagnosticAnalyzer", DeclaringAnalyzer);
    }

    /// <summary>
    /// The same claim in the package README, which states it as a sentence and spells its numbers as
    /// words. It also says how many of the matched projects are fixtures, which is the complement of
    /// the figure above and drifts on its own if nothing ties the two together.
    /// </summary>
    [Fact]
    public void The_package_readme_states_the_measured_figures()
    {
        MarkdownDocument readme = Repository.Require(PackageReadme);

        Match referencing = Require(
            readme,
            "`Microsoft\\.CodeAnalysis`\\*\\s+matches\\s+(?<matched>[a-z]+)\\s+projects\\s+of\\s+" +
            "which\\s+(?<correct>[a-z]+)\\s+is\\s+an\\s+analyzer",
            "the sentence counting the projects that reference Roslyn");

        AssertCount(readme, "projects referencing Roslyn", Word(readme, referencing, "matched"), ReferencingRoslyn);
        AssertCount(readme, "of those, real analyzers", Word(readme, referencing, "correct"), Producing);

        Match declaring = Require(
            readme,
            "`DiagnosticAnalyzer`\\*\\s+matches\\s+(?<matched>[a-z]+),\\s+and\\s+(?<fixtures>[a-z]+)\\s+" +
            "of\\s+those\\s+are\\s+fixtures",
            "the sentence counting the projects that declare a DiagnosticAnalyzer");

        int matched = Word(readme, declaring, "matched");
        int fixtures = Word(readme, declaring, "fixtures");

        AssertCount(readme, "projects declaring a DiagnosticAnalyzer", matched, DeclaringAnalyzer);

        Assert.True(
            matched - fixtures == Producing.Count,
            $"{readme.Path} says {matched} projects declare a DiagnosticAnalyzer and that {fixtures} " +
            $"of those are fixtures, which leaves {matched - fixtures} real — but " +
            $"{Producing.Count} project(s) declare ProducesDiagnosticRules: " +
            $"{Names(Producing)}. The two halves of the sentence have to add up.");
    }

    /// <summary>
    /// Guards every assertion above against an empty world. A project layout this reader stopped
    /// understanding would leave each count at zero, and a page claiming zero is not a page anybody
    /// would write — but a page claiming nine against a measurement of zero fails for the wrong
    /// reason, and the message would send the reader to the prose instead of to this file.
    /// </summary>
    [Fact]
    public void The_measurement_finds_a_tree_to_measure()
    {
        Assert.True(
            AllProjects.Value.Count >= 20,
            $"Only {AllProjects.Value.Count} project files were found under {Repository.Root}. The " +
            "heuristics below would then be counted against a tree that was never read.");

        Assert.True(
            Producing.Count == 1,
            $"{Producing.Count} project(s) declare ProducesDiagnosticRules ({Names(Producing)}). Both " +
            "tables state a single 'actually an analyzer' figure per row, so a second producing " +
            "project is not a failing count — it is a change the pages have to describe rather than " +
            "tally. Rewrite the tables, then teach this test the new shape.");

        Assert.True(
            ReferencingRoslyn.Count > DeclaringAnalyzer.Count,
            "The reference heuristic no longer over-selects more than the type heuristic. That is not " +
            "a broken test: it is the argument of ADR-0023 changing shape, and the pages that rank " +
            "the two need rereading before this test is adjusted.");

        foreach (ProjectFile producing in Producing)
        {
            Assert.True(
                producing.ReferencesRoslyn && producing.DeclaresAnalyzer,
                $"{producing.Path} declares ProducesDiagnosticRules but is selected by neither " +
                "heuristic, so the 'actually an analyzer' column would count a project that is in " +
                "no matched set.");
        }
    }

    /// <summary>
    /// Reads one row of a figures table: the row whose first cell backticks
    /// <paramref name="token"/>, followed by the matched count and the correct count.
    /// </summary>
    /// <remarks>
    /// Anchored on the backticked token rather than on the surrounding words, which is what lets the
    /// English and the French table share one reader. The prose around it is translated; the name of
    /// a package and the name of a type are not.
    /// </remarks>
    private static void AssertRow(MarkdownDocument document, string token, IReadOnlyList<ProjectFile> measured)
    {
        Match row = Require(
            document,
            $"^\\|[^|]*`{Regex.Escape(token)}`[^|]*\\|\\s*(?<matched>\\d+)\\s*\\|\\s*(?<correct>\\d+)",
            $"a table row measuring `{token}`");

        AssertCount(document, $"projects matched by `{token}`", int.Parse(row.Groups["matched"].Value), measured);
        AssertCount(document, $"of those, real analyzers (`{token}` row)", int.Parse(row.Groups["correct"].Value), Producing);
    }

    private static void AssertCount(
        MarkdownDocument document,
        string what,
        int stated,
        IReadOnlyList<ProjectFile> measured)
    {
        Assert.True(
            stated == measured.Count,
            $"{document.Path} states {stated} for '{what}', and the tree holds {measured.Count}: " +
            $"{Names(measured)}. The figure is evidence for ADR-0023's decision, so it is the page " +
            "that follows the repository — recount, do not relax the assertion.");
    }

    private static Match Require(MarkdownDocument document, string pattern, string what)
    {
        Match match = Regex.Match(document.Prose, pattern, RegexOptions.Multiline, MatchTimeout);

        Assert.True(
            match.Success,
            $"{document.Path} no longer carries {what} in a shape this test can read. The figures are " +
            "checked against the repository, so a rewrite that this reader cannot follow silently " +
            "stops checking them — which is the failure it exists to prevent. Update the pattern in " +
            $"{nameof(DiscoveryHeuristicTests)} to match the new wording.");

        return match;
    }

    private static int Word(MarkdownDocument document, Match match, string group)
    {
        string word = match.Groups[group].Value;

        Assert.True(
            Numbers.ContainsKey(word),
            $"{document.Path} spells a count as '{word}', which is outside the small vocabulary this " +
            "test reads. Add it to the map, or spell the number as the rest of the sentence does.");

        return Numbers[word];
    }

    private static string Names(IReadOnlyList<ProjectFile> projects) =>
        projects.Count == 0 ? "none" : string.Join(", ", projects.Select(project => project.Path));

    /// <summary>
    /// Every project in the tree, with the two heuristics and the declaration evaluated against it.
    /// </summary>
    /// <remarks>
    /// Walked here rather than through <see cref="Repository"/>, which reads Markdown everywhere and
    /// C# under <c>src/</c> only. The heuristics are about what a discovery pass over a whole
    /// solution would select, and half of what it wrongly selects here is under <c>tests/</c> — the
    /// fixtures are the interesting false positives, so a reader restricted to shipped code would
    /// measure the argument with its best evidence removed.
    /// </remarks>
    private static List<ProjectFile> LoadProjects()
    {
        List<ProjectFile> projects = [];

        foreach (string file in Directory.EnumerateFiles(Repository.Root, "*.csproj", SearchOption.AllDirectories))
        {
            string relative = file[Repository.Root.Length..].Replace(Path.DirectorySeparatorChar, '/');
            if (relative.Contains("/bin/", StringComparison.Ordinal)) continue;
            if (relative.Contains("/obj/", StringComparison.Ordinal)) continue;

            string text = File.ReadAllText(file);

            projects.Add(new ProjectFile(
                relative,
                ReferencesRoslyn: Regex.IsMatch(
                    text,
                    "<PackageReference\\s+Include=\"Microsoft\\.CodeAnalysis",
                    RegexOptions.None,
                    MatchTimeout),
                DeclaresAnalyzer: AnyAnalyzerUnder(Path.GetDirectoryName(file)!),
                ProducesDiagnosticRules: Regex.IsMatch(
                    text,
                    "<ProducesDiagnosticRules>\\s*true\\s*</ProducesDiagnosticRules>",
                    RegexOptions.IgnoreCase,
                    MatchTimeout)));
        }

        return projects.OrderBy(project => project.Path, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Whether any source under a project's folder declares a type deriving from
    /// <c>DiagnosticAnalyzer</c>. The word boundary keeps <c>DiagnosticAnalyzerAttribute</c> out: the
    /// attribute sits on every analyzer and on nothing else, but a heuristic reading it would be a
    /// third heuristic rather than the one the pages describe.
    /// </summary>
    private static bool AnyAnalyzerUnder(string folder)
    {
        foreach (string source in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            string relative = source[Repository.Root.Length..].Replace(Path.DirectorySeparatorChar, '/');
            if (relative.Contains("/bin/", StringComparison.Ordinal)) continue;
            if (relative.Contains("/obj/", StringComparison.Ordinal)) continue;

            if (Regex.IsMatch(
                    File.ReadAllText(source),
                    ":\\s*DiagnosticAnalyzer\\b",
                    RegexOptions.None,
                    MatchTimeout))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// One project, what each heuristic makes of it, and whether it opts into the catalogue — which
    /// is what makes it really an analyzer for the purposes of the third column.
    /// </summary>
    private sealed record ProjectFile(
        string Path,
        bool ReferencesRoslyn,
        bool DeclaresAnalyzer,
        bool ProducesDiagnosticRules);
}
