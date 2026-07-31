using System;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiagnosticCatalog.CodeFixes;

/// <summary>
/// Adds the <c>using</c> directive a written reference needs (§12.2).
/// </summary>
/// <remarks>
/// The analyzer sends the reference already relative to its namespace — <c>SonarRules.S1144</c>, not the
/// fully qualified form — because that is the shape §12.2 shows and the one an author would have
/// written. It only binds if the namespace is imported, so inserting the directive is part of the fix
/// rather than a courtesy.
/// </remarks>
internal static class UsingDirectives
{
    /// <summary>
    /// Whether <paramref name="namespace"/> is already reachable from <paramref name="useSite"/>.
    /// </summary>
    /// <remarks>
    /// Asked of the use site rather than of the file, and the difference is a real one: a file may
    /// DECLARE <c>Vendor.Catalog</c> in a namespace block and still not have it in scope for code
    /// written outside that block. Scanning the whole file for the name reports "already imported",
    /// no using is added, and the rewritten reference does not compile.
    /// </remarks>
    internal static bool IsInScope(SyntaxNode useSite, string? @namespace)
    {
        // The global namespace needs no import.
        if (string.IsNullOrEmpty(@namespace)) { return true; }

        foreach (SyntaxNode ancestor in useSite.AncestorsAndSelf())
        {
            switch (ancestor)
            {
                case BaseNamespaceDeclarationSyntax declaration:
                    // Code inside a namespace reaches that namespace's own members without an import,
                    // and an outer namespace's too — namespace A.B sees A.
                    if (Encloses(FullName(declaration), @namespace!)) { return true; }

                    if (Imports(declaration.Usings, @namespace!)) { return true; }

                    break;

                case CompilationUnitSyntax unit:
                    if (Imports(unit.Usings, @namespace!)) { return true; }

                    break;
            }
        }

        return false;
    }

    /// <summary>Appends the import to the file's directives.</summary>
    /// <remarks>
    /// Appended rather than sorted into place: the file's ordering is the author's, and a fix that
    /// reordered every using would bury its own change in the diff.
    /// </remarks>
    internal static CompilationUnitSyntax Add(CompilationUnitSyntax unit, string @namespace) =>
        unit.AddUsings(SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName(@namespace))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed));

    /// <summary>The declaration's full name, since a nested block declares only its own segment.</summary>
    private static string FullName(BaseNamespaceDeclarationSyntax declaration)
    {
        string name = declaration.Name.ToString();

        foreach (SyntaxNode ancestor in declaration.Ancestors())
        {
            if (ancestor is BaseNamespaceDeclarationSyntax outer)
            {
                name = outer.Name + "." + name;
            }
        }

        return name;
    }

    /// <summary>True when code inside <paramref name="declared"/> can see <paramref name="wanted"/>.</summary>
    private static bool Encloses(string declared, string wanted) =>
        string.Equals(declared, wanted, StringComparison.Ordinal)
        || declared.StartsWith(wanted + ".", StringComparison.Ordinal);

    private static bool Imports(SyntaxList<UsingDirectiveSyntax> usings, string @namespace) =>
        usings.Any(directive =>
            directive.Alias is null
            && directive.StaticKeyword.IsKind(SyntaxKind.None)
            && directive.Name is not null
            && string.Equals(directive.Name.ToString(), @namespace, StringComparison.Ordinal));
}
