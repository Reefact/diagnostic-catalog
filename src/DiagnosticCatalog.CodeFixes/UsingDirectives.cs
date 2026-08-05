using System;
using System.Collections.Generic;
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

    /// <summary>Appends the imports to the file's directives.</summary>
    /// <remarks>
    /// Appended rather than sorted into place: the file's ordering is the author's, and a fix that
    /// reordered every using would bury its own change in the diff.
    /// <para>
    /// Several at once, because a fix-all over one document may need one import per catalog it references
    /// and they all belong at the same place. Added in the order the occurrences appear rather than
    /// sorted, so that regenerating the same fix produces the same file.
    /// </para>
    /// </remarks>
    internal static CompilationUnitSyntax Add(CompilationUnitSyntax unit, IReadOnlyList<string> namespaces)
    {
        // The file's own ending, read from the last import the author wrote and only then from
        // anywhere in the file. See LineEndings for what the two constants got wrong instead — one
        // of them wrote CRLF into every file, the other wrote whatever the machine running the fix
        // uses, and neither looked at the document.
        SyntaxTrivia newline = LineEndings.Of(LastImport(unit), unit);

        return unit.AddUsings([.. namespaces.Select(@namespace => SyntaxFactory
            .UsingDirective(SyntaxFactory.ParseName(@namespace))
            .WithTrailingTrivia(newline))]);
    }

    /// <summary>The token the new import follows, or none when the file imports nothing yet.</summary>
    private static SyntaxToken LastImport(CompilationUnitSyntax unit) =>
        unit.Usings.Count > 0 ? unit.Usings[unit.Usings.Count - 1].GetLastToken() : default;

    /// <summary>The declaration's full name, since a nested block declares only its own segment.</summary>
    private static string FullName(BaseNamespaceDeclarationSyntax declaration)
    {
        // Innermost first, because that is the order Ancestors() walks; reversed once at the end
        // rather than prepended segment by segment.
        List<string> segments = [declaration.Name.ToString()];

        foreach (BaseNamespaceDeclarationSyntax outer in declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>())
        {
            segments.Add(outer.Name.ToString());
        }

        segments.Reverse();

        return string.Join(".", segments);
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
