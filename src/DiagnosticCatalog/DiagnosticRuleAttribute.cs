using System;

namespace DiagnosticCatalog;

/// <summary>
/// Identifies a static class that represents a single analyzer diagnostic rule,
/// exposing that rule's identifier and category as compile-time constants.
/// </summary>
/// <remarks>
/// <para>
/// The purpose of a diagnostic rule class is to replace the magic strings passed to
/// <see cref="System.Diagnostics.CodeAnalysis.SuppressMessageAttribute"/>. Both of its
/// arguments are magic strings, and they differ only in how they fail. A wrong
/// <c>checkId</c> leaves a dead suppression: the diagnostic keeps being reported, with
/// nothing pointing at the cause. A wrong category is quieter still — the platform never
/// reads that argument, so nothing anywhere can report the mistake. Referencing constants
/// instead turns a renamed or retired rule into a build error, and gives the category a
/// single published source of truth.
/// </para>
/// <para>
/// This attribute is a marker. It suppresses no diagnostic, changes no compiler
/// behaviour, and carries no arguments — placing the identifier and category on the
/// attribute would duplicate the constants without removing the need for them, since
/// one attribute's arguments cannot be referenced from another attribute.
/// </para>
///
/// <para><b>Structural contract</b></para>
/// <para>
/// A type marked with this attribute is expected to be a static, non-generic class
/// exposing exactly one public constant named <c>Id</c> and exactly one named
/// <c>Category</c>:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     <c>public const string Id</c> — the canonical identifier of the diagnostic,
///     non-empty.
///     </description>
///   </item>
///   <item>
///     <description>
///     <c>public const string Category</c> — the category declared by the originating
///     analyzer's <c>DiagnosticDescriptor</c>, non-empty.
///     </description>
///   </item>
/// </list>
/// <para>
/// A rule may expose further optional constants — <c>Title</c>, <c>MessageFormat</c>,
/// <c>Description</c>, <c>HelpLinkUri</c> — which are the remaining
/// <c>DiagnosticDescriptor</c> arguments. They are not part of the mandatory contract.
/// </para>
/// <para>
/// Both members must be <c>const</c>. A property, a <c>static readonly</c> field, a
/// <c>record</c> or a static instance cannot be used as an attribute argument, so none
/// of them can replace these constants. This is also why the contract is structural
/// rather than expressed as an interface or a base class.
/// </para>
///
/// <para><b>Example</b></para>
/// <example>
/// Declaring a catalogue:
/// <code>
/// using DiagnosticCatalog;
///
/// namespace JustDummies.Analyzers.Suppressions;
///
/// [DiagnosticCategory]
/// public static class DummyCategory
/// {
///     public const string Usage = "Usage";
/// }
///
/// public static class Dummies
/// {
///     [DiagnosticRule]
///     public static class JD0007
///     {
///         public const string Id = nameof(JD0007);
///         public const string Category = DummyCategory.Usage;
///     }
/// }
/// </code>
/// The category reaches a constant declared in a <c>[DiagnosticCategory]</c> class rather than a
/// literal: a catalogue repeats very few categories across very many rules, and each transcription is
/// a place for one of them to drift. <c>DCAT0011</c> reports the literal form.
/// Consuming it:
/// <code>
/// using System.Diagnostics.CodeAnalysis;
/// using JustDummies.Analyzers.Suppressions;
///
/// [SuppressMessage(
///     Dummies.JD0007.Category,
///     Dummies.JD0007.Id,
///     Justification = "This member is instantiated by the test infrastructure.")]
/// public sealed class DummyFactory
/// {
/// }
/// </code>
/// Using <c>nameof</c> for <c>Id</c> is recommended whenever the diagnostic identifier
/// is a valid C# identifier: it keeps the class name and the identifier in sync by
/// construction.
/// </example>
///
/// <para><b>Validation and tooling</b></para>
/// <para>
/// This package contains the attributes only. The analyzers that validate rule
/// declarations against the contract above, and that check use sites of
/// <see cref="System.Diagnostics.CodeAnalysis.SuppressMessageAttribute"/>, ship
/// separately in <c>DiagnosticCatalog.Analyzers</c>. Referencing this package alone
/// declares rules; it performs no checking.
/// </para>
/// <para>
/// Those analyzers recognise the attribute by its fully qualified metadata name,
/// <c>DiagnosticCatalog.DiagnosticRuleAttribute</c>, regardless of the assembly that
/// declares it. A catalogue that wants no package dependency at all may therefore
/// declare its own <c>internal sealed class DiagnosticRuleAttribute</c> in this
/// namespace instead of referencing this package, and remain fully recognised.
/// </para>
/// <para>
/// Applying this attribute introduces no run-time behaviour. The declaring assembly is
/// deployed with the consuming application — it is a normal <c>lib/</c> dependency by
/// design (specification §16.1) — but the runtime materialises custom attributes only
/// when reflection asks for them, so <c>DiagnosticCatalog.dll</c> is never actually
/// loaded unless something reflects over the rule types. A suppression written with
/// <see cref="System.Diagnostics.CodeAnalysis.SuppressMessageAttribute"/> leaves no trace
/// at all, because that attribute is declared <c>[Conditional("CODE_ANALYSIS")]</c> and is
/// not emitted unless the symbol is defined.
/// <c>UnconditionalSuppressMessageAttribute</c> is the deliberate exception: it carries no
/// <c>[Conditional]</c> so that trimming tools can read it from the compiled assembly, and
/// it is emitted with the referenced constants folded in.
/// </para>
/// </remarks>
/// <seealso cref="System.Diagnostics.CodeAnalysis.SuppressMessageAttribute"/>
//
// MAINTAINER CONSTRAINT — this attribute must never become [Conditional].
//
// The analyzers discover rules declared in *referenced assemblies* by reading this
// marker out of their metadata. A conditional attribute is not emitted unless the
// symbol is defined at the declaring assembly's compile time, which would make every
// catalogue distributed as a NuGet package silently invisible: no rules found, no
// diagnostics reported, no error anywhere. See doc/specification.en.md §3.4 and §13.
//
// SuppressMessageAttribute can afford to be conditional because its only consumer,
// Roslyn, reads it from the semantic model of the compilation being built. Metadata
// consumers got a second, non-conditional attribute instead
// (UnconditionalSuppressMessageAttribute — see §3.4 and Appendix A8). This marker has
// no such escape hatch: cross-assembly rule discovery is the only way it is ever read.
[AttributeUsage(
    AttributeTargets.Class,
    AllowMultiple = false,
    Inherited = false)]
public sealed class DiagnosticRuleAttribute : Attribute
{
}
