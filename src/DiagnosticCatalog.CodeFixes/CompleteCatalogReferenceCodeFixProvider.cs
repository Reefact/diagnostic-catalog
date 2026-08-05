using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using DiagnosticCatalog.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// Completes a half-migrated suppression from the rule its other argument already names (DCAT0007).
/// </summary>
/// <remarks>
/// §12.3, and §11.7 calls it the only fully deterministic fix: there is nothing to choose, because the
/// migrated argument says which rule was meant. That holds only while the literal agrees with what the
/// rule declares — when it does not, the analyzer sends nothing and this offers nothing, since replacing
/// it would silence a different diagnostic than the one silenced today.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CompleteCatalogReferenceCodeFixProvider))]
[Shared]
public sealed class CompleteCatalogReferenceCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// One key for every occurrence, so <i>Fix all occurrences</i> completes a whole codebase at once.
    /// </summary>
    /// <remarks>
    /// Distinct from the DCAT0006 key: the two fixes answer different diagnostics and a fix-all of one
    /// must not sweep up the other's occurrences.
    /// </remarks>
    private const string EquivalenceKey = "DiagnosticCatalog.CompleteCatalogReference";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.MixedReferenceAndLiteral);

    /// <summary>Every occurrence of a document in one pass — see <see cref="DocumentFixAllProvider"/>.</summary>
    private static readonly FixAllProvider FixAll =
        new DocumentFixAllProvider("Complete the suppressions from their rules", FixAllAsync);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => FixAll;

    private static Task<Document> FixAllAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics,
        string? equivalenceKey,
        CancellationToken cancellation)
    {
        _ = equivalenceKey;

        List<SuppressionFixRequest> requests = [];

        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(FixProperties.Reference, out string? reference)
                || string.IsNullOrEmpty(reference))
            {
                continue;
            }

            // Absent when the literal names something else: the analyzer withholds the slot rather than
            // let a migration change what is suppressed, and a fix-all must honour that too.
            if (!diagnostic.Properties.TryGetValue(FixProperties.Slot, out string? slot)
                || !int.TryParse(slot, NumberStyles.None, CultureInfo.InvariantCulture, out int side))
            {
                continue;
            }

            bool rewriteCategory = side == SuppressionArgumentOrder.CategorySlot;
            bool rewriteCheckId = side == SuppressionArgumentOrder.CheckIdSlot;

            if (!rewriteCategory && !rewriteCheckId) { continue; }

            diagnostic.Properties.TryGetValue(FixProperties.Namespace, out string? @namespace);

            requests.Add(new SuppressionFixRequest(
                diagnostic, reference!, @namespace, rewriteCategory, rewriteCheckId));
        }

        return SuppressionFix.ApplyAllAsync(document, requests, cancellation);
    }

    /// <inheritdoc />
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(FixProperties.Reference, out string? reference)
                || string.IsNullOrEmpty(reference))
            {
                continue;
            }

            if (!diagnostic.Properties.TryGetValue(FixProperties.Slot, out string? slot)
                || !int.TryParse(slot, NumberStyles.None, CultureInfo.InvariantCulture, out int side))
            {
                continue;
            }

            diagnostic.Properties.TryGetValue(FixProperties.Namespace, out string? @namespace);

            // Exactly one side, named by the analyzer. The other is already a reference and is left
            // character for character as the author wrote it — an alias included.
            bool rewriteCategory = side == SuppressionArgumentOrder.CategorySlot;
            bool rewriteCheckId = side == SuppressionArgumentOrder.CheckIdSlot;

            if (!rewriteCategory && !rewriteCheckId) { continue; }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Complete the suppression from '" + reference + "'",
                    createChangedDocument: cancellation => SuppressionFix.ApplyAsync(
                        context.Document,
                        diagnostic,
                        reference!,
                        @namespace,
                        rewriteCategory,
                        rewriteCheckId,
                        cancellation),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }

        return Task.CompletedTask;
    }
}
