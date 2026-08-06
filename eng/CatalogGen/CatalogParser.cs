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

        // Normalised before anything below reads it, and this is a correctness requirement rather
        // than tidiness. Every pattern here is anchored to the end of a line or spells \n outright,
        // and .NET puts the Multiline `$` immediately BEFORE the \n — so on a CRLF file the
        // character preceding the anchor is \r and none of them match. What comes back is not an
        // empty result but a plausible one: the sourceVersion pattern is not anchored, so it still
        // reads, and the caller receives a Previous naming the right release with zero rules and
        // zero categories. The run then reports every rule as added, deletes every constant an
        // earlier run had carried forward as [Obsolete], and frees every published category name.
        //
        // The emitter writes LF (RenderSource ends on ReplaceLineEndings), so a file this tool has
        // just written is never the problem. The round trip through git is: core.autocrlf converts
        // on checkout, which is the Git for Windows default and the windows-latest default. This
        // repository escapes it through .gitattributes; `dcat` ships, and a consumer's repository
        // has no such rule.
        string text = File.ReadAllText(path).ReplaceLineEndings("\n");

        string sourceVersion = Regex.Match(text, @"sourceVersion:\s*""([^""]*)""",
                                           RegexOptions.None, RegexLimits.MatchTimeout).Groups[1].Value;

        // 4-space indent is the category class; rule members sit at 8.
        Dictionary<string, string> categoryLiterals = Regex.Matches(text, @"^    public const string (\w+) = ""((?:[^""\\]|\\.)*)"";$",
                                             RegexOptions.Multiline, RegexLimits.MatchTimeout)
            .ToDictionary(m => m.Groups[1].Value, m => Naming.Unescape(m.Groups[2].Value), StringComparer.Ordinal);

        SortedDictionary<string, RuleInfo> rules = new(StringComparer.Ordinal);
        // The documentation comment is captured rather than skipped, because it is where the rule's
        // title is written and the title has to survive a round trip: the emitter reproduces this
        // file from what this method returns, and a title it could not read back would be reported
        // as changed on every single run.
        MatchCollection blocks = Regex.Matches(
            text,
            @"^(?<doc>(?:    ///[^\n]*\n)*)(?<obsolete>    \[Obsolete\([^\n]*\)\]\n)?    \[DiagnosticRule\]\n    public static class (?<id>\w+)\n    \{\n(?<body>(?:.*\n)*?)    \}$",
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
                                     Retired: block["obsolete"].Success,
                                     Title: TitleFrom(block["doc"].Value, id, category));
        }

        // Inverted, because the emitter asks the question the other way round: given a category's
        // literal, what identifier was it published under? Recovering that is what lets an existing
        // constant keep its name when a new category would otherwise claim it.
        SortedDictionary<string, string> categoryNames = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> declared in categoryLiterals) categoryNames[declared.Value] = declared.Key;

        Console.WriteLine($"previous: {packageOrEmpty(sourceVersion)}{rules.Count} rules " +
                          $"({rules.Count(r => r.Value.Retired)} already retired), " +
                          $"{categoryNames.Count} categories");

        // The file itself travels alongside the parse, because what this run has to decide is not
        // "did the rules move" but "would I write this same file again". The fields above answer the
        // first; only the text answers the second.
        return new Previous(sourceVersion, rules, categoryNames, CatalogEmitter.Canonical(text));

        static string packageOrEmpty(string v) => string.IsNullOrEmpty(v) ? "" : $"{v}, ";
    }

    // The title as the previous run wrote it, or empty when that run wrote none. Two shapes yield
    // empty, and both are real: a catalogue generated before titles were emitted at all, whose
    // summary spans several lines and matches nothing here; and a rule the emitter fell back on,
    // whose summary is the identifier-and-category sentence rather than a title.
    private static string TitleFrom(string doc, string id, string category)
    {
        Match summary = Regex.Match(doc, @"^    /// <summary>(?<text>.*)</summary>$",
                                    RegexOptions.Multiline, RegexLimits.MatchTimeout);
        if (!summary.Success) return string.Empty;

        string text = summary.Groups["text"].Value;
        return string.Equals(text, CatalogEmitter.SummaryWithoutTitle(id, category), StringComparison.Ordinal)
            ? string.Empty
            : Naming.UnescapeXml(text);
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
