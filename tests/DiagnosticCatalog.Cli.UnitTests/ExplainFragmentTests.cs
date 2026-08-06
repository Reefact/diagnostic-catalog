using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CatalogGen.UnitTests;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace DiagnosticCatalog.Cli.UnitTests;

/// <summary>
/// The suppression <c>dcat explain</c> prints is COMPILED, exactly as printed.
/// </summary>
/// <remarks>
/// <para>
/// It is the one line the command exists to produce, and the only claim worth making about it is
/// that it works where it is pasted. A test that searched the output for a substring made a weaker
/// claim wearing the same name: it could not see a missing <c>using</c>, a namespace left off, a
/// nesting level dropped, or an identifier C# will not accept unescaped — every one of which
/// produces a line that reads correctly and does not build.
/// </para>
/// <para>
/// Compiled with NO <c>using</c> directives of any kind, which is the property being asserted. A
/// reader pastes this into a file whose imports are their own business; a fragment that compiled
/// only beside <c>using System.Diagnostics.CodeAnalysis;</c> and <c>using Vendor.Catalog;</c> is a
/// fragment that fails for most of the people who copy it, at the moment they are least equipped to
/// know why.
/// </para>
/// </remarks>
public sealed class ExplainFragmentTests : IDisposable
{
    private readonly string _work = Directory.CreateTempSubdirectory("dcat-explain-").FullName;

    public void Dispose() => Directory.Delete(_work, recursive: true);

    /// <summary>The attribute the command printed, from its first line to the end of the output.</summary>
    private static string SuppressionIn(string output)
    {
        int start = output.IndexOf('[');

        Assert.True(start >= 0, "explain printed no attribute at all:" + Environment.NewLine + output);

        return output[start..].Trim();
    }

    /// <summary>
    /// Compiles <paramref name="suppression"/> onto a declaration of its own, against the catalogue
    /// it names and nothing else.
    /// </summary>
    private static void Compiles(string suppression, string cataloguePath)
    {
        string program = suppression + Environment.NewLine
                       + "public static class PastedByAReader" + Environment.NewLine
                       + "{" + Environment.NewLine
                       + "}" + Environment.NewLine;

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Consumer",
            syntaxTrees: [CSharpSyntaxTree.ParseText(program)],
            references: [.. PlatformReferences, MetadataReference.CreateFromFile(cataloguePath)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        ImmutableArray<Diagnostic> errors =
            [.. compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)];

        Assert.True(
            errors.IsEmpty,
            "the suppression dcat printed must compile where it is pasted; it reported: "
            + string.Join("; ", errors.Select(d => d.ToString())) + Environment.NewLine + program);
    }

    /// <summary>Runs <c>explain</c> over a fixture catalogue and returns what it printed.</summary>
    private async Task<(string Suppression, string Catalogue)> ExplainAsync(
        string fixtureSource, string ruleId, string assemblyName)
    {
        string catalogue = CatalogueFixture.Write(_work, assemblyName, fixtureSource);

        (int exitCode, string output, string error) = await CliRun.Async("explain", catalogue, ruleId);

        Assert.True(exitCode == ExitCodes.Success, "explain failed: " + error);

        return (SuppressionIn(output), catalogue);
    }

    [Fact]
    public async Task A_rule_in_a_namespace_compiles_where_it_is_pasted()
    {
        (string suppression, string catalogue) =
            await ExplainAsync(CatalogueFixture.TwoRulesAndOneRetired, "ACME0002", "Acme.Namespaced");

        Compiles(suppression, catalogue);
        Assert.Contains("global::Vendor.Catalog.AcmeRules.ACME0002.Category", suppression, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rule_in_the_global_namespace_compiles_where_it_is_pasted()
    {
        (string suppression, string catalogue) =
            await ExplainAsync(CatalogueFixture.RuleInTheGlobalNamespace, "GLB0001", "Acme.Global");

        Compiles(suppression, catalogue);
        Assert.Contains("global::GlobalRules.GLB0001.Category", suppression, StringComparison.Ordinal);
        // The failure a namespace assembled by concatenation produces, spelled out so a regression
        // is read as such rather than as "it did not compile".
        Assert.DoesNotContain("global::.", suppression, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rule_nested_several_types_deep_compiles_where_it_is_pasted()
    {
        (string suppression, string catalogue) =
            await ExplainAsync(CatalogueFixture.RuleNestedSeveralDeep, "DEEP0001", "Acme.Deep");

        Compiles(suppression, catalogue);
        Assert.Contains("global::Vendor.Catalog.Outer.Inner.DEEP0001.Category", suppression,
                        StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rule_whose_type_is_not_named_after_its_id_compiles_where_it_is_pasted()
    {
        // The §8.2 case: "ACME-0003" carries a character C# forbids in a type name, so the type is
        // ACME_0003 and both spellings exist. Only the TYPE name can be written at a use site.
        (string suppression, string catalogue) =
            await ExplainAsync(CatalogueFixture.RuleNamedApartFromItsId, "ACME-0003", "Acme.Apart");

        Compiles(suppression, catalogue);
        Assert.Contains("global::Vendor.Catalog.AcmeRules.ACME_0003.Category", suppression,
                        StringComparison.Ordinal);
        Assert.DoesNotContain("ACME-0003.Category", suppression, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rule_whose_every_name_is_a_keyword_compiles_where_it_is_pasted()
    {
        (string suppression, string catalogue) =
            await ExplainAsync(CatalogueFixture.RuleNamedWithKeywords, "KEY0001", "Acme.Keywords");

        Compiles(suppression, catalogue);
        Assert.Contains("global::Vendor.@class.@event.@lock.Category", suppression, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rule_with_no_container_at_all_compiles_where_it_is_pasted()
    {
        (string suppression, string catalogue) =
            await ExplainAsync(CatalogueFixture.NoProvenance, "LOOSE0001", "Acme.Loose");

        Compiles(suppression, catalogue);
        Assert.Contains("global::Vendor.Catalog.LOOSE0001.Category", suppression, StringComparison.Ordinal);
    }

    private static ImmutableArray<MetadataReference> PlatformReferences { get; } = ImmutableArray.CreateRange(
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => path.Length > 0)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)));
}
