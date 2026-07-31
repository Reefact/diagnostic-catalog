using Spectre.Console;
using Xunit;

namespace DiagnosticCatalog.Cli.UnitTests;

/// <summary>
/// What <c>dcat generate</c> accepts and what it refuses.
/// </summary>
/// <remarks>
/// The tool runs unattended in the nightly job and, once published, on machines nobody here will
/// see. What it does with a command line that is wrong therefore matters as much as what it does
/// with one that is right: an invocation quietly reinterpreted is a catalogue generated from
/// something the caller did not ask for, and nothing downstream would say so.
/// </remarks>
public sealed class GenerateSettingsTests
{
    [Fact]
    public void A_manifest_on_its_own_is_accepted()
    {
        GenerateSettings settings = new() { Manifest = "eng/catalogs.json" };

        Assert.True(settings.Validate().Successful);
    }

    [Fact]
    public void A_package_with_a_destination_is_accepted()
    {
        GenerateSettings settings = new()
        {
            Package = "SonarAnalyzer.CSharp",
            Namespace = "DiagnosticCatalog.Sonar",
            Container = "SonarRule",
            Output = "src/DiagnosticCatalog.Sonar/SonarRules.g.cs",
        };

        Assert.True(settings.Validate().Successful);
    }

    [Fact]
    public void The_upstream_release_defaults_to_the_latest_stable()
        => Assert.Equal("latest", new GenerateSettings().PackageVersion);

    [Fact]
    public void An_assembly_with_a_destination_is_accepted_without_a_package()
    {
        GenerateSettings settings = new()
        {
            Assemblies = ["bin/My.Analyzers.dll"],
            Namespace = "My.Catalog",
            Container = "MyRule",
            Output = "src/My.Catalog/MyRules.g.cs",
        };

        Assert.True(settings.Validate().Successful);
        Assert.Null(settings.Package);
    }

    [Fact]
    public void A_package_and_an_assembly_together_are_refused()
    {
        // Both name a source. Resolving it by precedence would generate a catalogue from something
        // the caller did not ask for, and nothing in the output would say which one was read.
        GenerateSettings settings = new()
        {
            Package = "SonarAnalyzer.CSharp",
            Assemblies = ["bin/My.Analyzers.dll"],
            Namespace = "N",
            Container = "C",
            Output = "o.g.cs",
        };

        ValidationResult result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("two different sources", result.Message);
    }

    [Fact]
    public void A_manifest_alongside_a_source_is_refused()
    {
        // The manifest carries its own sources, so this command line contradicts its own input.
        GenerateSettings settings = new() { Manifest = "catalogs.json", Package = "SonarAnalyzer.CSharp" };

        Assert.False(settings.Validate().Successful);
    }

    [Fact]
    public void Naming_no_source_at_all_is_refused()
        => Assert.False(new GenerateSettings().Validate().Successful);

    [Fact]
    public void A_source_with_nowhere_to_write_it_is_refused_naming_what_is_missing()
    {
        GenerateSettings settings = new() { Package = "SonarAnalyzer.CSharp", Namespace = "N" };

        ValidationResult result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("--container", result.Message);
        Assert.Contains("--output", result.Message);
        Assert.DoesNotContain("--namespace", result.Message);
    }

    [Fact]
    public void Describing_the_source_of_a_package_is_refused_rather_than_ignored()
    {
        // A package states its own name and release. A caller who passed these believes they took
        // effect, and a catalogue whose recorded source came from somewhere else is exactly the
        // drift that recording a source exists to prevent.
        GenerateSettings settings = new()
        {
            Package = "SonarAnalyzer.CSharp",
            SourceVersion = "9.9.9",
            Namespace = "N",
            Container = "C",
            Output = "o.g.cs",
        };

        Assert.False(settings.Validate().Successful);
    }
}
