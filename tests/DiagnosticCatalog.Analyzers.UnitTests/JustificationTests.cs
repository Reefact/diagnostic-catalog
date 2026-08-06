using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// DCAT0014 — a suppression that references a catalogue rule and says nothing about why.
/// </summary>
/// <remarks>
/// <para>
/// The pair says WHICH diagnostic is silenced and every other use-site rule here checks it. Nothing
/// said WHY, and that half cannot be recovered afterwards: the warning is gone, and whether silencing
/// it was reasonable lives in the head of whoever wrote the line.
/// </para>
/// <para>
/// The contract is <b>presence, never quality</b>, and the boundary is what these fixtures pin. A
/// justification of one letter passes; the specification rules out judging what a justification says
/// (§5) and validating one intelligently (§24), and both stay true of a check that reads a length.
/// The single exception is the IDE's own <c>&lt;Pending&gt;</c> placeholder, which is that tool's word
/// for "not written yet" rather than a shape this generalises — the theory below asserts that a value
/// saying nothing still passes, so that nobody later reads the placeholder case as a licence to rule
/// on prose.
/// </para>
/// <para>
/// <b>Every suppression is held to it, literals included</b> (ADR-0039), and the fixtures below pin
/// that too. It is the one diagnostic here whose question does not depend on the catalogue: a literal
/// suppression silences a warning exactly as a reference does. It matters most where DCAT0006 cannot
/// reach — a literal naming a rule no referenced catalogue knows was, before this, reported by
/// nothing at all.
/// </para>
/// </remarks>
public sealed class JustificationTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new SuppressionUsageAnalyzer();

    private const string Usings = """
        using DiagnosticCatalog;
        using System.Diagnostics.CodeAnalysis;

        """;

    /// <summary>Two rules and a trim rule, so a fixture picks the shape it needs.</summary>
    private const string Rules = """
        public static class SonarRules
        {
            [DiagnosticRule]
            public static class S1144
            {
                public const string Id = nameof(S1144);
                public const string Category = "Major Code Smell";
            }

            [DiagnosticRule]
            public static class S2094
            {
                public const string Id = nameof(S2094);
                public const string Category = "Major Code Smell";
            }
        }

        public static class TrimRules
        {
            [DiagnosticRule]
            public static class IL2026
            {
                public const string Id = nameof(IL2026);
                public const string Category = "Trimming";
            }
        }

        """;

    /// <summary>A coherent, migrated suppression carrying <paramref name="justification"/> verbatim.</summary>
    private static string Suppressing(string justification) =>
        Usings + Rules + $$"""
            [SuppressMessage(SonarRules.S1144.Category, SonarRules.S1144.Id{{justification}})]
            public sealed class Target { }
            """;

    // --- what satisfies it --------------------------------------------------------------------

    [Fact]
    public Task A_written_justification_is_accepted() =>
        AnalyzerHarness.ReportsNothingAsync(
            Analyzer,
            Suppressing(", Justification = \"Called by the serializer through reflection.\""));

    [Theory]
    [InlineData("x")]      // One character. Presence is the contract, and this is what that means.
    [InlineData("TODO")]   // Prose that says nothing, and is not the IDE's marker for "none".
    [InlineData(" a ")]    // Not blank once there is a letter in it.
    public Task Any_value_that_is_not_blank_is_accepted(string justification) =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Suppressing($", Justification = \"{justification}\""));

    [Fact]
    public Task A_justification_reached_through_a_constant_is_accepted() =>
        // Attribute arguments fold, so the constant is read exactly as the literal it holds. The form
        // matters because a codebase repeating one reason across many suppressions writes it this way.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + Rules + """
            internal static class Reasons
            {
                public const string Reflection = "Instantiated by the DI container.";
            }

            [SuppressMessage(SonarRules.S1144.Category, SonarRules.S1144.Id, Justification = Reasons.Reflection)]
            public sealed class Target { }
            """);

    [Fact]
    public Task The_argument_is_found_whatever_else_the_attribute_carries() =>
        // Named arguments come in any order and Scope, Target and MessageId may sit around it. Reading
        // by position rather than by name would find the wrong one, or none.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + """
            [assembly: SuppressMessage(
                SonarRules.S1144.Category,
                SonarRules.S1144.Id,
                Scope = "member",
                Justification = "Called by the serializer.",
                Target = "~M:Target.Rebuild")]

            """ + Rules + """
            public sealed class Target { }
            """);

    // --- what it reports ----------------------------------------------------------------------

    [Fact]
    public Task An_absent_justification_is_reported() =>
        AnalyzerHarness.ReportsAsync(Analyzer, Suppressing(string.Empty), "DCAT0014");

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("\"\\t\"")]
    [InlineData("null")]   // The one blank form C# spells without a string at all, and it compiles.
    public Task A_blank_justification_is_reported(string expression) =>
        AnalyzerHarness.ReportsAsync(Analyzer, Suppressing($", Justification = {expression}"), "DCAT0014");

    [Fact]
    public Task The_IDE_placeholder_is_reported() =>
        // What Visual Studio writes when it generates a suppression. Non-blank, and it is the tool's
        // own word for "nobody has filled this in" — accepting it would let the whole rule be
        // discharged by the fix that creates the problem.
        AnalyzerHarness.ReportsAsync(Analyzer, Suppressing(", Justification = \"<Pending>\""), "DCAT0014");

    [Fact]
    public Task An_assembly_level_suppression_is_reported_like_any_other() =>
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + """
            [assembly: SuppressMessage(SonarRules.S1144.Category, SonarRules.S1144.Id)]

            """ + Rules + """
            public sealed class Target { }
            """, "DCAT0014");

    [Fact]
    public Task An_unconditional_suppression_is_held_to_the_same_requirement() =>
        // The trimmer's attribute declares Justification too, and a suppression read by a tool that
        // runs after the compiler is the one that most needs to say why it is there.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [UnconditionalSuppressMessage(TrimRules.IL2026.Category, TrimRules.IL2026.Id)]
            public sealed class Target { }
            """, "DCAT0014");

    // --- the literals, which it does NOT leave alone -------------------------------------------

    [Fact]
    public Task A_suppression_written_entirely_in_literals_is_reported() =>
        // No catalogue anywhere in this compilation — nothing resolves to a rule — and it is still
        // reported. That is ADR-0039's decision and the reason for it: a literal suppression silences
        // a warning exactly as a reference does and says exactly as little about why, and a codebase
        // that has adopted nothing is the one the question is worth asking of most.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + """
            [SuppressMessage("Major Code Smell", "S1144")]
            public sealed class Target { }
            """, "DCAT0014");

    [Fact]
    public Task A_literal_pair_matching_a_known_rule_reports_the_migration_and_this() =>
        // Independent faults on one line: the pair can be migrated (DCAT0006) and the line says
        // nothing about why (DCAT0014). Applying the migration fix leaves the second standing, which
        // is the point — converting a suppression does not answer the question it never answered.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage("Major Code Smell", "S1144")]
            public sealed class Target { }
            """, "DCAT0006", "DCAT0014");

    [Fact]
    public Task A_literal_naming_a_rule_no_catalogue_knows_is_reported() =>
        // The case nothing else in this package reaches. DCAT0006 stays silent because no known rule
        // matches the pair, so before this diagnostic the line was reported by nothing at all — the
        // gap that made restricting DCAT0014 to catalogue references untenable.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage("Usage", "xUnit1004")]
            public sealed class Target { }
            """, "DCAT0014");

    [Fact]
    public async Task A_literal_identifier_is_named_by_what_it_silences()
    {
        // Truncated at the first colon, as Roslyn truncates it before matching. The message names
        // S1144 rather than reciting the rule's own title back at whoever wrote it.
        ImmutableArray<Diagnostic> reported = await AnalyzerHarness.RunAsync(Analyzer, Usings + """
            [SuppressMessage("Major Code Smell", "S1144:Unused private members should be removed")]
            public sealed class Target { }
            """);

        Diagnostic missing = Assert.Single(reported, diagnostic => diagnostic.Id == "DCAT0014");

        Assert.Contains("'S1144'", missing.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("Unused private members", missing.GetMessage(), StringComparison.Ordinal);
    }

    // --- what it stays off --------------------------------------------------------------------

    [Fact]
    public Task An_identifier_that_names_nothing_is_left_alone() =>
        // `null` compiles in that slot and silences nothing — Roslyn matches a suppression on the
        // identifier, and there is no identifier here. A suppression that suppresses nothing has
        // nothing to justify, and this diagnostic would have nothing to name in its message.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, Usings + """
            [SuppressMessage("Major Code Smell", null)]
            public sealed class Target { }
            """);

    // --- alongside the other faults -----------------------------------------------------------

    [Fact]
    public Task A_half_migrated_suppression_reports_both_defects() =>
        // Independent faults, both reported. The pair being half migrated and the line saying nothing
        // about why are different questions, and fixing either must not hide the other.
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Category, "S1144")]
            public sealed class Target { }
            """, "DCAT0007", "DCAT0014");

    [Fact]
    public Task An_incoherent_pair_without_a_justification_reports_both() =>
        AnalyzerHarness.ReportsAsync(Analyzer, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Category, SonarRules.S2094.Id)]
            public sealed class Target { }
            """, "DCAT0001", "DCAT0014");

    // --- what the message says ----------------------------------------------------------------

    [Fact]
    public async Task The_message_names_the_rule_the_identifier_slot_carries()
    {
        // Roslyn matches a suppression on the identifier alone, so that slot decides which diagnostic
        // this line silences — and it is the rule the message must name when the two disagree.
        ImmutableArray<Diagnostic> reported = await AnalyzerHarness.RunAsync(Analyzer, Usings + Rules + """
            [SuppressMessage(SonarRules.S1144.Category, SonarRules.S2094.Id)]
            public sealed class Target { }
            """);

        Diagnostic missing = Assert.Single(reported, diagnostic => diagnostic.Id == "DCAT0014");

        Assert.Contains("'S2094'", missing.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_message_separates_an_absent_justification_from_a_blank_one()
    {
        // Three faults under one id, and the repair differs: an absent argument is one nobody wrote,
        // a blank one is one somebody started, and the placeholder is one a tool wrote. A message
        // that said "no justification" to all three would describe two of them wrongly.
        ImmutableArray<Diagnostic> absent = await AnalyzerHarness.RunAsync(Analyzer, Suppressing(string.Empty));

        ImmutableArray<Diagnostic> blank = await AnalyzerHarness.RunAsync(
            Analyzer,
            Suppressing(", Justification = \"\""));

        ImmutableArray<Diagnostic> pending = await AnalyzerHarness.RunAsync(
            Analyzer,
            Suppressing(", Justification = \"<Pending>\""));

        Assert.Contains("carries no Justification", Assert.Single(absent).GetMessage(), StringComparison.Ordinal);
        Assert.Contains("carries a blank Justification", Assert.Single(blank).GetMessage(), StringComparison.Ordinal);
        Assert.Contains("\"<Pending>\" placeholder", Assert.Single(pending).GetMessage(), StringComparison.Ordinal);
    }
}
