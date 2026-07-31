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
/// Makes a rule type static when nothing else stands in the way (DCAT0002, §12.4).
/// </summary>
/// <remarks>
/// <para>
/// DCAT0002 is one diagnostic over three faults: the type is not a class, or it is generic, or it is not
/// static. Only the last has a repair the code determines on its own. Removing type parameters, or turning
/// a struct into a class, changes what the type <i>is</i> — and the author had a reason for writing it that
/// way that this fix cannot read (ADR-0018).
/// </para>
/// <para>
/// Even for a plain non-static class, <c>static</c> is only writable when the class could hold it: no base
/// type, no interfaces, no instance member, no instance constructor. Otherwise the fix would produce a
/// declaration the compiler rejects, trading a warning for a build error. These are conditions of the
/// language, not preferences, which is why they are tested one by one.
/// </para>
/// <para>
/// A <c>partial</c> class is refused outright. Its other parts may carry the instance members that decide
/// the question and this fix cannot see them, so "no instance member" would be a claim about one file
/// dressed up as a claim about the type. The analyzer also reports once per part, so a <i>Fix all</i> would
/// visit each of them.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeRuleTypeStaticCodeFixProvider))]
[Shared]
public sealed class MakeRuleTypeStaticCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// One key for every occurrence: there is nothing to choose, so <i>Fix all occurrences</i> applies the
    /// same repair everywhere it applies at all.
    /// </summary>
    private const string EquivalenceKey = "DiagnosticCatalog.MakeRuleTypeStatic";

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
        ImmutableArray.Create(DiagnosticIds.InvalidRuleType);

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
            if (RuleDeclaration.Find(root, diagnostic) is not ClassDeclarationSyntax type) { continue; }

            if (!CanBeMadeStatic(type)) { continue; }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Make '" + type.Identifier.ValueText + "' static",
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

        if (RuleDeclaration.Find(root, diagnostic) is not ClassDeclarationSyntax type) { return document; }

        if (!CanBeMadeStatic(type)) { return document; }

        return document.WithSyntaxRoot(root.ReplaceNode(type, WithStatic(type)));
    }

    private static bool CanBeMadeStatic(ClassDeclarationSyntax type)
    {
        if (type.Modifiers.Any(SyntaxKind.PartialKeyword)) { return false; }

        // Already static, so DCAT0002 fired for one of the two faults this provider does not repair.
        if (type.Modifiers.Any(SyntaxKind.StaticKeyword)) { return false; }

        if (type.TypeParameterList is not null) { return false; }

        // A static class derives from nothing but object and implements no interface. The base list also
        // covers a primary constructor's argument list, which is instance state by another spelling.
        if (type.BaseList is not null) { return false; }
        if (type.ParameterList is not null) { return false; }

        return type.Members.All(HoldsNoInstanceState);
    }

    /// <summary>Whether a member is one a static class is allowed to declare.</summary>
    /// <remarks>
    /// Written as a closed list that refuses what it does not recognise. The reverse — allowing anything
    /// not explicitly rejected — would offer the fix for a member shape added to a later C# and produce a
    /// build error the moment somebody used it.
    /// </remarks>
    private static bool HoldsNoInstanceState(MemberDeclarationSyntax member)
    {
        switch (member)
        {
            // A const field is static by definition, whether or not it says so.
            case FieldDeclarationSyntax field:
                return field.Modifiers.Any(SyntaxKind.StaticKeyword)
                    || field.Modifiers.Any(SyntaxKind.ConstKeyword);

            case ConstructorDeclarationSyntax constructor:
                return constructor.Modifiers.Any(SyntaxKind.StaticKeyword);

            case MethodDeclarationSyntax method:
                return method.Modifiers.Any(SyntaxKind.StaticKeyword);

            case PropertyDeclarationSyntax property:
                return property.Modifiers.Any(SyntaxKind.StaticKeyword);

            case EventDeclarationSyntax @event:
                return @event.Modifiers.Any(SyntaxKind.StaticKeyword);

            case EventFieldDeclarationSyntax @event:
                return @event.Modifiers.Any(SyntaxKind.StaticKeyword);

            // A nested type is not instance state, and needs no modifier of its own.
            case BaseTypeDeclarationSyntax:
            case DelegateDeclarationSyntax:
                return true;

            // Indexers, destructors, operators and conversions cannot appear in a static class at all.
            default:
                return false;
        }
    }

    private static ClassDeclarationSyntax WithStatic(ClassDeclarationSyntax type)
    {
        // `sealed` and `abstract` are not merely redundant on a static class: the compiler rejects both
        // beside it (CS0441, CS0418). `public sealed class` is the ordinary shape this fix meets.
        List<SyntaxToken> kept = type.Modifiers
            .Where(token => !token.IsKind(SyntaxKind.SealedKeyword) && !token.IsKind(SyntaxKind.AbstractKeyword))
            .ToList();

        // After the accessibility, which is where §8 and every example in the specification write it.
        int insertion = 0;

        for (int index = 0; index < kept.Count; index++)
        {
            if (RuleDeclaration.IsAccessibility(kept[index])) { insertion = index + 1; }
        }

        kept.Insert(insertion, SyntaxFactory.Token(SyntaxKind.StaticKeyword));

        SyntaxTriviaList leading = type.Modifiers.Count > 0
            ? type.Modifiers[0].LeadingTrivia
            : type.Keyword.LeadingTrivia;

        ClassDeclarationSyntax bare = type.Modifiers.Count > 0
            ? type
            : type.WithKeyword(type.Keyword.WithLeadingTrivia(SyntaxTriviaList.Empty));

        return bare.WithModifiers(RuleDeclaration.Respell(kept, leading));
    }
}
