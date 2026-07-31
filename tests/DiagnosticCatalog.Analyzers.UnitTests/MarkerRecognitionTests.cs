using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// How the marker is recognised — the single place where getting it wrong costs everything and shows
/// nothing.
/// </summary>
/// <remarks>
/// §7.2 requires matching on the fully qualified metadata name rather than on a resolved symbol. The
/// tempting implementation resolves DiagnosticCatalog.DiagnosticRuleAttribute once and compares with
/// SymbolEqualityComparer, and it passes every test written against a snippet that references the real
/// foundation. The two fixtures below are the ones it fails — and its failure mode is not a wrong
/// answer but silence: no rule found, no diagnostic reported, output identical to a clean codebase.
/// </remarks>
public sealed class MarkerRecognitionTests
{
    private static readonly DiagnosticAnalyzer Analyzer = new DiagnosticRuleDefinitionAnalyzer();

    [Fact]
    public Task A_catalogue_declaring_its_own_marker_is_still_analysed() =>
        // The dependency-free pattern §7.2 blesses, the one PolySharp uses for IsExternalInit: a
        // catalogue declares its own internal attribute in the DiagnosticCatalog namespace rather than
        // taking a package reference. It is a different symbol from the foundation's, so a symbol
        // comparison finds nothing here. The rule below is not static, and DCAT0002 must still fire.
        AnalyzerHarness.ReportsAsync(Analyzer, """
            namespace DiagnosticCatalog
            {
                [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
                internal sealed class DiagnosticRuleAttribute : System.Attribute
                {
                }
            }

            namespace Vendor.Catalog
            {
                [global::DiagnosticCatalog.DiagnosticRule]
                public sealed class JD0007
                {
                    public const string Id = nameof(JD0007);
                    public const string Category = "Usage";
                }
            }
            """, "DCAT0002");

    [Fact]
    public Task An_attribute_of_the_same_short_name_in_another_namespace_is_not_the_marker() =>
        // The converse: recognition is by FULL metadata name, so somebody else's [DiagnosticRule] is
        // not this library's. Matching on the simple name would claim types that are none of our
        // business and report contract violations against them.
        AnalyzerHarness.ReportsNothingAsync(Analyzer, """
            namespace Somewhere.Else
            {
                [System.AttributeUsage(System.AttributeTargets.Class)]
                internal sealed class DiagnosticRuleAttribute : System.Attribute
                {
                }

                [DiagnosticRule]
                public sealed class NotOurRule
                {
                }
            }
            """);
}
