using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every table that states a <c>DCAT</c> diagnostic's default severity states the severity that
/// diagnostic actually ships with.
/// </summary>
/// <remarks>
/// <para>
/// Three documents carry that column, in two languages each, and until this test none of them was
/// connected to anything. ADR-0027 changed three severities; the build stayed green, the whole test
/// suite stayed green, and six tables went on describing the previous defaults. They were found by
/// reading. A severity is the difference between a build that fails and a line in a log nobody
/// reads, so a table that gets it wrong does not mislead about a detail — it misleads about whether
/// referencing the package will stop the build.
/// </para>
/// <para>
/// The expected value is read from <c>AnalyzerReleases.Shipped.md</c> and
/// <c>AnalyzerReleases.Unshipped.md</c>, for the same reason
/// <see cref="DiagnosticCoverageTests"/> reads the id set there: <c>RS2001</c> fails the build when
/// a descriptor's severity stops matching its entry — verified by changing one in source and
/// watching it fire — so those files are a claim the compiler is already keeping true. Comparing
/// prose against them is comparing prose against something checked, rather than against another
/// claim.
/// </para>
/// <para>
/// The severity column is found by its HEADER rather than by position. A table that gains a column
/// should not silently start comparing the wrong one, and naming the header is also how this test
/// says which column it means without a comment saying so.
/// </para>
/// </remarks>
public sealed class DiagnosticSeverityDocumentationTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The documents that state a default severity. Each is named, rather than "every table with a
    /// DCAT id in it": an obligation any file can discharge is one no file has, and a page added
    /// later carrying the same column should be added here deliberately.
    /// </summary>
    private static readonly string[] Tables =
    [
        "doc/guide/diagnostics.{0}.md",
        "doc/guide/configuration.{0}.md",
        "doc/specification.{0}.md",
    ];

    /// <summary>
    /// The header of the column holding the severity, in either language. `Default severity` is
    /// listed before `Default` so the longer name wins when a table uses it.
    /// </summary>
    private static readonly string[] SeverityHeaders =
        ["Default severity", "Sévérité par défaut", "Default", "Défaut"];

    /// <summary>
    /// French tables translate the value; the French specification happens not to. Both spellings
    /// are accepted for the same severity rather than one being imposed, because which to use is a
    /// translation decision and this test is not the place to make it.
    /// </summary>
    private static readonly Dictionary<string, string> Vocabulary = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Error"] = "Error",
        ["Erreur"] = "Error",
        ["Warning"] = "Warning",
        ["Avertissement"] = "Warning",
        ["Info"] = "Info",
        ["Suggestion"] = "Info",
    };

    public static TheoryData<string, string> TablesByLanguage()
    {
        TheoryData<string, string> data = [];
        foreach (string table in Tables)
        {
            data.Add(table, "en");
            data.Add(table, "fr");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(TablesByLanguage))]
    public void The_documented_severity_is_the_shipped_one(string pathFormat, string language)
    {
        string relative = string.Format(pathFormat, language);
        string path = Path.Combine(Repository.Root, relative);
        Assert.True(File.Exists(path), $"{relative} does not exist");

        Dictionary<string, string> shipped = ShippedSeverities();
        string[] lines = File.ReadAllLines(path);

        int column = -1;
        List<string> wrong = [];
        List<string> checkedIds = [];

        foreach (string line in lines)
        {
            if (!line.StartsWith("| ", StringComparison.Ordinal)) { continue; }

            string[] cells = Cells(line);

            int header = IndexOfSeverityHeader(cells);
            if (header >= 0)
            {
                column = header;

                continue;
            }

            string? id = IdOf(cells.Length > 0 ? cells[0] : string.Empty);

            // A row for an id the analyzers do not ship — the specification lists DCAT0008 and
            // DCAT0010, which are designed and deliberately left out of 1.0 (§24). There is nothing
            // to compare them against, and inventing an expectation would make this test the
            // authority on a severity nobody has chosen yet.
            if (id is null || !shipped.TryGetValue(id, out string? expected)) { continue; }

            Assert.True(
                column >= 0,
                $"{relative}: the row for {id} appears before any header naming a severity column. "
                + $"Expected one of: {string.Join(", ", SeverityHeaders)}.");

            Assert.True(
                column < cells.Length,
                $"{relative}: the row for {id} has {cells.Length} cells, too few to hold the severity "
                + $"column at index {column}.");

            checkedIds.Add(id);
            string documented = Normalise(cells[column]);

            if (!string.Equals(documented, expected, StringComparison.Ordinal))
            {
                wrong.Add($"{id}: the table says '{cells[column].Trim()}', the analyzer ships {expected}");
            }
        }

        Assert.True(
            checkedIds.Count > 0,
            $"{relative} matched no shipped DCAT row. Either the table moved or this test stopped "
            + "reading it — an empty comparison passes for the wrong reason.");

        Assert.True(wrong.Count == 0, $"{relative} states a severity the analyzers do not ship:\n  " + string.Join("\n  ", wrong));
    }

    [Fact]
    public void Every_shipped_diagnostic_has_its_severity_documented_somewhere()
    {
        // The check above compares the rows a table HAS. A row silently deleted would leave it with
        // nothing to disagree with, so the set is asserted separately.
        Dictionary<string, string> shipped = ShippedSeverities();
        string reference = Path.Combine(Repository.Root, "doc", "guide", "diagnostics.en.md");
        string text = File.ReadAllText(reference);

        List<string> missing = shipped.Keys.Where(id => !text.Contains(id, StringComparison.Ordinal)).ToList();

        Assert.True(missing.Count == 0, "the diagnostics guide lists no severity for: " + string.Join(", ", missing));
    }

    private static Dictionary<string, string> ShippedSeverities()
    {
        Dictionary<string, string> severities = new(StringComparer.Ordinal);

        foreach (string file in new[] { "AnalyzerReleases.Shipped.md", "AnalyzerReleases.Unshipped.md" })
        {
            string path = Path.Combine(Repository.Root, "src", "DiagnosticCatalog.Analyzers", file);
            if (!File.Exists(path)) { continue; }

            foreach (Match row in Regex.Matches(
                         File.ReadAllText(path),
                         @"^(?<id>DCAT\d{4})\s*\|[^|]*\|\s*(?<severity>\w+)\s*\|",
                         RegexOptions.Multiline,
                         MatchTimeout))
            {
                // Unshipped wins: it is the entry describing what the next release carries, which is
                // what the documentation is written against.
                severities[row.Groups["id"].Value] = row.Groups["severity"].Value;
            }
        }

        Assert.True(
            severities.Count > 0,
            "no severity was read from the analyzer release files — the format changed, and every "
            + "comparison below would pass by comparing nothing.");

        return severities;
    }

    private static string[] Cells(string row) =>
        row.Trim().Trim('|').Split('|');

    private static int IndexOfSeverityHeader(string[] cells)
    {
        foreach (string header in SeverityHeaders)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                if (string.Equals(cells[i].Trim(), header, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string? IdOf(string cell)
    {
        Match match = Regex.Match(cell, @"`(?<id>DCAT\d{4})`", RegexOptions.None, MatchTimeout);

        return match.Success ? match.Groups["id"].Value : null;
    }

    private static string Normalise(string cell)
    {
        string text = cell.Replace("*", string.Empty).Trim();

        // "None (opt-in)" and anything else carrying a qualifier: compare the first word, which is
        // the severity, and let the qualifier be prose.
        string first = text.Split(' ')[0].Trim();

        return Vocabulary.TryGetValue(first, out string? canonical) ? canonical : first;
    }
}
