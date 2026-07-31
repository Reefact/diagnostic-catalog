using System.Text;

namespace CatalogGen;

// Extracted from Program.cs so it can be exercised by tests: a static local function in a
// top-level-statements program is unreachable from another assembly. Static local functions
// cannot capture, so the move is a relocation and cannot alter behaviour.

internal static class Naming
{
    internal static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    internal static string Unescape(string s) => s.Replace("\\\"", "\"").Replace("\\\\", "\\");

    // A vendor's rule title goes into a documentation comment, not into a string literal, so it
    // needs the other escaping entirely. Five SonarAnalyzer titles and five .NET analyzer ones
    // carry angle brackets — "Value types should implement \"IEquatable<T>\"" among them — and an
    // unescaped one is not a subtle defect: the compiler rejects the file with CS1570, which this
    // repository promotes to an error.
    //
    // The ampersand is replaced first on the way out and last on the way back, so the pair round
    // trips: without that ordering, a title containing "&lt;" literally would come back as "<".
    internal static string EscapeXml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    internal static string UnescapeXml(string s) =>
        s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");

    // A rule's summary is a sentence, so it ends on exactly one full stop. Vendors are not
    // consistent about this: 964 of the 967 titles across the three mirrored packages carry no
    // final stop and three do, which would otherwise ship as a catalogue whose sentences end two
    // different ways for no reason a reader could see.
    //
    // Trimming before appending is what keeps it at one. None of the 967 ends on other terminal
    // punctuation — no question mark, no exclamation, no colon — so appending a full stop cannot
    // produce "?." here; a title that did would need this revisited rather than extended blindly.
    //
    // Idempotent, which is what lets both the descriptor reader and the emitter apply it: a title
    // normalised twice is the title normalised once, so the value written and the value read back
    // agree and a rule is not reported as retitled on every run.
    internal static string Sentence(string s)
    {
        string line = OneLine(s).TrimEnd('.');

        return line.Length == 0 ? string.Empty : line + ".";
    }

    // A documentation comment is line-oriented, and a title is written onto exactly one line. No
    // title in any of the three mirrored packages currently contains a line break, so this changes
    // nothing today; it is here so that one appearing upstream produces a title with a space in it
    // rather than a generated file that no longer compiles.
    internal static string OneLine(string s)
    {
        StringBuilder sb = new(s.Length);
        bool pendingSpace = false;
        foreach (char ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace) sb.Append(' ');
            pendingSpace = false;
            sb.Append(ch);
        }

        return sb.ToString();
    }

    internal static string ParentDir(string path)
    {
        int i = path.LastIndexOf('/');
        if (i < 0) return string.Empty;
        int j = path.LastIndexOf('/', i - 1);
        return j < 0 ? path[..i] : path[(j + 1)..i];
    }

    // Mechanical, and deliberately not clever. Stripping a common prefix would read better —
    // StyleCop's categories all start with "StyleCop.CSharp." — but the common prefix changes
    // the moment upstream adds a category outside it, which would rename every existing
    // constant and break every consumer that referenced one (§23.1).
    internal static string ToIdentifier(string value)
    {
        StringBuilder sb = new();
        bool upperNext = true;
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(upperNext ? char.ToUpperInvariant(ch) : ch);
                upperNext = false;
            }
            else
            {
                upperNext = true;
            }
        }
        string result = sb.ToString();
        if (result.Length == 0) return "Unnamed";
        return char.IsDigit(result[0]) ? "_" + result : result;
    }
}
