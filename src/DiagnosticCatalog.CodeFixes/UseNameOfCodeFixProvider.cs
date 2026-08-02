using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

using DiagnosticCatalog.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// Rewrites an <c>Id</c> that spells its own type name as <c>nameof</c> (DCAT0012, §11.12).
/// </summary>
/// <remarks>
/// <para>
/// The one definition fix that repairs something already correct. Nothing is broken — the literal and the
/// type name agree, which is why the diagnostic exists in the first place: they agree TODAY, and only the
/// author's memory keeps them agreeing after a rename. So the fix decides nothing ADR-0018 reserves to the
/// author, because there is nothing to decide. The value it writes is the value already there.
/// </para>
/// <para>
/// It is refused in two shapes. A field declaring several constants at once is left alone, for the reason
/// §12 gives throughout: rewriting a shared declaration edits a member this diagnostic never named. And a
/// generic rule type is left alone because <c>nameof</c> would have to name the constructed type, and a
/// generic rule is already reported as DCAT0002 — the repair that matters there is not this one.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseNameOfCodeFixProvider))]
[Shared]
public sealed class UseNameOfCodeFixProvider : CodeFixProvider
{
    private const string EquivalenceKey = "DiagnosticCatalog.UseNameOf";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.IdNotWrittenAsNameOf);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document
            .GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (root is null) { return; }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!TryFindInitialiser(root, diagnostic, out _, out TypeDeclarationSyntax? type)
                || type is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Use nameof(" + type.Identifier.ValueText + ")",
                    createChangedDocument: cancellation => ApplyAsync(context.Document, diagnostic, cancellation),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }
    }

    private static async Task<Document> ApplyAsync(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellation)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);

        if (root is null) { return document; }

        if (!TryFindInitialiser(root, diagnostic, out ExpressionSyntax? value, out TypeDeclarationSyntax? type)
            || value is null
            || type is null)
        {
            return document;
        }

        // The trivia the literal carried, kept: the expression may sit inside a longer line whose spacing
        // is nobody's business but the author's.
        InvocationExpressionSyntax replacement = RuleDeclaration.NameOf(type)
            .WithTriviaFrom(value);

        return document.WithSyntaxRoot(root.ReplaceNode(value, replacement));
    }

    /// <summary>
    /// The reported expression, and the type whose name it should be spelling.
    /// </summary>
    /// <remarks>
    /// DCAT0012 reports on the INITIALISER rather than on the type's identifier, unlike every other
    /// definition diagnostic, so the walk here goes up rather than down: expression, its declarator, the
    /// declaration that holds it, and the type that holds that.
    /// </remarks>
    private static bool TryFindInitialiser(
        SyntaxNode root,
        Diagnostic diagnostic,
        out ExpressionSyntax? value,
        out TypeDeclarationSyntax? type)
    {
        value = null;
        type = null;

        if (root.FindNode(diagnostic.Location.SourceSpan) is not ExpressionSyntax reported) { return false; }

        if (reported.Parent is not EqualsValueClauseSyntax clause) { return false; }
        if (clause.Parent is not VariableDeclaratorSyntax declarator) { return false; }
        if (declarator.Parent is not VariableDeclarationSyntax declaration) { return false; }

        // One declarator, as §12 requires of every definition fix.
        if (declaration.Variables.Count != 1) { return false; }

        if (declaration.Parent is not FieldDeclarationSyntax field) { return false; }
        if (field.Parent is not TypeDeclarationSyntax declaring) { return false; }

        // A generic rule is DCAT0002's business, and nameof would have to name the constructed type.
        if (declaring.TypeParameterList is not null) { return false; }

        value = reported;
        type = declaring;

        return true;
    }
}
