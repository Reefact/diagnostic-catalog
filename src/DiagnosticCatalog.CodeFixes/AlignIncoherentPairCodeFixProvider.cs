using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

using DiagnosticCatalog.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// Aligns a suppression whose category and identifier come from two rules (DCAT0001).
/// </summary>
/// <remarks>
/// <para>
/// Two fixes, always both, with the distinct equivalence keys §12.1 names. The constraint that shapes
/// this provider is its last sentence: <b>the fix must never guess which rule was intended.</b> Only the
/// author knows whether the category or the identifier was the typo, so offering one — or ordering them
/// to suggest a preference — would make that guess on their behalf.
/// </para>
/// <para>
/// The two are not equally consequential, which is a reason to offer both rather than to rank them.
/// Roslyn matches a suppression on <c>checkId</c> alone and never consults the category, so correcting
/// the category leaves what is suppressed untouched, while correcting the identifier changes it. A
/// provider that quietly preferred the harmless one would still be choosing, and would be wrong every
/// time the identifier was the part written correctly.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AlignIncoherentPairCodeFixProvider))]
[Shared]
public sealed class AlignIncoherentPairCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.MembersFromDifferentRules);

    /// <summary>Every occurrence of a document in one pass — see <see cref="DocumentFixAllProvider"/>.</summary>
    /// <remarks>
    /// The invoked equivalence key decides which of the two corrections is applied, and it is the only
    /// thing that may: §12.1 forbids this provider from choosing, and a fix-all that read anything else
    /// would be making that choice once per occurrence.
    /// </remarks>
    private static readonly FixAllProvider FixAll =
        new DocumentFixAllProvider("Align the suppressions on one rule", FixAllAsync);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => FixAll;

    private static Task<Document> FixAllAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics,
        string? equivalenceKey,
        CancellationToken cancellation)
    {
        // Neither alignment is the default. An unrecognised key is a fix-all this provider did not
        // register, and applying either correction to it would align a whole document on a rule nobody
        // named.
        if (equivalenceKey != FixProperties.AlignOnCategory && equivalenceKey != FixProperties.AlignOnId)
        {
            return Task.FromResult(document);
        }

        bool rewriteCheckId = equivalenceKey == FixProperties.AlignOnCategory;

        List<SuppressionFixRequest> requests = [];

        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(
                    FixProperties.ReferenceKey(equivalenceKey), out string? reference)
                || string.IsNullOrEmpty(reference))
            {
                continue;
            }

            diagnostic.Properties.TryGetValue(
                FixProperties.NamespaceKey(equivalenceKey), out string? @namespace);

            requests.Add(new SuppressionFixRequest(
                diagnostic, reference!, @namespace, !rewriteCheckId, rewriteCheckId));
        }

        return SuppressionFix.ApplyAllAsync(document, requests, cancellation);
    }

    /// <inheritdoc />
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            // Keep the category's rule, correct the identifier.
            Register(
                context,
                diagnostic,
                FixProperties.AlignOnCategory,
                rewriteCategory: false,
                rewriteCheckId: true);

            // Keep the identifier's rule, correct the category.
            Register(
                context,
                diagnostic,
                FixProperties.AlignOnId,
                rewriteCategory: true,
                rewriteCheckId: false);
        }

        return Task.CompletedTask;
    }

    private static void Register(
        CodeFixContext context,
        Diagnostic diagnostic,
        string alignment,
        bool rewriteCategory,
        bool rewriteCheckId)
    {
        if (!diagnostic.Properties.TryGetValue(FixProperties.ReferenceKey(alignment), out string? reference)
            || string.IsNullOrEmpty(reference))
        {
            return;
        }

        diagnostic.Properties.TryGetValue(FixProperties.NamespaceKey(alignment), out string? @namespace);

        // The rewritten member is the one the alignment does NOT keep, which is what makes the two
        // titles readable side by side in the lightbulb: "Use X.Id" against "Use Y.Category".
        string member = rewriteCheckId ? "Id" : "Category";

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use " + reference + "." + member,
                createChangedDocument: cancellation => SuppressionFix.ApplyAsync(
                    context.Document,
                    diagnostic,
                    reference!,
                    @namespace,
                    rewriteCategory,
                    rewriteCheckId,
                    cancellation),
                equivalenceKey: alignment),
            diagnostic);
    }
}
