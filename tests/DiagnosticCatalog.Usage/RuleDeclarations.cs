// -------------------------------------------------------------------------------------------------
// A hand-written catalogue, as a third-party vendor would publish one.
//
// Meridian.Analyzers is fictional; the declaration shapes are not. Each type carries a one-line
// comment naming the shape it stands for, and every one of them is permitted by the rule contract
// (doc/guide/rule-contract.en.md) or by the author's guide (doc/guide/authoring-a-catalogue.en.md).
// None of them may be reported.
// -------------------------------------------------------------------------------------------------

using DiagnosticCatalog;

// Permitted shape: the marker reached through a using alias. The attribute is matched by its fully
// qualified metadata name once resolved, so the spelling at the declaration site is the author's.
using MeridianDiagnostic = DiagnosticCatalog.DiagnosticRuleAttribute;

// Permitted shape: the provenance record a catalogue that mirrors somebody else's analyzer carries.
[assembly: CatalogSource(
    source:        "Meridian.Analyzers",
    sourceVersion: "3.4.0",
    generatedOn:   "2026-07-31")]

namespace Meridian.Analyzers.Suppressions;

/// <summary>
/// The diagnostic categories Meridian.Analyzers uses, declared once each.
/// </summary>
// Permitted shape: a marked category holder, declared once and shared by every rule below.
[DiagnosticCategory]
public static class MeridianCategory
{
    /// <summary>The prefix every published Meridian category carries.</summary>
    public const string Prefix = "Meridian.";

    // Permitted shape: a category value built by concatenating two constants. Still a compile-time
    // constant, so it remains usable as an attribute argument.
    /// <summary>The <c>Meridian.Reliability</c> category.</summary>
    public const string Reliability = Prefix + "Reliability";

    /// <summary>The <c>Meridian.Naming</c> category.</summary>
    public const string Naming = Prefix + "Naming";

    /// <summary>The <c>Meridian.Performance</c> category.</summary>
    public const string Performance = Prefix + "Performance";

    /// <summary>
    /// The categories Meridian still ships as preview, kept apart so a consumer can see at a glance
    /// which rules are not yet covered by the compatibility promise.
    /// </summary>
    // Permitted shape: a category holder nested inside another, marked in its own right.
    [DiagnosticCategory]
    public static class Preview
    {
        /// <summary>The <c>Meridian.Preview.Concurrency</c> category.</summary>
        public const string Concurrency = Prefix + "Preview.Concurrency";
    }
}

/// <summary>
/// The categories inherited from the analyzer Meridian.Analyzers replaced, kept for the rules that
/// were carried over unchanged.
/// </summary>
// Permitted shape: a marked holder whose own constants are chained through another holder. The
// marker is what a rule's category must reach (DCAT0011); how that holder builds its value is its
// own business, so the concatenation below is untouched by the requirement.
[DiagnosticCategory]
public static class LegacyCategory
{
    // Permitted shape: a constant chained through a second holder, one hop further from the literal.
    /// <summary>The <c>Meridian.Migration</c> category.</summary>
    public const string Migration = MeridianCategory.Prefix + "Migration";
}

#region Reliability and performance rules

/// <summary>
/// The Meridian.Analyzers diagnostic rules.
/// </summary>
// Permitted shape: a partial container, split across regions of this one file. A rule is found by
// symbol, so which part declares it changes nothing.
public static partial class MeridianRule
{
    /// <summary>A handle obtained from the runtime should be disposed on every path.</summary>
    // Permitted shape: the plainest rule there is — Id by nameof, Category from the shared holder.
    [DiagnosticRule]
    public static class MRD0001
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(MRD0001);

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = MeridianCategory.Reliability;
    }

    /// <summary>A pooled buffer should be returned to the pool it came from.</summary>
    // Permitted shape: a rule carrying the rest of the DiagnosticDescriptor arguments, plus a nested
    // type of its own. Only Id and Category are part of the contract; the extras are ignored.
    [DiagnosticRule]
    public static class MRD0002
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(MRD0002);

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = MeridianCategory.Performance;

        /// <summary>The rule's title, as the analyzer declares it.</summary>
        public const string Title = "A rented buffer should be returned to its pool";

        /// <summary>The rule's message format, as the analyzer declares it.</summary>
        public const string MessageFormat = "Buffer rented at '{0}' is never returned";

        /// <summary>The rule's long description, as the analyzer declares it.</summary>
        public const string Description =
            "A buffer rented from an array pool and never returned is not a leak the garbage " +
            "collector reports, so the cost shows up as pressure rather than as an error.";

        /// <summary>Where the rule is documented.</summary>
        public const string HelpLinkUri = "https://meridian.example/rules/MRD0002";

        /// <summary>What the code fix for this rule registers itself as.</summary>
        public static class Fix
        {
            /// <summary>The equivalence key, so "fix all" groups the occurrences.</summary>
            public const string EquivalenceKey = "Meridian.MRD0002.ReturnBuffer";

            /// <summary>The title the IDE shows in the light-bulb menu.</summary>
            public const string Title = "Return the buffer to its pool";
        }
    }

    /// <summary>A task started inside a lock should not be awaited while the lock is held.</summary>
    // Permitted shape: a partial rule type. The contract is checked on the symbol, so Id here and
    // Category in the other region is one satisfied rule, not two half-declared ones. The marker
    // goes on one part only — it is declared AllowMultiple = false.
    [DiagnosticRule]
    public static partial class MRD0050
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(MRD0050);
    }

    /// <summary>A migrated rule keeps the identifier its users already wrote down.</summary>
    // Permitted shape: the marker written out in full rather than with the C# suffix elision.
    [DiagnosticRuleAttribute]
    public static class MRD0003
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(MRD0003);

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = LegacyCategory.Migration;
    }
}

#endregion

#region Naming rules

/// <content>The naming rules, and the second half of the partial rule above.</content>
public static partial class MeridianRule
{
    // Permitted shape: an identifier that is not a valid C# identifier. nameof cannot spell it, so
    // the constant carries the canonical form and the type name carries the closest legal one.
    /// <summary>A public constant should be named in PascalCase.</summary>
    [DiagnosticRule]
    public static class MRD_0100
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = "MRD-0100";

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = MeridianCategory.Naming;
    }

    /// <summary>An awaitable-returning method should be named with the 'Async' suffix.</summary>
    // Permitted shape: the marker applied through a using alias declared at the top of this file.
    [MeridianDiagnostic]
    public static class MRD0101
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(MRD0101);

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = MeridianCategory.Naming;
    }

    /// <content>The category half of the rule declared in the region above.</content>
    public static partial class MRD0050
    {
        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = MeridianCategory.Preview.Concurrency;
    }

    /// <summary>The rules Meridian reports on asynchronous code.</summary>
    // Permitted shape: intermediate containers, as deep as the author cares to nest them.
    public static class Async
    {
        /// <summary>The rules Meridian reports on asynchronous streams.</summary>
        public static class Streams
        {
            /// <summary>An async iterator should accept a cancellation token.</summary>
            // Permitted shape: a rule three containers deep. The contract looks at the rule type and
            // never at what encloses it.
            [DiagnosticRule]
            public static class MRD0131
            {
                /// <summary>The canonical identifier of this diagnostic.</summary>
                public const string Id = nameof(MRD0131);

                /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
                public const string Category = MeridianCategory.Preview.Concurrency;
            }
        }
    }
}

#endregion

/// <summary>A configuration section should be bound to a named options type.</summary>
// Permitted shape: a rule at namespace level, in no container at all. The guide recommends a
// container for how the use site reads, not because the contract asks for one.
[DiagnosticRule]
public static class MRD9001
{
    /// <summary>The canonical identifier of this diagnostic.</summary>
    public const string Id = nameof(MRD9001);

    /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
    public const string Category = MeridianCategory.Reliability;
}

/// <summary>
/// The rules Meridian's generic <c>ContractAnalyzer&lt;TSyntax&gt;</c> reports, kept beside it so the
/// analyzer and the constants its users write share a shape.
/// </summary>
/// <typeparam name="TSyntax">The syntax node the analyzer is instantiated for.</typeparam>
// Permitted shape: a generic container. The contract requires the RULE to be non-generic; these are,
// and their arity is read off the rule rather than off what encloses it.
public static class ContractRule<TSyntax>
{
    /// <summary>A contract precondition should be checked before any side effect.</summary>
    [DiagnosticRule]
    public static class MRD0600
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(MRD0600);

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = MeridianCategory.Reliability;
    }
}

/// <summary>
/// The categories of the rules Meridian runs on its own source and does not publish.
/// </summary>
// Permitted shape: a category constant written as nameof, so renaming the member renames the value.
[DiagnosticCategory]
internal static class InternalCategory
{
    /// <summary>The <c>Interop</c> category.</summary>
    public const string Interop = nameof(Interop);

    /// <summary>The <c>Telemetry</c> category.</summary>
    public const string Telemetry = nameof(Telemetry);
}

/// <summary>
/// The rules Meridian runs on its own source. They are part of the build, not of the package.
/// </summary>
// Permitted shape: an internal container and internal rules. The contract requires Id and Category
// to be public; it says nothing about the type that declares them.
internal static class MeridianInternalRule
{
    /// <summary>A native handle should be wrapped in a SafeHandle before it crosses a boundary.</summary>
    [DiagnosticRule]
    internal static class MRD8001
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(MRD8001);

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = InternalCategory.Interop;
    }

    /// <summary>A metric name should be declared once, as a constant.</summary>
    // Permitted shape: the category read off a marked holder whose own constant is written with
    // nameof — the value is "Telemetry", and the constant is what the compiler folds in. The nameof
    // belongs on the holder: written at the RULE it is not a reference to a declared constant, which
    // is DCAT0011.
    [DiagnosticRule]
    internal static class MRD8002
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(MRD8002);

        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>
        public const string Category = InternalCategory.Telemetry;
    }
}
