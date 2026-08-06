// -------------------------------------------------------------------------------------------------
// The documented cases that must stay SILENT.
//
// The guides describe both halves of the analyzers' behaviour: the code that is reported, and the
// code that must be left alone. Only the second half can live in this project — a deliberate
// violation here reads exactly like a false positive nobody has triaged yet. The reported half is
// asserted in DiagnosticCatalog.Analyzers.UnitTests, where the expectation can be stated.
//
// Every type below carries a comment naming the guide section it comes from, so a reader can go from
// a documented promise to the code that keeps it honest. This is the file that answers acceptance
// criterion 15 for the silent half: if a section of the guides promises silence, it appears here.
// -------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;

using DiagnosticCatalog;
using DiagnosticCatalog.Sonar;

// rule-contract.en.md, "Which attributes are analysed" — "Aliases on the attribute itself are
// resolved. Analysis never depends on the short name written in source." Used by AliasedAttributeName
// below; the analyzer identifies the attribute through the semantic model, never by this spelling.
using Suppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// specification.en.md §21.2 — "an assembly-level suppression" and "a GlobalSuppressions.cs file". The
// same attribute with the same two arguments, applied at assembly scope with the Scope/Target pair the
// IDE writes. The analyzer runs on the attribute's syntax node, so where it sits changes nothing, and
// a coherent pair must be as silent here as on a member.
[assembly: SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "The members below exist to be compiled, not to be called.",
    Scope = "namespaceanddescendants",
    Target = "~N:DiagnosticCatalog.Usage.DocumentedForms")]

namespace DiagnosticCatalog.Usage.DocumentedForms
{
    // === rule-contract.en.md — the declaration side =============================================

    /// <summary>The guide's own example, satisfying all five requirements.</summary>
    // rule-contract.en.md, "The whole contract, in five requirements" — rows 1 to 5, all satisfied:
    // marked, a static non-generic class, a public non-blank `const string Id`, the same for
    // `Category`, and that `Category` reaching a constant declared in a `[DiagnosticCategory]` class.
    // "Nothing else is required. No base class, no interface, nothing to register."
    [DiagnosticRule]
    public static class JD0007
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(JD0007);

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = DummyCategory.Usage;
    }

    /// <summary>A type shaped like a rule, carrying no marker.</summary>
    // rule-contract.en.md, "The whole contract, in five requirements", row 1 — "an unmarked type is
    // simply not a rule". This one fails rows 2, 3 and 4 outright: it is not static, its `Id` is not a
    // constant, and its `Category` is not a string. None of DCAT0002, DCAT0003 or DCAT0004 may fire,
    // because the marker is the only signal there is.
    public sealed class LooksLikeARule
    {
        /// <summary>Not a constant, so unusable as an attribute argument — and nobody's business.</summary>
        public static readonly string Id = "JD9999";

        /// <summary>Not even a string.</summary>
        public const int Category = 1;
    }

    /// <summary>A rule whose identifier is not a valid C# identifier.</summary>
    // rule-contract.en.md, "`Id` — and when it differs from the type name" — "not every identifier is
    // a valid C# identifier". When the two differ, the type name yields and the constant carries the
    // canonical form. The guide's own example, verbatim.
    // DCAT0005 is expected here and cannot be cleared: the identifier carries a character C#
    // forbids, so this name is already the closest one there is. Waived at the site rather than
    // in .editorconfig, so the next declaration like it is met by a reader instead of by silence.
    #pragma warning disable DCAT0005
    [DiagnosticRule]
    public static class RULE_001
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = "RULE-001";

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = DummyCategory.Usage;
    }
    #pragma warning restore DCAT0005

    /// <summary>A rule whose category nothing anywhere can confirm.</summary>
    // rule-contract.en.md, "`Category` — the member nothing can verify" — its VALUE "has no mechanical
    // check anywhere". Any non-blank string satisfies the contract, including one no analyzer on earth
    // publishes; accuracy is a matter of the catalogue's credibility, not of a check. Requirement 5
    // does not change that: it checks that the category has a single declaration, never that the
    // string in it is right, so declaring this value once leaves it exactly as unverifiable.
    [DiagnosticRule]
    public static class JD0008
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(JD0008);

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = DummyCategory.DummyHygiene;
    }

    /// <summary>The categories this catalogue uses, declared once each.</summary>
    // rule-contract.en.md, "Categories declared once — requirement 5" — the marker is REQUIRED, and
    // what it buys is that tooling can tell a category constant from any other string constant in the
    // assembly. Declaring the container is not itself checked by anything, and must report nothing.
    [DiagnosticCategory]
    public static class DummyCategory
    {
        /// <summary>The <c>Usage</c> category.</summary>
        public const string Usage = "Usage";

        /// <summary>A category no analyzer on earth publishes.</summary>
        public const string DummyHygiene = "Dummy Hygiene";

        /// <summary>The category the trim warnings mirrored below are declared under.</summary>
        public const string Trimming = "Trimming";
    }

    /// <summary>A rule whose category is initialised from another constant.</summary>
    // rule-contract.en.md, "Categories declared once" — "A `const` initialised from another `const` is
    // still a compile-time constant", so this remains valid as an attribute argument. Its use site is
    // CategoryThroughASharedConstant below, which is where the interesting half of the promise sits.
    [DiagnosticRule]
    public static class JD0009
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(JD0009);

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = DummyCategory.Usage;
    }

    /// <summary>A rule carrying members the model does not know about.</summary>
    // rule-contract.en.md, "What is out of the model", row 3 — `Severity` is not a rule member: an enum
    // can be `const`, but `DiagnosticSeverity` lives in Microsoft.CodeAnalysis.Common and declaring it
    // "forces Roslyn on every consumer of the catalogue". A catalogue that wants the value anyway
    // spells it as a plain string. The contract neither requires nor forbids it — "Nothing else is
    // required" — so an extra member must not turn a satisfied rule into a reported one.
    [DiagnosticRule]
    public static class JD0010
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(JD0010);

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = DummyCategory.Usage;

        /// <summary>The severity the analyzer declares, as a plain string rather than an enum.</summary>
        public const string Severity = "Warning";

        /// <summary>Where the rule is documented.</summary>
        public const string HelpLinkUri = "https://dummies.example/rules/JD0010";
    }

    /// <summary>The trim warnings this catalogue mirrors.</summary>
    public static class TrimRule
    {
        /// <summary>Members annotated with RequiresDynamicCode may break when compiled ahead of time.</summary>
        // rule-contract.en.md, "Which attributes are analysed", row 2 — the decoder behind
        // `UnconditionalSuppressMessage` "accepts only `IL####` identifiers", so a rule whose Id is one
        // of those is honoured and must be left alone. Used by TrimSuppression below.
        [DiagnosticRule]
        public static class IL3050
        {
            /// <summary>The canonical identifier of this diagnostic.</summary>
            public const string Id = nameof(IL3050);

            /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
            public const string Category = DummyCategory.Trimming;
        }

        /// <summary>The same warning, published in the trimmer's own friendly-name form.</summary>
        // rule-contract.en.md, "How an identifier is matched" — "`UnconditionalSuppressMessage` honours
        // the same form — `IL2026:FriendlyName` — which is why `DCAT0009` mirrors the trimmer's decoder
        // rather than applying a stricter pattern. Reporting an identifier the trimmer *does* honour
        // would be telling you to change something that works." The id is not a valid C# identifier,
        // which the "`Id` — and when it differs from the type name" section already blesses.
        // DCAT0005 is expected here and cannot be cleared: the identifier carries a character C#
        // forbids, so this name is already the closest one there is. Waived at the site rather than
        // in .editorconfig, so the next declaration like it is met by a reader instead of by silence.
        #pragma warning disable DCAT0005
        [DiagnosticRule]
        public static class IL2026Annotated
        {
            /// <summary>The canonical identifier of this diagnostic.</summary>
            public const string Id = "IL2026:Members annotated with RequiresUnreferencedCode";

            /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
            public const string Category = DummyCategory.Trimming;
        }
        #pragma warning restore DCAT0005
    }
}

namespace Contoso.CodeQuality
{
    // rule-contract.en.md, "The marker is matched by name, never by symbol", the converse paragraph —
    // "an attribute of the same **short** name in another namespace is somebody else's and is
    // deliberately not matched." Somebody else's marker, in somebody else's namespace.
    //
    // The type it decorates satisfies none of the five requirements: it is not static, it has no `Id`
    // and no `Category`. A marker matched on the short name — or on any name but the fully qualified
    // `DiagnosticCatalog.DiagnosticRuleAttribute` — would report DCAT0002, DCAT0003 and DCAT0004 here,
    // against a type that is none of this library's business. Nothing may be reported.
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DiagnosticRuleAttribute : Attribute
    {
    }

    /// <summary>A naming convention Contoso's own tooling discovers through its own marker.</summary>
    [DiagnosticRule]
    public sealed class ContosoNamingConvention
    {
        /// <summary>The convention's display name.</summary>
        public string Name { get; set; } = string.Empty;
    }
}

namespace DiagnosticCatalog.Usage.DocumentedForms.UseSites
{
    /// <summary>Qualified member access — the canonical form.</summary>
    // rule-contract.en.md, "Accepted syntactic forms at a use site", form 1 — "**Qualified member
    // access** — the canonical form". Also writing-suppressions.en.md, "2. Write the suppression".
    internal static class QualifiedMemberAccess
    {
        [SuppressMessage(
            SonarRule.S1144.Category,
            SonarRule.S1144.Id,
            Justification = "Called by the serializer through reflection.")]
        internal static int Rebuild() => 1;
    }

    /// <summary>The suppression attribute reached through a using alias.</summary>
    // rule-contract.en.md, "Which attributes are analysed" — "**Aliases on the attribute itself are
    // resolved.** Analysis never depends on the short name written in source." The alias is declared at
    // the top of this file, exactly as the guide's snippet declares it.
    internal static class AliasedAttributeName
    {
        [Suppress(
            SonarRule.S2094.Category,
            SonarRule.S2094.Id,
            Justification = "A marker type, deliberately empty.")]
        internal static int Marker() => 2;
    }

    /// <summary>The pair written by parameter name, and reversed.</summary>
    // specification.en.md §21.4 requires this shape of the code fix ("the pair written by parameter
    // name and reversed is not swapped"); the coherent version of it must be silent at the use site.
    // C# allows the two constructor parameters to be named and written in any order, so an analyzer
    // reading them by position alone would pair `("S1144", "Major Code Smell")` and see a mismatch.
    internal static class NamedAndReversedPair
    {
        [SuppressMessage(
            checkId: SonarRule.S1144.Id,
            category: SonarRule.S1144.Category,
            Justification = "Instantiated by the DI container.")]
        internal static int Resolve() => 3;
    }

    /// <summary>A coherent suppression whose justification says very little.</summary>
    // diagnostics.en.md, `DCAT0014` — "presence is the whole contract: the value is read for its
    // length, never for its meaning". DCAT0014 asks for the property and stops there, so a reason this
    // thin is silent while an absent one is reported. The line matters more than it looks: a check
    // that started weighing what a justification SAYS would report here, and specification §5 rules
    // that out.
    internal static class ThinJustification
    {
        [SuppressMessage(SonarRule.S3903.Category, SonarRule.S3903.Id, Justification = "Interop shim.")]
        internal static int Unnamespaced() => 4;
    }

    /// <summary>A rule whose category is reached through a shared constant.</summary>
    // rule-contract.en.md, "Categories declared once" — the use site of JD0009. `Category` resolves to
    // the field declared on the rule; the analyzer does NOT follow its initialiser. One that did would
    // see `DummyCategory` on one side and `JD0009` on the other, conclude the two arguments come from
    // different rules, and report DCAT0001 on every correctly generated catalogue — including all four
    // this repository ships, since every one of them writes `Category = SonarCategory.MajorCodeSmell`.
    internal static class CategoryThroughASharedConstant
    {
        [SuppressMessage(JD0009.Category, JD0009.Id, Justification = "Generated code, reviewed once.")]
        internal static int Generated() => 5;
    }

    /// <summary>A trim suppression naming an <c>IL####</c> rule.</summary>
    // rule-contract.en.md, "Which attributes are analysed", row 2 — `UnconditionalSuppressMessage` is
    // analysed, "**Is** emitted, and its decoder accepts only `IL####` identifiers". An id the decoder
    // honours is a suppression that works, so DCAT0009 must stay quiet.
    internal static class TrimSuppression
    {
        [UnconditionalSuppressMessage(
            TrimRule.IL3050.Category,
            TrimRule.IL3050.Id,
            Justification = "The reflected members are preserved by a trimmer descriptor.")]
        internal static int Reflect() => 6;
    }

    /// <summary>A trim suppression naming the same warning in its friendly-name form.</summary>
    // rule-contract.en.md, "How an identifier is matched" — the trimmer's decoder "reads exactly four
    // characters at offset 2 and ignores whatever follows", so `IL2026:FriendlyName` suppresses IL2026.
    // "Reporting an identifier the trimmer *does* honour would be telling you to change something that
    // works."
    internal static class TrimSuppressionWithAFriendlyName
    {
        [UnconditionalSuppressMessage(
            TrimRule.IL2026Annotated.Category,
            TrimRule.IL2026Annotated.Id,
            Justification = "The reflected members are preserved by a trimmer descriptor.")]
        internal static int Reflect() => 7;
    }
}

namespace DiagnosticCatalog.Usage.DocumentedForms.WithATypeAlias
{
    // rule-contract.en.md, "Accepted syntactic forms at a use site", form 2 — "**A type alias** — fully
    // equivalent, and recommended when the container name is long". Also writing-suppressions.en.md,
    // "The shorthands, and one to avoid". The guide's snippet, verbatim down to the alias name.
    using Unused = DiagnosticCatalog.Sonar.SonarRule.S1144;

    /// <summary>The alias form.</summary>
    internal static class TypeAliasForm
    {
        [SuppressMessage(Unused.Category, Unused.Id, Justification = "Called by the serializer.")]
        internal static int Rebuild() => 8;
    }
}

namespace DiagnosticCatalog.Usage.DocumentedForms.WithUsingStatic
{
    // rule-contract.en.md, "Accepted syntactic forms at a use site", form 3 — "**`using static`** —
    // recognised, **not recommended**". Exactly one such directive is in scope here: a second one would
    // make `Category` and `Id` ambiguous, which is the compile error the guide describes and the reason
    // it does not promote the form. The analyzer resolves it because it works on symbols.
    using static DiagnosticCatalog.Sonar.SonarRule.S2094;

    /// <summary>The <c>using static</c> form.</summary>
    internal static class UsingStaticForm
    {
        [SuppressMessage(Category, Id, Justification = "A marker type, deliberately empty.")]
        internal static int Marker() => 9;
    }
}

namespace DiagnosticCatalog.Usage.DocumentedForms.NotChecked
{
    /// <summary>A pragma, which no constant can ever reach.</summary>
    // rule-contract.en.md, "What is out of the model", row 1, and writing-suppressions.en.md, "Two
    // things this cannot help with" — "`#pragma warning disable S1144` [...] Takes a bare identifier
    // token, not an expression. There is no position a constant could occupy." Out of the model means
    // out of the model in both directions: nothing here is reported, and no migration is offered.
    internal static class PragmaWarningDisable
    {
#pragma warning disable S1144 // Unused private types or members should be removed
        internal static int Unused() => 10;
#pragma warning restore S1144
    }

    /// <summary>A literal pair whose category is simply wrong.</summary>
    // diagnostics.en.md, "What is deliberately not checked", first bullet — "validate an arbitrary
    // string. `[SuppressMessage(\"Usage\", \"S1144\")]` with a wrong category matches no known rule and
    // is reported by nothing. What makes a wrong category impossible is the *constant*, which the
    // compiler checks". The guide's own example, verbatim: S1144's category is "Major Code Smell".
    internal static class ArbitraryCategoryString
    {
        [SuppressMessage("Usage", "S1144", Justification = "Called by the serializer.")]
        internal static int Rebuild() => 11;
    }

    /// <summary>The two literal suppressions the guide opens with.</summary>
    // writing-suppressions.en.md, "The problem, in one example" — "all of the following compile, ship,
    // and do nothing at all". Adopting a catalogue does not turn them into diagnostics: neither pair
    // matches a rule the compilation can see, and diagnostics.en.md pins that under DCAT0006 —
    // "Reported only when a known rule matches the pair". The third line of the guide's snippet, the
    // one spelled correctly, IS reported, and is tested in
    // DiagnosticCatalog.Analyzers.UnitTests.LiteralSuppressionTests.
    internal static class MisspelledLiteralSuppressions
    {
        [SuppressMessage("Major Code Smell", "S1145", Justification = "Called by the serializer.")]
        internal static int OneDigitOut() => 12;

        [SuppressMessage("Major Code Smell", "S 1144", Justification = "Called by the serializer.")]
        internal static int StraySpace() => 13;
    }

    /// <summary>A literal suppression naming a vendor with no catalogue.</summary>
    // diagnostics.en.md, `DCAT0006` — "Reported only when a known rule matches the pair, so a codebase
    // that has adopted no catalogue stays completely silent." Referencing one catalogue must not make
    // every OTHER vendor's suppressions light up, or adoption would be unaffordable.
    internal static class VendorWithNoCatalogue
    {
        [SuppressMessage("Contoso Hygiene", "CX0001", Justification = "Reviewed at the design meeting.")]
        internal static int Reviewed() => 14;
    }

    /// <summary>An attribute that is not a suppression, carrying suppression-shaped strings.</summary>
    // rule-contract.en.md, "Which attributes are analysed" — the table has two rows and only two. An
    // attribute the semantic model resolves to anything else is not analysed, whatever its arguments
    // happen to look like.
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    internal sealed class ReviewedAttribute : Attribute
    {
        internal ReviewedAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }

        internal string Category { get; }

        internal string CheckId { get; }
    }

    /// <summary>Somebody else's two-string attribute.</summary>
    [Reviewed("Major Code Smell", "S1144")]
    internal static class NotASuppressionAttribute
    {
        internal static int Reviewed() => 15;
    }

    /// <summary>A hand-written trim suppression, with no catalogue behind it.</summary>
    // diagnostics.en.md, `DCAT0009` — the diagnostic is about "a non-`IL` **rule** used in
    // `UnconditionalSuppressMessage`", and specification.en.md §11.9 and §21.2 both say "a rule".
    // Firing on literals would flood every project that hand-writes trim suppressions without ever
    // adopting a catalogue — the audience the diagnostic is not addressed to. The pair matches no rule
    // in this compilation, so DCAT0006 has nothing to say about it either.
    internal static class LiteralTrimSuppression
    {
        [UnconditionalSuppressMessage("Major Code Smell", "S9999", Justification = "Legacy, kept as written.")]
        internal static int Legacy() => 16;
    }
}
