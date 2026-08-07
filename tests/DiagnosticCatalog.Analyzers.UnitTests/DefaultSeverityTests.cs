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
/// The tiers are ADR-0040's, restated here as data so that placing a new id is a decision somebody
/// has to write down rather than a default it inherits from whichever neighbour it was declared
/// beside. An <see cref="DiagnosticSeverity.Error"/> means the mandatory contract is not satisfied,
/// the suppression is incorrect or has no effect, or the package does not deliver the behaviour it
/// promises. A <see cref="DiagnosticSeverity.Warning"/> means the code works today and stays liable
/// to drift, badly anchored, or misleading. <see cref="DiagnosticSeverity.Info"/> is a legitimate
/// exception nobody can repair, reported so the boundary is visible.
///
/// What the tiers deliberately are NOT is a split between the use site and the definition site. That
/// was the previous model, and it put a rule declaration missing its `Id` — a catalogue member no
/// suppression can name — a tier below a suppression that names it wrongly, though neither works.
/// </remarks>
public sealed class DefaultSeverityTests
{
    private static readonly IReadOnlyDictionary<string, DiagnosticSeverity> Expected =
        new Dictionary<string, DiagnosticSeverity>
        {
            // Error — the mandatory contract is not satisfied, the suppression is incorrect or has no
            // effect, or the package does not deliver what it promises.
            //
            // DCAT0001, DCAT0006 and DCAT0007 are the suppression itself: a pair naming two rules, a
            // pair of literals a reference would replace, a pair left half migrated.
            ["DCAT0001"] = DiagnosticSeverity.Error,
            ["DCAT0006"] = DiagnosticSeverity.Error,
            ["DCAT0007"] = DiagnosticSeverity.Error,

            // DCAT0002-DCAT0004 are §8's structural contract: a rule missing either constant, or
            // declared in a shape that cannot carry one, publishes nothing a suppression can name.
            ["DCAT0002"] = DiagnosticSeverity.Error,
            ["DCAT0003"] = DiagnosticSeverity.Error,
            ["DCAT0004"] = DiagnosticSeverity.Error,

            // DCAT0009 is a suppression every tool in the chain discards. It under-detects — an
            // identifier reached through a constant is missed — and that is deliberately NOT a reason
            // to lower it: an undetected form is a false negative, and it says nothing about the
            // certainty of the ones reported.
            ["DCAT0009"] = DiagnosticSeverity.Error,

            // DCAT0014 is the justification ADR-0039 requires. Presence is mandatory; what the value
            // says is never judged.
            ["DCAT0014"] = DiagnosticSeverity.Error,

            // DCAT0015 is the package failing its own promise: a catalogue whose consumers are checked
            // by nothing, silently.
            ["DCAT0015"] = DiagnosticSeverity.Error,

            // Warning — it works today, and is liable to drift (DCAT0011, DCAT0012) or misleads whoever
            // reads the use site (DCAT0013).
            ["DCAT0011"] = DiagnosticSeverity.Warning,
            ["DCAT0012"] = DiagnosticSeverity.Warning,
            ["DCAT0013"] = DiagnosticSeverity.Warning,

            // Info, and the only one shipped. Alone in that tier because it is the only rule reporting
            // something its author cannot act on: the identifier carries a character C# will not take,
            // so the name is already as close as a name can get. It is reported anyway because DCAT0013
            // fails the same comparison one step later, and an exception nobody can see is one nobody
            // can reason about — Info is what makes the boundary visible and configurable without
            // asking for work that does not exist.
            ["DCAT0005"] = DiagnosticSeverity.Info,
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
