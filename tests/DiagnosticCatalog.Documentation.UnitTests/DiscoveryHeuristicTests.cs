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
/// produce analyzers — are recounted from the tree and held to what the pages state, wherever a page
/// states them.
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
/// <b>The pages are found, not listed.</b> An earlier version of this test named three of them, and
/// four others repeated the same claim without being read: the recount landed on the three that were
/// enumerated and left the guide contradicting itself, which is the exact failure the test was
/// written to prevent, one level up. A hand-kept list of pages is itself a figure nothing recounts.
/// So the claim is swept for across every document, in both languages and in both shapes the
/// documentation writes it — a table row carrying digits, and a sentence spelling its numbers as
/// words — and the page that repeats it next is checked without anybody remembering to add it.
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
    /// <summary>
    /// The decision record whose figures are dated evidence rather than a claim about the tree as it
    /// is now. Matched as a path prefix so that both halves of the pair are excluded together.
    /// </summary>
    private const string DatedRecord = "doc/adr/0023-";

    /// <summary>
    /// The fewest pages expected to state the claim. Set below what the tree carries so that merging
    /// two pages does not fail it, and far enough above zero that patterns which stopped matching
    /// cannot pass for a documentation set that argues the case nowhere.
    /// </summary>
    private const int FewestPagesWorthChecking = 4;

    private static readonly Lazy<IReadOnlyList<ProjectFile>> AllProjects = new(LoadProjects);

    /// <summary>
    /// The two heuristics the pages rank, each with the token its sentences and table rows are
    /// anchored on. The token is a package name or a type name, which is what translation leaves
    /// alone — so one reader serves the English page and the French one.
    /// </summary>
    private static readonly Heuristic[] Heuristics =
    [
        new("Microsoft.CodeAnalysis", project => project.ReferencesRoslyn),
        new("DiagnosticAnalyzer", project => project.DeclaresAnalyzer),
    ];

    /// <summary>
    /// The projects that really produce catalogued analyzers, measured by the declaration the pages
    /// argue for. Using <c>ProducesDiagnosticRules</c> as the truth is the point rather than a
    /// shortcut: the third column of each table asks "and how many of those were right", and the
    /// answer is by construction whatever the property says.
    /// </summary>
    private static List<ProjectFile> Producing =>
        AllProjects.Value.Where(project => project.ProducesDiagnosticRules).ToList();

    public static TheoryData<string> PagesStatingAFigure()
    {
        TheoryData<string> paths = [];
        foreach (MarkdownDocument document in Repository.Documents)
        {
            if (ClaimsIn(document).Count > 0)
            {
                paths.Add(document.Path);
            }
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(PagesStatingAFigure))]
    public void Every_stated_figure_is_the_measured_one(string path)
    {
        MarkdownDocument document = Repository.Require(path);

        foreach (Claim claim in ClaimsIn(document))
        {
            List<ProjectFile> measured = AllProjects.Value.Where(claim.About.Selects).ToList();

            Assert.True(
                claim.Matched == measured.Count,
                $"{path} states {claim.Matched} for the projects `{claim.About.Token}` selects, and " +
                $"the tree holds {measured.Count}: {Names(measured)}.\nWritten as \"{claim.Quoted}\". " +
                "The figure is evidence for ADR-0023's decision, so it is the page that follows the " +
                "repository — recount, do not relax the assertion.");

            if (claim.Correct is not int correct) continue;

            Assert.True(
                correct == Producing.Count,
                $"{path} says {correct} of the projects `{claim.About.Token}` selects really produce " +
                $"analyzers, and {Producing.Count} declare ProducesDiagnosticRules: {Names(Producing)}.\n" +
                $"Written as \"{claim.Quoted}\".");
        }
    }

    /// <summary>
    /// Guards the sweep against an empty world. A project layout this reader stopped understanding
    /// would leave every count at zero; patterns that stopped matching the way a page words the claim
    /// would leave every page unswept. Both read exactly like success.
    /// </summary>
    [Fact]
    public void The_measurement_finds_a_tree_and_pages_to_measure()
    {
        Assert.True(
            AllProjects.Value.Count >= 20,
            $"Only {AllProjects.Value.Count} project files were found under {Repository.Root}. The " +
            "heuristics would then be counted against a tree that was never read.");

        Assert.True(
            Producing.Count == 1,
            $"{Producing.Count} project(s) declare ProducesDiagnosticRules ({Names(Producing)}). The " +
            "pages state a single 'actually an analyzer' figure, so a second producing project is not " +
            "a failing count — it is a change the pages have to describe rather than tally. Rewrite " +
            "them, then teach this test the new shape.");

        List<ProjectFile> referencing = AllProjects.Value.Where(Heuristics[0].Selects).ToList();
        List<ProjectFile> declaring = AllProjects.Value.Where(Heuristics[1].Selects).ToList();

        Assert.True(
            referencing.Count > declaring.Count,
            "The reference heuristic no longer over-selects more than the type heuristic. That is not " +
            "a broken test: it is the argument of ADR-0023 changing shape, and the pages that rank " +
            "the two need rereading before this test is adjusted.");

        foreach (ProjectFile producing in Producing)
        {
            Assert.True(
                producing.ReferencesRoslyn && producing.DeclaresAnalyzer,
                $"{producing.Path} declares ProducesDiagnosticRules but is selected by neither " +
                "heuristic, so the 'actually an analyzer' figure would count a project that is in " +
                "no matched set.");
        }

        int pages = Repository.Documents.Count(document => ClaimsIn(document).Count > 0);

        Assert.True(
            pages >= FewestPagesWorthChecking,
            $"Only {pages} page(s) were found stating a discovery figure, which is below the " +
            $"{FewestPagesWorthChecking} this expects. Either the pages stopped making the argument, " +
            "or they now word it in a shape the patterns here do not read — and in that second case " +
            "every figure is silently unchecked, which is what this sweep replaced a hand-kept list " +
            "of pages to avoid.");
    }

    /// <summary>
    /// Every figure a document states about either heuristic, in the shapes the documentation writes:
    /// a table row carrying digits, and a sentence spelling its numbers as words in either language.
    /// </summary>
    /// <remarks>
    /// The two never collide — a table cell holds digits and the prose patterns require letters — so a
    /// page carrying both a table and a sentence is read once for each rather than twice for one.
    /// </remarks>
    private static List<Claim> ClaimsIn(MarkdownDocument document)
    {
        List<Claim> claims = [];

        if (document.Path.StartsWith(DatedRecord, StringComparison.Ordinal)) return claims;

        foreach (Heuristic heuristic in Heuristics)
        {
            string token = Regex.Escape(heuristic.Token);

            foreach (Match row in ProseFigures.Sweep(
                         document,
                         $"^\\|[^|]*`{token}`[^|]*\\|\\s*(?<matched>\\d+)\\s*\\|\\s*(?<correct>\\d+)"))
            {
                claims.Add(new Claim(
                    heuristic,
                    int.Parse(row.Groups["matched"].Value),
                    int.Parse(row.Groups["correct"].Value),
                    Quote(row)));
            }

            foreach (string pattern in SentenceShapes(token))
            {
                foreach (Match sentence in ProseFigures.Sweep(document, pattern))
                {
                    // A page may make the argument without counting — the CLI changelog says the type
                    // heuristic "matches fixtures", which is the same claim told qualitatively. That is
                    // prose about the repository, not a figure, and there is nothing here to recount.
                    if (!ProseFigures.Knows(sentence.Groups["matched"].Value)) continue;

                    claims.Add(new Claim(
                        heuristic,
                        ProseFigures.Read(sentence.Groups["matched"].Value, document.Path, nameof(DiscoveryHeuristicTests)),
                        ReadCorrect(sentence, document.Path),
                        Quote(sentence)));
                }
            }
        }

        return claims;
    }

    /// <summary>
    /// How each language words the claim. The count of matched projects is required; the count that
    /// really are analyzers is optional, because a page may follow it with what is wrong with the
    /// others instead of how many were right.
    /// </summary>
    private static IEnumerable<string> SentenceShapes(string token)
    {
        yield return
            $"`{token}`[^|]{{0,80}}?matches\\s+(?<matched>[A-Za-z]+)(?:\\s+projects?)?" +
            "(?:\\s+of\\s+which\\s+(?<correct>[A-Za-z]+)\\s+is\\s+an\\s+analyzer)?";

        yield return
            $"`{token}`[^|]{{0,90}}?correspond\\s+à\\s+(?<matched>[A-Za-zà]+)(?:\\s+projets?)?" +
            "(?:\\s+dont\\s+(?<correct>[A-Za-zé]+)\\s+est\\s+un\\s+analyseur)?";
    }

    private static int? ReadCorrect(Match sentence, string path)
    {
        Group correct = sentence.Groups["correct"];

        return correct.Success && ProseFigures.Knows(correct.Value)
            ? ProseFigures.Read(correct.Value, path, nameof(DiscoveryHeuristicTests))
            : null;
    }

    /// <summary>The matched text on one line, so a failure message stays readable.</summary>
    private static string Quote(Match match) =>
        Regex.Replace(match.Value.Replace('\n', ' '), "\\s+", " ", RegexOptions.None, ProseFigures.MatchTimeout).Trim();

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
                    ProseFigures.MatchTimeout),
                DeclaresAnalyzer: AnyAnalyzerUnder(Path.GetDirectoryName(file)!),
                ProducesDiagnosticRules: Regex.IsMatch(
                    text,
                    "<ProducesDiagnosticRules>\\s*true\\s*</ProducesDiagnosticRules>",
                    RegexOptions.IgnoreCase,
                    ProseFigures.MatchTimeout)));
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
                    ProseFigures.MatchTimeout))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>One heuristic the pages rank: the token they name it by, and what it selects.</summary>
    private sealed record Heuristic(string Token, Func<ProjectFile, bool> Selects);

    /// <summary>
    /// One figure a page states: which heuristic it is about, how many projects it claims are
    /// matched, how many of those it claims are really analyzers when it says so, and the words it
    /// used — which is what a failure quotes back so the reader can find the sentence.
    /// </summary>
    private sealed record Claim(Heuristic About, int Matched, int? Correct, string Quoted);

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
