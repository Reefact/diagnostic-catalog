using System;
using System.IO;
using System.Linq;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// Reading a catalogue back out of the assembly that ships it.
/// </summary>
/// <remarks>
/// This is the read <c>dcat list</c> and <c>dcat explain</c> rest on, and it is the only one in the
/// tool that answers a question about somebody else's package without running any of it. What it
/// reports is what a consumer is told their rules are, so the failures worth pinning are the quiet
/// ones: a rule counted twice, a retired rule read as current, provenance silently absent.
/// </remarks>
public sealed class CatalogueInspectorTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("catalogue-inspector-").FullName;

    public void Dispose() => Directory.Delete(_work, recursive: true);

    [Fact]
    public void A_catalogue_reports_the_release_it_was_generated_from()
    {
        string path = CatalogueFixture.Write(_work, "Acme.Catalog", CatalogueFixture.TwoRulesAndOneRetired);

        CatalogueContents? contents = CatalogueInspector.Read(path);

        Assert.NotNull(contents);
        Assert.Equal("Acme.Analyzers", contents.Source);
        Assert.Equal("1.2.3", contents.SourceVersion);
        Assert.Equal("2026-07-30", contents.GeneratedOn);
    }

    [Fact]
    public void Every_rule_is_reported_once()
    {
        // Once, not twice. Enumerating the assembly's types AND recursing through their nested ones
        // read every rule a second time, which showed up as a catalogue of 394 rules where the file
        // declared 197 — a count nobody would question until they compared it with the source.
        string path = CatalogueFixture.Write(_work, "Acme.Catalog", CatalogueFixture.TwoRulesAndOneRetired);

        CatalogueContents? contents = CatalogueInspector.Read(path);

        Assert.NotNull(contents);
        Assert.Equal(["ACME0001", "ACME0002"], contents.Rules.Select(r => r.Id));
    }

    [Fact]
    public void Rules_are_published_by_identifier_rather_than_by_declaration_order()
    {
        // The fixture declares ACME0002 first. A reader that published in metadata order would pass
        // every other assertion here and list a catalogue in an order nobody can predict.
        string path = CatalogueFixture.Write(_work, "Acme.Catalog", CatalogueFixture.TwoRulesAndOneRetired);

        CatalogueContents? contents = CatalogueInspector.Read(path);

        Assert.NotNull(contents);
        Assert.Equal(contents.Rules.Select(r => r.Id).OrderBy(id => id, StringComparer.Ordinal),
                     contents.Rules.Select(r => r.Id));
    }

    [Fact]
    public void A_rule_carries_the_container_a_suppression_has_to_name()
    {
        // The whole point of explaining a rule is producing a line that can be copied. A suppression
        // is written AcmeRules.ACME0002.Category, so a reader that dropped the container would emit
        // a reference that does not compile.
        string path = CatalogueFixture.Write(_work, "Acme.Catalog", CatalogueFixture.TwoRulesAndOneRetired);

        CataloguedRule rule = Assert.Single(
            CatalogueInspector.Read(path)!.Rules, r => r.Id == "ACME0002");

        Assert.Equal("AcmeRules", rule.Container);
        Assert.Equal("Usage", rule.Category);
        Assert.Equal("https://acme.example/ACME0002", rule.HelpLinkUri);
        Assert.False(rule.Retired);
    }

    [Fact]
    public void A_retired_rule_is_reported_as_retired()
    {
        // ADR-0010: a rule the vendor stopped declaring is carried forward as [Obsolete] rather than
        // deleted. Reading it back as current would tell a consumer to keep using it.
        string path = CatalogueFixture.Write(_work, "Acme.Catalog", CatalogueFixture.TwoRulesAndOneRetired);

        CataloguedRule rule = Assert.Single(
            CatalogueInspector.Read(path)!.Rules, r => r.Id == "ACME0001");

        Assert.True(rule.Retired);
        Assert.Equal("Naming", rule.Category);
    }

    [Fact]
    public void A_rule_declaring_no_help_link_reports_an_empty_one_rather_than_null()
    {
        // Empty rather than null, because every consumer of this record would otherwise have to
        // decide what a null means, and the answer — "the vendor published no link" — is the same.
        string path = CatalogueFixture.Write(_work, "Acme.Catalog", CatalogueFixture.TwoRulesAndOneRetired);

        CataloguedRule rule = Assert.Single(
            CatalogueInspector.Read(path)!.Rules, r => r.Id == "ACME0001");

        Assert.Equal(string.Empty, rule.HelpLinkUri);
    }

    [Fact]
    public void A_catalogue_with_no_recorded_source_still_reads_its_rules()
    {
        // Provenance is what a generated catalogue stamps; a hand-written one may carry none. The
        // rules are the answer either way, and refusing the read would make the tool useless against
        // exactly the catalogues a consumer wrote themselves.
        string path = CatalogueFixture.Write(_work, "Loose.Catalog", CatalogueFixture.NoProvenance);

        CatalogueContents? contents = CatalogueInspector.Read(path);

        Assert.NotNull(contents);
        Assert.Null(contents.Source);
        Assert.Null(contents.SourceVersion);
        Assert.Null(contents.GeneratedOn);
        Assert.Equal("LOOSE0001", Assert.Single(contents.Rules).Id);
    }

    [Fact]
    public void A_rule_declared_outside_any_container_reports_an_empty_container()
    {
        string path = CatalogueFixture.Write(_work, "Loose.Catalog", CatalogueFixture.NoProvenance);

        Assert.Equal(string.Empty, Assert.Single(CatalogueInspector.Read(path)!.Rules).Container);
    }

    [Fact]
    public void An_assembly_that_is_not_a_catalogue_reads_as_a_catalogue_of_nothing()
    {
        // Not an error. "This assembly carries no rules" is a true and useful answer, and the caller
        // reports the count; failing here would make a mistyped path indistinguishable from a
        // package that simply has none.
        string path = CatalogueFixture.Write(_work, "Ordinary.Library", CatalogueFixture.NotACatalogue);

        CatalogueContents? contents = CatalogueInspector.Read(path);

        Assert.NotNull(contents);
        Assert.Empty(contents.Rules);
    }

    [Fact]
    public void A_rule_declaring_none_of_its_constants_falls_back_to_what_metadata_does_say()
    {
        // The shape DCAT0003 reports at compile time — and a REFERENCED assembly can carry it
        // anyway, having been built elsewhere. Reading it as the type's own name is the only honest
        // answer available; dropping the rule would make a malformed catalogue look like a short
        // one, which is the failure this tool exists to make visible rather than silent.
        string path = CatalogueFixture.Write(_work, "Broken.Catalog", CatalogueFixture.MarkedButIncomplete);

        CataloguedRule rule = Assert.Single(CatalogueInspector.Read(path)!.Rules);

        Assert.Equal("HALF0001", rule.Id);
        Assert.Equal(string.Empty, rule.Category);
        Assert.Equal(string.Empty, rule.HelpLinkUri);
    }

    [Fact]
    public void An_assembly_that_is_not_there_is_reported_rather_than_thrown()
    {
        // Reported as null, because the caller turns it into an exit code. A stack trace would tell
        // a pipeline that the tool is broken rather than that its argument is.
        Assert.Null(CatalogueInspector.Read(Path.Combine(_work, "absent.dll")));
    }

    [Fact]
    public void A_file_that_is_not_an_assembly_is_reported_rather_than_thrown()
    {
        string path = Path.Combine(_work, "not-an-assembly.dll");
        File.WriteAllText(path, "this is not a PE file");

        Assert.Null(CatalogueInspector.Read(path));
    }

    [Fact]
    public void A_relative_path_is_resolved_against_the_working_directory()
    {
        // The tool is run from wherever the caller happens to be, so a path they can type has to be
        // a path it can read.
        string path = CatalogueFixture.Write(_work, "Acme.Catalog", CatalogueFixture.TwoRulesAndOneRetired);
        string original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_work);

            Assert.NotNull(CatalogueInspector.Read(Path.GetFileName(path)));
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public void The_reader_reports_a_rule_s_type_name_apart_from_its_identifier()
    {
        // They are the same string for almost every rule, which is exactly why carrying only one of
        // them went unnoticed: a use site must write the TYPE, and the identifier is what the rule
        // declares. Where §8.2 forces them apart, a reader holding one cannot recover the other.
        CatalogueContents? contents = CatalogueInspector.Read(
            CatalogueFixture.Write(_work, "Named.Apart", CatalogueFixture.RuleNamedApartFromItsId));

        Assert.NotNull(contents);

        CataloguedRule rule = Assert.Single(contents.Rules);

        Assert.Equal("ACME-0003", rule.Id);
        Assert.Equal("ACME_0003", rule.TypeName);
        Assert.Equal("AcmeRules", rule.Container);
    }
}
