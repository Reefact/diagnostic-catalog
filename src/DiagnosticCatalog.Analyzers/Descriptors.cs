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
        id: DiagnosticIds.MembersFromDifferentRules,
        title: "Category and Id must reference the same diagnostic rule",
        messageFormat: "The category comes from '{0}' and the id from '{1}': a suppression must reference one rule",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "The two arguments are compared by the rule that DECLARES them, not by their values. Two "
            + "rules sharing a category today produce a suppression that works and is still reported, "
            + "because the pairing is a copy-paste error: the day the vendor recategorises one of them, "
            + "the suppression carries the wrong category and nothing in the platform will say so.");

    internal static readonly DiagnosticDescriptor ReplaceableStringLiterals = new(
        id: DiagnosticIds.ReplaceableStringLiterals,
        title: "Use a diagnostic catalog reference instead of string literals",
        messageFormat: "Reference {2} instead of the string literals \"{0}\" and \"{1}\"",
        category: Category,
        // Warning, where the specification table says Info. The catalogue package being referenced at
        // all is the statement of intent: a project that has taken the dependency has decided its
        // suppressions are catalogue references, and a suggestion no build output shows does not carry
        // that. The cost is real and belongs in the release notes — adopting a catalogue turns every
        // existing literal suppression into a warning at once, and fails the build outright under
        // TreatWarningsAsErrors. Severity remains configurable per project in .editorconfig.
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "The literals match a known diagnostic rule, so they can be replaced by references the "
            + "compiler checks. The identifier is truncated at the first colon before matching, exactly "
            + "as Roslyn does, so the suffixed form Visual Studio generates is recognised — and the "
            + "suffix is dropped by the replacement, its content belonging to the rule's own "
            + "documentation.");

    internal static readonly DiagnosticDescriptor MixedReferenceAndLiteral = new(
        id: DiagnosticIds.MixedReferenceAndLiteral,
        title: "Suppression mixes a catalog reference with a string literal",
        messageFormat: "This suppression references '{0}' on one side and the string value \"{1}\" on the other: {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "The most common half-migrated state, and the one case where the intended rule is known "
            + "without ambiguity — the already-migrated argument names it. Completing it is offered only "
            + "when the value agrees with what that rule declares: replacing one that names something "
            + "else would change which diagnostic is suppressed, which is a decision for the author "
            + "rather than a mechanical migration. The message says VALUE rather than literal because "
            + "the argument need not be one: a constant carrying the same string reads identically to "
            + "the analyzer, and telling its author to look for a literal sends them hunting for source "
            + "that is not there.");

    internal static readonly DiagnosticDescriptor NonIlUnconditionalSuppression = new(
        id: DiagnosticIds.NonIlUnconditionalSuppression,
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
        id: DiagnosticIds.InvalidRuleType,
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
        id: DiagnosticIds.InvalidRuleId,
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

    internal static readonly DiagnosticDescriptor UnreferencedRuleCategory = new(
        id: DiagnosticIds.UnreferencedRuleCategory,
        title: "A diagnostic rule's category must reference a declared category constant",
        messageFormat: "'{0}' does not reach its category through a constant declared in a [DiagnosticCategory] class",
        category: Category,
        // Warning, with the other definition diagnostics, and for ADR-0027's reason rather than by
        // default: it addresses whoever AUTHORS a catalogue, not whoever consumes one. There is also
        // no error to report — the rule compiles, folds to the right literal, and suppresses exactly
        // what it should. What is wrong is that the value has no single declaration, which is a
        // property of the catalogue rather than a defect in this rule.
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "A catalogue repeats very few distinct categories across very many rules, and each "
            + "transcription is a place for one of them to drift. Declaring every category once, in a "
            + "class marked [DiagnosticCategory], gives the catalogue a single spelling per value — and "
            + "the indirection is free, because a const initialised from another const is still a "
            + "compile-time constant and still folds to the literal in metadata. The marker is what "
            + "lets tooling tell a category constant from any other string constant in the assembly; "
            + "without it the class is invisible and the reference buys nothing.");

    internal static readonly DiagnosticDescriptor InvalidRuleCategory = new(
        id: DiagnosticIds.InvalidRuleCategory,
        title: "A diagnostic rule must expose a public constant string named Category",
        messageFormat: "'{0}' does not expose a public constant string named Category",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "The category must be usable as an attribute argument, under the same rules as Id. Its value "
            + "should be the one the originating analyzer's DiagnosticDescriptor declares; nothing in the "
            + "platform verifies that, which is why the constant exists at all.");

    internal static readonly DiagnosticDescriptor RuleTypeNameDiffersFromId = new(
        id: DiagnosticIds.RuleTypeNameDiffersFromId,
        title: "The diagnostic rule type name should match its Id",
        messageFormat: "'{0}' cannot be named for its identifier \"{1}\", which is not a valid C# identifier",
        category: Category,
        // Info, and it stays Info however plainly the divergence reads: there is nothing for the author
        // to do. Between RULE_0001 and RULE0001 for "RULE-0001" this library has no ground to elect a
        // winner, and a diagnostic whose only repair is to swap one blessed spelling for another blessed
        // spelling is noise. What it IS for is being seen and configurable — silence would leave the
        // exception DCAT0013 carves out invisible, and an invisible exception inside a rule that fails
        // builds is the one shape nobody can reason about.
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description:
            "The identifier carries a character C# forbids in a type name, so the name is the identifier "
            + "legalised and no closer spelling exists. Reported rather than passed over in silence: the "
            + "same divergence is an error when nothing forced it (DCAT0013), and a reader who cannot see "
            + "where the boundary falls cannot tell a blessed declaration from one nobody has checked.");

    internal static readonly DiagnosticDescriptor IdNotWrittenAsNameOf = new(
        id: DiagnosticIds.IdNotWrittenAsNameOf,
        title: "A rule identifier should be written as nameof",
        messageFormat: "Write nameof({0}) rather than a literal, so the identifier cannot drift from the type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "The literal agrees with the type name today and nothing holds it there: renaming the type "
            + "leaves the identifier behind, and the declaration goes on compiling while every reference "
            + "to it names a rule it no longer identifies. The nameof form cannot come apart, which is why "
            + "§7.3 recommends it. This is the one check here that reads syntax — nameof(X) and \"X\" fold "
            + "to the same constant, so a referenced assembly carries no trace of which was written, and "
            + "there is nothing to report against.");

    internal static readonly DiagnosticDescriptor RuleTypeNameDoesNotSayId = new(
        id: DiagnosticIds.RuleTypeNameDoesNotSayId,
        title: "The diagnostic rule type name does not say its Id",
        messageFormat: "'{0}' declares the identifier \"{1}\", which its name does not say",
        category: Category,
        // Warning, alongside the other three definition diagnostics, and NOT Error. The rule is new and
        // already has one known false-positive shape behind it — the friendly-name form, which cost a
        // reading of the usage corpus to find — so it earns a release before it is allowed to stop a
        // build. Anyone wanting it stricter today writes one line of .editorconfig; that escalation is
        // exactly what reporting it at all buys, and it is why silence was never the alternative.
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "The identifier is a valid C# identifier, so the type could have been named it, and was not. "
            + "Every use site then reads a name that does not say which diagnostic it suppresses — the "
            + "reference compiles, resolves and works, and misleads every reader of it. No fix is offered: "
            + "renaming the type changes a published name, and rewriting the identifier changes which "
            + "diagnostic is suppressed. Which of those is the typo is not something a tool can know.");
}
