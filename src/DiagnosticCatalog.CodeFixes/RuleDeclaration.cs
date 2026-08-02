using System.Collections.Generic;
using System.Linq;

using DiagnosticCatalog.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// Reading a rule declaration back from what a definition diagnostic reports.
/// </summary>
/// <remarks>
/// <para>
/// The definition diagnostics say far less than the use-site ones do. They carry no properties — the
/// analyzer evaluates §8 over symbols and reports <c>Diagnostic.Create(descriptor, location, type.Name)</c>
/// with no dictionary — and their location is the type's identifier token whatever the fault was. One id
/// therefore covers several faults: DCAT0003 is raised alike for a member that is absent, private,
/// <c>static readonly</c>, of the wrong type, or blank.
/// </para>
/// <para>
/// So a definition fix cannot be told what to write; it has to re-derive it, and it must decide for itself
/// whether the case in front of it is one of the repairs ADR-0018 allows. That is what this class exists
/// for: every provider asks it the same questions, so three answers about what a rule declaration looks
/// like cannot drift apart.
/// </para>
/// </remarks>
internal static class RuleDeclaration
{
    /// <summary>The member §8.2 requires.</summary>
    internal const string IdMember = "Id";

    /// <summary>The member §8.3 requires.</summary>
    internal const string CategoryMember = "Category";

    /// <summary>
    /// Which of the two constants a definition diagnostic is about, or null when it is about neither.
    /// </summary>
    /// <remarks>
    /// The diagnostic id is the only thing that says so. The message carries the type's name and nothing
    /// else, and the location is the same identifier token for DCAT0003 and DCAT0004 alike.
    /// </remarks>
    internal static string? MemberOf(string diagnosticId)
    {
        if (diagnosticId == DiagnosticIds.InvalidRuleId) { return IdMember; }
        if (diagnosticId == DiagnosticIds.InvalidRuleCategory) { return CategoryMember; }

        return null;
    }

    /// <summary>The type declaration a definition diagnostic points at, or null.</summary>
    /// <remarks>
    /// The reported span is the identifier token, so the smallest node containing it is the declaration
    /// itself. A delegate is not a <see cref="BaseTypeDeclarationSyntax"/> and comes back null, which is
    /// the right answer: there is nothing to insert a constant into.
    /// </remarks>
    internal static BaseTypeDeclarationSyntax? Find(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindNode(diagnostic.Location.SourceSpan) as BaseTypeDeclarationSyntax;

    /// <summary>Whether the type already declares a member of that name, of any kind.</summary>
    /// <remarks>
    /// Any kind, not just a field. A property or a method named <c>Id</c> fails the contract exactly as a
    /// missing one does, but the repair is not the same and the name is not free — inserting a constant
    /// beside it would not compile.
    /// </remarks>
    internal static bool DeclaresMember(TypeDeclarationSyntax type, string name) =>
        type.Members.Any(member => Declares(member, name));

    /// <summary>
    /// The single-variable field of that name, when the type declares one.
    /// </summary>
    internal static bool TryFindField(
        TypeDeclarationSyntax type,
        string name,
        out FieldDeclarationSyntax? field,
        out VariableDeclaratorSyntax? declarator)
    {
        field = null;
        declarator = null;

        foreach (MemberDeclarationSyntax member in type.Members)
        {
            if (member is not FieldDeclarationSyntax candidate) { continue; }

            // One declarator, deliberately. `public const string Id = "a", Other = "b";` declares two
            // members with one modifier list, so repairing the modifiers would silently repair the other
            // one too — a change to a member the diagnostic never mentioned.
            if (candidate.Declaration.Variables.Count != 1) { continue; }

            VariableDeclaratorSyntax only = candidate.Declaration.Variables[0];

            if (only.Identifier.ValueText != name) { continue; }

            field = candidate;
            declarator = only;

            return true;
        }

        return false;
    }

    /// <summary><c>nameof(<paramref name="type"/>)</c>, as §7.3 recommends writing an identifier.</summary>
    /// <remarks>
    /// Here rather than in the provider that first needed it, so that the fix ADDING an <c>Id</c> and the
    /// fix REWRITING one cannot end up spelling the recommended form two different ways.
    /// </remarks>
    internal static InvocationExpressionSyntax NameOf(TypeDeclarationSyntax type) =>
        SyntaxFactory.InvocationExpression(
            SyntaxFactory.IdentifierName(NameOfKeyword()),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    // The identifier token itself, not its text: a rule may be spelled `@class`, and
                    // the text alone would write a keyword into the argument.
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(type.Identifier.WithoutTrivia())))));

    /// <summary>The <c>nameof</c> token, as a contextual keyword rather than as a name.</summary>
    /// <remarks>
    /// <c>SyntaxFactory.IdentifierName("nameof")</c> looks like the same thing and is not: the binder
    /// recognises the operator by the token's contextual kind, so an ordinary identifier binds as a call to
    /// a method named <c>nameof</c> and the compilation fails with CS0103. Nothing about the printed source
    /// differs, which is why this is written out rather than left to look redundant.
    /// </remarks>
    private static SyntaxToken NameOfKeyword() =>
        SyntaxFactory.Identifier(
            SyntaxTriviaList.Empty,
            SyntaxKind.NameOfKeyword,
            "nameof",
            "nameof",
            SyntaxTriviaList.Empty);

    /// <summary>Whether the token is one of the four accessibility keywords.</summary>
    internal static bool IsAccessibility(SyntaxToken token) =>
        token.IsKind(SyntaxKind.PublicKeyword)
        || token.IsKind(SyntaxKind.InternalKeyword)
        || token.IsKind(SyntaxKind.ProtectedKeyword)
        || token.IsKind(SyntaxKind.PrivateKeyword);

    /// <summary>
    /// A modifier list spelled the way source is: one space after each keyword, and the leading trivia the
    /// declaration already had.
    /// </summary>
    /// <remarks>
    /// Done by hand rather than left to the formatter. Running the formatter over the declaration would
    /// reformat members this fix never touched, and the repository's rule against reformatting untouched
    /// code is exactly about that.
    /// </remarks>
    internal static SyntaxTokenList Respell(IEnumerable<SyntaxToken> tokens, SyntaxTriviaList leading)
    {
        List<SyntaxToken> spelled = tokens
            .Select(token => token
                .WithLeadingTrivia(SyntaxTriviaList.Empty)
                .WithTrailingTrivia(SyntaxFactory.Space))
            .ToList();

        spelled[0] = spelled[0].WithLeadingTrivia(leading);

        return SyntaxFactory.TokenList(spelled);
    }

    private static bool Declares(MemberDeclarationSyntax member, string name)
    {
        switch (member)
        {
            case FieldDeclarationSyntax field:
                return field.Declaration.Variables.Any(v => v.Identifier.ValueText == name);

            case EventFieldDeclarationSyntax @event:
                return @event.Declaration.Variables.Any(v => v.Identifier.ValueText == name);

            case PropertyDeclarationSyntax property:
                return property.Identifier.ValueText == name;

            case MethodDeclarationSyntax method:
                return method.Identifier.ValueText == name;

            case EventDeclarationSyntax @event:
                return @event.Identifier.ValueText == name;

            case BaseTypeDeclarationSyntax nested:
                return nested.Identifier.ValueText == name;

            case DelegateDeclarationSyntax nested:
                return nested.Identifier.ValueText == name;

            default:
                return false;
        }
    }
}
