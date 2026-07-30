using System.Text;

namespace CatalogGen;

// Extracted from Program.cs so it can be exercised by tests: a static local function in a
// top-level-statements program is unreachable from another assembly. Static local functions
// cannot capture, so the move is a relocation and cannot alter behaviour.

internal static class Naming
{
    internal static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    internal static string Unescape(string s) => s.Replace("\\\"", "\"").Replace("\\\\", "\\");

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
