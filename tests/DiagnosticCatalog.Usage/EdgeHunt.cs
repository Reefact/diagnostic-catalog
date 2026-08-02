// EdgeHunt.cs — the adversary's file.
//
// Everything here is code a real consumer could write, and every construct honours the rule
// contract: a suppression names one rule, a rule is a static non-generic class carrying two
// public constant strings. What varies is the SHAPE — the spelling, the target, the scope, the
// declaration form — chosen against the assumptions the analyzers make rather than against the
// contract they check.
//
// The assumptions being attacked, one section each:
//
//   RuleMarker.IsRule          matches the marker by fully qualified metadata name, so every
//                              spelling that resolves to it must count.
//   RuleContract.Check         reads Id and Category off IFieldSymbol.ConstantValue, so every
//                              constant form the compiler folds must count.
//   SuppressionAttribute       resolves an argument through GetSymbolInfo and asks whether the
//                              symbol is a field on a rule type — an assumption about the shape
//                              of the argument EXPRESSION, not about its meaning.
//   SuppressionArgumentOrder   reads the pair by slot, not by position.
//   SuppressionUsageAnalyzer   registers on SyntaxKind.Attribute, so every attribute target in
//                              the language reaches it.
//   RuleIndex.Collect          descends namespaces and types, so a rule can hide anywhere.
//   IlWarningId.IsHonoured     mirrors ILLink's decoder, so an IL#### rule must pass.
//
// THREE PROBES CURRENTLY REPORT, all in EdgeHunt.Indirection and all one root cause: an argument
// whose value comes from the catalogue through a named constant that is not declared ON a rule
// type is bucketed with a hand-written literal. See the comments there. They are left in place
// deliberately — this project's build is the assertion, and silencing them would delete the
// finding. They are NOT contract violations: every one of them breaks the build the day the rule
// it names is renamed or retired, which is the whole property the catalogue exists to give.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading.Tasks;

using DiagnosticCatalog;
using DiagnosticCatalog.NetAnalyzers;
using DiagnosticCatalog.Sonar;
using DiagnosticCatalog.StyleCop;

using EdgeHunt.Catalog;

// Aliases, declared once and used from every namespace block below. An alias is resolved by the
// semantic model, so none of these may change what is reported.
using EhMarker = DiagnosticCatalog.DiagnosticRuleAttribute;
using EhSonar = DiagnosticCatalog.Sonar.SonarRule;
using EhUnused = DiagnosticCatalog.Sonar.SonarRule.S1144;
using EhEmptyClass = global::DiagnosticCatalog.Sonar.SonarRule.S2094;
using EhString = System.String;

// Assembly- and module-level suppressions. Both carry Scope/Target/Justification as named
// PROPERTY arguments, which the pair reader must step over without disturbing the positional
// count that decides which slot is which.
[assembly: SuppressMessage(
    EhEmptyClass.Category,
    EhEmptyClass.Id,
    Scope = "type",
    Target = "~T:EdgeHunt.Targets.EmptyOnPurpose",
    Justification = "A marker type carries no members by design.")]
[module: SuppressMessage(
    EhUnused.Category,
    EhUnused.Id,
    Justification = "Module-wide: the reflection host reaches private members.")]

namespace EdgeHunt.Catalog
{
    /// <summary>
    /// The categories. Every value is computed at compile time from other constants, which is the
    /// point: <c>RuleContract</c> reads the folded value off the symbol and must never care how
    /// the initialiser was spelled.
    /// </summary>
    [DiagnosticCategory]
    public static class EdgeHuntCategory
    {
        public const string Vendor = "EdgeHunt";

        public const string Separator = " ";

        /// <summary>Assembled from three constants by concatenation.</summary>
        public const string Correctness = Vendor + Separator + "Correctness";

        /// <summary>Assembled by a constant interpolated string (C# 10).</summary>
        public const string Layout = $"{Vendor}{Separator}Layout";

        /// <summary>The same assembly, one interpolation deeper.</summary>
        public const string VendorInterpolated = $"{Vendor} Interpolated";

        /// <summary>A category holding a colon. Only the identifier is truncated at one.</summary>
        public const string Qualified = Vendor + ":Layout";

        /// <summary>Non-ASCII, in composed form.</summary>
        public const string Accented = "Sécurité";

        public const string Trimming = Vendor + Separator + "Trimming";

        public const string House = Vendor + Separator + "House Rules";
    }

    /// <summary>Rules whose only oddity is how the marker is spelled.</summary>
    public static partial class EdgeHuntRule
    {
        /// <summary>The attribute written with its <c>Attribute</c> suffix.</summary>
        [DiagnosticRuleAttribute]
        public static class EH0001
        {
            public const string Id = nameof(EH0001);

            public const string Category = EdgeHuntCategory.Correctness;
        }

        /// <summary>The attribute written fully qualified, through <c>global::</c>.</summary>
        [global::DiagnosticCatalog.DiagnosticRuleAttribute]
        public static class EH0002
        {
            public const string Id = nameof(EH0002);

            public const string Category = EdgeHuntCategory.Correctness;
        }

        /// <summary>The attribute reached through a using alias.</summary>
        [EhMarker]
        public static class EH0003
        {
            public const string Id = nameof(EH0003);

            public const string Category = EdgeHuntCategory.Correctness;
        }

        /// <summary>The attribute written with an explicit <c>type:</c> target.</summary>
        [type: DiagnosticRule]
        public static class EH0004
        {
            public const string Id = nameof(EH0004);

            public const string Category = EdgeHuntCategory.Correctness;
        }

        /// <summary>Both constants declared by ONE field declaration, two declarators.</summary>
        [DiagnosticRule]
        public static class EH0005
        {
            public const string Id = "EH0005", Category = EdgeHuntCategory.Layout;
        }

        /// <summary>The category an interpolated constant, named on the holder rather than in place.</summary>
        // Interpolating AT THE RULE is DCAT0011 — a constant expression is not a reference to a
        // declared constant — so the interpolation sits where it is legal. The rejected form is in
        // DiagnosticCatalog.Analyzers.UnitTests, which is where a deliberate violation belongs.
        [DiagnosticRule]
        public static class EH0006
        {
            public const string Id = nameof(EH0006);

            public const string Category = EdgeHuntCategory.VendorInterpolated;
        }

        /// <summary>The two constants typed through an alias and through <c>String</c>.</summary>
        [DiagnosticRule]
        public static class EH0007
        {
            public const EhString Id = nameof(EH0007);

            public const String Category = EdgeHuntCategory.Correctness;
        }

        /// <summary>The optional constants, plus one that is not a string at all.</summary>
        [DiagnosticRule]
        public static class EH0008
        {
            public const string Id = nameof(EH0008);

            public const string Category = EdgeHuntCategory.Correctness;

            public const string Title = "A rented probe should be returned";

            public const string MessageFormat = "Probe '{0}' is never returned";

            public const string HelpLinkUri = "https://edgehunt.example/rules/EH0008";

            public const int Ordinal = 8;

            internal const string Note = "Not public: invisible to the contract, and harmless.";
        }

        /// <summary>A category containing a colon.</summary>
        [DiagnosticRule]
        public static class EH0009
        {
            public const string Id = nameof(EH0009);

            public const string Category = EdgeHuntCategory.Qualified;
        }

        /// <summary>A rule type named with non-ASCII letters, its id read back by nameof.</summary>
        [DiagnosticRule]
        public static class Règle0010
        {
            public const string Id = nameof(Règle0010);

            public const string Category = EdgeHuntCategory.Accented;
        }

        /// <summary>A rule type whose name is an escaped keyword: the id folds to "operator".</summary>
        [DiagnosticRule]
        public static class @operator
        {
            public const string Id = nameof(@operator);

            public const string Category = EdgeHuntCategory.Correctness;
        }

        /// <summary>Half of a partial rule: the marker and the identifier.</summary>
        [DiagnosticRule]
        public static partial class EH0011
        {
            public const string Id = nameof(EH0011);
        }

        /// <summary>A rule declared inside another rule.</summary>
        [DiagnosticRule]
        public static class EH0012
        {
            public const string Id = nameof(EH0012);

            public const string Category = EdgeHuntCategory.Correctness;

            [DiagnosticRule]
            public static class EH0013
            {
                public const string Id = nameof(EH0013);

                public const string Category = EdgeHuntCategory.Correctness;
            }
        }
    }

    /// <summary>The other half of <c>EH0011</c>: the category, and no marker.</summary>
    public static partial class EdgeHuntRule
    {
        public static partial class EH0011
        {
            public const string Category = EdgeHuntCategory.Correctness;
        }
    }

    /// <summary>Ten levels of nesting between the namespace and the rule.</summary>
    public static class D1
    {
        public static class D2
        {
            public static class D3
            {
                public static class D4
                {
                    public static class D5
                    {
                        public static class D6
                        {
                            public static class D7
                            {
                                public static class D8
                                {
                                    public static class D9
                                    {
                                        public static class D10
                                        {
                                            [DiagnosticRule]
                                            public static class EH0099
                                            {
                                                public const string Id = nameof(EH0099);

                                                public const string Category = EdgeHuntCategory.Layout;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>A rule nested in an interface — legal since C# 8, and reachable by the sweep.</summary>
    public interface IEdgeHuntCatalog
    {
        [DiagnosticRule]
        public static class EH0400
        {
            public const string Id = nameof(EH0400);

            public const string Category = EdgeHuntCategory.Correctness;
        }
    }

    /// <summary>A file-local rule: a real type with a mangled metadata name.</summary>
    [DiagnosticRule]
    file static class EdgeHuntFileLocalRule
    {
        public const string Id = "EH0500";

        public const string Category = EdgeHuntCategory.Correctness;
    }

    /// <summary>Consumes the file-local rule, which nothing outside this file could.</summary>
    internal static class FileLocalConsumer
    {
        [SuppressMessage(
            EdgeHuntFileLocalRule.Category,
            EdgeHuntFileLocalRule.Id,
            Justification = "The file-local rule is as real as any other.")]
        internal static int Suppressed() => 1;
    }
}

namespace EdgeHunt.Targets
{
    /// <summary>
    /// One coherent suppression on every attribute target the language offers. The use-site
    /// analyzer registers on <c>SyntaxKind.Attribute</c>, so all of them reach it; none may report.
    /// </summary>
    internal sealed class EmptyOnPurpose
    {
    }

    [return: SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a delegate's return.")]
    internal delegate int Transform(
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a delegate's parameter.")]
        int value);

    internal enum Flavour
    {
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On an enum member.")]
        Plain,

        Fancy,
    }

    internal interface IReset
    {
        void Reset();
    }

    internal sealed class TargetZoo<
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a type parameter.")]
        TItem>
        : IReset
    {
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a field.")]
        private readonly List<TItem> items = [];

        private EventHandler? changed;

        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a static constructor.")]
        static TargetZoo()
        {
        }

        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On an instance constructor.")]
        internal TargetZoo(
            [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a constructor parameter.")]
            int capacity)
        {
            Capacity = capacity;
        }

        internal event EventHandler? Changed
        {
            [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On an event adder.")]
            add => this.changed += value;

            [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On an event remover.")]
            remove => this.changed -= value;
        }

        [field: SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a backing field.")]
        internal int Capacity { get; }

        internal int Count
        {
            [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a getter.")]
            get => this.items.Count;
        }

        internal TItem this[
            [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On an indexer parameter.")]
            int index]
        {
            [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On an indexer getter.")]
            get => this.items[index];

            [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On an indexer setter.")]
            set => this.items[index] = value;
        }

        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On an operator.")]
        public static TargetZoo<TItem> operator +(TargetZoo<TItem> left, TItem right)
        {
            left.items.Add(right);

            return left;
        }

        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a conversion operator.")]
        public static explicit operator int(TargetZoo<TItem> value) => value.items.Count;

        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a generic method.")]
        [return: SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On its return value.")]
        internal TResult Map<
            [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a method type parameter.")]
            TResult>(
            [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a method parameter.")]
            Func<TItem, TResult> map) => map(this.items[0]);

        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On an explicit implementation.")]
        void IReset.Reset() => this.items.Clear();
    }

    /// <summary>A primary constructor parameter, and an auto-property initialised from it.</summary>
    internal sealed class Seeded(
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a primary constructor parameter.")]
        int seed)
    {
        internal int Seed { get; } = seed;
    }

    /// <summary>A positional record: the parameter and the property it generates.</summary>
    internal sealed record Measurement(
        [property: SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a generated property.")]
        int Value,
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a record parameter.")]
        string Unit);

    /// <summary>Bodies the compiler rewrites: an iterator, an async method, an expression tree.</summary>
    internal static class Bodies
    {
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On an iterator.")]
        internal static IEnumerable<int> Iterate()
        {
            [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a local function.")]
            static int Twice(int value) => value * 2;

            yield return Twice(1);
            yield return Twice(2);
        }

        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On an async method.")]
        internal static async Task<int> ShiftAsync()
        {
            Func<int, int> shift =
                [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a lambda.")]
                static (int value) => value + 1;

            await Task.Yield();

            return shift(1);
        }

        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "On a method returning a tree.")]
        internal static Expression<Func<int, int>> Tree() => value => value + 1;
    }
}

namespace EdgeHunt.ArgumentOrder
{
    /// <summary>
    /// Every legal ordering of the two constructor arguments, and of the properties that follow
    /// them. Slot must come from the parameter name, never from the index in the list.
    /// </summary>
    internal static class Permutations
    {
        [SuppressMessage(checkId: EhUnused.Id, category: EhUnused.Category, Justification = "Both named, reversed.")]
        internal static int Reversed() => 1;

        [SuppressMessage(category: EhUnused.Category, EhUnused.Id, Justification = "Named, then positional.")]
        internal static int NonTrailingNamed() => 2;

        [SuppressMessage(EhUnused.Category, checkId: EhUnused.Id, Justification = "Positional, then named.")]
        internal static int TrailingNamed() => 3;

        [SuppressMessage(
            EhUnused.Category,
            EhUnused.Id,
            Target = "~M:EdgeHunt.ArgumentOrder.Permutations.EveryProperty",
            MessageId = "probe",
            Scope = "member",
            Justification = "Every property, in an order nothing sorts.")]
        internal static int EveryProperty() => 4;
    }
}

namespace EdgeHunt.Spellings
{
    using static DiagnosticCatalog.Sonar.SonarRule;

    /// <summary>
    /// <c>using static</c> applied to the CONTAINER rather than to a rule: it imports every nested
    /// rule type by its simple name, and — unlike the per-rule form — several rules stay usable in
    /// one file because <c>Category</c> and <c>Id</c> are never bare.
    /// </summary>
    internal static class ViaUsingStaticContainer
    {
        [SuppressMessage(S1144.Category, S1144.Id, Justification = "Reflected over.")]
        internal static int First() => 1;

        [SuppressMessage(S2094.Category, S2094.Id, Justification = "A marker type.")]
        internal static int Second() => 2;
    }

    /// <summary>The alias forms, mixed across the two arguments of one attribute.</summary>
    internal static class ViaAliases
    {
        [SuppressMessage(EhSonar.S3776.Category, EhSonar.S3776.Id, Justification = "Alias on the container.")]
        internal static int Container() => 1;

        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "Alias on the rule.")]
        internal static int Rule() => 2;

        [SuppressMessage(
            global::DiagnosticCatalog.Sonar.SonarRule.S2094.Category,
            EhEmptyClass.Id,
            Justification = "global:: on one side, an alias to the same rule on the other.")]
        internal static int Mixed() => 3;

        [SuppressMessage(
            EhSonar.S1144.Category,
            global::DiagnosticCatalog.Sonar.SonarRule.S1144.Id,
            Justification = "A container alias on one side, a fully qualified name on the other.")]
        internal static int AlsoMixed() => 4;

        [SuppressMessage(
            EdgeHunt.Catalog.EdgeHuntRule.EH0009.Category,
            EdgeHunt.Catalog.EdgeHuntRule.EH0009.Id,
            Justification = "A category holding a colon: only the identifier is truncated at one.")]
        internal static int ColonInCategory() => 5;

        [SuppressMessage(
            EdgeHuntRule.Règle0010.Category,
            EdgeHuntRule.Règle0010.Id,
            Justification = "A rule named and categorised outside ASCII.")]
        internal static int NonAscii() => 6;

        [SuppressMessage(
            EdgeHuntRule.@operator.Category,
            EdgeHuntRule.@operator.Id,
            Justification = "A rule type named with an escaped keyword.")]
        internal static int EscapedIdentifier() => 7;

        [SuppressMessage(
            D1.D2.D3.D4.D5.D6.D7.D8.D9.D10.EH0099.Category,
            D1.D2.D3.D4.D5.D6.D7.D8.D9.D10.EH0099.Id,
            Justification = "Ten containers deep.")]
        internal static int DeeplyNested() => 8;

        [SuppressMessage(
            IEdgeHuntCatalog.EH0400.Category,
            IEdgeHuntCatalog.EH0400.Id,
            Justification = "A rule declared inside an interface.")]
        internal static int InsideAnInterface() => 9;

        [SuppressMessage(
            EdgeHuntRule.EH0012.EH0013.Category,
            EdgeHuntRule.EH0012.EH0013.Id,
            Justification = "A rule declared inside another rule.")]
        internal static int RuleInsideRule() => 10;

        [SuppressMessage(
            EdgeHuntRule.EH0011.Category,
            EdgeHuntRule.EH0011.Id,
            Justification = "A rule whose two constants come from two partial declarations.")]
        internal static int PartialRule() => 11;

        [SuppressMessage(
            EdgeHuntRule.EH0005.Category,
            EdgeHuntRule.EH0005.Id,
            Justification = "A rule whose two constants share one field declaration.")]
        internal static int OneFieldDeclaration() => 12;
    }
}

namespace EdgeHunt.BareMembers
{
    using static DiagnosticCatalog.StyleCop.StyleCopRule.SA1600;

    /// <summary>
    /// The per-rule <c>using static</c>, which leaves <c>Category</c> and <c>Id</c> bare. It is
    /// recognised and not recommended; a second one in the same scope would not compile, which is
    /// why this namespace block holds exactly one.
    /// </summary>
    internal static class Undocumented
    {
        [SuppressMessage(Category, Id, Justification = "Internal plumbing.")]
        internal static int Member() => 1;
    }
}

namespace EdgeHunt.Shadowing.Sonar
{
    /// <summary>
    /// A namespace whose last segment collides with the catalogue's, holding a type whose name
    /// collides too. Reaching the real catalogue from <c>EdgeHunt.Shadowing</c> now needs
    /// <c>global::</c> or an alias — and the analyzer must follow the symbols, not the spelling.
    /// </summary>
    public static class SonarRule
    {
        [DiagnosticRule]
        public static class EH3002
        {
            public const string Id = nameof(EH3002);

            public const string Category = EdgeHuntCategory.House;
        }
    }
}

namespace EdgeHunt.Shadowing
{
    /// <summary>An in-house rule numbered <c>S1144</c> long before this codebase met Sonar.</summary>
    public static class SonarRule
    {
        [DiagnosticRule]
        public static class S1144
        {
            public const string Id = nameof(S1144);

            public const string Category = EdgeHuntCategory.House;
        }
    }

    internal static class Shadowed
    {
        /// <summary>Binds to the house rule above: the enclosing namespace wins over the using.</summary>
        [SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "The house rule.")]
        internal static int House() => 1;

        /// <summary>The catalogue's S1144, reachable here only through the alias.</summary>
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "The vendor rule.")]
        internal static int Vendor() => 2;

        /// <summary>The catalogue's S1144 again, spelled out from the root.</summary>
        [SuppressMessage(
            global::DiagnosticCatalog.Sonar.SonarRule.S1144.Category,
            global::DiagnosticCatalog.Sonar.SonarRule.S1144.Id,
            Justification = "The vendor rule, fully qualified.")]
        internal static int VendorQualified() => 3;

        /// <summary>The rule in the shadowing namespace, one segment away.</summary>
        [SuppressMessage(
            Sonar.SonarRule.EH3002.Category,
            Sonar.SonarRule.EH3002.Id,
            Justification = "Through the shadowing namespace.")]
        internal static int ThroughShadowNamespace() => 4;
    }
}

namespace EdgeHunt.Impostor
{
    /// <summary>
    /// A consumer's own attribute, named exactly like the BCL one and taking the same two strings.
    /// It binds ahead of the imported <c>System.Diagnostics.CodeAnalysis</c> type everywhere in
    /// this namespace, and it is nobody's suppression: nothing here may be analysed, whatever the
    /// arguments say.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class SuppressMessageAttribute : Attribute
    {
        internal SuppressMessageAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }

        internal string Category { get; }

        internal string CheckId { get; }
    }

    internal static class NotASuppression
    {
        /// <summary>Literals naming a rule the compilation can see — but not in this attribute.</summary>
        [SuppressMessage("Major Code Smell", "S1144")]
        internal static int Literals() => 1;

        /// <summary>Two rules at once, which in the BCL attribute would be incoherent.</summary>
        [SuppressMessage(EhUnused.Category, EhEmptyClass.Id)]
        internal static int TwoRules() => 2;
    }
}

namespace EdgeHunt.AttributeSpelling
{
    using Suppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;
    using Unconditional = System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute;

    /// <summary>
    /// The attribute itself, spelled every way that resolves to it. <c>Identify</c> reads the
    /// constructor's containing type from the semantic model, so the short name written here must
    /// never decide which of the two attributes — or neither — is being analysed. The alias for
    /// <c>UnconditionalSuppressMessage</c> is the discriminating one: DCAT0009's guard keys on that
    /// metadata name, and it has to find it behind a name that does not contain it.
    /// </summary>
    internal static class Spelled
    {
        [Suppress(EhUnused.Category, EhUnused.Id, Justification = "Through an alias.")]
        internal static int ViaAlias() => 1;

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            EhUnused.Category,
            EhUnused.Id,
            Justification = "Fully qualified.")]
        internal static int FullyQualified() => 2;

        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute(
            EhUnused.Category,
            EhUnused.Id,
            Justification = "global:: and the Attribute suffix.")]
        internal static int GlobalWithSuffix() => 3;

        [Unconditional(
            EdgeHunt.Trimming.TrimRule.IL2026.Category,
            EdgeHunt.Trimming.TrimRule.IL2026.Id,
            Justification = "The reflected members are rooted by the trimmer descriptor.")]
        internal static void UnconditionalViaAlias()
        {
        }
    }
}

namespace EdgeHunt.Trimming
{
    /// <summary>
    /// An in-house catalogue for the linker's own warnings. ILLink has no
    /// <c>DiagnosticDescriptor</c> and therefore no category of its own, so the team picked one.
    /// </summary>
    public static class TrimRule
    {
        [DiagnosticRule]
        public static class IL2026
        {
            public const string Id = nameof(IL2026);

            public const string Category = EdgeHuntCategory.Trimming;
        }

        [DiagnosticRule]
        public static class IL2091
        {
            public const string Id = nameof(IL2091);

            public const string Category = EdgeHuntCategory.Trimming;
        }

        [DiagnosticRule]
        public static class IL3050
        {
            public const string Id = nameof(IL3050);

            public const string Category = EdgeHuntCategory.Trimming;
        }
    }

    /// <summary>
    /// <c>UnconditionalSuppressMessage</c> naming identifiers ILLink's decoder honours. DCAT0009
    /// mirrors that decoder, so every one of these must pass.
    /// </summary>
    internal static class Trimmed
    {
        [UnconditionalSuppressMessage(
            TrimRule.IL2026.Category,
            TrimRule.IL2026.Id,
            Justification = "The reflected members are rooted by the trimmer descriptor.")]
        internal static void Reflects()
        {
        }

        [UnconditionalSuppressMessage(
            checkId: TrimRule.IL3050.Id,
            category: TrimRule.IL3050.Category,
            Justification = "The generic instantiation is rooted.")]
        internal static void AheadOfTime()
        {
        }

        /// <summary>Both suppression attributes on one member, each naming its own rule.</summary>
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "Reflected over.")]
        [UnconditionalSuppressMessage(
            TrimRule.IL2091.Category,
            TrimRule.IL2091.Id,
            Justification = "The type argument is preserved.")]
        internal static void Both()
        {
        }

        /// <summary>
        /// A hand-written trim suppression with no catalogue behind it. DCAT0009 asks about rule
        /// members only, and DCAT0006 finds no rule under this pair, so both must stay quiet.
        /// </summary>
        [UnconditionalSuppressMessage(
            "ReflectionAnalysis",
            "IL2075:DynamicallyAccessedMembers",
            Justification = "The member is annotated on the other side of the call.")]
        internal static void HandWritten()
        {
        }
    }
}

namespace EdgeHunt.NearMisses
{
    /// <summary>
    /// Literal suppressions that resemble a rule in the catalogue without being one. DCAT0006 must
    /// find nothing: reporting any of these would be inventing a rule the project cannot see.
    /// </summary>
    internal static class Silent
    {
        /// <summary>A prefix of S1144, and not itself a rule the catalogue contains.</summary>
        [SuppressMessage("Major Code Smell", "S114", Justification = "An in-house checker's rule.")]
        internal static int Prefix() => 1;

        /// <summary>S1144 with a digit appended.</summary>
        [SuppressMessage("Major Code Smell", "S11440", Justification = "An in-house checker's rule.")]
        internal static int Extended() => 2;

        /// <summary>A prefix of SA1600.</summary>
        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA160", Justification = "An in-house rule.")]
        internal static int StyleCopPrefix() => 3;

        /// <summary>The right identifier under a category that differs only by case.</summary>
        [SuppressMessage("major code smell", "S1144", Justification = "Written before the catalogue existed.")]
        internal static int WrongCase() => 4;

        /// <summary>The right identifier under a category nothing declares. Deliberately unchecked.</summary>
        [SuppressMessage("Usage", "S1144", Justification = "Written before the catalogue existed.")]
        internal static int WrongCategory() => 5;

        /// <summary>A vendor this project references no catalogue for.</summary>
        [SuppressMessage("Roslynator", "RCS1001", Justification = "Braces are optional here.")]
        internal static int NoCatalogue() => 6;

        /// <summary>A suffixed identifier whose prefix names nothing known.</summary>
        [SuppressMessage("Style", "IDE0008:Use explicit type", Justification = "Generated shape.")]
        internal static int Suffixed() => 7;
    }
}

namespace EdgeHunt.Generics
{
    /// <summary>A rule reached through a constructed generic container.</summary>
    public static class Boxed<TValue>
    {
        [DiagnosticRule]
        public static class EH0600
        {
            public const string Id = nameof(EH0600);

            public const string Category = EdgeHuntCategory.Correctness;
        }
    }

    internal static class Closed
    {
        [SuppressMessage(
            Boxed<int>.EH0600.Category,
            Boxed<int>.EH0600.Id,
            Justification = "One rule, reached through one constructed container.")]
        internal static int Same() => 1;
    }

    internal sealed class Open<TValue>
    {
        [SuppressMessage(
            Boxed<TValue>.EH0600.Category,
            Boxed<TValue>.EH0600.Id,
            Justification = "One rule, reached through the enclosing type's own parameter.")]
        internal int Value => 1;
    }
}

namespace EdgeHunt.Volume
{
    /// <summary>Repetition, in the two shapes the language allows.</summary>
    internal static class Repeated
    {
        /// <summary>The same rule twice, with different justifications.</summary>
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "The serializer reads it.")]
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "So does the test host.")]
        internal static int Twice() => 1;

        /// <summary>The same attribute twice, argument for argument.</summary>
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "Merged from two files.")]
        [SuppressMessage(EhUnused.Category, EhUnused.Id, Justification = "Merged from two files.")]
        internal static int Identical() => 2;

        /// <summary>Two vendors on one member, in one list and in two.</summary>
        [SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "Sonar."),
         SuppressMessage(StyleCopRule.SA1101.Category, StyleCopRule.SA1101.Id, Justification = "StyleCop.")]
        [SuppressMessage(NetAnalyzersRule.CA1822.Category, NetAnalyzersRule.CA1822.Id, Justification = "CA.")]
        internal static int ThreeVendors() => 3;
    }

    /// <summary>
    /// Twenty-nine suppressions in a single attribute list, each naming a different rule. Nothing
    /// in the analyzer batches per member; this is here so that nothing starts to.
    /// </summary>
    [
        SuppressMessage(SonarRule.S100.Category, SonarRule.S100.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S101.Category, SonarRule.S101.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S103.Category, SonarRule.S103.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S104.Category, SonarRule.S104.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S105.Category, SonarRule.S105.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S106.Category, SonarRule.S106.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S107.Category, SonarRule.S107.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S108.Category, SonarRule.S108.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S109.Category, SonarRule.S109.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S110.Category, SonarRule.S110.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S112.Category, SonarRule.S112.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S113.Category, SonarRule.S113.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1006.Category, SonarRule.S1006.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1048.Category, SonarRule.S1048.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1066.Category, SonarRule.S1066.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1067.Category, SonarRule.S1067.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1075.Category, SonarRule.S1075.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1104.Category, SonarRule.S1104.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1109.Category, SonarRule.S1109.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1110.Category, SonarRule.S1110.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1116.Category, SonarRule.S1116.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1117.Category, SonarRule.S1117.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1118.Category, SonarRule.S1118.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1121.Category, SonarRule.S1121.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1123.Category, SonarRule.S1123.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1125.Category, SonarRule.S1125.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1128.Category, SonarRule.S1128.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1133.Category, SonarRule.S1133.Id, Justification = "Bulk."),
        SuppressMessage(SonarRule.S1134.Category, SonarRule.S1134.Id, Justification = "Bulk.")
    ]
    internal static class Bulk
    {
        internal static int Count => 29;
    }
}

namespace EdgeHunt.Indirection
{
    /// <summary>
    /// The pair, factored out once so that fifty members do not repeat it. Both constants are
    /// initialised FROM the catalogue, so a renamed or retired rule still breaks the build — the
    /// property the contract is about.
    /// </summary>
    internal static class ReflectionSuppressions
    {
        internal const string Category = SonarRule.S1144.Category;

        internal const string Id = SonarRule.S1144.Id;
    }

    /// <summary>
    /// Forms whose ARGUMENT EXPRESSION is not a member access on a rule type, although the value
    /// it carries comes from one. <c>SuppressionAttribute.Resolve</c> asks
    /// <c>GetSymbolInfo</c> for a field on a rule type and falls back to the folded constant, so
    /// these land in the same bucket a hand-written literal does.
    ///
    /// The intermediate-constant form is listed as an ACCEPTED use-site form in
    /// doc/guide/rule-contract.en.md ("An intermediate constant — checkable, contrary to first
    /// reading"). It is written here on that basis.
    /// </summary>
    // -----------------------------------------------------------------------------------------
    // SETTLED. These sites were OPEN FINDINGS, kept because they were reported. Both are now
    // reported by nothing, and the pragma that made the branch buildable is gone.
    //
    // OneSide / BothSides — the intermediate constant. Resolve now follows one hop into a
    // constant's initialiser, so a rule member hoisted into a named constant resolves to its rule
    // rather than being compared as a literal. Not following it FROM a rule member is still
    // deliberate and still right: a rule writes Category = SonarCategory.MajorCodeSmell, and
    // walking through would make DCAT0001 fire on every catalogue this repository ships. The hop
    // is exactly one, and only from a declaring type that is not itself a rule.
    //
    // NamedCategoryConstant — settled the other way, and more sharply. The category container is
    // now internal (ADR-0026), so naming a category apart from its rule is CS0122 rather than a
    // diagnostic. The case is written out in prose below rather than exercised, because a suite
    // whose build IS the assertion cannot hold source the compiler refuses.
    //
    // Both were found by writing what a consumer would plausibly write and letting the build
    // answer. Neither was reachable from the analyzer's own unit tests, which assert what the
    // analyzer does rather than what a reader of the guide would try.
    // -----------------------------------------------------------------------------------------

    internal static class Factored
    {
        /// <summary>One side factored, the other written out.</summary>
        [SuppressMessage(
            SonarRule.S1144.Category,
            ReflectionSuppressions.Id,
            Justification = "The reflection host reaches this member.")]
        internal static int OneSide() => 1;

        /// <summary>Both sides factored.</summary>
        [SuppressMessage(
            ReflectionSuppressions.Category,
            ReflectionSuppressions.Id,
            Justification = "The reflection host reaches this member.")]
        internal static int BothSides() => 2;

        /// <summary>
        /// A parenthesised member access: the same symbol, the same folded value, the same emitted
        /// attribute. Only the syntax node in between is different.
        /// </summary>
        [SuppressMessage(
            (SonarRule.S2094.Category),
            SonarRule.S2094.Id,
            Justification = "A marker type carries no members by design.")]
        internal static int Parenthesised() => 3;

        /// <summary>
        /// The category named on its own — <c>SonarCategory.MajorCodeSmell</c> beside
        /// <c>SonarRule.S1144.Id</c> — is deliberately ABSENT, and its absence is the finding.
        /// </summary>
        /// <remarks>
        /// This suite wrote it as legitimate consumer code, because it reads like one: two catalogue
        /// references, no literal, and an IDE completion list offers it. The build failed, and the
        /// investigation that followed is
        /// <see href="../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md">ADR-0026</see>:
        /// the two spellings fold to the same string until the vendor moves the rule, after which the
        /// rule member follows and the category named alone does not — the suppression keeps compiling
        /// and silently stops matching.
        ///
        /// The container is now <c>internal</c>, so writing it here is <c>CS0122</c> rather than a
        /// diagnostic. It cannot be exercised by a suite whose build IS the assertion: the file would
        /// not compile, and a form the compiler refuses needs no coverage from us. The case is left
        /// written out in prose rather than deleted, so the next reader learns the form was tried and
        /// why it is gone, instead of finding a gap.
        /// </remarks>
        internal static int NamedCategoryConstantIsUnwritable() => 4;
    }

}

/// <summary>
/// A rule in the global namespace: no containing namespace at all, which is the one shape the
/// sweep's namespace walk and the fix's namespace arithmetic both special-case.
/// </summary>
/// <summary>
/// Its category container, also in the global namespace: the marker is matched by metadata name and
/// the container is found by symbol, so neither cares that there is no namespace to walk.
/// </summary>
[DiagnosticCategory]
public static class EdgeHuntGlobalCategory
{
    public const string Global = "EdgeHunt Global";
}

[DiagnosticRule]
public static class EdgeHuntGlobalRule
{
    public const string Id = nameof(EdgeHuntGlobalRule);

    public const string Category = EdgeHuntGlobalCategory.Global;
}
