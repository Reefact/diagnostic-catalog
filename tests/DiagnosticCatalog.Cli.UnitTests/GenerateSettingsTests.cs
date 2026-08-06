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
        => Assert.Equal("latest", new GenerateSettings().ReleaseToRead);

    [Fact]
    public void The_language_defaults_to_c_sharp()
        => Assert.Equal("cs", new GenerateSettings().LanguageToRead);

    [Fact]
    public void An_omitted_release_and_language_are_left_unset_rather_than_filled_in()
    {
        // What separates "the caller did not say" from "the caller said the default", and the
        // distinction the manifest rules downstream are built on. A default applied at PARSE time
        // erases it before any validation can see it.
        GenerateSettings settings = new();

        Assert.Null(settings.PackageVersion);
        Assert.Null(settings.Language);
    }

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
        Assert.Contains("different sources", result.Message);
        Assert.Contains("--package", result.Message);
        Assert.Contains("--assembly", result.Message);
    }

    [Fact]
    public void A_manifest_alongside_a_source_is_refused()
    {
        // The manifest carries its own sources, so this command line contradicts its own input.
        GenerateSettings settings = new() { Manifest = "catalogs.json", Package = "SonarAnalyzer.CSharp" };

        Assert.False(settings.Validate().Successful);
    }

    [Fact]
    public void A_package_on_disk_with_a_destination_is_accepted()
    {
        GenerateSettings settings = new()
        {
            Nupkg = "packages/Vendor.Analyzers.1.0.0.nupkg",
            Namespace = "Vendor.Catalog",
            Container = "VendorRule",
            Output = "src/Vendor.Catalog/VendorRules.g.cs",
        };

        Assert.True(settings.Validate().Successful);
    }

    [Fact]
    public void Describing_the_source_of_a_package_on_disk_is_allowed()
    {
        // Unlike a feed, which states its own release: a .nupkg can be rebuilt without its version
        // moving, and can be renamed, so what its .nuspec says is a default rather than the last word.
        GenerateSettings settings = new()
        {
            Nupkg = "v.nupkg",
            SourceName = "Vendor.Analyzers",
            SourceVersion = "9.9.9",
            Namespace = "N",
            Container = "C",
            Output = "o.g.cs",
        };

        Assert.True(settings.Validate().Successful);
    }

    [Fact]
    public void A_package_on_disk_and_a_feed_together_are_refused()
    {
        GenerateSettings settings = new()
        {
            Package = "SonarAnalyzer.CSharp",
            Nupkg = "v.nupkg",
            Namespace = "N",
            Container = "C",
            Output = "o.g.cs",
        };

        ValidationResult result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("different sources", result.Message);
    }

    [Fact]
    public void A_package_on_disk_and_an_assembly_together_are_refused()
    {
        GenerateSettings settings = new()
        {
            Nupkg = "v.nupkg",
            Assemblies = ["a.dll"],
            Namespace = "N",
            Container = "C",
            Output = "o.g.cs",
        };

        Assert.False(settings.Validate().Successful);
    }

    [Fact]
    public void A_feed_may_be_named_for_a_package()
    {
        GenerateSettings settings = new()
        {
            Package = "Vendor.Analyzers",
            Source = "maison",
            Namespace = "N",
            Container = "C",
            Output = "o.g.cs",
        };

        Assert.True(settings.Validate().Successful);
    }

    [Fact]
    public void Naming_a_feed_for_a_source_that_is_not_a_feed_is_refused()
    {
        // Nothing would be read from it, and nothing would say so — the same reason --source-name
        // is refused with --package, in the other direction.
        GenerateSettings settings = new()
        {
            Nupkg = "v.nupkg",
            Source = "maison",
            Namespace = "N",
            Container = "C",
            Output = "o.g.cs",
        };

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

    // --- a manifest carries every source AND every destination -------------------------------
    //
    // The manifest branch of Validate returned as soon as it had checked the source switches, so
    // everything else a manifest already states was accepted and then read from nowhere. The switch
    // that costs something is --source: a run meant for a private feed resolved against nuget.org
    // and said nothing about it.

    [Fact]
    public void A_feed_named_alongside_a_manifest_is_refused_rather_than_ignored()
    {
        GenerateSettings settings = new() { Manifest = "catalogs.json", Source = "my-internal-feed" };

        ValidationResult result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("--source", result.Message);
    }

    [Fact]
    public void A_destination_named_alongside_a_manifest_is_refused_rather_than_ignored()
    {
        GenerateSettings settings = new()
        {
            Manifest = "catalogs.json",
            Namespace = "N",
            Container = "C",
            Output = "o.g.cs",
        };

        ValidationResult result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("--namespace", result.Message);
        Assert.Contains("--container", result.Message);
        Assert.Contains("--output", result.Message);
    }

    [Fact]
    public void Describing_the_source_alongside_a_manifest_is_refused_rather_than_ignored()
    {
        GenerateSettings settings = new()
        {
            Manifest = "catalogs.json",
            SourceName = "Vendor",
            SourceVersion = "9.9.9",
            Configuration = "Debug",
        };

        ValidationResult result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("--source-name", result.Message);
        Assert.Contains("--source-version", result.Message);
        Assert.Contains("--configuration", result.Message);
    }

    [Fact]
    public void A_package_version_named_alongside_a_manifest_is_refused_rather_than_ignored()
    {
        GenerateSettings settings = new() { Manifest = "catalogs.json", PackageVersion = "1.2.3" };

        ValidationResult result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("--package-version", result.Message);
    }

    [Fact]
    public void A_default_release_typed_alongside_a_manifest_is_refused_like_any_other_value()
    {
        // `--package-version latest` beside a manifest is a caller who believes the switch takes
        // effect, and it does not: every entry states its own release. That the value happens to be
        // the one the tool would have chosen changes nothing about the misunderstanding — and it is
        // the likeliest one to be typed, since a caller spelling out the default is a caller making
        // sure of it.
        GenerateSettings settings = new() { Manifest = "catalogs.json", PackageVersion = "latest" };

        ValidationResult result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("--package-version", result.Message);
    }

    [Fact]
    public void A_default_language_typed_alongside_a_manifest_is_refused_like_any_other_value()
    {
        GenerateSettings settings = new() { Manifest = "catalogs.json", Language = "cs" };

        ValidationResult result = settings.Validate();

        Assert.False(result.Successful);
        Assert.Contains("--language", result.Message);
    }

    [Fact]
    public void A_manifest_carrying_only_what_describes_the_run_is_accepted()
    {
        // --summary and --date say what the RUN does rather than what a catalogue is, so a manifest
        // states neither and both are meaningful beside one.
        GenerateSettings settings = new()
        {
            Manifest = "catalogs.json",
            Date = "2026-01-01",
            Summary = "summary.md",
        };

        Assert.True(settings.Validate().Successful);
    }
}
