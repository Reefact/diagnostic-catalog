using System.Collections.Generic;
using System.Linq;

using DiagnosticCatalog.Analyzers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// Rewrites the two positional arguments of a suppression, and nothing else.
/// </summary>
/// <remarks>
/// "Nothing else" is the contract §21.4 tests for. <c>Justification</c>, <c>Scope</c>, <c>Target</c> and
/// <c>MessageId</c> are the author's words and settings; a fix that dropped a justification while
/// migrating a suppression would destroy the one part a reviewer actually reads.
/// </remarks>
internal static class SuppressionRewriter
{
    /// <summary>
    /// Replaces the category and identifier literals with <c>reference.Category</c> and
    /// <c>reference.Id</c>.
    /// </summary>
    internal static AttributeSyntax WithCatalogReference(AttributeSyntax attribute, string reference)
    {
        AttributeArgumentListSyntax arguments = attribute.ArgumentList!;

        List<AttributeArgumentSyntax> rewritten = new();
        int positional = 0;

        foreach (AttributeArgumentSyntax argument in arguments.Arguments)
        {
            // Justification, Scope, Target and MessageId keep their place and their content untouched.
            if (argument.NameEquals is not null)
            {
                rewritten.Add(argument);

                continue;
            }

            // The slot, not the position: the pair may be written by parameter name and reversed, and
            // rewriting by position would put the category where the identifier belongs.
            string? member = SuppressionArgumentOrder.SlotOf(argument, positional) switch
            {
                SuppressionArgumentOrder.CategorySlot => "Category",
                SuppressionArgumentOrder.CheckIdSlot => "Id",
                _ => null,
            };

            positional++;

            rewritten.Add(member is null
                ? argument
                : argument.WithExpression(Member(reference, member))
                    .WithTriviaFrom(argument));
        }

        return attribute.WithArgumentList(
            arguments.WithArguments(SyntaxFactory.SeparatedList(
                rewritten,
                arguments.Arguments.GetSeparators())));
    }

    /// <summary>Builds <c>Container.RULE.Member</c> as a qualified member access.</summary>
    private static ExpressionSyntax Member(string reference, string member)
    {
        ExpressionSyntax expression = SyntaxFactory.IdentifierName(reference.Split('.').First());

        foreach (string part in reference.Split('.').Skip(1))
        {
            expression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                expression,
                SyntaxFactory.IdentifierName(part));
        }

        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            expression,
            SyntaxFactory.IdentifierName(member));
    }
}
