using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// Applies a catalog reference to a suppression, and imports what it needs.
/// </summary>
/// <remarks>
/// Shared by the two fixes that write one. They differ only in how much of the pair they rewrite —
/// DCAT0006 both sides, DCAT0007 the one still written as a literal — so keeping the document surgery
/// in one place is what stops them drifting into two answers about trivia, imports and placement.
/// </remarks>
internal static class SuppressionFix
{
    internal static async Task<Document> ApplyAsync(
        Document document,
        Diagnostic diagnostic,
        string reference,
        string? @namespace,
        bool rewriteCategory,
        bool rewriteCheckId,
        CancellationToken cancellation)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);

        if (root is null) { return document; }

        if (root.FindNode(diagnostic.Location.SourceSpan) is not AttributeSyntax attribute
            || attribute.ArgumentList is null)
        {
            return document;
        }

        // Asked before the rewrite, while the attribute is still the node the document knows: the
        // answer depends on where the suppression SITS, not on what the file happens to declare
        // elsewhere.
        bool imported = UsingDirectives.IsInScope(attribute, @namespace);

        AttributeSyntax rewritten = SuppressionRewriter.WithCatalogReference(
            attribute,
            reference,
            rewriteCategory,
            rewriteCheckId);

        SyntaxNode updated = root.ReplaceNode(attribute, rewritten);

        if (!imported && updated is CompilationUnitSyntax unit)
        {
            updated = UsingDirectives.Add(unit, @namespace!);
        }

        return document.WithSyntaxRoot(updated);
    }
}
