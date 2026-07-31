using System.Text.RegularExpressions;

namespace CatalogGen;

// Extracted from Program.cs so it can be exercised by tests: a static local function in a
// top-level-statements program is unreachable from another assembly. Static local functions
// cannot capture, so the move is a relocation and cannot alter behaviour.

internal static class CatalogParser
{
    // Parses a previously generated file back into rules. The format is fixed and emitted by
    // this same tool, so the parse is reliable — and it avoids carrying a second artefact
    // next to the .g.cs purely to remember what the last run produced.
    internal static Previous? ReadPrevious(string path)
    {
        if (!File.Exists(path)) return null;
        string text = File.ReadAllText(path);

        string sourceVersion = Regex.Match(text, @"sourceVersion:\s*""([^""]*)""",
                                           RegexOptions.None, RegexLimits.MatchTimeout).Groups[1].Value;

        // 4-space indent is the category class; rule members sit at 8.
        Dictionary<string, string> categoryLiterals = Regex.Matches(text, @"^    public const string (\w+) = ""((?:[^""\\]|\\.)*)"";$",
                                             RegexOptions.Multiline, RegexLimits.MatchTimeout)
            .ToDictionary(m => m.Groups[1].Value, m => Naming.Unescape(m.Groups[2].Value), StringComparer.Ordinal);

        SortedDictionary<string, RuleInfo> rules = new(StringComparer.Ordinal);
        MatchCollection blocks = Regex.Matches(
            text,
            @"^(?<obsolete>    \[Obsolete\([^\n]*\)\]\n)?    \[DiagnosticRule\]\n    public static class (?<id>\w+)\n    \{\n(?<body>(?:.*\n)*?)    \}$",
            RegexOptions.Multiline,
            RegexLimits.MatchTimeout);

        foreach (GroupCollection block in blocks.Select(b => b.Groups))
        {
            string id = block["id"].Value;
            string body = block["body"].Value;
            Match catRef = Regex.Match(body, @"public const string Category = \w+\.(\w+);",
                                       RegexOptions.None, RegexLimits.MatchTimeout);
            if (!catRef.Success || !categoryLiterals.TryGetValue(catRef.Groups[1].Value, out string? category))
                continue;
            Match help = Regex.Match(body, @"public const string HelpLinkUri = ""((?:[^""\\]|\\.)*)"";",
                                     RegexOptions.None, RegexLimits.MatchTimeout);
            rules[id] = new RuleInfo(category, help.Success ? Naming.Unescape(help.Groups[1].Value) : string.Empty,
                                     Retired: block["obsolete"].Success);
        }

        // Inverted, because the emitter asks the question the other way round: given a category's
        // literal, what identifier was it published under? Recovering that is what lets an existing
        // constant keep its name when a new category would otherwise claim it.
        SortedDictionary<string, string> categoryNames = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> declared in categoryLiterals) categoryNames[declared.Value] = declared.Key;

        Console.WriteLine($"previous: {packageOrEmpty(sourceVersion)}{rules.Count} rules " +
                          $"({rules.Count(r => r.Value.Retired)} already retired), " +
                          $"{categoryNames.Count} categories");
        return new Previous(sourceVersion, rules, categoryNames);

        static string packageOrEmpty(string v) => string.IsNullOrEmpty(v) ? "" : $"{v}, ";
    }
}

    internal static class RegexLimits
    {
        // ReadPrevious parses a file this tool wrote itself, so the well-formed case is never in
        // doubt. The unattended case is: the nightly job runs the generator against whatever the
        // upstream release produced, and the block pattern nests quantifiers — the shape that
        // backtracks catastrophically when the input does not match the way it expects. Without a
        // bound, that surfaces as a job wedged until the runner's six-hour cap, with no output to
        // read. With one, it surfaces as a RegexMatchTimeoutException naming the pattern.
        //
        // Ten seconds is far above any real parse (the largest catalogue is ~6 000 lines and
        // matches in milliseconds) and far below anything a human would wait through.
        internal static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);
    }
