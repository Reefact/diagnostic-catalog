using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// DCAT0015: a project that publishes a catalogue and packs no <c>build/&lt;package id&gt;.props</c>
/// delivers no analyzer to its consumers, and says nothing about it (ADR-0038).
/// </summary>
/// <remarks>
/// <para>
/// The only diagnostic in this assembly whose trigger is not in the compilation. MSBuild classifies
/// the packaging and publishes the verdict through <c>CompilerVisibleProperty</c>; these tests supply
/// that verdict directly, which is what the compiler would have been handed. What they cannot cover
/// is the classification itself — that lives in
/// <c>src/DiagnosticCatalog/buildTransitive/DiagnosticCatalog.targets</c> and is exercised by
/// <c>tools/packaging/verify-consumption.sh</c> against a real restore.
/// </para>
/// <para>
/// The silence cases matter more than the reporting one here. A diagnostic about silence that fires
/// where it should not is worse than the silence it replaces: it appears on a package that is
/// correct, in a build its author cannot explain.
/// </para>
/// </remarks>
public sealed class CatalogueOptInTests
{
    private const string Catalogue = """
        using DiagnosticCatalog;

        [DiagnosticCategory]
        internal static class Categories
        {
            public const string Usage = "Usage";
        }

        [DiagnosticRule]
        public static class S1144
        {
            public const string Id = nameof(S1144);
            public const string Category = Categories.Usage;
        }
        """;

    /// <summary>A compilation that declares no rule at all — a consumer, not a catalogue.</summary>
    private const string NotACatalogue = """
        public static class Ordinary
        {
            public const string Id = "S1144";
        }
        """;

    private static Dictionary<string, string> Build(string optIn, string packageId = "Acme.Catalog") =>
        new() { ["DiagnosticCatalogAnalyzerOptIn"] = optIn, ["PackageId"] = packageId };

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        IReadOnlyDictionary<string, string>? properties) =>
        await AnalyzerHarness.RunAsync(
            new DiagnosticRuleDefinitionAnalyzer(),
            source,
            buildProperties: properties).ConfigureAwait(false);

    private static async Task<int> CountAsync(string source, IReadOnlyDictionary<string, string>? properties)
    {
        ImmutableArray<Diagnostic> reported = await RunAsync(source, properties).ConfigureAwait(false);

        return reported.Count(diagnostic => diagnostic.Id == "DCAT0015");
    }

    [Fact]
    public async Task A_catalogue_packing_no_opt_in_is_reported()
    {
        Assert.Equal(1, await CountAsync(Catalogue, Build("missing")));
    }

    [Fact]
    public async Task The_message_names_the_file_to_add()
    {
        ImmutableArray<Diagnostic> reported = await RunAsync(Catalogue, Build("missing", "Contoso.Rules"));

        Diagnostic reportedOptIn = Assert.Single(reported.Where(diagnostic => diagnostic.Id == "DCAT0015"));

        // The package id, not the assembly name: build/<package id>.props is the only name NuGet
        // imports, and the fixture's assembly is called Snippet precisely so a message built from the
        // wrong one would be caught here rather than read as correct.
        Assert.Contains("build/Contoso.Rules.props", reportedOptIn.GetMessage(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task It_is_reported_once_however_many_rules_the_catalogue_declares()
    {
        const string many = """
            using DiagnosticCatalog;

            [DiagnosticCategory]
            internal static class Categories
            {
                public const string Usage = "Usage";
            }

            [DiagnosticRule]
            public static class S1144
            {
                public const string Id = nameof(S1144);
                public const string Category = Categories.Usage;
            }

            [DiagnosticRule]
            public static class S2222
            {
                public const string Id = nameof(S2222);
                public const string Category = Categories.Usage;
            }
            """;

        // A packaging defect is one defect. Reporting it per rule would put a hundred identical
        // warnings on a real catalogue and bury every other diagnostic it carries.
        Assert.Equal(1, await CountAsync(many, Build("missing")));
    }

    [Fact]
    public async Task A_catalogue_that_packs_the_opt_in_is_not_reported()
    {
        Assert.Equal(0, await CountAsync(Catalogue, Build("packed")));
    }

    [Fact]
    public async Task A_project_declaring_no_rule_is_not_reported()
    {
        // Packable, no opt-in, and not a catalogue: an ordinary library that happens to reference the
        // foundation owes its consumers no analyzer.
        Assert.Equal(0, await CountAsync(NotACatalogue, Build("missing")));
    }

    [Fact]
    public async Task A_build_that_said_nothing_is_not_reported()
    {
        // The state every other test in this assembly compiles under, and the state of any project
        // built without the foundation's targets. Absence must read as "not measured", never as
        // "missing" — otherwise this fires on projects that never opted into being measured at all.
        Assert.Equal(0, await CountAsync(Catalogue, null));
    }

    [Fact]
    public async Task A_verdict_with_no_package_id_is_not_reported()
    {
        // The message names build/<package id>.props. With no id there is no file to name, and a
        // message that guessed one would send its reader to a path NuGet does not import.
        Dictionary<string, string> anonymous = new() { ["DiagnosticCatalogAnalyzerOptIn"] = "missing" };

        Assert.Equal(0, await CountAsync(Catalogue, anonymous));
    }

    [Fact]
    public async Task An_unrecognised_verdict_is_not_reported()
    {
        // Only the exact word the targets write means anything. A future value, a typo or a
        // half-migrated build reads as "not measured" rather than as a defect.
        Assert.Equal(0, await CountAsync(Catalogue, Build("Missing")));
        Assert.Equal(0, await CountAsync(Catalogue, Build("")));
        Assert.Equal(0, await CountAsync(Catalogue, Build("unknown")));
    }

    [Fact]
    public async Task It_does_not_disturb_the_other_definition_diagnostics()
    {
        // The catalogue fixture is deliberately correct, so DCAT0015 is the only thing to report. A
        // compilation-level action that also silenced or duplicated the symbol-level ones would show
        // up here rather than in whichever suite noticed later.
        ImmutableArray<Diagnostic> reported = await RunAsync(Catalogue, Build("missing"));

        Assert.Equal(["DCAT0015"], reported.Select(diagnostic => diagnostic.Id).ToArray());
    }
}
