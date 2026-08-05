using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// One suppression to rewrite, and what to rewrite it to.
/// </summary>
/// <remarks>
/// Named rather than passed as five arguments because <see cref="SuppressionFix.ApplyAllAsync"/> takes a
/// list of them: the fixes differ only in how much of the pair they rewrite, and a positional pair of
/// booleans read off a list is exactly where the two would be swapped unnoticed.
/// </remarks>
internal sealed class SuppressionFixRequest
{
    internal SuppressionFixRequest(
        Diagnostic diagnostic,
        string reference,
        string? @namespace,
        bool rewriteCategory,
        bool rewriteCheckId)
    {
        Diagnostic = diagnostic;
        Reference = reference;
        Namespace = @namespace;
        RewriteCategory = rewriteCategory;
        RewriteCheckId = rewriteCheckId;
    }

    internal Diagnostic Diagnostic { get; }

    internal string Reference { get; }

    internal string? Namespace { get; }

    internal bool RewriteCategory { get; }

    internal bool RewriteCheckId { get; }
}

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
    /// <summary>Rewrites one suppression.</summary>
    /// <remarks>
    /// Expressed as a one-element batch rather than as its own document surgery. Fixing one occurrence
    /// and fixing all of them must not be two implementations: they were, and only the second one lost
    /// occurrences, which is the shape of defect a single-occurrence test cannot see.
    /// </remarks>
    internal static Task<Document> ApplyAsync(
        Document document,
        Diagnostic diagnostic,
        string reference,
        string? @namespace,
        bool rewriteCategory,
        bool rewriteCheckId,
        CancellationToken cancellation) =>
        ApplyAllAsync(
            document,
            [new SuppressionFixRequest(diagnostic, reference, @namespace, rewriteCategory, rewriteCheckId)],
            cancellation);

    /// <summary>
    /// Rewrites every suppression in <paramref name="requests"/>, in one pass over the document.
    /// </summary>
    /// <remarks>
    /// One pass, and that is the whole point of the method. Every import lands at the same offset — the
    /// end of the compilation unit's <c>using</c> list — so occurrences computed independently and merged
    /// afterwards conflict there, and a merge resolves a conflict by dropping one side's whole change.
    /// See <see cref="DocumentFixAllProvider"/> for what that cost in practice.
    /// </remarks>
    internal static async Task<Document> ApplyAllAsync(
        Document document,
        IReadOnlyList<SuppressionFixRequest> requests,
        CancellationToken cancellation)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);

        if (root is null) { return document; }

        List<AttributeSyntax> targets = [];
        Dictionary<AttributeSyntax, SuppressionFixRequest> planned = [];
        List<string> imports = [];

        // Ordered by where each occurrence SITS, not by the order it arrived in. The engine hands
        // them over as the analyzer reported them, and an analyzer running concurrently — which
        // these do, EnableConcurrentExecution is on — reports in completion order. Measured: the
        // same file fixed twice produced its imports in opposite orders across runs. Both compile
        // and both are correct, which is what makes it worth pinning rather than shrugging at: the
        // bytes differ for no reason a reader of the diff could see.
        foreach (SuppressionFixRequest request in requests.OrderBy(r => r.Diagnostic.Location.SourceSpan.Start))
        {
            if (root.FindNode(request.Diagnostic.Location.SourceSpan) is not AttributeSyntax attribute
                || attribute.ArgumentList is null)
            {
                continue;
            }

            // One rewrite per attribute. Two diagnostics can name the same suppression — DCAT0007 and
            // DCAT0009 both report on the whole attribute — and rewriting it twice would replace a node
            // that is no longer in the tree.
            if (planned.ContainsKey(attribute)) { continue; }

            // Asked while the attribute is still the node the document knows, and BEFORE anything is
            // rewritten: the answer depends on where the suppression SITS, not on what the file happens
            // to declare elsewhere.
            if (!UsingDirectives.IsInScope(attribute, request.Namespace)
                && request.Namespace is { Length: > 0 }
                && !imports.Contains(request.Namespace, StringComparer.Ordinal))
            {
                imports.Add(request.Namespace);
            }

            targets.Add(attribute);
            planned.Add(attribute, request);
        }

        if (targets.Count == 0) { return document; }

        // The SECOND argument, not the first: it is the node with its own descendants already rewritten,
        // which is what keeps a nested occurrence from being discarded by its ancestor's replacement.
        SyntaxNode updated = root.ReplaceNodes(
            targets,
            (original, current) => SuppressionRewriter.WithCatalogReference(
                current,
                planned[original].Reference,
                planned[original].RewriteCategory,
                planned[original].RewriteCheckId));

        if (imports.Count > 0 && updated is CompilationUnitSyntax unit)
        {
            updated = UsingDirectives.Add(unit, imports);
        }

        return document.WithSyntaxRoot(updated);
    }
}
