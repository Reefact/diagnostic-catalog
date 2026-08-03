// GeneratedShapes.cs -- C# that nobody laid out by hand.
//
// Source generators, T4 templates, scaffolding and decade-old migrations emit code no style guide
// ever touched. An analyzer walking syntax trees meets those shapes in the wild long before it meets
// them in a fixture, so this file is the fixture: every suppression below is one a generator could
// plausibly emit, and every one of them is coherent. The whole file must stay DCAT-silent.
//
// Deliberately absent: an auto-generated banner at the top of the file. Roslyn reads that as "this
// whole tree is generated", and SuppressionUsageAnalyzer runs with GeneratedCodeAnalysisFlags.None --
// the file would be skipped entire and assert nothing at all.

using System;                                                                //
using System.CodeDom.Compiler;                                               //
using System.Diagnostics;                                                    //
using System.Diagnostics.CodeAnalysis;                                       //
using System.Globalization;                                                  //
using System.Runtime.CompilerServices;                                       //
using DiagnosticCatalog;                                                     //
using DiagnosticCatalog.NetAnalyzers;                                        //
using DiagnosticCatalog.Sonar;                                               //
using DiagnosticCatalog.StyleCop;                                            //
using Suppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;   //
using UnusedPrivateMember = DiagnosticCatalog.Sonar.SonarRule.S1144;         //

// Imitates GlobalSuppressions.cs: assembly scope, a Scope/Target pair, and the named property
// arguments trailing the two positional ones that carry the rule.
[assembly: SuppressMessage(
    SonarRule.S3963.Category,
    SonarRule.S3963.Id,
    Justification = "The template writes its initialisers inline; it has no static constructor to move them into.",
    Scope = "namespaceanddescendants",
    Target = "~N:DiagnosticCatalog.Usage.MachineWritten")]

namespace DiagnosticCatalog.Usage.MachineWritten
{
    // Imitates a template whose whole output is one line, because its emitter never writes a newline.
    internal static class OneEnormousLine { [Suppress(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "Invoked through the host's reflection entry point.")] private static int Seed() { return 42; } [Suppress(UnusedPrivateMember.Category, UnusedPrivateMember.Id, Justification = "Same, through the alias the template writes when the container name is long.")] private static int Salt() { return 7; } internal static int Use() { return Seed() + Salt(); } }

// Imitates an emitter that writes one token per line because it appends a newline after every write.
internal
static
class
OneTokenPerLine
{
[
Suppress
(
SonarRule
.
S1121
.
Category
,
SonarRule
.
S1121
.
Id
,
Justification
=
"The template has no statement slot at that point, so the assignment folds into the expression."
)
]
internal
static
int
Fold
(
int
seed
)
{
int
step
;
return
(
step
=
seed
+
1
)
+
step
;
}
}

// Imitates output with no layout pass at all: column zero, then tabs and spaces mixed line by line.
internal static class NoIndentation
{
[Suppress(StyleCopRule.SA1600.Category, StyleCopRule.SA1600.Id, Justification = "Emitted members carry no documentation comments.")]
internal static string Name => "no-indent";
	[Suppress(StyleCopRule.SA1309.Category, StyleCopRule.SA1309.Id, Justification = "The template names its backing fields the way the model names its columns.")]
 	private static readonly string _value = "mixed-indentation";
 		internal static string Value => _value;
	[Suppress(StyleCopRule.SA1402.Category, StyleCopRule.SA1402.Id, Justification = "One emitted file per model, whatever the number of types in it.")]
internal static class AlsoInThisFile
{
 	internal static string Marker => "second-type";
}
}

    // Imitates a generator that emits its own nullable context around each member and toggles mid-type.
#nullable disable
    internal static class NullableToggledMidType
    {
        [Suppress(SonarRule.S1172.Category, SonarRule.S1172.Id, Justification = "The signature is fixed by the template's contract; the second parameter holds the overload set together.")]
        internal static string Format(string value, string reservedByTheTemplate) => value;

#nullable enable
        [Suppress(NetAnalyzersRule.CA1062.Category, NetAnalyzersRule.CA1062.Id, Justification = "The context the template re-enables here already carries the check.")]
        internal static string? Passthrough(string? value) => value;
#nullable restore

        internal static string Plain(string value) => value;
    }
#nullable restore

    // Imitates pragmas that cross rather than nest: the compiler suppressions open before the catalogue
    // suppression and close after it, in the other order.
#pragma warning disable CS0618
    [Suppress(SonarRule.S1133.Category, SonarRule.S1133.Id, Justification = "The deprecated overload stays until the next major version of the model.")]
#pragma warning disable CS0619
    internal static class DeprecatedSurface
    {
        [Obsolete("Use Render(string, IFormatProvider) instead.")]
        internal static string Render(string value) => value;

        internal static string RenderCompat(string value) => Render(value);
    }
#pragma warning restore CS0619
#pragma warning restore CS0618

    // Imitates a generator that still emits the branch for frameworks it used to support. The
    // suppressions below sit in branches that are NOT compiled: they are disabled text, and the pair
    // each of them names deliberately matches no rule in any catalogue, compiled or not.
#if NET10_0_OR_GREATER
    [Suppress(NetAnalyzersRule.CA1416.Category, NetAnalyzersRule.CA1416.Id, Justification = "The guarded call is emitted only for the platforms the model declares.")]
#elif NETSTANDARD2_0
    [SuppressMessage("Microsoft.Portability", "CA1900:ValueTypeFieldsShouldBePortable")]
#else
    [SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes")]
#endif
    internal static class PlatformBranch
    {
        internal static string Platform => "net10.0";
    }

    // Imitates a template that picks one attribute of a list per branch, from inside the brackets.
    [
#if NET10_0_OR_GREATER
        Suppress(SonarRule.S4136.Category, SonarRule.S4136.Id, Justification = "Overloads are emitted in model order, not in adjacency order.")
#else
        SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")
#endif
    ]
    internal static class OverloadOrder
    {
        internal static string Render(string value) => value;

        internal static string Render(int value, string format) => value.ToString(format, CultureInfo.InvariantCulture);
    }

    // Imitates T4: the emitted file maps its lines back onto the template that produced them, and hides
    // the plumbing from the debugger.
#line 118 "Templates/Widget.tt"
    [Suppress(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "Called from the template's runtime, not from the emitted surface.")]
    internal static class LineMapped
    {
        private static int Seed() => 7;

        internal static int Use() => Seed();
    }
#line default
#line hidden
    internal static class HiddenPlumbing
    {
        internal static int Zero => 0;
    }
#line default

    #region Emitted members -- do not edit

    [GeneratedCode("DiagnosticCatalog.Usage.Templates", "1.0.0.0")]

    // The template writes its provenance comment between the attributes and the member they decorate,
    // one blank line per branch it took. Note that GeneratedCode is what switches the use-site analyzer
    // off for this type, so what is proved here is the layout parsing, not the analysis.

    [DebuggerNonUserCode]
    /* a block comment too, because there is one emitter branch per attribute */
    [Suppress(StyleCopRule.SA1600.Category, StyleCopRule.SA1600.Id, Justification = "Emitted members carry no documentation comments.")]

    [CompilerGenerated]
    internal static class DecoratedFromEveryAngle
    {
        internal static string Marker => "decorated";
    }

    #endregion

    // Imitates a template that annotates every argument it writes and closes every line with a comment.
    [Suppress( /* category */ SonarRule.S107.Category,   // too many parameters
               /* checkId  */ SonarRule.S107.Id,         // the identifier
               Justification =                           // the reason
                   "The parameter list mirrors the model's columns one for one." /* verbatim */ )] //
    internal static class WideSignature
    {
        internal static string Row( //
            string a, //
            string b, //
            string c, //
            string d, //
            string e, //
            string f, //
            string g, //
            string h) => //
            a + b + c + d + e + f + g + h; //
    }

    // Imitates justifications built the four ways an emitter builds them: constant concatenation from
    // fragments, a verbatim path, escape sequences, and a raw literal quoting the model.
    internal static class Justifications
    {
        [Suppress(
            SonarRule.S1121.Category,
            SonarRule.S1121.Id,
            Justification = "The assignment inside the expression is what the template emits for a fold: " +
                            "rewriting it would need a statement, and the template has no statement slot " +
                            "at that point in the emission. This reason is written as a concatenation " +
                            "because the template assembles it from three separate fragments -- the " +
                            "rule's own wording, the kind of the node being emitted, and the model " +
                            "element it came from -- and the compiler folds all three into one literal " +
                            "before the attribute ever sees them, which is the only reason a " +
                            "concatenation is legal in an attribute argument at all.")]
        internal static int Fold(int seed)
        {
            int step;

            return (step = seed + 1) + step;
        }

        [Suppress(
            NetAnalyzersRule.CA1707.Category,
            NetAnalyzersRule.CA1707.Id,
            Justification = @"Emitted from ..\..\templates\Row.tt, where the column named ""order_id"" carries the underscore.")]
        internal static string Order_Id => "order_id";

        [Suppress(
            StyleCopRule.SA1633.Category,
            StyleCopRule.SA1633.Id,
            Justification = "Ligne g\u00e9n\u00e9r\u00e9e\u00a0: the header is written by the tool.\tTab, \"quote\" and \\ backslash all survive the escape pass.")]
        internal static string Header => "generated";

        [Suppress(
            NetAnalyzersRule.CA1305.Category,
            NetAnalyzersRule.CA1305.Id,
            Justification = """
                            The template pins CultureInfo.InvariantCulture into the call it emits, so the
                            "provider" this rule asks for is decided at generation time. Raw literal
                            because the reason quotes the model verbatim: {"culture": "invariant"}.
                            """)]
        internal static string Amount(int value) => value.ToString("D4", CultureInfo.InvariantCulture);
    }

    // Imitates an emitter that writes constructor arguments by name in model order rather than in
    // parameter order, and packs several attributes into a single bracket set.
    [Suppress(checkId: SonarRule.S2094.Id, category: SonarRule.S2094.Category, Justification = "Marker type; its members are emitted into a second partial file."),
     Suppress(checkId: UnusedPrivateMember.Id, category: UnusedPrivateMember.Category, Justification = "Reflected over by the host.")]
    internal static class ArgumentsOutOfOrder
    {
        private static int Seed() => 3;

        internal static int Use() => Seed();
    }

    // Imitates scaffolding that spells the attribute target out rather than relying on the default.
    internal static class ExplicitTargets
    {
        [return: Suppress(NetAnalyzersRule.CA1861.Category, NetAnalyzersRule.CA1861.Id, Justification = "The constant table is emitted at the call site by design.")]
        internal static int[] Table() => new[] { 1, 2, 3 };

        [field: Suppress(NetAnalyzersRule.CA2211.Category, NetAnalyzersRule.CA2211.Id, Justification = "The backing field is compiler-generated and never leaves the property.")]
        internal static string Name { get; } = "explicit-targets";
    }

    // Imitates AOT and interop scaffolding: the trimmer's own attribute, with the identifier written the
    // way the trimmer's decoder reads it -- IL####, friendly-name suffix and all. No catalogue in this
    // compilation describes IL2075, so there is nothing for the literals to be migrated to.
    internal static class TrimSurface
    {
        [UnconditionalSuppressMessage("Trimming", "IL2075:DynamicallyAccessedMembers", Justification = "Every type walked here is emitted into this same assembly by the same template, so its members are rooted.")]
        internal static int CountProperties(Type type) => type.GetProperties().Length;
    }

    // Imitates a catalogue emitted next to the code that uses it: its own category class, a rule split
    // across two partial declarations, and a rule buried three container types deep whose identifier is
    // not a legal C# identifier and so cannot be nameof'd.
    public static class MachineWrittenCatalog
    {
        [DiagnosticCategory]
        public static class MachineWrittenCategory
        {
            public const string Emitted = "Emitted";
        }

        [DiagnosticRule]
        public static partial class MW0001
        {
            public const string Id = nameof(MW0001);
        }

        public static partial class MW0001
        {
            public const string Category = MachineWrittenCategory.Emitted;
        }

        public static class Model
        {
            public static class Column
            {
                public static class Constraints
                {
                    // DCAT0005 is expected here and cannot be cleared: the identifier carries a character C#
                    // forbids, so this name is already the closest one there is. Waived at the site rather than
                    // in .editorconfig, so the next declaration like it is met by a reader instead of by silence.
                    #pragma warning disable DCAT0005
                    [DiagnosticRule]
                    public static class MW0002
                    {
                        public const string Id = "MW-0002";

                        public const string Category = MachineWrittenCategory.Emitted;
                    }
                    #pragma warning restore DCAT0005
                }
            }
        }
    }

    // Imitates the emitted code that consumes the emitted catalogue, both halves from the same rule.
    internal static class UsesTheEmittedCatalogue
    {
        [Suppress(MachineWrittenCatalog.MW0001.Category, MachineWrittenCatalog.MW0001.Id, Justification = "The partial halves of the rule are emitted in the order the model lists them.")]
        internal static string Split => "partial";

        [Suppress(
            MachineWrittenCatalog.Model.Column.Constraints.MW0002.Category,
            MachineWrittenCatalog.Model.Column.Constraints.MW0002.Id,
            Justification = "The identifier carries a hyphen, so the type name and the identifier differ on purpose.")]
        internal static string Nested => "deep";
    }
}

namespace DiagnosticCatalog.Usage.MachineWritten
{
    // Imitates the one-rule-per-file shape: a second namespace body in the same file, scoping its own
    // using static so the bare Category and Id cannot collide with the block above.
    using static DiagnosticCatalog.Sonar.SonarRule.S3459;

    [Suppress(Category, Id, Justification = "Assigned by the deserializer the template emits beside the type.")]
    internal static class UsingStaticScopedToItsOwnBlock
    {
        internal static string Payload => "unassigned-by-design";
    }
}

namespace DiagnosticCatalog.Usage.MachineWritten
{
    namespace Emitted
    {
        namespace Model
        {
            namespace V1
            {
                internal static class Node
                {
                    [Suppress(SonarRule.S3459.Category, SonarRule.S3459.Id, Justification = "Populated by the deserializer emitted alongside it.")]
                    internal static string Payload => _payload;

                    private static readonly string _payload = string.Empty;
                }

                namespace Detail
                {
                    internal static class Leaf
                    {
                        [Suppress(DiagnosticCatalog.Sonar.SonarRule.S2094.Category, DiagnosticCatalog.Sonar.SonarRule.S2094.Id, Justification = "The leaf carries no members until the model declares one.")]
                        internal static class Empty
                        {
                        }
                    }
                }
            }
        }
    }
}
