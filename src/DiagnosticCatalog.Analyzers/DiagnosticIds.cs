namespace DiagnosticCatalog.Analyzers;

/// <summary>
/// The identifiers this package publishes.
/// </summary>
/// <remarks>
/// <para>
/// A DCAT id is a contract from its first release: consumers write it in <c>.editorconfig</c> and in
/// suppressions of their own, so renaming or removing one is a breaking change (CLAUDE.md). What records
/// the contract is <c>AnalyzerReleases.Shipped.md</c>, which RS2008 keeps in step with the declarations
/// here.
/// </para>
/// <para>
/// This file is <b>linked into the code-fix assembly</b> rather than shared through a reference. The two
/// assemblies deliberately do not reference each other — RS1022 bans Workspaces types from an analyzer,
/// and a reference in the other direction would forbid the build-order one the analyzer needs to pack
/// both. Linking the source keeps a single spelling of every id across that gap.
/// </para>
/// </remarks>
internal static class DiagnosticIds
{
    internal const string MembersFromDifferentRules = "DCAT0001";

    internal const string InvalidRuleType = "DCAT0002";

    internal const string InvalidRuleId = "DCAT0003";

    internal const string InvalidRuleCategory = "DCAT0004";

    internal const string RuleTypeNameDiffersFromId = "DCAT0005";

    internal const string ReplaceableStringLiterals = "DCAT0006";

    internal const string MixedReferenceAndLiteral = "DCAT0007";

    internal const string NonIlUnconditionalSuppression = "DCAT0009";

    internal const string UnreferencedRuleCategory = "DCAT0011";

    internal const string IdNotWrittenAsNameOf = "DCAT0012";

    internal const string RuleTypeNameDoesNotSayId = "DCAT0013";
}
