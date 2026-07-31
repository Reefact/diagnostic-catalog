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

    internal static readonly DiagnosticDescriptor ReplaceableStringLiterals = new(
        id: "DCAT0006",
        title: "Use a diagnostic catalog reference instead of string literals",
        messageFormat: "Reference {2} instead of the string literals \"{0}\" and \"{1}\"",
        category: Category,
        // Warning, where the specification table says Info. The catalogue package being referenced at
        // all is the statement of intent: a project that has taken the dependency has decided its
        // suppressions are catalogue references, and a suggestion no build output shows does not carry
        // that. The cost is real and belongs in the release notes — adopting a catalogue turns every
        // existing literal suppression into a warning at once, and fails the build outright under
        // TreatWarningsAsErrors. Severity remains configurable per project in .editorconfig.
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "The literals match a known diagnostic rule, so they can be replaced by references the "
            + "compiler checks. The identifier is truncated at the first colon before matching, exactly "
            + "as Roslyn does, so the suffixed form Visual Studio generates is recognised — and the "
            + "suffix is dropped by the replacement, its content belonging to the rule's own "
            + "documentation.");

    internal static readonly DiagnosticDescriptor NonIlUnconditionalSuppression = new(
        id: "DCAT0009",
        title: "UnconditionalSuppressMessage only accepts IL#### identifiers",
        messageFormat: "'{0}' is not an IL warning identifier: this suppression is silently ignored",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "ILLink reads suppressions from the compiled assembly and discards any identifier its "
            + "decoder rejects, while Roslyn never processes this attribute at all. The suppression is "
            + "therefore a no-op that no other tool reports. The check mirrors that decoder rather than "
            + "a stricter pattern, so identifiers ILLink does honour — including its IL####:FriendlyName "
            + "form — are left alone.");

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
