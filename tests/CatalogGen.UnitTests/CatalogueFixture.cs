using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// Compiles a catalogue assembly to disk, so a test can state exactly what is in one.
/// </summary>
/// <remarks>
/// <para>
/// Compiled rather than copied from the catalogues this repository ships, and the reason is
/// coverage of the SHAPES rather than of one vendor's contents. A retired rule, a rule carrying a
/// help link and a rule carrying none, an assembly with no provenance at all — the real catalogues
/// happen to contain some of those and never all of them, so asserting against one would leave the
/// branches that matter untested and would change the day the mirrored release changed.
/// </para>
/// <para>
/// The markers are declared INSIDE the fixture rather than referenced from the foundation. That is
/// the §7.2 shape — a catalogue may embed its own marker instead of taking a package dependency —
/// and it is what makes these assemblies self-contained: <see cref="CatalogueInspector"/> resolves
/// the attribute types before it can recognise them, so a fixture referencing the foundation would
/// only be readable where the foundation happened to sit beside it.
/// </para>
/// </remarks>
internal static class CatalogueFixture
{
    /// <summary>The marker declarations every fixture carries, under the names the reader matches on.</summary>
    internal const string Markers = """
        namespace DiagnosticCatalog
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            internal sealed class DiagnosticRuleAttribute : System.Attribute { }

            [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
            internal sealed class CatalogSourceAttribute : System.Attribute
            {
                public CatalogSourceAttribute(string source, string sourceVersion, string generatedOn)
                {
                    Source = source;
                    SourceVersion = sourceVersion;
                    GeneratedOn = generatedOn;
                }

                public string Source { get; }
                public string SourceVersion { get; }
                public string GeneratedOn { get; }
            }
        }
        """;

    /// <summary>
    /// A catalogue with the four shapes worth telling apart: a rule with a help link and one
    /// without, a retired rule, and a declaration order that is not the published order.
    /// </summary>
    internal const string TwoRulesAndOneRetired = """
        [assembly: DiagnosticCatalog.CatalogSource("Acme.Analyzers", "1.2.3", "2026-07-30")]

        namespace Vendor.Catalog
        {
            public static class AcmeRules
            {
                // Declared second on purpose: the reader publishes by identifier, not by position.
                [DiagnosticCatalog.DiagnosticRule]
                public static class ACME0002
                {
                    public const string Id = "ACME0002";
                    public const string Category = "Usage";
                    public const string HelpLinkUri = "https://acme.example/ACME0002";
                }

                [DiagnosticCatalog.DiagnosticRule]
                [System.Obsolete("ACME0001 is no longer declared by Acme.Analyzers 1.2.3.")]
                public static class ACME0001
                {
                    public const string Id = "ACME0001";
                    public const string Category = "Naming";
                }
            }
        }
        """;

    /// <summary>
    /// A rule whose TYPE is not named after the identifier it declares.
    /// </summary>
    /// <remarks>
    /// The §8.2 case the specification blesses and DCAT0005 reports rather than refuses: an
    /// identifier carrying a character C# forbids in a type name, so the type is that identifier
    /// legalised. Both spellings then exist and only one of them can be written at a use site — the
    /// TYPE name — which is what makes this the shape a copyable reference has to get right.
    /// </remarks>
    internal const string RuleNamedApartFromItsId = """
        namespace Vendor.Catalog
        {
            public static class AcmeRules
            {
                [DiagnosticCatalog.DiagnosticRule]
                public static class ACME_0003
                {
                    public const string Id = "ACME-0003";
                    public const string Category = "Naming";
                }
            }
        }
        """;

    /// <summary>A catalogue declared in the global namespace.</summary>
    /// <remarks>
    /// Nothing requires a catalogue to be namespaced, and a hand-written one for an internal ruleset
    /// often is not. It is the shape where a reference assembled from a namespace and a name has a
    /// leading dot in it, which does not compile — the same failure the container-less rule below
    /// produces one level up.
    /// </remarks>
    internal const string RuleInTheGlobalNamespace = """
        public static class GlobalRules
        {
            [DiagnosticCatalog.DiagnosticRule]
            public static class GLB0001
            {
                public const string Id = "GLB0001";
                public const string Category = "Usage";
            }
        }
        """;

    /// <summary>A rule nested three types deep.</summary>
    /// <remarks>
    /// A reference that carried only the IMMEDIATE declaring type would name
    /// <c>Inner.DEEP0001</c>, which binds to nothing from a file that has not imported its way into
    /// <c>Outer</c>. The whole chain is what a use site has to write.
    /// </remarks>
    internal const string RuleNestedSeveralDeep = """
        namespace Vendor.Catalog
        {
            public static class Outer
            {
                public static class Inner
                {
                    [DiagnosticCatalog.DiagnosticRule]
                    public static class DEEP0001
                    {
                        public const string Id = "DEEP0001";
                        public const string Category = "Usage";
                    }
                }
            }
        }
        """;

    /// <summary>A catalogue every part of whose name is a C# keyword.</summary>
    /// <remarks>
    /// Metadata has no keywords, so a namespace called <c>class</c> and a type called <c>event</c>
    /// are perfectly ordinary to a reader and unwritable in C# without <c>@</c>. Contrived as a
    /// catalogue and not as a case: <c>dcat explain</c> is pointed at whatever assembly a consumer
    /// has, and the one thing its output may not be is uncompilable.
    /// </remarks>
    internal const string RuleNamedWithKeywords = """
        namespace Vendor.@class
        {
            public static class @event
            {
                [DiagnosticCatalog.DiagnosticRule]
                public static class @lock
                {
                    public const string Id = "KEY0001";
                    public const string Category = "Usage";
                }
            }
        }
        """;

    /// <summary>An assembly that is perfectly valid and is not a catalogue.</summary>
    internal const string NotACatalogue = """
        namespace Vendor.Ordinary
        {
            public static class Helper
            {
                public const string Id = "not a rule";
            }
        }
        """;

    /// <summary>Rules with no <c>[assembly: CatalogSource]</c>, which a hand-written catalogue may omit.</summary>
    internal const string NoProvenance = """
        namespace Vendor.Catalog
        {
            [DiagnosticCatalog.DiagnosticRule]
            public static class LOOSE0001
            {
                public const string Id = "LOOSE0001";
                public const string Category = "Usage";
            }
        }
        """;

    /// <summary>
    /// A type carrying the marker and none of the constants a rule must declare — the shape
    /// DCAT0003 exists to report at compile time, and which a referenced assembly can carry anyway.
    /// </summary>
    internal const string MarkedButIncomplete = """
        namespace Vendor.Catalog
        {
            public static class BrokenRules
            {
                [DiagnosticCatalog.DiagnosticRule]
                public static class HALF0001
                {
                }
            }
        }
        """;

    /// <summary>Compiles <paramref name="source"/> into <paramref name="directory"/> and returns its path.</summary>
    internal static string Write(string directory, string name, string source)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: name,
            syntaxTrees: [CSharpSyntaxTree.ParseText(Markers), CSharpSyntaxTree.ParseText(source)],
            references: PlatformReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + ".dll");

        EmitResult result = compilation.Emit(path);

        // A fixture that failed to build would be read as a catalogue with no rules, and every
        // assertion about its contents would pass for the wrong reason.
        Assert.True(
            result.Success,
            "the fixture catalogue must compile; it reported: "
            + string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));

        return path;
    }

    private static ImmutableArray<MetadataReference> PlatformReferences { get; } = ImmutableArray.CreateRange(
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(path => path.Length > 0)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)));
}
