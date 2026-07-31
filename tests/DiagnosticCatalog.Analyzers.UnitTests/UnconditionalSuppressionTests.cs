using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// DCAT0009 — a rule whose Id is not an IL warning id used in UnconditionalSuppressMessage.
/// </summary>
/// <remarks>
/// <para>
/// The diagnostic exists because such a suppression is a <b>silent no-op</b> (§9.1): ILLink reads
/// suppressions from the compiled assembly and discards any whose id its decoder rejects. Roslyn does
/// not process the attribute either, so nothing anywhere reports it.
/// </para>
/// <para>
/// That justification is also the contract. The predicate replicates ILLink's decoder rather than the
/// tighter <c>^IL\d{4}$</c> that §11.9's wording suggests, because the two disagree on ids ILLink
/// actually honours — and reporting one of those would tell a developer to change a suppression that
/// works. The cases are pinned below so the predicate cannot later be "tidied" into the strict form.
/// </para>
/// </remarks>
public sealed class UnconditionalSuppressionTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new SuppressionUsageAnalyzer();

    private const string Usings = """
        using DiagnosticCatalog;
        using System.Diagnostics.CodeAnalysis;

        """;

    /// <summary>A trim rule and a Sonar-shaped one, so a test picks the id it needs.</summary>
    private const string Rules = """
        public static class TrimRules
        {
            [DiagnosticRule]
            public static class IL2026
            {
                public const string Id = nameof(IL2026);
                public const string Category = "Trimming";
            }
        }

        public static class SomeRules
        {
            [DiagnosticRule]
            public static class S1144
            {
                public const string Id = nameof(S1144);
                public const string Category = "Major Code Smell";
            }
        }

        """;

    /// <summary>Declares one rule whose Id is <paramref name="id"/>, and suppresses with it.</summary>
    private static string WithId(string id) =>
        Usings + $$"""
            public static class Probe
            {
                [DiagnosticRule]
                public static class TheRule
                {
                    public const string Id = "{{id}}";
                    public const string Category = "Trimming";
                }
            }

            [UnconditionalSuppressMessage(Probe.TheRule.Category, Probe.TheRule.Id, Justification = "...")]
            public sealed class Target { }
            """;

    // --- the two §21.2 cases ------------------------------------------------------------------

    [Fact]
    public Task An_IL_rule_is_accepted() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + Rules + """
            [UnconditionalSuppressMessage(TrimRules.IL2026.Category, TrimRules.IL2026.Id, Justification = "...")]
            public sealed class Target { }
            """);

    [Fact]
    public Task A_non_IL_rule_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [UnconditionalSuppressMessage(SomeRules.S1144.Category, SomeRules.S1144.Id, Justification = "...")]
            public sealed class Target { }
            """, "DCAT0009");

    [Fact]
    public Task The_ordinary_suppression_attribute_carries_no_such_constraint() =>
        // The constraint belongs to ILLink's decoder, not to suppressions in general. Reporting S1144
        // under SuppressMessage would condemn the library's entire normal use.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + Rules + """
            [SuppressMessage(SomeRules.S1144.Category, SomeRules.S1144.Id, Justification = "...")]
            public sealed class Target { }
            """);

    // --- where the decoder and ^IL\d{4}$ disagree ---------------------------------------------

    [Theory]
    [InlineData("IL2026")]
    [InlineData("IL3050")]
    [InlineData("IL2026:FriendlyName")] // ILLink's own friendly-name form: honoured, suppresses IL2026.
    [InlineData("IL20265")]             // The decoder reads 4 chars at offset 2 and ignores the rest.
    public Task An_id_the_decoder_honours_is_not_reported(string id) =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, WithId(id));

    [Theory]
    [InlineData("S1144")]
    [InlineData("CA1822")]
    [InlineData("SA1600")]
    [InlineData("IL123")]   // Five characters: below the decoder's length floor, so discarded.
    [InlineData("ILabcd")]
    [InlineData("ILIL2026")]
    public Task An_id_the_decoder_discards_is_reported(string id) =>
        AnalyzerHarness.ReportsAsync(Analyzer, WithId(id), "DCAT0009");

    // --- what is deliberately out of scope ----------------------------------------------------

    [Fact]
    public Task A_literal_id_is_not_reported() =>
        // §11.9 and §21.2 both say "a rule", and this analyzer's subject is the symbolic reference.
        // Firing on literals would also flood every project that hand-writes trim suppressions without
        // ever adopting a catalogue — the audience the diagnostic is not addressed to.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + """
            [UnconditionalSuppressMessage("Major Code Smell", "S1144", Justification = "...")]
            public sealed class Target { }
            """);

    [Fact]
    public Task An_incoherent_non_IL_pair_reports_both_defects() =>
        // Independent faults: the pair references two rules AND the id would be discarded. Fixing one
        // must not hide the other, exactly as the definition analyzer reports every violation at once.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [UnconditionalSuppressMessage(TrimRules.IL2026.Category, SomeRules.S1144.Id, Justification = "...")]
            public sealed class Target { }
            """, "DCAT0001", "DCAT0009");
}
