using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// The line ending a fix must write, read from the document it is writing into.
/// </summary>
/// <remarks>
/// <para>
/// Never <c>SyntaxFactory.CarriageReturnLineFeed</c>, never <c>SyntaxFactory.ElasticEndOfLine</c>, and
/// never the environment's. A fix that wrote CRLF into an LF file, or the reverse, shows up in somebody's
/// diff as a change to a line it never touched — and the two wrong answers do not even agree with each
/// other:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     a <b>hard</b> CRLF reaches the document verbatim, so an LF file gains a line nobody spelled that
///     way;
///     </description>
///   </item>
///   <item>
///     <description>
///     an <b>elastic</b> one is normalised by the formatter to the workspace's newline option, which
///     defaults to <c>Environment.NewLine</c> — so the same fix on the same file writes LF on Linux and
///     CRLF on Windows, and reads neither from the file.
///     </description>
///   </item>
/// </list>
/// <para>
/// Shared rather than written twice, which is not tidiness: the member fix already read the document and
/// the import fix did not, and two fixes of one assembly answering differently about the same question is
/// how the second one was wrong without anybody noticing.
/// </para>
/// </remarks>
internal static class LineEndings
{
    /// <summary>The ending in use where something is going.</summary>
    /// <remarks>
    /// From the line the insertion point sits on, and only then from anywhere in the file. A file with
    /// mixed endings is not a hypothetical — a generated header pasted onto hand-written source is enough
    /// — and there the first ending in the document is nobody's line ending in particular.
    /// </remarks>
    internal static SyntaxTrivia Of(SyntaxToken preceding, SyntaxNode root)
    {
        SyntaxTrivia[] local = preceding.TrailingTrivia
            .Where(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            .Take(1)
            .ToArray();

        if (local.Length > 0) { return local[0]; }

        SyntaxTrivia[] anywhere = root.DescendantTrivia()
            .Where(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            .Take(1)
            .ToArray();

        // No line ending anywhere means a single-line file, which has no layout to preserve.
        return anywhere.Length > 0 ? anywhere[0] : SyntaxFactory.LineFeed;
    }
}
