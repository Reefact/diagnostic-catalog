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
/// Declares the <c>Id</c> or <c>Category</c> a rule is missing (DCAT0003, DCAT0004, §12.4).
/// </summary>
/// <remarks>
/// <para>
/// The two members are not written the same way, and the difference is the whole of what §12.4 means by
/// "a placeholder".
/// </para>
/// <para>
/// <c>Id</c> is written <c>nameof(TheRule)</c>. That is not a placeholder at all — it is §8.2's recommended
/// form, it is derived from the declaration this fix is already reading, and for a catalogue whose types are
/// named after their rules it is the correct value rather than a stand-in. It also cannot drift from the
/// type it names, which is why the specification recommends it in the first place.
/// </para>
/// <para>
/// <c>Category</c> takes the placeholder literal §12.4 spells out, <see cref="CategoryPlaceholder"/>,
/// because there is nothing in the code to derive it from: the category belongs to the analyzer the rule
/// mirrors, and inventing a plausible one is the failure this library exists to prevent — nothing in a
/// consumer's build would ever report it (§3.2). <b>Note the consequence, which is real:</b> the
/// placeholder is a non-blank string, so DCAT0004 stops being reported the moment the fix is applied. The
/// fix trades a warning that names the problem for a marker only a reader will notice. That is the deal
/// §12.4 struck, and it is the reason the title of this action names the constant it declares rather than
/// claiming to complete the rule.
/// </para>
/// <para>
/// Refused on a <c>partial</c> type: DCAT0003 is reported once per part, so a <i>Fix all</i> would insert
/// the member into each of them and the type would then declare it several times.
/// </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddRuleMemberCodeFixProvider))]
[Shared]
public sealed class AddRuleMemberCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// The placeholder value §12.4 writes for a category, verbatim.
    /// </summary>
    /// <remarks>
    /// A constant rather than a literal at the point of use, so that the prose above can point at it
    /// instead of spelling it out. Spelling it out in a comment is what S1135 reads as an unfinished task,
    /// and it is not one: the word is the value this fix is specified to emit.
    /// </remarks>
    private const string CategoryPlaceholder = "TODO";

    /// <summary>
    /// One key across both diagnostics, for the same reason the repair fix uses one: a rule missing both
    /// constants is one thing wrong twice.
    /// </summary>
    private const string EquivalenceKey = "DiagnosticCatalog.AddRuleMember";

    /// <summary>One level of indentation, used only where the document offers none to copy.</summary>
    private const string Level = "    ";

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

        if (root is null) { return; }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (RuleDeclaration.MemberOf(diagnostic.Id) is not string member) { continue; }

            if (RuleDeclaration.Find(root, diagnostic) is not TypeDeclarationSyntax type) { continue; }

            if (!CanDeclare(type, member)) { continue; }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Declare 'public const string " + member + "'",
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

        if (root is null) { return document; }

        if (RuleDeclaration.Find(root, diagnostic) is not TypeDeclarationSyntax type) { return document; }

        if (!CanDeclare(type, member)) { return document; }

        int position = Position(type, member);

        FieldDeclarationSyntax addition = LaidOut(Declaration(type, member), type, position, root);

        TypeDeclarationSyntax declared = type.WithMembers(type.Members.Insert(position, addition));

        return document.WithSyntaxRoot(root.ReplaceNode(type, declared));
    }

    private static bool CanDeclare(TypeDeclarationSyntax type, string member)
    {
        if (type.Modifiers.Any(SyntaxKind.PartialKeyword)) { return false; }

        // The name has to be free. A property or a method called Id fails the contract exactly as an absent
        // one does, and declaring a constant beside it would not compile.
        if (RuleDeclaration.DeclaresMember(type, member)) { return false; }

        // `nameof(TheRule)` needs the type's arguments to bind on a generic type, and a rule has no business
        // being generic anyway — DCAT0002 is already saying so. Only the Id half depends on the name.
        return member != RuleDeclaration.IdMember || type.TypeParameterList is null;
    }

    /// <summary>
    /// Where the member goes: <c>Id</c> first, <c>Category</c> straight after it.
    /// </summary>
    /// <remarks>
    /// The order every example in the specification is written in. It costs nothing to honour and a fix that
    /// appended blindly would leave a diff no author would have written.
    /// </remarks>
    private static int Position(TypeDeclarationSyntax type, string member)
    {
        if (member == RuleDeclaration.IdMember) { return 0; }

        for (int index = 0; index < type.Members.Count; index++)
        {
            if (type.Members[index] is FieldDeclarationSyntax field
                && field.Declaration.Variables.Any(
                    variable => variable.Identifier.ValueText == RuleDeclaration.IdMember))
            {
                return index + 1;
            }
        }

        return type.Members.Count;
    }

    /// <summary>
    /// The new member, spelled to sit where it is going.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written out rather than left to <c>Formatter.Annotation</c>, and the reason was measured rather
    /// than anticipated: the formatter reformats a region around the annotated node, not the node alone,
    /// and it writes line endings with the platform's newline. Inserting into a single-line type body was
    /// therefore enough to rewrite the line ending <b>above the type declaration</b> — outside the change,
    /// invisible on Linux, and a failing test on Windows.
    /// </para>
    /// <para>
    /// So the layout is copied from the member the new one lands beside: its indentation, and whether it
    /// sits on a line of its own. A type whose body holds nothing has nobody to copy, and only there is a
    /// convention assumed.
    /// </para>
    /// </remarks>
    private static FieldDeclarationSyntax LaidOut(
        FieldDeclarationSyntax addition,
        TypeDeclarationSyntax type,
        int position,
        SyntaxNode root)
    {
        if (Anchor(type, position) is not MemberDeclarationSyntax anchor)
        {
            // An empty body says nothing about how its members are laid out. The closing brace gives the
            // type's own indentation and one level is added to it — four spaces, which is what every
            // example in this repository and in the specification is written with.
            return addition
                .WithLeadingTrivia(Indent(type.CloseBraceToken.LeadingTrivia).Add(SyntaxFactory.Whitespace(Level)))
                .WithTrailingTrivia(NewLine(root));
        }

        return addition
            .WithLeadingTrivia(Indent(anchor.GetLeadingTrivia()))
            .WithTrailingTrivia(OnItsOwnLine(anchor) ? NewLine(root) : SyntaxFactory.Space);
    }

    /// <summary>The member whose layout the new one copies, or null when the body is empty.</summary>
    private static MemberDeclarationSyntax? Anchor(TypeDeclarationSyntax type, int position)
    {
        if (type.Members.Count == 0) { return null; }

        // Appending has no member at the position, so the last one is what the new member follows.
        return position < type.Members.Count
            ? type.Members[position]
            : type.Members[type.Members.Count - 1];
    }

    /// <summary>
    /// The indentation a member's leading trivia ends with, and nothing else from it.
    /// </summary>
    /// <remarks>
    /// Only the final run. Everything before it — a doc comment, an attribute, a directive — belongs to
    /// the member being copied from, and carrying it over would declare it twice.
    /// </remarks>
    private static SyntaxTriviaList Indent(SyntaxTriviaList leading)
    {
        if (leading.Count == 0) { return SyntaxTriviaList.Empty; }

        SyntaxTrivia last = leading[leading.Count - 1];

        return last.IsKind(SyntaxKind.WhitespaceTrivia)
            ? SyntaxFactory.TriviaList(last)
            : SyntaxTriviaList.Empty;
    }

    private static bool OnItsOwnLine(MemberDeclarationSyntax member) =>
        member.GetFirstToken().GetPreviousToken().TrailingTrivia
            .Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia))
        || member.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia));

    /// <summary>The line ending the file already uses.</summary>
    /// <remarks>
    /// Read from the document rather than taken from <c>SyntaxFactory.CarriageReturnLineFeed</c> or from
    /// the environment: a fix that wrote CRLF into an LF file, or the reverse, would show up in somebody's
    /// diff as a change to a line it never touched.
    /// </remarks>
    private static SyntaxTrivia NewLine(SyntaxNode root)
    {
        SyntaxTrivia[] endings = root.DescendantTrivia()
            .Where(trivia => trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            .Take(1)
            .ToArray();

        // No line ending anywhere means a single-line file, which has no layout to preserve.
        return endings.Length > 0 ? endings[0] : SyntaxFactory.LineFeed;
    }

    /// <summary>The <c>nameof</c> token, as a contextual keyword rather than as a name.</summary>
    /// <remarks>
    /// <c>SyntaxFactory.IdentifierName("nameof")</c> looks like the same thing and is not: the binder
    /// recognises the operator by the token's contextual kind, so an ordinary identifier binds as a call to
    /// a method named <c>nameof</c> and the compilation fails with CS0103. Nothing about the printed source
    /// differs, which is why this is written out rather than left to look redundant.
    /// </remarks>
    private static SyntaxToken NameOf() =>
        SyntaxFactory.Identifier(
            SyntaxTriviaList.Empty,
            SyntaxKind.NameOfKeyword,
            "nameof",
            "nameof",
            SyntaxTriviaList.Empty);

    private static FieldDeclarationSyntax Declaration(TypeDeclarationSyntax type, string member)
    {
        ExpressionSyntax value = member == RuleDeclaration.IdMember
            ? SyntaxFactory.InvocationExpression(
                SyntaxFactory.IdentifierName(NameOf()),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        // The identifier token itself, not its text: a rule may be spelled `@class`, and
                        // the text alone would write a keyword into the argument.
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName(type.Identifier.WithoutTrivia())))))
            : SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(CategoryPlaceholder));

        return SyntaxFactory
            .FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory
                            .VariableDeclarator(SyntaxFactory.Identifier(member))
                            .WithInitializer(SyntaxFactory.EqualsValueClause(value)))))
            .WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.ConstKeyword)));
    }
}
