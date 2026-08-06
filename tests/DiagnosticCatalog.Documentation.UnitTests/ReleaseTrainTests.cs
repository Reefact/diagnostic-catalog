using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// The release trains a document states are the trains <c>tools/trains.sh</c> defines — the same
/// ids, prefixes and scopes, in both languages, and no train the repository publishes nothing for.
/// </summary>
/// <remarks>
/// <para>
/// <c>tools/trains.sh</c> is the single source: the packaging and release-notes scripts source it,
/// so what a release publishes and what its notes describe cannot drift apart. What it cannot reach
/// is prose. A train added there routes a tag and publishes a package while every page describing
/// the set still lists the old one, and the failure is silent in the direction that matters —
/// a reader consults the table to learn which tag to push.
/// </para>
/// <para>
/// That is not hypothetical: <c>publicapi</c> and <c>bannedapi</c> routed tags, packed packages and
/// appeared in the diagram of the release-trains page while its own table listed thirteen rows and
/// said fifteen.
/// </para>
/// <para>
/// The scope list is checked against the linter's rather than against the table a second time. Every
/// scope the linter accepts must route to exactly one train and every scope a train names must be
/// one the linter accepts — an equality that has not always held, and that neither file states on
/// its own. The shell suite asserts the same thing from the other side; this one is what makes the
/// DOCUMENTED set part of it.
/// </para>
/// </remarks>
public sealed class ReleaseTrainTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>The page that states the table in full, in each language.</summary>
    private static readonly Dictionary<string, string> TrainPage = new(StringComparer.Ordinal)
    {
        ["en"] = "doc/guide/release-trains.en.md",
        ["fr"] = "doc/guide/release-trains.fr.md",
    };

    public static TheoryData<string> Languages() => ["en", "fr"];

    /// <summary>One row of <c>tools/trains.sh</c>.</summary>
    private sealed record Train(string Id, string Prefix, IReadOnlyList<string> Scopes);

    [Theory]
    [MemberData(nameof(Languages))]
    public void The_documented_table_lists_every_train_and_only_those(string language)
    {
        IReadOnlyList<Train> defined = Defined();
        IReadOnlyList<string> documented = [.. Documented(language).Select(row => row.Id)];

        Assert.True(
            defined.Select(train => train.Id).SequenceEqual(documented, StringComparer.Ordinal),
            $"{TrainPage[language]} lists a different set of trains from tools/trains.sh, or lists " +
            "them in a different order.\n" +
            $"  trains.sh: {string.Join(", ", defined.Select(train => train.Id))}\n" +
            $"  the page:  {string.Join(", ", documented)}\n" +
            "The table is what a reader consults to learn which tag to push; a train missing from it " +
            "publishes a package nobody can find the tag for.");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Each_documented_row_states_the_prefix_and_the_scopes_the_train_carries(string language)
    {
        Dictionary<string, Train> defined = Defined().ToDictionary(train => train.Id, StringComparer.Ordinal);

        foreach (Train row in Documented(language))
        {
            Train train = defined[row.Id];

            Assert.True(
                string.Equals(train.Prefix, row.Prefix, StringComparison.Ordinal),
                $"{TrainPage[language]} gives the {row.Id} train the tag prefix {row.Prefix}; " +
                $"tools/trains.sh routes {train.Prefix}. A tag typed from the page would be rejected " +
                "by the release workflow.");

            Assert.True(
                train.Scopes.OrderBy(scope => scope, StringComparer.Ordinal)
                     .SequenceEqual(row.Scopes.OrderBy(scope => scope, StringComparer.Ordinal),
                                    StringComparer.Ordinal),
                $"{TrainPage[language]} gives the {row.Id} train the scopes " +
                $"{string.Join(", ", row.Scopes)}; tools/trains.sh routes " +
                $"{string.Join(", ", train.Scopes)}. A commit scoped from the page reaches another " +
                "train's release notes, or none.");
        }
    }

    [Fact]
    public void Every_train_publishes_a_project_and_every_project_rides_a_documented_train()
    {
        IReadOnlyList<Train> defined = Defined();
        Dictionary<string, List<string>> byTrain = ProjectsByTrain();

        foreach (Train train in defined)
        {
            Assert.True(
                byTrain.ContainsKey(train.Id),
                $"tools/trains.sh defines the {train.Id} train and no project under src/ declares " +
                $"<ReleaseTrain>{train.Id}</ReleaseTrain>. The train routes a tag, the release packs " +
                "nothing, and the tag is spent.");
        }

        foreach (KeyValuePair<string, List<string>> declared in byTrain)
        {
            Assert.True(
                defined.Any(train => string.Equals(train.Id, declared.Key, StringComparison.Ordinal)),
                $"{string.Join(", ", declared.Value)} declares the train '{declared.Key}', which " +
                "tools/trains.sh does not define. Such a project is never packed, silently.");
        }
    }

    [Fact]
    public void Every_scope_the_linter_accepts_routes_to_exactly_one_train()
    {
        IReadOnlyList<Train> defined = Defined();
        IReadOnlyList<string> accepted = AcceptedScopes();

        List<string> routed = [.. defined.SelectMany(train => train.Scopes)];

        List<string> unroutable = [.. accepted.Except(routed, StringComparer.Ordinal)];
        Assert.True(
            unroutable.Count == 0,
            $"commit-lint accepts {string.Join(", ", unroutable)}, which no train carries. A commit " +
            "scoped that way is silently dropped from the release notes and the changelog.");

        List<string> unaccepted = [.. routed.Except(accepted, StringComparer.Ordinal)];
        Assert.True(
            unaccepted.Count == 0,
            $"tools/trains.sh routes {string.Join(", ", unaccepted)}, which commit-lint refuses. No " +
            "commit can ever carry that scope, so the train it routes to takes none.");

        List<string> shared = [.. routed.GroupBy(scope => scope, StringComparer.Ordinal)
                                        .Where(group => group.Count() > 1)
                                        .Select(group => group.Key)];
        Assert.True(
            shared.Count == 0,
            $"{string.Join(", ", shared)} is carried by more than one train, so a commit with that " +
            "scope reaches two sets of release notes.");
    }

    [Fact]
    public void The_trains_are_discovered()
    {
        Assert.True(
            Defined().Count >= 4,
            "Fewer than four trains were read out of tools/trains.sh, so every theory here would " +
            "assert almost nothing. Check that the trains_rows() heredoc still has the shape this " +
            "reads: <id>|<tag-prefix>|<scopes csv>|<package label>.");

        foreach (string language in new[] { "en", "fr" })
        {
            Assert.True(
                Documented(language).Count >= 4,
                $"{TrainPage[language]} yields fewer than four table rows this can read. The rows " +
                "are matched as | `id` | `prefix` | `scopes` | …; a table written another way leaves " +
                "every train unchecked in this half.");
        }
    }

    /// <summary>The rows of <c>trains_rows()</c>, in the order they are written.</summary>
    private static List<Train> Defined()
    {
        string script = File.ReadAllText(Path.Combine(Repository.Root, "tools", "trains.sh"));

        int start = script.IndexOf("trains_rows() {", StringComparison.Ordinal);
        Assert.True(start >= 0, "tools/trains.sh declares no trains_rows() function.");

        int rows = script.IndexOf("<<'ROWS'", start, StringComparison.Ordinal);
        int end = script.IndexOf("\nROWS", rows, StringComparison.Ordinal);
        Assert.True(rows >= 0 && end > rows, "tools/trains.sh: the trains_rows() heredoc moved.");

        List<Train> trains = [];
        foreach (string line in script[(rows + "<<'ROWS'".Length)..end].Split('\n'))
        {
            string[] fields = line.Split('|');
            if (fields.Length < 4) continue;

            trains.Add(new Train(fields[0].Trim(), fields[1].Trim(),
                                 [.. fields[2].Split(',').Select(scope => scope.Trim())]));
        }

        return trains;
    }

    /// <summary>The rows of the table on the release-trains page of one language.</summary>
    private static List<Train> Documented(string language)
    {
        MarkdownDocument page = Repository.Require(TrainPage[language]);

        List<Train> rows = [];
        foreach (Match row in Regex.Matches(
                     page.Text,
                     "^\\|\\s*`(?<id>[a-z]+)`\\s*\\|\\s*`(?<prefix>[a-z]+-v)`\\s*\\|\\s*(?<scopes>[^|]+)\\|",
                     RegexOptions.Multiline,
                     MatchTimeout))
        {
            List<string> scopes = [];
            foreach (Match scope in Regex.Matches(row.Groups["scopes"].Value, "`(?<scope>[a-z]+)`",
                                                  RegexOptions.None, MatchTimeout))
            {
                scopes.Add(scope.Groups["scope"].Value);
            }

            rows.Add(new Train(row.Groups["id"].Value, row.Groups["prefix"].Value, scopes));
        }

        return rows;
    }

    /// <summary>Which train each project declares, by train id.</summary>
    private static Dictionary<string, List<string>> ProjectsByTrain()
    {
        Dictionary<string, List<string>> byTrain = new(StringComparer.Ordinal);

        foreach (string project in Directory.EnumerateFiles(
                     Path.Combine(Repository.Root, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            if (project.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                 StringComparison.Ordinal)
                || project.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                    StringComparison.Ordinal))
            {
                continue;
            }

            // Comments removed first: a project that spells the element in its prose — one does,
            // warning its own author never to — declares nothing, and tools/trains.sh agrees.
            string text = Regex.Replace(File.ReadAllText(project), "<!--.*?-->", string.Empty,
                                        RegexOptions.Singleline, MatchTimeout);

            Match declaration = Regex.Match(text, "<ReleaseTrain>\\s*(?<train>[^<\\s]+)\\s*</ReleaseTrain>",
                                            RegexOptions.None, MatchTimeout);
            if (!declaration.Success) continue;

            string train = declaration.Groups["train"].Value;
            if (!byTrain.TryGetValue(train, out List<string>? projects))
            {
                projects = [];
                byTrain[train] = projects;
            }

            projects.Add(Path.GetFileName(project));
        }

        return byTrain;
    }

    /// <summary>The closed scope list <c>commit-lint</c> accepts.</summary>
    private static List<string> AcceptedScopes()
    {
        string linter = File.ReadAllText(
            Path.Combine(Repository.Root, "tools", "commit-lint", "lint-commit-message.sh"));

        Match scopes = Regex.Match(linter, "^SCOPES='(?<scopes>[^']*)'", RegexOptions.Multiline, MatchTimeout);
        Assert.True(scopes.Success, "tools/commit-lint/lint-commit-message.sh declares no SCOPES list.");

        return [.. scopes.Groups["scopes"].Value.Split('|').Select(scope => scope.Trim())];
    }
}
