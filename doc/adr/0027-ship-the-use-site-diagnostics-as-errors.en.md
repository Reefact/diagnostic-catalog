# ADR-0027 | Ship the use-site diagnostics as errors

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0027-ship-the-use-site-diagnostics-as-errors.fr.md)

**Status:** Superseded by [ADR-0040](0040-grade-every-dcat-diagnostic-by-what-it-says.en.md)
**Proposed:** 2026-08-02
**Accepted:** 2026-08-02
**Decision Makers:** Reefact

## Context

Every `DCAT` diagnostic was going to ship at `Warning`. Nothing decided that; it is the
default a `DiagnosticDescriptor` gets when the author does not think about it, and no
ADR had ever examined it.

Ask instead what referencing `DiagnosticCatalog.Sonar` is *for*. A team adds it so that
no suppression in their codebase is a magic string — so that `[SuppressMessage("Major
Code Smell", "S1144")]` becomes `[SuppressMessage(SonarRule.S1144.Category,
SonarRule.S1144.Id)]` and stays correct when the vendor moves the rule, when a rename
sweeps the solution, when someone greps for who suppresses what.

That property is not partial. A codebase where half the suppressions are references and
half are literals does not have it; it has it in the places somebody remembered. The
guarantee is a property of the whole, and a diagnostic that reports the gap as a warning
leaves the whole to attention.

The configuration guide had already reached that conclusion in its own advice column,
recommending `error` for `DCAT0001` and `DCAT0007` and "error once converted" for
`DCAT0006`. The shipped default was simply lagging the documentation that described it.

## Decision

**The three use-site diagnostics ship as `DiagnosticSeverity.Error`:** `DCAT0001`,
`DCAT0006`, `DCAT0007`.

The rest stay `Warning`:

* `DCAT0002`, `DCAT0003`, `DCAT0004` address whoever **authors** a catalogue, not whoever
  consumes one. Different audience, different build, and for a generated catalogue the
  generator already guarantees them.
* `DCAT0009` is use-site and would qualify, but it still misses an identifier reached
  through a constant. Promoting a rule that under-detects fails builds unevenly, for a
  reason the author cannot see from the diagnostic.

## Rationale

The argument is about which way the default should be wrong.

At `Warning`, a team that wants the guarantee has to know a line exists to write, find
it, and write it. Most never will, and the ones who most need it are the ones least
likely to be reading the configuration guide. At `Error`, a team that does *not* want it
writes one line, deliberately, having read the message that told them why.

Both are one line. Only one of them is discovered by default.

Severity is per-rule and per-path overridable through ordinary `.editorconfig` — no
proprietary format, no MSBuild property — so `Error` is not a position anyone is stuck
with:

```ini
dotnet_diagnostic.DCAT0006.severity = suggestion
```

## Consequences

**Referencing the package can turn a green build red.** A codebase with existing literal
suppressions fails on the first build after adding it. That is the intended signal and
also the worst moment to meet it, so the configuration guide gives the downgrade line
next to the table, and `DCAT0006` — the only one of the three reporting *work not yet
done* rather than *something already wrong* — is the one it names.

**It is not a breaking change.** `DiagnosticCatalog.Analyzers` publishes for the first
time in `1.0.0-preview.1`. No consumer has a build that this changes; the severity is
part of what the package is on the day it appears.

**The fix for the intermediate constant became a prerequisite.** A suppression naming a
rule member hoisted into a named constant was reported by `DCAT0007` — a false positive
the guide's own accepted-forms list contradicted. As a warning it was noise. As an error
it would fail the build of somebody doing exactly what the documentation asks, so
`SuppressionAttribute.Resolve` now follows one hop into a constant's initialiser. That
went in first, with a test seen failing against the unfixed analyzer.

**The policy is pinned by a test.** `DefaultSeverityTests` asserts every shipped
descriptor's default severity and that none is disabled. Before it existed, all three
severities were changed and the whole suite stayed green — a policy nothing observes is
a policy that drifts.

## Follow-up Actions

* Promote `DCAT0009` once it detects an identifier reached through a constant.
* Revisit `DCAT0002`–`DCAT0004` if hand-written catalogues turn out to be common; the
  reasoning here is about audience, not about severity being unimportant to them.
