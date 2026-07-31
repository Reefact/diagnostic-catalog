using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// The generator runs unattended in the nightly job, where nobody reads the arguments it was given.
/// What it accepts and what it refuses is therefore worth pinning down.
/// </summary>
public sealed class CommandLineTests
{
    private static readonly string[] SingleCatalogue =
    [
        "--package", "SonarAnalyzer.CSharp",
        "--version", "latest",
        "--namespace", "DiagnosticCatalog.Sonar",
        "--container", "SonarRule",
        "--output", "src/DiagnosticCatalog.Sonar/SonarRules.g.cs",
    ];

    [Fact]
    public void A_complete_single_catalogue_invocation_is_accepted()
    {
        Cli? cli = CommandLine.ParseArgs(SingleCatalogue);

        Assert.NotNull(cli);
        Assert.Equal("SonarAnalyzer.CSharp", cli!.Package);
        Assert.Equal("latest", cli.Version);
        Assert.Equal("DiagnosticCatalog.Sonar", cli.Namespace);
        Assert.Equal("SonarRule", cli.Container);
        Assert.Equal("src/DiagnosticCatalog.Sonar/SonarRules.g.cs", cli.Output);
    }

    [Fact]
    public void A_manifest_on_its_own_is_accepted()
    {
        Cli? cli = CommandLine.ParseArgs(["--manifest", "eng/catalogs.json"]);

        Assert.NotNull(cli);
        Assert.Equal("eng/catalogs.json", cli!.Manifest);
        Assert.Null(cli.Package);
    }

    [Fact]
    public void The_language_defaults_to_c_sharp_when_it_is_not_given()
        => Assert.Equal("cs", CommandLine.ParseArgs(SingleCatalogue)!.Language);

    [Fact]
    public void The_date_is_left_unset_so_the_generator_can_default_it()
        => Assert.Null(CommandLine.ParseArgs(SingleCatalogue)!.Date);

    [Fact]
    public void A_pinned_date_is_carried_through()
        => Assert.Equal("2026-01-01", CommandLine.ParseArgs([.. SingleCatalogue, "--date", "2026-01-01"])!.Date);

    [Fact]
    public void An_incomplete_single_catalogue_invocation_is_refused()
    {
        // Every one of the five is required together; dropping --output alone is enough.
        Assert.Null(CommandLine.ParseArgs(SingleCatalogue[..^2]));
    }

    [Fact]
    public void Nothing_at_all_is_refused() => Assert.Null(CommandLine.ParseArgs([]));

    [Fact]
    public void An_unknown_argument_is_refused()
        => Assert.Null(CommandLine.ParseArgs([.. SingleCatalogue, "--colour", "blue"]));

    [Fact]
    public void A_trailing_argument_with_no_value_is_ignored_rather_than_refused()
    {
        // Documented, not endorsed. Arguments are read in pairs and the loop stops when fewer than
        // two remain, so a switch left dangling at the end is neither applied nor reported. Here the
        // invocation is complete without it and the run proceeds as though --date were absent.
        Cli? cli = CommandLine.ParseArgs([.. SingleCatalogue, "--date"]);

        Assert.NotNull(cli);
        Assert.Null(cli!.Date);
    }

    // --- reading assemblies already on disk rather than a package ------------------

    private static readonly string[] Destination =
    [
        "--namespace", "Vendor.Catalog",
        "--container", "VendorRule",
        "--output", "src/Vendor.Catalog/VendorRules.g.cs",
    ];

    [Fact]
    public void An_assembly_and_a_destination_are_accepted_without_a_package()
    {
        Cli? cli = CommandLine.ParseArgs(["--assembly", "bin/My.Analyzers.dll", .. Destination]);

        Assert.NotNull(cli);
        Assert.Equal(["bin/My.Analyzers.dll"], cli!.Assemblies);
        Assert.Null(cli.Package);
    }

    [Fact]
    public void Repeating_the_assembly_switch_accumulates_rather_than_overwrites()
    {
        // A vendor's rules are routinely split across assemblies that have to be read together:
        // StyleCop declares its across the analyzer and the code-fix assembly. Overwriting would
        // generate a catalogue silently missing every rule declared by all but the last.
        Cli? cli = CommandLine.ParseArgs(
            ["--assembly", "a.dll", "--assembly", "b.dll", .. Destination]);

        Assert.NotNull(cli);
        Assert.Equal(["a.dll", "b.dll"], cli!.Assemblies);
    }

    [Fact]
    public void A_package_and_an_assembly_together_are_refused()
    {
        // Both name a source. Resolving it by precedence would generate a catalogue from something
        // the caller did not ask for, and nothing in the output would say which one was read.
        Assert.Null(CommandLine.ParseArgs([.. SingleCatalogue, "--assembly", "a.dll"]));
    }

    [Fact]
    public void An_assembly_with_nowhere_to_write_it_is_refused()
        => Assert.Null(CommandLine.ParseArgs(["--assembly", "bin/My.Analyzers.dll"]));

    [Fact]
    public void The_source_name_and_version_overrides_are_carried_through()
    {
        Cli? cli = CommandLine.ParseArgs(
            ["--assembly", "a.dll", .. Destination, "--source-name", "My.Analyzers", "--source-version", "1.4.0"]);

        Assert.NotNull(cli);
        Assert.Equal("My.Analyzers", cli!.SourceName);
        Assert.Equal("1.4.0", cli.SourceVersion);
    }

    [Fact]
    public void A_package_invocation_names_no_assembly()
        => Assert.Empty(CommandLine.ParseArgs(SingleCatalogue)!.Assemblies);
}
