// One question, asked in as many spellings as the language allows: does the analyzer still see the
// same two fields?
//
// Every suppression in this file references a catalogue rule on BOTH halves of its pair, so every
// one of them is a suppression the analyzers should pass over in silence. What varies is only how
// it is written — the attribute's name, the path to the constants, the order of the arguments, the
// kind of declaration the attribute sits on. A DCAT reported here is a spelling the analyzer lost
// the thread on, not a suppression anybody needs to change.

global using SyntaxFormsSuppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

// Assembly level, with Scope and Target, fully qualified and with no using directive in sight.
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
    DiagnosticCatalog.Sonar.SonarRule.S3925.Category,
    DiagnosticCatalog.Sonar.SonarRule.S3925.Id,
    Justification = "Nothing in this assembly crosses a boundary that deserialises it.",
    Scope = "type",
    Target = "~T:DiagnosticCatalog.Usage.SyntaxForms.AssemblyScoped")]

// Module level, through the global alias declared above, and with a trailing comma in the section.
[module: SyntaxFormsSuppress(
    DiagnosticCatalog.StyleCop.StyleCopRule.SA1633.Category,
    DiagnosticCatalog.StyleCop.StyleCopRule.SA1633.Id,
    Justification = "The licence header is applied by the packaging step, not by hand."),]

namespace DiagnosticCatalog.Usage.SyntaxForms;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using DiagnosticCatalog.NetAnalyzers;
using DiagnosticCatalog.Self;
using DiagnosticCatalog.Sonar;
using DiagnosticCatalog.StyleCop;

// The attribute itself: two type aliases (one with the conventional suffix, one without) and a
// namespace alias.
using Suppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;
using TerseAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;
using Cas = System.Diagnostics.CodeAnalysis;

// The catalogue: a namespace alias, a container alias, a rule alias, and a rule alias whose name
// has to be escaped to be written at all.
using SonarNs = DiagnosticCatalog.Sonar;
using SonarRules = DiagnosticCatalog.Sonar.SonarRule;
using UnusedPrivateMember = DiagnosticCatalog.Sonar.SonarRule.S1144;
using @event = DiagnosticCatalog.Sonar.SonarRule.S2325;

// The marker, aliased, for the house catalogue at the bottom of the file.
using TeamRuleAttribute = DiagnosticCatalog.DiagnosticRuleAttribute;

// using static, in both shapes it comes in: the rule itself (bare Category and Id — one rule per
// file, which is why the guide recognises it without recommending it), and the container, which
// puts the rule names in scope and has no such limit.
using static DiagnosticCatalog.Sonar.SonarRule.S3925;
using static DiagnosticCatalog.NetAnalyzers.NetAnalyzersRule;
using static DiagnosticCatalog.StyleCop.StyleCopRule;

/// <summary>The type the assembly-level suppression at the top of this file targets.</summary>
internal sealed class AssemblyScoped
{
    internal static string Moniker => nameof(AssemblyScoped);
}

/// <summary>The short attribute name, and the same attribute written out in full.</summary>
internal static class ShortAndFullAttributeName
{
    [SuppressMessage(
        SonarRule.S1144.Category,
        SonarRule.S1144.Id,
        Justification = "Reflected over by the fixture loader.")]
    internal static int Seed() => 17;

    [SuppressMessageAttribute(
        SonarRule.S4487.Category,
        SonarRule.S4487.Id,
        Justification = "Written by the deserialiser and read by nothing else.")]
    internal static int Version => 3;
}

/// <summary>No using directive: the attribute spelled out, with and without the suffix.</summary>
internal static class FullyQualifiedAttributeName
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        SonarRule.S1172.Category,
        SonarRule.S1172.Id,
        Justification = "The signature is imposed by the callback contract.")]
    internal static int Ignore(int unused) => 0;

    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute(
        SonarRule.S125.Category,
        SonarRule.S125.Id,
        Justification = "The block quotes the wire format it parses.")]
    internal static string Format() => "0x0A 0x0D";

    [global::System.Diagnostics.CodeAnalysis.SuppressMessage(
        global::DiagnosticCatalog.Sonar.SonarRule.S107.Category,
        global::DiagnosticCatalog.Sonar.SonarRule.S107.Id,
        Justification = "The parameters mirror the columns of the source table.")]
    internal static int Row(int a, int b, int c, int d, int e, int f, int g, int h) =>
        a + b + c + d + e + f + g + h;
}

/// <summary>The attribute reached through an alias — four of them, one per shape.</summary>
internal static class AliasedAttributeName
{
    /// <summary>An alias without the conventional suffix.</summary>
    [Suppress(
        NetAnalyzersRule.CA1822.Category,
        NetAnalyzersRule.CA1822.Id,
        Justification = "The instance shape is required by the plugin contract.")]
    internal static int Weight() => 1;

    /// <summary>An alias WITH the suffix, used without it — the attribute-name shorthand.</summary>
    [Terse(
        NetAnalyzersRule.CA1054.Category,
        NetAnalyzersRule.CA1054.Id,
        Justification = "The configuration file stores the endpoint as text.")]
    internal static string Endpoint => "https://example.invalid/hook";

    /// <summary>The global alias declared at the top of this file.</summary>
    [SyntaxFormsSuppress(
        NetAnalyzersRule.CA1051.Category,
        NetAnalyzersRule.CA1051.Id,
        Justification = "The interop layout requires visible fields.")]
    internal static int Slot() => 0;

    /// <summary>A namespace alias, dotted.</summary>
    [Cas.SuppressMessage(
        NetAnalyzersRule.CA2211.Category,
        NetAnalyzersRule.CA2211.Id,
        Justification = "The switch is set once by the host before anything reads it.")]
    internal static int Toggle() => 1;

    /// <summary>The same namespace alias, through the alias qualifier.</summary>
    [Cas::SuppressMessage(
        NetAnalyzersRule.CA1034.Category,
        NetAnalyzersRule.CA1034.Id,
        Justification = "The nested type is the options bag of the type that contains it.")]
    internal static int Nested() => 2;
}

/// <summary>The catalogue reached through every path that leads to the same two fields.</summary>
internal static class AliasedCatalogueReference
{
    /// <summary>A namespace alias, dotted.</summary>
    [SuppressMessage(
        SonarNs.SonarRule.S1481.Category,
        SonarNs.SonarRule.S1481.Id,
        Justification = "The local names the tuple element the reader needs.")]
    internal static int Bound() => 4;

    /// <summary>The same namespace alias, through the alias qualifier.</summary>
    [SuppressMessage(
        SonarNs::SonarRule.S3776.Category,
        SonarNs::SonarRule.S3776.Id,
        Justification = "The branch table is generated and reads better in one place.")]
    internal static int Dispatch() => 5;

    /// <summary>An alias to the container type.</summary>
    [SuppressMessage(
        SonarRules.S2094.Category,
        SonarRules.S2094.Id,
        Justification = "The empty type is a marker the serialiser looks for.")]
    internal static int Marker() => 6;

    /// <summary>An alias to the rule type — the form the guide recommends for long paths.</summary>
    [SuppressMessage(
        UnusedPrivateMember.Category,
        UnusedPrivateMember.Id,
        Justification = "Kept as documentation of the previous encoding.")]
    internal static int Legacy() => 7;

    /// <summary>Fully qualified, with the global alias qualifier in front.</summary>
    [SuppressMessage(
        global::DiagnosticCatalog.StyleCop.StyleCopRule.SA1309.Category,
        global::DiagnosticCatalog.StyleCop.StyleCopRule.SA1309.Id,
        Justification = "The underscore prefix is the house convention for backing fields.")]
    internal static int Field() => 8;

    /// <summary>Line breaks and spaces inside the member-access path itself.</summary>
    [SuppressMessage(
        SonarRule
            . S1075
            . Category,
        SonarRule . S1075 . Id,
        Justification = "The URI is the vendor's documented well-known address.")]
    internal static string WellKnown => "https://example.invalid/.well-known/keys";
}

/// <summary>What <c>using static</c> puts in scope: bare members, and bare rule names.</summary>
internal static class StaticallyImportedReference
{
    /// <summary>The rule imported statically, so its members have no qualifier at all.</summary>
    [SuppressMessage(
        Category,
        Id,
        Justification = "The type is never round-tripped through a formatter.")]
    internal static int Serialisable() => 9;

    /// <summary>The container imported statically, so the rule name has no qualifier.</summary>
    [SuppressMessage(
        CA1000.Category,
        CA1000.Id,
        Justification = "The factory is discovered through the generic type itself.")]
    internal static int Factory() => 10;

    /// <summary>The same, from the other statically imported container.</summary>
    [SuppressMessage(
        SA1101.Category,
        SA1101.Id,
        Justification = "The house style omits the prefix.")]
    internal static int Prefix() => 11;
}

/// <summary>Redundant parentheses around each half. Legal, and still the same two fields.</summary>
internal static class ParenthesisedReference
{
    [SuppressMessage(
        (SonarRule.S2325.Category),
        (SonarRule.S2325.Id),
        Justification = "The method is an override point subclasses are expected to use.")]
    internal static int Overridable() => 12;
}

/// <summary>Verbatim identifiers: on the path, on the alias, and on the declaration itself.</summary>
internal static class VerbatimSpelling
{
    /// <summary>An <c>@</c> in front of identifiers that never needed one.</summary>
    [SuppressMessage(
        @SonarRule.@S1104.@Category,
        @SonarRule.@S1104.@Id,
        Justification = "The record type exposes its data by design.")]
    internal static int Exposed() => 13;

    /// <summary>An alias whose own name is a keyword.</summary>
    [SuppressMessage(
        @event.Category,
        @event.Id,
        Justification = "The handler is registered by reflection and must stay an instance method.")]
    internal static int Handler() => 14;
}

/// <summary>A declaration whose every name has to be escaped to be written.</summary>
[SuppressMessage(
    SonarRule.S101.Category,
    SonarRule.S101.Id,
    Justification = "The name is fixed by the schema this type mirrors.")]
internal static class @class
{
    [SuppressMessage(
        SonarRule.S100.Category,
        SonarRule.S100.Id,
        Justification = "The name is fixed by the schema this type mirrors.")]
    internal static int @int => 15;

    [SuppressMessage(
        StyleCopRule.SA1300.Category,
        StyleCopRule.SA1300.Id,
        Justification = "The name is fixed by the schema this type mirrors.")]
    internal static int @return() => 16;
}

/// <summary>The two constructor parameters, named and reordered every way C# permits.</summary>
internal static class ArgumentOrder
{
    /// <summary>Both named, in declaration order.</summary>
    [SuppressMessage(
        category: SonarRule.S1172.Category,
        checkId: SonarRule.S1172.Id,
        Justification = "The signature is imposed by the event contract.")]
    internal static int InOrder() => 17;

    /// <summary>Both named, reversed — the identifier written first.</summary>
    [SuppressMessage(
        checkId: SonarRule.S2094.Id,
        category: SonarRule.S2094.Category,
        Justification = "The empty type is a marker the serialiser looks for.")]
    internal static int Reversed() => 18;

    /// <summary>Positional category, named identifier.</summary>
    [SuppressMessage(
        SonarRule.S125.Category,
        checkId: SonarRule.S125.Id,
        Justification = "The block quotes the wire format it parses.")]
    internal static int HalfNamed() => 19;

    /// <summary>Named category in its own position, then a positional identifier.</summary>
    [SuppressMessage(
        category: SonarRule.S1481.Category,
        SonarRule.S1481.Id,
        Justification = "The local names the tuple element the reader needs.")]
    internal static int NamedThenPositional() => 20;

    /// <summary>Reversed, with the properties trailing behind the reversal.</summary>
    [SuppressMessage(
        checkId: NetAnalyzersRule.CA2007.Id,
        category: NetAnalyzersRule.CA2007.Category,
        Justification = "The application has no synchronisation context to capture.",
        MessageId = "await")]
    internal static int Awaited() => 21;
}

/// <summary>What can be written in the properties beside the pair.</summary>
internal static class ArgumentDecoration
{
    private const string Loader = "DiagnosticCatalog.Usage.SyntaxForms.ArgumentDecoration";

    /// <summary>The smallest attribute the analyzers leave alone: the pair, and a reason.</summary>
    // The pair on its own used to sit here. DCAT0014 reports it now — what is suppressed was checked
    // and why it is suppressed was not written down — so the bare form belongs in the unit tests,
    // where the expectation can be stated, and this keeps the floor beneath it.
    [SuppressMessage(SonarRule.S4487.Category, SonarRule.S4487.Id, Justification = "Read by the debugger only.")]
    internal static int Bare() => 22;

    /// <summary>A justification built with <c>nameof</c>.</summary>
    [SuppressMessage(
        SonarRule.S1144.Category,
        SonarRule.S1144.Id,
        Justification = "Invoked by name from " + nameof(Bare) + "'s caller.")]
    internal static int Named() => 23;

    /// <summary>A justification that is a constant interpolated string.</summary>
    [SuppressMessage(
        NetAnalyzersRule.CA1062.Category,
        NetAnalyzersRule.CA1062.Id,
        Justification = $"Every caller is inside {Loader}, which validates first.")]
    internal static int Interpolated() => 24;

    /// <summary>A verbatim justification.</summary>
    [SuppressMessage(
        StyleCopRule.SA1201.Category,
        StyleCopRule.SA1201.Id,
        Justification = @"The members are grouped by the workflow step they belong to.")]
    internal static int Verbatim() => 25;

    /// <summary>A raw string justification.</summary>
    [SuppressMessage(
        StyleCopRule.SA1402.Category,
        StyleCopRule.SA1402.Id,
        Justification = """The types are one unit: splitting the file would separate them.""")]
    internal static int Raw() => 26;

    /// <summary>Every property the attribute has, all at once.</summary>
    [SuppressMessage(
        DcatRule.DCAT0006.Category,
        DcatRule.DCAT0006.Id,
        Justification = "The literals name a rule from a vendor with no catalogue here.",
        MessageId = "literal",
        Scope = "member",
        Target = "~M:DiagnosticCatalog.Usage.SyntaxForms.ArgumentDecoration.Everything")]
    internal static int Everything() => 27;
}

/// <summary>Comments and line breaks in the places an argument list allows them.</summary>
internal static class CommentedArguments
{
    [SuppressMessage(
        /* category */ SonarRule.S3776.Category,
        /* checkId  */ SonarRule.S3776.Id,
        Justification = "The state machine reads better as one method.")]
    internal static int Inline() => 28;

    [SuppressMessage(
        SonarRule.S107.Category,     // the category half
        SonarRule.S107.Id,           // the identifier half
        Justification = "The parameters mirror the columns of the source table.")]
    internal static int Trailing() => 29;

    [
        SuppressMessage(
            NetAnalyzersRule.CA1815.Category,
            NetAnalyzersRule.CA1815.Id,
            Justification = "The struct is never compared.")
    ]
    internal static int Spread() => 30;
}

/// <summary>How the attribute sections themselves are arranged.</summary>
internal static class AttributeListShapes
{
    /// <summary>Two suppressions in one section, with a trailing comma.</summary>
    [SuppressMessage(
         NetAnalyzersRule.CA1062.Category,
         NetAnalyzersRule.CA1062.Id,
         Justification = "The caller is generated and never passes null."),
     SuppressMessage(
         NetAnalyzersRule.CA1054.Category,
         NetAnalyzersRule.CA1054.Id,
         Justification = "The configuration file stores the endpoint as text."),]
    internal static int OneSection(string endpoint) => endpoint.Length;

    /// <summary>The same two, stacked as separate sections.</summary>
    [SuppressMessage(
        NetAnalyzersRule.CA1062.Category,
        NetAnalyzersRule.CA1062.Id,
        Justification = "The caller is generated and never passes null.")]
    [SuppressMessage(
        NetAnalyzersRule.CA1054.Category,
        NetAnalyzersRule.CA1054.Id,
        Justification = "The configuration file stores the endpoint as text.")]
    internal static int TwoSections(string endpoint) => endpoint.Length;

    /// <summary>Sharing a section with an attribute that has nothing to do with suppression.</summary>
    [Obsolete("Superseded by TwoSections."), SuppressMessage(
        SonarRule.S1144.Category,
        SonarRule.S1144.Id,
        Justification = "Kept for one release so downstream builds keep resolving it.")]
    internal static int Deprecated() => 31;

    /// <summary>Behind a preprocessor directive, the way a multi-targeted project writes it.</summary>
#if NET10_0_OR_GREATER
    [SuppressMessage(
        NetAnalyzersRule.CA1024.Category,
        NetAnalyzersRule.CA1024.Id,
        Justification = "The call is a measurement, not a property read.")]
#endif
    internal static int GetElapsed() => 32;

    /// <summary>An explicit target specifier on a type-level attribute.</summary>
    [type: SuppressMessage(
        NetAnalyzersRule.CA1034.Category,
        NetAnalyzersRule.CA1034.Id,
        Justification = "The nested type is the options bag of the type that contains it.")]
    internal sealed class Options
    {
        internal int Retries => 3;
    }
}

/// <summary>Nesting, three levels down, with the attribute at each one.</summary>
[SuppressMessage(
    StyleCopRule.SA1649.Category,
    StyleCopRule.SA1649.Id,
    Justification = "The file is named for the question it asks, not for one of its types.")]
internal static class Outer
{
    [SuppressMessage(
        NetAnalyzersRule.CA1034.Category,
        NetAnalyzersRule.CA1034.Id,
        Justification = "The nested type is the options bag of the type that contains it.")]
    internal static class Middle
    {
        [SuppressMessage(
            SonarRules.S2094.Category,
            SonarRules.S2094.Id,
            Justification = "The empty type is a marker the serialiser looks for.")]
        internal static class Inner
        {
            [SuppressMessage(
                UnusedPrivateMember.Category,
                UnusedPrivateMember.Id,
                Justification = "Reached only through the marker interface.")]
            internal static int Depth() => 3;
        }
    }
}

/// <summary>Generic types, generic methods, and the type parameters themselves.</summary>
[SuppressMessage(
    NetAnalyzersRule.CA1000.Category,
    NetAnalyzersRule.CA1000.Id,
    Justification = "The factory is discovered through the generic type itself.")]
internal sealed class Cache<[SuppressMessage(
    StyleCopRule.SA1600.Category,
    StyleCopRule.SA1600.Id,
    Justification = "The type parameter is documented on the type.")] TKey, TValue>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> entries = new();

    internal int Count => this.entries.Count;

    [SuppressMessage(
        SonarRule.S2325.Category,
        SonarRule.S2325.Id,
        Justification = "The method is an override point subclasses are expected to use.")]
    internal TValue? Read(TKey key) => this.entries.TryGetValue(key, out TValue? value) ? value : default;

    [SuppressMessage(
        SonarRule.S1172.Category,
        SonarRule.S1172.Id,
        Justification = "The signature is imposed by the visitor contract.")]
    internal static TResult? Project<TResult>(TKey key, TResult? seed)
        where TResult : class => seed;
}

/// <summary>Value types, in each of the shapes they come in.</summary>
[SuppressMessage(
    NetAnalyzersRule.CA1815.Category,
    NetAnalyzersRule.CA1815.Id,
    Justification = "The struct is never compared.")]
internal struct Tick
{
    internal long Count;

    internal Tick(long count) => this.Count = count;
}

/// <summary>A readonly struct, with an operator and a conversion on it.</summary>
[SuppressMessage(
    NetAnalyzersRule.CA1815.Category,
    NetAnalyzersRule.CA1815.Id,
    Justification = "Equality is provided by the wrapper type, not by this one.")]
internal readonly struct Money
{
    internal Money(decimal amount) => this.Amount = amount;

    internal decimal Amount { get; }

    [SuppressMessage(
        NetAnalyzersRule.CA2225.Category,
        NetAnalyzersRule.CA2225.Id,
        Justification = "The named alternative would read worse than the operator at every call site.")]
    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    [SuppressMessage(
        NetAnalyzersRule.CA2225.Category,
        NetAnalyzersRule.CA2225.Id,
        Justification = "The named alternative would read worse than the operator at every call site.")]
    public static explicit operator decimal(Money money) => money.Amount;
}

/// <summary>Records: the type, and the attribute targets a positional parameter accepts.</summary>
[SuppressMessage(
    StyleCopRule.SA1402.Category,
    StyleCopRule.SA1402.Id,
    Justification = "The record and its cache belong to one unit.")]
internal sealed record Order(
    [property: SuppressMessage(
        NetAnalyzersRule.CA1721.Category,
        NetAnalyzersRule.CA1721.Id,
        Justification = "The name matches the column it maps to.")]
    int Quantity,
    [param: SuppressMessage(
        SonarRule.S1172.Category,
        SonarRule.S1172.Id,
        Justification = "The parameter is part of the record's published shape.")]
    string Reference);

/// <summary>A positional record struct.</summary>
[SuppressMessage(
    NetAnalyzersRule.CA1815.Category,
    NetAnalyzersRule.CA1815.Id,
    Justification = "The compiler-generated equality is the whole point of the shape.")]
internal readonly record struct Sample(int Value);

/// <summary>An interface, one of its default members, and a delegate beside it.</summary>
[SuppressMessage(
    NetAnalyzersRule.CA1040.Category,
    NetAnalyzersRule.CA1040.Id,
    Justification = "The interface marks the types the loader is allowed to construct.")]
internal interface IProbe
{
    [SuppressMessage(
        SonarRule.S2325.Category,
        SonarRule.S2325.Id,
        Justification = "The default is deliberately constant for implementers that do not care.")]
    int Depth => 0;
}

/// <summary>A delegate declaration, and its return value.</summary>
[SuppressMessage(
    NetAnalyzersRule.CA1003.Category,
    NetAnalyzersRule.CA1003.Id,
    Justification = "The signature is fixed by the native callback it marshals.")]
internal delegate bool Notify(string message);

/// <summary>An enum, and one of its members.</summary>
[SuppressMessage(
    NetAnalyzersRule.CA1008.Category,
    NetAnalyzersRule.CA1008.Id,
    Justification = "The values mirror the wire protocol, which has no zero.")]
internal enum Phase
{
    [SuppressMessage(
        StyleCopRule.SA1602.Category,
        StyleCopRule.SA1602.Id,
        Justification = "The names are the protocol's own and are documented there.")]
    Idle = 1,

    Handshake = 2,
}

/// <summary>Every member kind the attribute can sit on, and the targets that reach through them.</summary>
internal sealed class MemberSurfaces : IProbe
{
    [SuppressMessage(
        StyleCopRule.SA1401.Category,
        StyleCopRule.SA1401.Id,
        Justification = "The field is part of the interop layout.")]
    internal int Slot;

    [SuppressMessage(
        SonarRule.S1104.Category,
        SonarRule.S1104.Id,
        Justification = "The static state is written once by the host.")]
    internal static int Started;

    private readonly List<string> log = [];

    [SuppressMessage(
        NetAnalyzersRule.CA1810.Category,
        NetAnalyzersRule.CA1810.Id,
        Justification = "The initialisation has to run before the first instance exists.")]
    static MemberSurfaces() => Started = 1;

    [SuppressMessage(
        SonarRule.S1172.Category,
        SonarRule.S1172.Id,
        Justification = "The parameter selects the overload the container resolves.")]
    internal MemberSurfaces(int capacity) => this.Slot = capacity;

    [SuppressMessage(
        NetAnalyzersRule.CA1063.Category,
        NetAnalyzersRule.CA1063.Id,
        Justification = "The handle is owned by the host and released by it.")]
    ~MemberSurfaces() => this.Slot = 0;

    [SuppressMessage(
        NetAnalyzersRule.CA1003.Category,
        NetAnalyzersRule.CA1003.Id,
        Justification = "The signature is fixed by the native callback it marshals.")]
    internal event Notify? Notified;

    /// <summary>An event with accessors, each carrying its own suppression.</summary>
    internal event EventHandler? Closed
    {
        [SuppressMessage(
            SonarRule.S2325.Category,
            SonarRule.S2325.Id,
            Justification = "Registration is delegated to the shared broker.")]
        add => this.log.Add("+" + value?.Method.Name);

        [SuppressMessage(
            SonarRule.S2325.Category,
            SonarRule.S2325.Id,
            Justification = "Registration is delegated to the shared broker.")]
        remove => this.log.Add("-" + value?.Method.Name);
    }

    /// <summary>An auto-property, suppressed through its backing field.</summary>
    [field: SuppressMessage(
        StyleCopRule.SA1309.Category,
        StyleCopRule.SA1309.Id,
        Justification = "The compiler names the backing field, not this codebase.")]
    internal int Retries { get; } = 3;

    /// <summary>A property whose accessors are suppressed one at a time.</summary>
    internal int Depth
    {
        [SuppressMessage(
            NetAnalyzersRule.CA1024.Category,
            NetAnalyzersRule.CA1024.Id,
            Justification = "The read is a measurement, and staying a property keeps the binder working.")]
        get => this.log.Count;

        [SuppressMessage(
            SonarRule.S4487.Category,
            SonarRule.S4487.Id,
            Justification = "The setter exists for the binder, which never reads it back.")]
        set => this.log.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>An indexer.</summary>
    [SuppressMessage(
        NetAnalyzersRule.CA1043.Category,
        NetAnalyzersRule.CA1043.Id,
        Justification = "The index is the position in the log, which is what callers have.")]
    internal string this[int index] => this.log[index];

    /// <summary>A return value, and a parameter, each with its own attribute.</summary>
    [return: SuppressMessage(
        NetAnalyzersRule.CA1054.Category,
        NetAnalyzersRule.CA1054.Id,
        Justification = "The caller writes the result straight back into the configuration file.")]
    internal string Resolve(
        [SuppressMessage(
            NetAnalyzersRule.CA1062.Category,
            NetAnalyzersRule.CA1062.Id,
            Justification = "The only caller is generated and never passes null.")]
        string key) => key + "://";

    internal bool Raise(string message) => this.Notified?.Invoke(message) ?? false;
}

/// <summary>A partial class whose type-level suppression sits on one part only.</summary>
[SuppressMessage(
    StyleCopRule.SA1402.Category,
    StyleCopRule.SA1402.Id,
    Justification = "The parts are one unit; the split follows the generator's output.")]
internal partial class Ledger
{
    [SuppressMessage(
        SonarRule.S1172.Category,
        SonarRule.S1172.Id,
        Justification = "The signature is imposed by the generated caller.")]
    internal partial void Post(int amount);
}

/// <summary>The other part, carrying no attribute of its own.</summary>
internal partial class Ledger
{
    internal int Balance { get; private set; }

    internal partial void Post(int amount) => this.Balance += amount;
}

/// <summary>Attributes inside a method body: on a local function, and on a lambda.</summary>
internal static class LocalScopes
{
    internal static int Total()
    {
        [SuppressMessage(
            SonarRule.S3776.Category,
            SonarRule.S3776.Id,
            Justification = "The arithmetic is clearer inline than behind another name.")]
        static int Add(int left, int right) => left + right;

        Func<int, int> twice =
            [SuppressMessage(
                SonarRule.S1172.Category,
                SonarRule.S1172.Id,
                Justification = "The delegate shape is fixed by the pipeline it is handed to.")]
            (int value) => value * 2;

        return twice(Add(1, 2));
    }

    internal static int Nested()
    {
        static int Outerworker()
        {
            [SuppressMessage(
                SonarRule.S2325.Category,
                SonarRule.S2325.Id,
                Justification = "The helper stays local so nothing else can call it.")]
            static int Inner() => 41;

            return Inner() + 1;
        }

        return Outerworker();
    }
}

/// <summary>A primary constructor parameter.</summary>
internal sealed class Gauge(
    [SuppressMessage(
        NetAnalyzersRule.CA1062.Category,
        NetAnalyzersRule.CA1062.Id,
        Justification = "The only caller is generated and never passes null.")]
    string unit)
{
    internal string Unit => unit;
}

/// <summary>A trim suppression: literal <c>IL</c> identifiers, which no catalogue here describes.</summary>
internal static class TrimSurfaces
{
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:UnrecognizedReflectionPattern",
        Justification = "The reflected members are rooted by the trimmer descriptor.")]
    internal static string Describe(Type type) => type.FullName ?? type.Name;

    [global::System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "The instantiation is rooted by the application manifest.")]
    internal static string Rank<T>() => typeof(T).Name;
}

/// <summary>A team's own catalogue, hand-written the way a consumer would write one.</summary>
[DiagnosticCategory]
public static class HouseCategory
{
    /// <summary>Rules about which layer may reference which.</summary>
    public const string Layering = "House.Layering";

    /// <summary>Rules about naming.</summary>
    public const string Naming = "House.Naming";

    /// <summary>Rules about layout.</summary>
    public const string Layout = "House.Layout";
}

/// <summary>The house rules, with the marker written three ways.</summary>
public static class HouseRule
{
    /// <summary>The marker written short.</summary>
    [DiagnosticRule]
    public static class HR0001
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(HR0001);

        /// <summary>The category, reached through the category catalogue.</summary>
        public const string Category = HouseCategory.Layering;
    }

    /// <summary>The marker written by its fully qualified metadata name.</summary>
    [global::DiagnosticCatalog.DiagnosticRuleAttribute]
    public static class HR0002
    {
        /// <summary>The canonical identifier of this diagnostic.</summary>
        public const string Id = nameof(HR0002);

        /// <summary>The category, reached through the category catalogue.</summary>
        public const string Category = HouseCategory.Naming;
    }

    /// <summary>The marker through an alias, and an id that is not a C# identifier.</summary>
    // DCAT0005 is expected here and cannot be cleared: the identifier carries a character C#
    // forbids, so this name is already the closest one there is. Waived at the site rather than
    // in .editorconfig, so the next declaration like it is met by a reader instead of by silence.
    #pragma warning disable DCAT0005
    [TeamRule]
    public static class HR_0003
    {
        /// <summary>The canonical identifier, which the type name cannot spell.</summary>
        public const string Id = "HR-0003";

        /// <summary>The category, reached through the house container like every other rule.</summary>
        public const string Category = HouseCategory.Layout;
    }
    #pragma warning restore DCAT0005
}

/// <summary>Suppressions naming the house catalogue rather than a shipped one.</summary>
internal static class HouseRuleUse
{
    [SuppressMessage(
        HouseRule.HR0001.Category,
        HouseRule.HR0001.Id,
        Justification = "The reference is to the shared contracts assembly, which every layer may use.")]
    internal static int Layered() => 33;

    [SuppressMessage(
        checkId: HouseRule.HR0002.Id,
        category: HouseRule.HR0002.Category,
        Justification = "The name is the vendor's and is not ours to change.")]
    internal static int Named() => 34;

    [Suppress(
        HouseRule.HR_0003.Category,
        HouseRule.HR_0003.Id,
        Justification = "The layout follows the generated file it sits beside.")]
    internal static int Laid() => 35;
}
