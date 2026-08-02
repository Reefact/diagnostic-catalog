using System.Threading.Tasks;

using DiagnosticCatalog.CodeFixes;

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// What a rule type's NAME says about the identifier it declares: DCAT0005, DCAT0012 and DCAT0013.
/// </summary>
/// <remarks>
/// <para>
/// The three read as one decision, and are tested as one, because each is defined by where the previous
/// one stops:
/// </para>
/// <code>
/// value of Id == type name ?
/// |- yes -> written as nameof ?  yes -> nothing
/// |                              no  -> DCAT0012
/// \- no  -> the id is a legal C# identifier ?  yes -> DCAT0013   (the exact name was available)
///                                              no  -> the name leads with the id ?
///                                                     yes -> DCAT0005   (divergence imposed)
///                                                     no  -> DCAT0013
/// </code>
/// <para>
/// The middle question is the one the specification got backwards. §11.5 used
/// <c>IsValidIdentifier</c> to SILENCE the diagnostic — an id that cannot be a C# identifier was taken
/// as proof the name was forced — which let <c>RULE42</c> declaring <c>"RULE-0001"</c> through without
/// a word. Here it separates a divergence the author chose from one C# imposed, and both branches
/// report.
/// </para>
/// </remarks>
public sealed class RuleNamingTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new DiagnosticRuleDefinitionAnalyzer();

    private static readonly CodeFixProvider UseNameOf = new UseNameOfCodeFixProvider();

    private const string UsingFoundation = "using DiagnosticCatalog;\n";

    /// <summary>The categories these fixtures reach through, as §8.5 requires of every rule.</summary>
    private const string CategoryContainer = """
        [DiagnosticCategory]
        internal static class Cat
        {
            public const string Usage = "Usage";

            public const string Trimming = "Trimming";
        }

        """;

    /// <summary>A fixture with LF line endings whatever the checkout used, as DefinitionFixTests explains.</summary>
    private static string Rule(string declaration) =>
        (UsingFoundation + declaration).Replace("\r\n", "\n");

    // --- The name IS the id: only the form is left to judge ------------------------------------

    [Fact]
    public Task An_id_written_as_nameof_is_not_reported() =>
        AnalyzerHarness.ReportsNothingAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = Cat.Usage;
            }
            """);

    [Fact]
    public Task An_id_written_as_a_literal_is_reported() =>
        // The declaration is correct today and nothing holds it there: renaming the type leaves the
        // literal behind, still compiling, now naming a rule the type no longer is.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = "JD0007";
                public const string Category = Cat.Usage;
            }
            """, "DCAT0012");

    [Fact]
    public Task An_id_written_as_a_qualified_nameof_is_not_reported() =>
        // nameof(Outer.JD0007) folds to the same string and is held together by the same operator.
        // Insisting on the unqualified spelling would be a style this library has no reason to impose.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, UsingFoundation + CategoryContainer + """
            public static class Outer
            {
                [DiagnosticRule]
                public static class JD0007
                {
                    public const string Id = nameof(Outer.JD0007);
                    public const string Category = Cat.Usage;
                }
            }
            """);

    [Fact]
    public Task A_literal_id_sharing_one_field_declaration_is_reported() =>
        // Two declarators on one field. The analyzer reads the declarator that belongs to Id and reports
        // it; the FIX declines this shape, because repairing a shared declaration would touch a member
        // the diagnostic never mentioned.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = "JD0007", Category = Cat.Usage;
            }
            """, "DCAT0012");

    [Fact]
    public Task A_literal_id_in_a_referenced_assembly_is_not_reported() =>
        // Metadata carries the folded constant and no trace of how it was written, so there is no form
        // left to recommend. Not a gap in coverage: the question stops existing at the assembly boundary.
        AnalyzerHarness.ReportsAgainstReferenceAsync(
            Analyzer,
            UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = "JD0007";
                public const string Category = Cat.Usage;
            }
            """,
            "public static class Consumer { }");

    // --- The name is NOT the id, and the exact name was available -------------------------------

    [Fact]
    public Task A_name_unrelated_to_a_legal_id_is_reported() =>
        // The §11.5 example. "JD0007" is a perfectly good type name and the type is called something
        // else, so every use site reads RuleSeven.Id and suppresses JD0007.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class RuleSeven
            {
                public const string Id = "JD0007";
                public const string Category = Cat.Usage;
            }
            """, "DCAT0013");

    [Fact]
    public Task A_name_differing_only_in_punctuation_from_a_legal_id_is_reported() =>
        // The case comparing letters and digits alone would forgive: RULE001 and "RULE_001" normalise
        // to the same string, yet an underscore is legal in an identifier and the type could have been
        // spelled exactly RULE_001. Nothing imposed the divergence, so it is not the imposed kind.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class RULE001
            {
                public const string Id = "RULE_001";
                public const string Category = Cat.Usage;
            }
            """, "DCAT0013");

    // --- The name is NOT the id, and no name could have been -------------------------------------

    [Fact]
    public Task A_name_legalising_an_illegal_id_with_underscores_is_reported_as_information() =>
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class RULE_0001
            {
                public const string Id = "RULE-0001";
                public const string Category = Cat.Usage;
            }
            """, "DCAT0005");

    [Fact]
    public Task A_name_legalising_an_illegal_id_by_dropping_is_reported_as_information() =>
        // The other legalisation of the same id, and there is no ground to prefer either — which is
        // precisely why the diagnostic offers no repair and stays at Info.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class RULE0001
            {
                public const string Id = "RULE-0001";
                public const string Category = Cat.Usage;
            }
            """, "DCAT0005");

    [Fact]
    public Task A_name_ignoring_an_illegal_id_is_reported_as_a_warning() =>
        // The declaration §11.5's trigger condition let through in silence. The id cannot be a type
        // name, so nothing was available — but RULE42 is not a legalisation of it either, and the
        // reference says nothing true about what it suppresses.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class RULE42
            {
                public const string Id = "RULE-0001";
                public const string Category = Cat.Usage;
            }
            """, "DCAT0013");

    [Fact]
    public Task A_name_leading_with_a_friendly_name_id_is_reported_as_information() =>
        // The trimmer's own IL####:FriendlyName form, which ILLink honours and DCAT0009 deliberately
        // leaves alone. The identifier is truncated at the first colon before comparison — the same
        // normalisation §11.6 applies — so the name is read as leading with IL2026 and the divergence
        // counts as imposed. Demanding more would be telling an author to rename a type after a
        // sentence.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class IL2026Annotated
            {
                public const string Id = "IL2026:Members annotated with RequiresUnreferencedCode";
                public const string Category = Cat.Trimming;
            }
            """, "DCAT0005");

    // --- The DCAT0012 fix ---------------------------------------------------------------------------

    [Fact]
    public async Task The_literal_becomes_nameof()
    {
        string fixed_ = await CodeFixHarness.ApplyAsync(Analyzer, UseNameOf, Rule(CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = "JD0007";
                public const string Category = Cat.Usage;
            }
            """));

        Assert.Equal(Rule(CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = nameof(JD0007);
                public const string Category = Cat.Usage;
            }
            """), fixed_);
    }

    [Fact]
    public Task No_fix_is_offered_for_a_shared_field_declaration() =>
        // Rewriting it would edit Category, which this diagnostic never mentioned.
        CodeFixHarness.OffersNothingAsync(Analyzer, UseNameOf, Rule(CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007
            {
                public const string Id = "JD0007", Category = Cat.Usage;
            }
            """));

    [Fact]
    public Task No_fix_is_offered_for_a_generic_rule_type() =>
        // nameof would have to name the constructed type, and the repair this rule needs is DCAT0002's.
        CodeFixHarness.OffersNothingAsync(Analyzer, UseNameOf, Rule(CategoryContainer + """
            [DiagnosticRule]
            public static class JD0007<T>
            {
                public const string Id = "JD0007";
                public const string Category = Cat.Usage;
            }
            """));

    // --- Interaction with the structural contract --------------------------------------------------

    [Fact]
    public Task A_rule_with_no_usable_id_draws_no_naming_diagnostic() =>
        // DCAT0003 first. All three naming diagnostics read what Id HOLDS, and a rule without one has
        // nothing to compare — reporting a name mismatch on top would name a second thing to fix that
        // fixing the first makes disappear.
        AnalyzerHarness.ReportsAsync(Analyzer, UsingFoundation + CategoryContainer + """
            [DiagnosticRule]
            public static class RuleSeven
            {
                public static readonly string Id = "JD0007";
                public const string Category = Cat.Usage;
            }
            """, "DCAT0003");
}
