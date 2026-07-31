using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;

using DiagnosticCatalog.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// Replaces the string literals of a suppression with the catalog reference they match (DCAT0006).
/// </summary>
/// <remarks>
/// The migration path §3.5 describes, and the reason DCAT0006 is called the primary entry point: a
/// codebase adopts a catalog by accepting this fix, not by hand-editing every suppression it has.
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseCatalogReferenceCodeFixProvider))]
[Shared]
public sealed class UseCatalogReferenceCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// One key for every occurrence, so <i>Fix all occurrences</i> applies one consistent choice across
    /// a document, project or solution (§12).
    /// </summary>
    /// <remarks>
    /// Constant rather than derived from the rule being referenced. Fix-all groups the occurrences it
    /// applies by this key, so a key varying per rule would fix only the occurrences of whichever rule
    /// the author happened to invoke it on — leaving the rest and reading as though the operation had
    /// silently missed them. The choice this fix makes is the same everywhere: replace literals with the
    /// single rule that matches them.
    /// </remarks>
    private const string EquivalenceKey = "DiagnosticCatalog.UseCatalogReference";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.ReplaceableStringLiterals);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            // Absent when several rules matched: §11.6 gives that case a diagnostic and no automatic
            // fix, and the analyzer expresses it by sending nothing to act on.
            if (!diagnostic.Properties.TryGetValue(FixProperties.Reference, out string? reference)
                || string.IsNullOrEmpty(reference))
            {
                continue;
            }

            diagnostic.Properties.TryGetValue(FixProperties.Namespace, out string? @namespace);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Use the '" + reference + "' catalog reference",
                    createChangedDocument: cancellation => SuppressionFix.ApplyAsync(
                        context.Document,
                        diagnostic,
                        reference!,
                        @namespace,
                        rewriteCategory: true,
                        rewriteCheckId: true,
                        cancellation),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }

        return Task.CompletedTask;
    }
}
