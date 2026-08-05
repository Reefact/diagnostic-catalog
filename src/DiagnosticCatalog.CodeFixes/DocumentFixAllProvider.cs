using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// Applies a fix to every occurrence in a document <b>in one pass over its syntax tree</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="WellKnownFixAllProviders.BatchFixer"/> cannot be used by these fixes, and this is not a
/// preference.</b> It computes each occurrence's fix against the PRISTINE document and then merges the
/// resulting text changes, discarding any that conflict with one already merged. Two of the fixes here
/// edit a shared place in the document rather than only their own occurrence:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     a suppression fix appends the rule's namespace to the compilation unit's <c>using</c> list, which
///     is the same offset for every occurrence in the file;
///     </description>
///   </item>
///   <item>
///     <description>
///     the member fix inserts a constant at the top of a rule's body, which is the same offset for the
///     <c>Id</c> and the <c>Category</c> of a rule declaring neither.
///     </description>
///   </item>
/// </list>
/// <para>
/// Two edits at one offset carrying DIFFERENT text is a conflict, and what the merge drops is not the
/// insertion alone — it is that occurrence's whole document change, rewritten attribute included. The
/// operation then reports success having done part of the work: measured, a file suppressing one Sonar
/// rule and one StyleCop rule came back with the Sonar suppression migrated, its import added, and the
/// StyleCop one untouched. Nothing said so. Two occurrences of ONE namespace never showed it, because
/// two identical insertions do not conflict.
/// </para>
/// <para>
/// Fixing every occurrence of a document together removes the merge entirely: there is one edit, so there
/// is nothing to reconcile. The cost is that each caller must be able to state its change for a SET of
/// diagnostics rather than for one, which is what <see cref="FixDocumentAsync"/> is.
/// </para>
/// <para>
/// Only the fixes that share an offset need this. A fix that rewrites the node its own diagnostic points
/// at — <c>UseNameOf</c>, <c>MakeRuleTypeStatic</c>, <c>MakeRuleMemberConstant</c> — touches a different
/// node per occurrence and is served correctly by the batch fixer, which is why those keep it.
/// </para>
/// </remarks>
internal sealed class DocumentFixAllProvider : FixAllProvider
{
    /// <summary>
    /// Rewrites <paramref name="document"/> for all of <paramref name="diagnostics"/> at once.
    /// </summary>
    /// <param name="equivalenceKey">
    /// Which of several actions the caller invoked, or null when the provider registers only one. DCAT0001
    /// offers two corrections under distinct keys (§12.1), and a fix-all of one must not apply the other.
    /// </param>
    internal delegate Task<Document> FixDocumentAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics,
        string? equivalenceKey,
        CancellationToken cancellation);

    private readonly string _title;
    private readonly FixDocumentAsync _fix;

    internal DocumentFixAllProvider(string title, FixDocumentAsync fix)
    {
        _title = title;
        _fix = fix;
    }

    /// <inheritdoc />
    public override IEnumerable<FixAllScope> GetSupportedFixAllScopes() =>
        [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution];

    /// <inheritdoc />
    public override Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext) =>
        Task.FromResult<CodeAction?>(CodeAction.Create(
            _title,
            cancellation => FixAsync(fixAllContext, cancellation),
            // Carried through, so the operation Roslyn records is the one the author invoked. A fix-all
            // action whose key differed from the action it came from would not be grouped with it.
            fixAllContext.CodeActionEquivalenceKey));

    private async Task<Solution> FixAsync(FixAllContext context, CancellationToken cancellation)
    {
        Solution solution = context.Solution;

        foreach (Document document in await DocumentsAsync(context).ConfigureAwait(false))
        {
            cancellation.ThrowIfCancellationRequested();

            ImmutableArray<Diagnostic> diagnostics = (await context
                .GetDocumentDiagnosticsAsync(document)
                .ConfigureAwait(false)).ToImmutableArray();

            if (diagnostics.IsEmpty) { continue; }

            // Each document is fixed against the solution as it stands, and the result is folded back
            // in before the next one. Documents do not share offsets, so this cannot conflict — but it
            // does mean a caller's fix reads the document it is actually changing.
            Document current = solution.GetDocument(document.Id)!;

            Document fixedDocument = await _fix(
                current,
                diagnostics,
                context.CodeActionEquivalenceKey,
                cancellation).ConfigureAwait(false);

            SyntaxNode? root = await fixedDocument.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);

            if (root is null) { continue; }

            solution = solution.WithDocumentSyntaxRoot(document.Id, root);
        }

        return solution;
    }

    /// <summary>The documents the invoked scope covers.</summary>
    /// <remarks>
    /// A document scope is the one document, whatever else the context could reach. The wider scopes are
    /// every C# document of the project, or of every project — the engine filters them down to those
    /// carrying a diagnostic when it is asked for them, which is why nothing is pre-selected here.
    /// </remarks>
    private static Task<IReadOnlyList<Document>> DocumentsAsync(FixAllContext context)
    {
        if (context.Scope == FixAllScope.Document && context.Document is not null)
        {
            return Task.FromResult<IReadOnlyList<Document>>([context.Document]);
        }

        IEnumerable<Project> projects = context.Scope == FixAllScope.Solution
            ? context.Solution.Projects
            : [context.Project];

        return Task.FromResult<IReadOnlyList<Document>>(
            [.. projects.Where(project => project.Language == context.Project.Language)
                        .SelectMany(project => project.Documents)]);
    }
}
