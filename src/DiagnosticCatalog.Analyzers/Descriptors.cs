using Microsoft.CodeAnalysis;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// The diagnostics this package reports, declared once.
/// </summary>
/// <remarks>
/// <para>
/// A DCAT id is a published contract from its first release: consumers write it in .editorconfig and in
/// suppressions of their own, so renaming or removing one is a breaking change (CLAUDE.md). What records
/// that contract is AnalyzerReleases.Shipped.md, which RS2008 keeps in step with the ids declared here.
/// </para>
/// <para>
/// The default severity of each one follows a single model rather than a per-rule judgement, and the
/// model is stated once here so a new id is placed by asking one question rather than by looking at
/// which neighbour it resembles:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Error</b> — this library's mandatory contract is not satisfied, the suppression is incorrect or
/// has no effect, or the package does not deliver the behaviour it promises. Nothing here is a matter
/// of taste, and none of it is repaired by anything other than the author's edit.
/// </description></item>
/// <item><description>
/// <b>Warning</b> — the code works today and stays liable to drift, badly anchored, or misleading. A
/// build is not the right place to stop for it; a review is.
/// </description></item>
/// <item><description>
/// <b>Info</b> — a legitimate exception nobody can repair, reported so the boundary is visible and
/// configurable rather than silent.
/// </description></item>
/// </list>
/// <para>
/// The split is NOT between the use site and the definition site: a catalogue that fails the structural
/// contract publishes constants nobody can suppress with, which is the same failure as a suppression
/// that silences nothing. Nor does incomplete detection lower a severity — a form the analyzer misses
/// is a false negative, and it says nothing about the certainty of the cases it does report.
/// </para>
/// <para>
/// Every severity stays overridable per id and per path in <c>.editorconfig</c>, which is what makes a
/// staged adoption possible without weakening the default anybody inherits.
/// </para>
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
        messageFormat:
            "The category slot references '{0}' and the identifier slot '{1}': a suppression must name "
            + "one rule's Category and that same rule's Id",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "The two arguments are compared by the rule that DECLARES them, not by their values. Two "
            + "rules sharing a category today produce a suppression that works and is still reported, "
            + "because the pairing is a copy-paste error: the day the vendor recategorises one of them, "
            + "the suppression carries the wrong category and nothing in the platform will say so. The "
            + "member is checked as well as the rule: a rule type carries more than the pair, so one "
            + "rule's own members can land in each other's slots, or the identifier slot can hold "
            + "something that is neither — and because Roslyn matches on the identifier alone, such a "
            + "suppression resolves, compiles and silences nothing. A misplaced member is reported "
            + "without a fix: whether the wrong member or the wrong rule was written is not something a "
            + "tool can know.");

    internal static readonly DiagnosticDescriptor ReplaceableStringLiterals = new(
        id: DiagnosticIds.ReplaceableStringLiterals,
        title: "Use a diagnostic catalog reference instead of string literals",
        messageFormat: "Reference {2} instead of the string literals \"{0}\" and \"{1}\"",
        category: Category,
        // Error. The catalogue package being referenced at all is the statement of intent: a project
        // that has taken the dependency has decided its suppressions are catalogue references, and a
        // suggestion no build output shows does not carry that. The cost is real and belongs in the
        // release notes — adopting a catalogue fails the build on every existing literal suppression
        // at once. Severity remains configurable per project in .editorconfig.
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
        // Error: the line has no effect at all. Every tool in the chain discards it, so the warning it
        // was written to silence is either still being reported or was never raised — and the author
        // believes otherwise. That the check under-detects, missing an identifier reached through a
        // constant, is not a reason to lower it: an undetected form is a false negative, and it says
        // nothing about the certainty of the ones that are reported.
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "ILLink reads suppressions from the compiled assembly and discards any identifier its "
            + "decoder rejects, while Roslyn never processes this attribute at all. The suppression is "
            + "therefore a no-op that no other tool reports. The check mirrors that decoder rather than "
            + "a stricter pattern, so identifiers ILLink does honour — including its IL####:FriendlyName "
            + "form — are left alone.");

    internal static readonly DiagnosticDescriptor MissingJustification = new(
        id: DiagnosticIds.MissingJustification,
        title: "A suppression must carry a justification",
        messageFormat: "This suppression of '{0}' {1}: nothing records why the diagnostic is silenced",
        category: Category,
        // Error: a justification is a mandatory part of this library's contract (ADR-0039), not a
        // nicety a build may leave for later. A suppression records a decision, and the one thing no
        // tool can recover afterwards is why it was taken — the warning is gone and the reason lives
        // in the head of whoever wrote the line. Adopting a catalogue therefore meets every unjustified
        // suppression at once, exactly as it meets every literal one through DCAT0006, and the same one
        // line of .editorconfig stages both.
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "The pair says WHICH diagnostic is silenced and the compiler now checks it; nothing says "
            + "WHY, and no tool can recover it later — the warning is gone, and the reason it was "
            + "acceptable lives in the head of whoever wrote the line. Presence is all that is asked: "
            + "the value is read for its length, never for its meaning, so this judges no justification "
            + "and rejects none for being thin. The one non-blank value it does refuse is the IDE's own "
            + "\"<Pending>\" placeholder, which is that tool's word for a justification nobody has "
            + "written yet. Every suppression is held to it, a literal one included: silencing a warning "
            + "without saying why costs the same whether or not the pair has been migrated, and the "
            + "codebases that have migrated least are the ones the question is worth asking of most.");

    internal static readonly DiagnosticDescriptor InvalidRuleType = new(
        id: DiagnosticIds.InvalidRuleType,
        title: "A diagnostic rule must be declared as a static non-generic class",
        messageFormat: "'{0}' is marked [DiagnosticRule] but is not a static non-generic class",
        category: Category,
        // Error, with the other two structural rules: the type claims to be a rule and cannot be used
        // as one. §8's shape is mandatory, and a declaration that misses it publishes a member nobody
        // can write a suppression against — which is the failure this library exists to remove, moved
        // one step upstream.
        defaultSeverity: DiagnosticSeverity.Error,
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
        // Error: the identifier is the half Roslyn actually matches on. Without it the rule declares
        // nothing a suppression can name, and the catalogue ships a type whose only effect is to look
        // like one.
        defaultSeverity: DiagnosticSeverity.Error,
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
        // Warning: the rule compiles, folds to the right literal, and suppresses exactly what it
        // should. What is wrong is that the value has no single declaration, so it is free to drift
        // from the one the catalogue's other rules carry — badly anchored rather than incorrect, which
        // is the whole of the warning tier.
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
        // Error, for the reason DCAT0003 is one: both constants are mandatory, and a rule missing
        // either of them cannot be written into the two-argument attribute this library exists to make
        // checkable.
        defaultSeverity: DiagnosticSeverity.Error,
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
        // exception DCAT0013 carves out invisible, and an invisible exception inside a rule that does
        // report is the one shape nobody can reason about.
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description:
            "The identifier carries a character C# forbids in a type name, so the name is the identifier "
            + "legalised and no closer spelling exists. Reported rather than passed over in silence: the "
            + "same divergence is reported when nothing forced it (DCAT0013), and a reader who cannot see "
            + "where the boundary falls cannot tell a blessed declaration from one nobody has checked.");

    internal static readonly DiagnosticDescriptor IdNotWrittenAsNameOf = new(
        id: DiagnosticIds.IdNotWrittenAsNameOf,
        title: "A rule identifier should be written as nameof",
        messageFormat: "Write nameof({0}) rather than a literal, so the identifier cannot drift from the type",
        category: Category,
        // Warning: the declaration is correct today and anchored to nothing. Renaming the type leaves
        // the literal behind, which is drift rather than a defect — the exact shape the warning tier
        // names.
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
        // Warning: the reference compiles, resolves and suppresses the right diagnostic. What it does
        // is mislead every reader of the use site, which is the third shape the warning tier names —
        // and unlike the error tier there is no repair a tool can point at, because renaming the type
        // and rewriting the identifier are both changes only the author can choose between.
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "The identifier is a valid C# identifier, so the type could have been named it, and was not. "
            + "Every use site then reads a name that does not say which diagnostic it suppresses — the "
            + "reference compiles, resolves and works, and misleads every reader of it. No fix is offered: "
            + "renaming the type changes a published name, and rewriting the identifier changes which "
            + "diagnostic is suppressed. Which of those is the typo is not something a tool can know.");

    internal static readonly DiagnosticDescriptor MissingAnalyzerOptIn = new(
        id: DiagnosticIds.MissingAnalyzerOptIn,
        title: "A catalogue package must ship the analyzer opt-in",
        messageFormat: "'{0}' packs no build/{0}.props, so referencing this catalogue checks nobody",
        category: Category,
        // Error: the package does not deliver the behaviour it promises. A catalogue exists so that its
        // consumers are checked, and one that packs no opt-in checks nobody — silently, in a way
        // indistinguishable from a clean codebase. This is the one diagnostic that reads something
        // OUTSIDE the compilation (see CataloguePackaging), which is a way of being wrong the others do
        // not have; the answer is that the classification is derived from the project's own pack
        // settings and has an explicit escape, DiagnosticCatalogAnalyzerOptIn, rather than that the
        // report is quieter than what it names.
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description:
            "A catalogue delivers the DCAT analyzers to its consumers by packing build/<its own package "
            + "id>.props, which sets EnableDiagnosticCatalogAnalyzers; NuGet imports a package's build/ "
            + "folder for a direct reference and for nothing further out, and that asymmetry is what "
            + "stops an application being analysed by a catalogue it reached through some library "
            + "(ADR-0038). A catalogue that packs no such file still compiles, still publishes, and still "
            + "gives its consumers the constants — it simply never reports the suppressions they have "
            + "not converted. That silence is indistinguishable from a codebase with nothing to report, "
            + "which is the failure this library exists to remove, so it is reported to the one person "
            + "who can fix it. A catalogue that arranges the opt-in some other way says so by setting "
            + "DiagnosticCatalogAnalyzerOptIn to 'packed' in its project file.",
        // Required by RS1037 for anything reported at compilation end, and it has a visible cost worth
        // stating: a CompilationEnd diagnostic reaches the IDE only under full-solution analysis, so a
        // catalogue author sees this in their BUILD and in CI rather than as a squiggle while typing.
        // Acceptable here — the defect is in a project file nobody is typing in, and it is a release
        // blocker rather than an editing hint.
        customTags: WellKnownDiagnosticTags.CompilationEnd);
}
