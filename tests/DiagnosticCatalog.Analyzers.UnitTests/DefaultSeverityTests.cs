using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// The default severity of every shipped diagnostic, pinned.
/// </summary>
/// <remarks>
/// A default severity is a policy, not an implementation detail: it decides whether a consumer who
/// configures nothing gets a build that fails or a line in a log they will never read. Changing one
/// silently changes what referencing the package does to a stranger's build, and until this test
/// existed nothing in the suite observed it — the three use-site rules were promoted to
/// <see cref="DiagnosticSeverity.Error"/> and 1888 tests stayed green.
///
/// The split is between what a suppression IS and what a catalogue DECLARES. A consumer installs this
/// package so that no suppression in their codebase is a magic string, and a rule serving that promise
/// fails the build. The definition rules address whoever writes a catalogue, which is a different
/// audience with a different build, so they stay advisory.
/// </remarks>
public sealed class DefaultSeverityTests
{
    private static readonly IReadOnlyDictionary<string, DiagnosticSeverity> Expected =
        new Dictionary<string, DiagnosticSeverity>
        {
            // Use site — what a consumer references the package for.
            ["DCAT0001"] = DiagnosticSeverity.Error,
            ["DCAT0006"] = DiagnosticSeverity.Error,
            ["DCAT0007"] = DiagnosticSeverity.Error,

            // Definition — addressed to whoever authors a catalogue.
            ["DCAT0002"] = DiagnosticSeverity.Warning,
            ["DCAT0003"] = DiagnosticSeverity.Warning,
            ["DCAT0004"] = DiagnosticSeverity.Warning,
            ["DCAT0011"] = DiagnosticSeverity.Warning,

            // Use site, but still under-detecting: it misses an identifier reached through a constant,
            // so promoting it would fail builds unevenly for a reason the author cannot see.
            ["DCAT0009"] = DiagnosticSeverity.Warning,
        };

    private static IEnumerable<DiagnosticDescriptor> Shipped =>
        new DiagnosticAnalyzer[] { new SuppressionUsageAnalyzer(), new DiagnosticRuleDefinitionAnalyzer() }
            .SelectMany(analyzer => analyzer.SupportedDiagnostics);

    [Fact]
    public void Every_shipped_diagnostic_carries_the_severity_the_documentation_states()
    {
        Dictionary<string, DiagnosticSeverity> actual = Shipped.ToDictionary(d => d.Id, d => d.DefaultSeverity);

        Assert.Equal(Expected.OrderBy(e => e.Key), actual.OrderBy(a => a.Key));
    }

    [Fact]
    public void Every_shipped_diagnostic_is_enabled_by_default()
    {
        // A rule shipped disabled is a rule nobody runs. The opt-in one the specification describes,
        // DCAT0008, is not implemented — the day it is, it belongs in Expected with its own reason
        // rather than quietly failing this.
        ImmutableArray<string> disabled = Shipped.Where(d => !d.IsEnabledByDefault)
                                                 .Select(d => d.Id)
                                                 .ToImmutableArray();

        Assert.Empty(disabled);
    }
}
