using Microsoft.CodeAnalysis;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// The diagnostics this package reports, declared once.
/// </summary>
/// <remarks>
/// A DCAT id is a published contract from its first release: consumers write it in .editorconfig and in
/// suppressions of their own, so renaming or removing one is a breaking change (CLAUDE.md). What records
/// that contract is AnalyzerReleases.Shipped.md, which RS2008 keeps in step with the ids declared here.
/// </remarks>
internal static class Descriptors
{
    /// <summary>
    /// The category these diagnostics report under — this library's own name, not one of the vendor
    /// categories a catalogue mirrors. It is what a consumer writes to configure them wholesale.
    /// </summary>
    internal const string Category = "DiagnosticCatalog";

    internal static readonly DiagnosticDescriptor MembersFromDifferentRules = new(
        id: "DCAT0001",
        title: "Category and Id must reference the same diagnostic rule",
        messageFormat: "The category comes from '{0}' and the id from '{1}': a suppression must reference one rule",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "The two arguments are compared by the rule that DECLARES them, not by their values. Two "
            + "rules sharing a category today produce a suppression that works and is still reported, "
            + "because the pairing is a copy-paste error: the day the vendor recategorises one of them, "
            + "the suppression carries the wrong category and nothing in the platform will say so.");

    internal static readonly DiagnosticDescriptor InvalidRuleType = new(
        id: "DCAT0002",
        title: "A diagnostic rule must be declared as a static non-generic class",
        messageFormat: "'{0}' is marked [DiagnosticRule] but is not a static non-generic class",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "A rule is a static class holding constants. It cannot be generic, because its members are "
            + "referenced as attribute arguments and a constructed generic type has no constant members "
            + "to offer; and it cannot be an instance type, because nothing ever instantiates it.");

    internal static readonly DiagnosticDescriptor InvalidRuleId = new(
        id: "DCAT0003",
        title: "A diagnostic rule must expose a public constant string named Id",
        messageFormat: "'{0}' does not expose a public constant string named Id",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "The identifier must be usable as an attribute argument, which requires a public const "
            + "string with a non-blank value. A static readonly field holds a value at run time but "
            + "cannot be one. The recommended form is nameof(TheRuleType), which cannot drift from the "
            + "type it names.");

    internal static readonly DiagnosticDescriptor InvalidRuleCategory = new(
        id: "DCAT0004",
        title: "A diagnostic rule must expose a public constant string named Category",
        messageFormat: "'{0}' does not expose a public constant string named Category",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "The category must be usable as an attribute argument, under the same rules as Id. Its value "
            + "should be the one the originating analyzer's DiagnosticDescriptor declares; nothing in the "
            + "platform verifies that, which is why the constant exists at all.");
}
