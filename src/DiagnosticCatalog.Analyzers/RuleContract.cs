using System;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// The ways a type marked as a diagnostic rule can fail the structural contract of specification §8.
/// Flags rather than a single value: a type can fail several at once, and each names a separate thing
/// to fix.
/// </summary>
[Flags]
internal enum RuleContractViolations
{
    None = 0,

    /// <summary>§8.1 — reported as DCAT0002.</summary>
    NotAStaticNonGenericClass = 1,

    /// <summary>§8.2 — reported as DCAT0003.</summary>
    InvalidId = 2,

    /// <summary>§8.3 — reported as DCAT0004.</summary>
    InvalidCategory = 4,
}

/// <summary>
/// What <see cref="RuleContract.Check"/> found: which parts of the contract failed, and — when they
/// held — the fields and values a caller needs.
/// </summary>
internal sealed class RuleContractResult
{
    internal RuleContractResult(
        RuleContractViolations violations,
        IFieldSymbol? idField,
        IFieldSymbol? categoryField,
        string? id,
        string? category)
    {
        Violations = violations;
        IdField = idField;
        CategoryField = categoryField;
        Id = id;
        Category = category;
    }

    internal RuleContractViolations Violations { get; }

    internal bool IsSatisfied => Violations == RuleContractViolations.None;

    /// <summary>The <c>Id</c> field, or null when §8.2 failed.</summary>
    internal IFieldSymbol? IdField { get; }

    /// <summary>The <c>Category</c> field, or null when §8.3 failed.</summary>
    internal IFieldSymbol? CategoryField { get; }

    /// <summary>The constant value of <c>Id</c>, or null when §8.2 failed.</summary>
    internal string? Id { get; }

    /// <summary>The constant value of <c>Category</c>, or null when §8.3 failed.</summary>
    internal string? Category { get; }
}

/// <summary>
/// The structural contract of specification §8, evaluated over symbols alone.
/// </summary>
/// <remarks>
/// No syntax, deliberately, and not for tidiness. Definition diagnostics (DCAT0002–DCAT0005) fire only
/// on source the compiler can see, so a malformed rule inside a REFERENCED assembly produces nothing
/// from them — the blind spot DCAT0010 covers by running this same contract at the use site, against a
/// metadata symbol that has no syntax at all (§11 preamble). Writing this against syntax now would mean
/// rewriting it then.
///
/// It also never looks for a base type or an interface. §8.4 makes the contract structural rather than
/// inheritance-imposed: a static class cannot inherit, and an abstract property could never be a
/// constant attribute argument.
/// </remarks>
internal static class RuleContract
{
    /// <summary>The member holding the identifier (§8.2).</summary>
    internal const string IdMember = "Id";

    /// <summary>The member holding the category (§8.3).</summary>
    internal const string CategoryMember = "Category";

    /// <summary>Evaluates <paramref name="rule"/> against §8. The caller decides it is a rule.</summary>
    internal static RuleContractResult Check(INamedTypeSymbol rule)
    {
        RuleContractViolations violations = RuleContractViolations.None;

        // §8.1 — a class, static, non-generic. TypeKind covers struct, interface, enum and delegate,
        // and IsStatic and Arity cover the rest.
        //
        // The TypeKind half looks unreachable and is not. The foundation's marker is declared
        // AttributeTargets.Class, so the compiler rejects it on a struct with CS0592 before any analyzer
        // runs. But a catalogue may declare its own marker (§7.2) with a wider usage, and a referenced
        // assembly may have been built against one — which is what DCAT0010 reads later, through this
        // same predicate. Dropping the check would make this contract disagree with itself depending on
        // where the type came from.
        if (rule.TypeKind != TypeKind.Class || !rule.IsStatic || rule.Arity > 0)
        {
            violations |= RuleContractViolations.NotAStaticNonGenericClass;
        }

        if (!TryReadConstant(rule, IdMember, out IFieldSymbol? idField, out string? id))
        {
            violations |= RuleContractViolations.InvalidId;
        }

        if (!TryReadConstant(rule, CategoryMember, out IFieldSymbol? categoryField, out string? category))
        {
            violations |= RuleContractViolations.InvalidCategory;
        }

        return new RuleContractResult(violations, idField, categoryField, id, category);
    }

    /// <summary>
    /// One public constant string of that name, holding something other than blank.
    /// </summary>
    /// <remarks>
    /// The VALUE is read from <see cref="IFieldSymbol.ConstantValue"/>, never from the initialiser
    /// syntax. <c>nameof(JD0007)</c> written inside <c>JD0007</c> is a valid constant expression
    /// resolving to the containing type's name (§7.3), and the recommended form; only the folded value
    /// is the truth. Reading syntax would also fail outright on a metadata symbol.
    ///
    /// Whitespace counts as absent. §8.2 requires Id to be "non-null, non-empty, not whitespace-only"
    /// while §11.3 lists only "empty"; §8.3 says only "non-empty" for Category while §11.4 says "same
    /// validations as Id". The stricter §8.2 reading is applied to both, because a category of " " is
    /// no more usable than one of "".
    /// </remarks>
    private static bool TryReadConstant(
        INamedTypeSymbol rule,
        string name,
        out IFieldSymbol? field,
        out string? value)
    {
        field = null;
        value = null;

        ImmutableArray<ISymbol> members = rule.GetMembers(name);

        // "Exactly one public member named Id" (§8.2). More than one is only reachable through method
        // overloads, which are not fields and fail below anyway; zero is the ordinary absent case.
        if (members.Length != 1) { return false; }

        if (members[0] is not IFieldSymbol candidate) { return false; }
        if (candidate.DeclaredAccessibility != Accessibility.Public) { return false; }
        // IsConst, not HasConstantValue: a `static readonly string` has a value at run time but cannot
        // be an attribute argument, which is the entire point of the constant.
        if (!candidate.IsConst) { return false; }
        if (candidate.Type.SpecialType != SpecialType.System_String) { return false; }
        if (candidate.ConstantValue is not string text || string.IsNullOrWhiteSpace(text)) { return false; }

        field = candidate;
        value = text;

        return true;
    }
}
