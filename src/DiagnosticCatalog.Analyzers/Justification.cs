using System;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// What a suppression's <c>Justification</c> turned out to be.
/// </summary>
internal enum JustificationState
{
    /// <summary>A value that says something. The requirement is met and nothing is reported.</summary>
    Written,

    /// <summary>No <c>Justification</c> argument at all.</summary>
    Absent,

    /// <summary>Present, and empty, whitespace or <c>null</c>.</summary>
    Blank,

    /// <summary>Present, and still carrying the placeholder the IDE writes.</summary>
    Placeholder,
}

/// <summary>
/// Reads the <c>Justification</c> named argument of a suppression attribute.
/// </summary>
/// <remarks>
/// <para>
/// The question asked here is whether a justification was WRITTEN, never whether it is a good one.
/// Specification §5 rules out judging one and §24 rules out validating one intelligently, and both
/// remain true: this reads a string's length, not its meaning. What it buys is the same thing every
/// other diagnostic here buys — the attribute that silences a rule has to say something no tool can
/// infer, and today nothing asks for it.
/// </para>
/// <para>
/// One value that is not blank is treated as though it were: <c>"&lt;Pending&gt;"</c>, which Visual
/// Studio's <i>Suppress → In Suppression File</i> writes to mean "nobody has filled this in yet". It is
/// matched exactly and case-sensitively, because it is one tool's literal output rather than a shape to
/// generalise — recognising the platform's own word for "none" is still reading a marker, while ruling
/// on "n/a", "obvious" or "see above" would be reading prose.
/// </para>
/// </remarks>
internal static class Justification
{
    /// <summary>The named argument, spelled as both suppression attributes declare it.</summary>
    internal const string MemberName = "Justification";

    /// <summary>The placeholder the IDE writes when it generates a suppression.</summary>
    internal const string Placeholder = "<Pending>";

    /// <summary>What <paramref name="attribute"/> carries in its <c>Justification</c> argument.</summary>
    internal static JustificationState Read(AttributeSyntax attribute, SemanticModel model)
    {
        if (attribute.ArgumentList is null) { return JustificationState.Absent; }

        foreach (AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments)
        {
            // Named, and named this. The two positional arguments are SuppressionAttribute's business,
            // and Scope, Target and MessageId are nobody's here.
            if (argument.NameEquals is not { } name) { continue; }

            if (name.Name.Identifier.ValueText != MemberName) { continue; }

            Optional<object?> constant = model.GetConstantValue(argument.Expression);

            // No constant value at all. An attribute argument always has one, so the expression is
            // erroneous and the compiler is already reporting it — saying it is blank on top of that
            // would be reporting a second fault against the same broken line.
            if (!constant.HasValue) { return JustificationState.Written; }

            string? value = constant.Value as string;

            // null included, which is the one blank form C# lets an attribute argument spell without
            // a string at all: Justification = null compiles and says exactly as much as "".
            if (string.IsNullOrWhiteSpace(value)) { return JustificationState.Blank; }

            return string.Equals(value, Placeholder, StringComparison.Ordinal)
                ? JustificationState.Placeholder
                : JustificationState.Written;
        }

        return JustificationState.Absent;
    }
}
