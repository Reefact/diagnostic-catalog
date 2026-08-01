using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using DiagnosticCatalog.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// Turns a rule's <c>Id</c> or <c>Category</c> into the public constant §8 asks for (DCAT0003, DCAT0004,
/// §12.4).
/// </summary>
/// <remarks>
/// <para>
/// The two faults §12.4 names separately — "make it public", "replace <c>static readonly string</c> with
/// <c>const string</c>" — are repaired in one action rather than two. A member can carry both at once, and
/// a fix that corrected the accessibility of a <c>private static readonly</c> would leave the diagnostic
/// reported on the very member it had just edited. There is no choice being made for the author here:
/// public and constant is the only end state §8.2 accepts, so offering the halves separately would be
/// offering a way to not finish.
/// </para>
/// <para>
/// The other four causes of DCAT0003 are refused, and each for the same reason: the value is not something
/// the code determines. A field of the wrong type, or holding a blank string, or a non-constant expression,
/// or a member that is not a field at all — in every case only the author knows what the identifier should
/// be, and ADR-0018 says a fix that guesses costs more than a diagnostic that waits.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeRuleMemberConstantCodeFixProvider))]
[Shared]
public sealed class MakeRuleMemberConstantCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// One key across both diagnostics. A rule missing the contract on <c>Id</c> and on <c>Category</c> has
    /// one thing wrong with it twice, and <i>Fix all occurrences</i> should not need running twice.
    /// </summary>
    private const string EquivalenceKey = "DiagnosticCatalog.MakeRuleMemberConstant";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.InvalidRuleId, DiagnosticIds.InvalidRuleCategory);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document
            .GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);

        SemanticModel? model = await context.Document
            .GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (root is null || model is null) { return; }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (RuleDeclaration.MemberOf(diagnostic.Id) is not string member) { continue; }

            if (!IsRepairable(root, model, diagnostic, member, context.CancellationToken)) { continue; }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Make '" + member + "' a public constant",
                    createChangedDocument: cancellation => ApplyAsync(
                        context.Document,
                        diagnostic,
                        member,
                        cancellation),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }
    }

    private static async Task<Document> ApplyAsync(
        Document document,
        Diagnostic diagnostic,
        string member,
        CancellationToken cancellation)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellation).ConfigureAwait(false);
        SemanticModel? model = await document.GetSemanticModelAsync(cancellation).ConfigureAwait(false);

        if (root is null || model is null) { return document; }

        if (!IsRepairable(root, model, diagnostic, member, cancellation)) { return document; }

        if (RuleDeclaration.Find(root, diagnostic) is not TypeDeclarationSyntax type) { return document; }

        if (!RuleDeclaration.TryFindField(type, member, out FieldDeclarationSyntax? field, out _)
            || field is null)
        {
            return document;
        }

        return document.WithSyntaxRoot(root.ReplaceNode(field, WithPublicConstant(field)));
    }

    private static bool IsRepairable(
        SyntaxNode root,
        SemanticModel model,
        Diagnostic diagnostic,
        string member,
        CancellationToken cancellation)
    {
        if (RuleDeclaration.Find(root, diagnostic) is not TypeDeclarationSyntax type) { return false; }

        if (!RuleDeclaration.TryFindField(
                type,
                member,
                out FieldDeclarationSyntax? field,
                out VariableDeclaratorSyntax? declarator)
            || field is null
            || declarator is null)
        {
            return false;
        }

        // Nothing to repair. Reachable only if the contract failed for a reason held elsewhere than in this
        // declaration, so writing the same modifiers back would offer a fix that changes no character.
        if (field.Modifiers.Any(SyntaxKind.ConstKeyword) && field.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return false;
        }

        if (declarator.Initializer is null) { return false; }

        // The declared type, not just the value's. `static readonly object Id = "x"` holds a string and
        // would become `const string`, narrowing a member somebody may be reading as object.
        if (model.GetTypeInfo(field.Declaration.Type, cancellation).Type?.SpecialType
            != SpecialType.System_String)
        {
            return false;
        }

        // A constant expression, non-blank — the two things `const` and §8.2 respectively demand. This is
        // where `= Compute()` and `= ""` are turned away, and they are turned away for good: the code says
        // nothing about what either should have been.
        Optional<object?> constant = model.GetConstantValue(declarator.Initializer.Value, cancellation);

        return constant.HasValue
            && constant.Value is string text
            && !string.IsNullOrWhiteSpace(text);
    }

    private static FieldDeclarationSyntax WithPublicConstant(FieldDeclarationSyntax field)
    {
        // `const` implies static and forbids readonly, so both go; the accessibility is replaced rather
        // than added to. Anything else the author wrote is kept — this fix has no opinion on it.
        List<SyntaxToken> respelled =
        [
            SyntaxFactory.Token(SyntaxKind.PublicKeyword),
            SyntaxFactory.Token(SyntaxKind.ConstKeyword),
        ];

        respelled.AddRange(field.Modifiers.Where(token =>
            !RuleDeclaration.IsAccessibility(token)
            && !token.IsKind(SyntaxKind.StaticKeyword)
            && !token.IsKind(SyntaxKind.ReadOnlyKeyword)
            && !token.IsKind(SyntaxKind.ConstKeyword)));

        SyntaxTriviaList leading = field.Modifiers.Count > 0
            ? field.Modifiers[0].LeadingTrivia
            : field.Declaration.GetLeadingTrivia();

        FieldDeclarationSyntax bare = field.Modifiers.Count > 0
            ? field
            : field.WithDeclaration(field.Declaration.WithLeadingTrivia(SyntaxTriviaList.Empty));

        return bare.WithModifiers(RuleDeclaration.Respell(respelled, leading));
    }
}
