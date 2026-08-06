using System;
using System.IO;
using System.Threading.Tasks;
using CatalogGen.UnitTests;
using Xunit;

namespace DiagnosticCatalog.Cli.UnitTests;

/// <summary>
/// <c>dcat list</c> and <c>dcat explain</c>, exercised through the real command tree.
/// </summary>
/// <remarks>
/// <para>
/// What these verbs PRINT is their whole product — nothing else comes back from them — so the
/// output is the contract and asserting the exit code alone would leave it unchecked.
/// </para>
/// <para>
/// The line that matters most is the suppression <c>explain</c> emits. It is the one a reader
/// copies into their own source, and it is the reason this repository exists: a reference that
/// named the rule without its container would not compile at the use site, and nothing downstream
/// would say so.
/// </para>
/// </remarks>
public sealed class InspectCommandTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("dcat-inspect-").FullName;

    public void Dispose() => Directory.Delete(_work, recursive: true);

    private string Catalogue(string source = null!)
        => CatalogueFixture.Write(_work, "Acme.Catalog", source ?? CatalogueFixture.TwoRulesAndOneRetired);

    /// <summary>Runs the tool with Console.Out and Console.Error captured — see <see cref="CliRun"/>.</summary>
    private static Task<(int ExitCode, string Out, string Error)> RunAsync(params string[] args)
        => CliRun.Async(args);

    [Fact]
    public async Task List_reports_every_rule_with_its_category()
    {
        (int exitCode, string output, _) = await RunAsync("list", Catalogue());

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("ACME0001", output, StringComparison.Ordinal);
        Assert.Contains("ACME0002", output, StringComparison.Ordinal);
        Assert.Contains("Naming", output, StringComparison.Ordinal);
        Assert.Contains("Usage", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_states_which_release_the_catalogue_was_generated_from()
    {
        // A catalogue is a snapshot, and how old it is decides whether its answer can be trusted —
        // so it is stated before the answer rather than left to be looked up.
        (int _, string output, _) = await RunAsync("list", Catalogue());

        Assert.Contains("Acme.Analyzers 1.2.3", output, StringComparison.Ordinal);
        Assert.Contains("generated 2026-07-30", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_marks_a_retired_rule()
    {
        (int _, string output, _) = await RunAsync("list", Catalogue());

        Assert.Matches(@"ACME0001\s+Naming\s+\[retired\]", output);
        Assert.DoesNotMatch(@"ACME0002.*\[retired\]", output);
    }

    [Fact]
    public async Task List_counts_the_rules_and_the_categories_it_printed()
    {
        (int _, string output, _) = await RunAsync("list", Catalogue());

        Assert.Contains("2 rule(s), 2 category(ies)", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_says_nothing_about_provenance_a_catalogue_does_not_record()
    {
        // A hand-written catalogue carries no [assembly: CatalogSource]. Printing an empty banner
        // would suggest the read failed, when it simply found nothing to report.
        (int exitCode, string output, _) = await RunAsync("list", Catalogue(CatalogueFixture.NoProvenance));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("LOOSE0001", output, StringComparison.Ordinal);
        Assert.Contains("1 rule(s), 1 category(ies)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("generated", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_reports_an_assembly_carrying_no_rules_as_empty_rather_than_failing()
    {
        // Succeeding here is the point: "this package has no catalogue" is a true answer, and
        // failing would make it indistinguishable from a path the tool could not read.
        (int exitCode, string output, _) = await RunAsync("list", Catalogue(CatalogueFixture.NotACatalogue));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("0 rule(s), 0 category(ies)", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_fails_on_an_assembly_that_is_not_there()
    {
        (int exitCode, _, _) = await RunAsync("list", Path.Combine(_work, "absent.dll"));

        Assert.Equal(ExitCodes.Failure, exitCode);
    }

    [Fact]
    public async Task List_naming_no_catalogue_is_a_usage_error()
        => Assert.Equal(ExitCodes.UsageError, (await RunAsync("list")).ExitCode);

    [Fact]
    public async Task Explain_emits_a_suppression_that_depends_on_no_import_of_the_reader_s()
    {
        // The line the reader came for, in the shape it has to have: every name reached from the
        // global namespace, the attribute included. Whether it BUILDS is asserted next door, by
        // compiling it — see ExplainFragmentTests. What is checked here is that the shape survives,
        // because it is the shape a later edit would quietly simplify.
        (int exitCode, string output, _) = await RunAsync("explain", Catalogue(), "ACME0002");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("[global::System.Diagnostics.CodeAnalysis.SuppressMessage(", output,
                        StringComparison.Ordinal);
        Assert.Contains("global::Vendor.Catalog.AcmeRules.ACME0002.Category,", output, StringComparison.Ordinal);
        Assert.Contains("global::Vendor.Catalog.AcmeRules.ACME0002.Id,", output, StringComparison.Ordinal);
        Assert.Contains("Justification", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explain_reports_the_declared_id_and_references_the_type_that_carries_it()
    {
        // The §8.2 case: "ACME-0003" carries a character C# forbids in a type name, so the type is
        // ACME_0003 and both spellings exist. The command has to print BOTH, each where it belongs —
        // the identifier as the rule's own fact, the type name in the reference — and this is the
        // one rule shape where getting them the wrong way round is visible.
        (int exitCode, string output, _) =
            await RunAsync("explain", Catalogue(CatalogueFixture.RuleNamedApartFromItsId), "ACME-0003");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("id        ACME-0003", output, StringComparison.Ordinal);
        Assert.Contains("global::Vendor.Catalog.AcmeRules.ACME_0003.Category,", output, StringComparison.Ordinal);
        Assert.DoesNotContain("ACME-0003.Category", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explain_reports_the_rule_s_own_facts()
    {
        (int _, string output, _) = await RunAsync("explain", Catalogue(), "ACME0002");

        Assert.Contains("id        ACME0002", output, StringComparison.Ordinal);
        Assert.Contains("category  Usage", output, StringComparison.Ordinal);
        Assert.Contains("help      https://acme.example/ACME0002", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explain_omits_the_help_line_for_a_rule_that_publishes_no_link()
    {
        // Not every vendor publishes one, and an empty `help` line would read as a broken link
        // rather than as an absent one.
        (int _, string output, _) = await RunAsync("explain", Catalogue(), "ACME0001");

        Assert.DoesNotContain("help  ", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explain_says_when_a_rule_is_retired()
    {
        // ADR-0010 keeps a retired rule's constant so existing suppressions still compile. Saying so
        // here is what stops a reader adopting one the vendor no longer declares.
        (int _, string output, _) = await RunAsync("explain", Catalogue(), "ACME0001");

        Assert.Contains("retired upstream", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explain_finds_a_rule_whatever_case_it_is_asked_for()
    {
        // Rule identifiers are shouted in every vendor's documentation and typed in whatever case
        // the reader has to hand.
        Assert.Equal(ExitCodes.Success, (await RunAsync("explain", Catalogue(), "acme0002")).ExitCode);
    }

    [Fact]
    public async Task Explain_names_the_catalogue_as_well_as_the_rule_it_could_not_find()
    {
        // The likeliest mistake is asking the right question of the wrong catalogue, so the answer
        // has to say which one was read.
        (int exitCode, _, string error) = await RunAsync("explain", Catalogue(), "ACME9999");

        Assert.Equal(ExitCodes.Failure, exitCode);
        Assert.Contains("ACME9999", error, StringComparison.Ordinal);
        Assert.Contains("Acme.Catalog.dll", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explain_names_a_rule_that_has_no_container_without_a_stray_dot()
    {
        // A rule declared at namespace level has no enclosing type, so its reference is the
        // namespace and the rule — the leading dot a naive concatenation produces would be a
        // reference that does not compile.
        (int exitCode, string output, _) =
            await RunAsync("explain", Catalogue(CatalogueFixture.NoProvenance), "LOOSE0001");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("    global::Vendor.Catalog.LOOSE0001.Category,", output, StringComparison.Ordinal);
        Assert.DoesNotContain("..", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_inspect_verb_given_a_blank_catalogue_is_a_usage_error()
    {
        // Blank rather than missing: the argument is present, so the parser is satisfied and only
        // the settings' own validation can refuse it.
        Assert.Equal(ExitCodes.UsageError, (await RunAsync("list", "   ")).ExitCode);
        Assert.Equal(ExitCodes.UsageError, (await RunAsync("explain", "   ", "ACME0001")).ExitCode);
    }

    [Fact]
    public async Task Explain_given_a_blank_rule_is_a_usage_error()
        => Assert.Equal(ExitCodes.UsageError, (await RunAsync("explain", Catalogue(), "   ")).ExitCode);

    [Fact]
    public async Task Explain_fails_on_an_assembly_that_is_not_there()
    {
        (int exitCode, _, _) = await RunAsync("explain", Path.Combine(_work, "absent.dll"), "ACME0001");

        Assert.Equal(ExitCodes.Failure, exitCode);
    }

    [Fact]
    public async Task Explain_naming_no_rule_is_a_usage_error()
        => Assert.Equal(ExitCodes.UsageError, (await RunAsync("explain", Catalogue())).ExitCode);
}
