# ADR-0022 | Fail on any diagnostic the warning ratchet cannot see

**Status:** Proposed
**Proposed:** 2026-08-01
**Decision Makers:** @reefact

## Context

This repository's warning ratchet promotes every compiler warning to an error in CI, so a new
warning can never merge. ADR-0021 brought the Sonar C# rules inside it, by generating the enforced
rule set from the server's quality profile.

That closed one gap and left another open, which was noted at the time and has since been observed.
The ratchet acts on **warnings**. A Roslyn analyzer diagnostic reported below warning severity — the
.NET SDK reports many of its own rules at `info` by default — is not a warning: `dotnet build` prints
nothing about it at any verbosity, and the ratchet has nothing to promote. SonarQube Cloud imports it
regardless, because its scanner reads what the compiler reported rather than what the console showed.

Measured on this repository: a build reporting **23 diagnostics at `info`** completed with zero
warnings and zero errors, and the dashboard listed **23 issues**, rule for rule. They arrived through
ordinary pull requests whose author had no signal at all — one of those pull requests carried a
commit stating it held its tests to the rules `main` enforces, which was true of the Sonar rules and
false of these.

The rules concerned are enabled by the SDK, not by this repository. Which rules those are, and at
what severity, moves with every SDK release.

Two mechanisms were measured before this decision. Enabling every analyzer rule
(`dotnet_analyzer_diagnostic.severity = warning`) reports **1065 sites**, most from rules the SDK
deliberately leaves off — 698 of them from a naming rule this repository's test-naming convention
contradicts on purpose. Enumerating the leaking rules by hand is possible today, at three rules, but
nothing enumerates the SDK's default set the way a quality profile enumerates Sonar's, so such a list
is a snapshot that rots on the next SDK.

## Decision

The build fails when it reports any unsuppressed analyzer diagnostic below warning severity.

## Rationale

The property that failed is not "some rule was off". It is that **the build reported something it
could not fail on** — and a report nobody can act on is exactly what the ratchet exists to abolish.
Stating the invariant is therefore stating the actual requirement, where a list of rules only
enumerates today's instances of it.

It also survives the SDK. A rule a future release starts reporting at `info` is caught the first time
it fires, named with its file, line and message, and has to be answered before merging — whereas a
list would let it through in silence, which is precisely the failure being fixed. This is the same
reasoning ADR-0021 used to generate the Sonar rule set rather than write it down; the difference is
that Sonar's set is enumerable and the SDK's is not, so here the check holds the invariant instead of
the membership.

Three answers are admitted, and they are the ones already used elsewhere in this repository: clear
the violation, raise the rule so the ratchet owns it, or suppress it at the site with a reason. All
three are visible in the tree, which keeps the property ADR-0021 established — a rule is either
enforced or its exception is written down, never quietly absent.

Suppressed diagnostics are ignored rather than reported, and that is not a loophole. A pragma or a
`SuppressMessage` is a decision recorded at the site, which is the third answer; the compiler marks
them in its log, so they are told apart by reading rather than assumed.

Raising all rules was rejected on measurement rather than on principle. At 1065 sites, most of them
from rules the SDK chooses not to run, the change would not be enforcement but a different rule set —
one nobody chose, contradicting conventions this repository holds deliberately.

The cost accepted is a second build in CI. The compiler writes the diagnostic log only when asked,
and asking changes what every project emits, so the check cannot share the matrix build without
altering the thing that build measures.

## Alternatives Considered

### Raise every analyzer rule to warning

Considered because it is one line in `.editorconfig` and needs no tooling or check.

Rejected on measurement: 1065 sites, dominated by rules the SDK leaves off by default. It would
impose a rule set nobody selected — including one that would rename every test in the repository —
and the work of clearing it has nothing to do with the leak being closed.

### Raise only the rules known to leak

Considered because it is precise, needs no new tooling, and makes the feedback immediate and local
rather than deferred to a check.

Rejected as insufficient on its own: the list is a snapshot of what the current SDK reports, and the
next release can add to it silently — which is the exact failure mode this ADR exists to remove. It
is adopted as a complement rather than as the mechanism: the three rules known to leak today are
raised, so a contributor meets them in their own build, and the check remains for what nobody has
listed yet.

### Have SonarQube Cloud stop importing them

Considered because the leak is only visible because the scanner imports external Roslyn issues, and
that import can be switched off.

Rejected because it treats the report rather than the cause. The diagnostics would still be produced
and still be unreadable to anyone building, and the repository would have chosen to see less rather
than to act on more.

## Consequences

### Positive

* A diagnostic that would reach the dashboard fails the pull request that writes it instead.
* The guard is stated as a property, so a rule a future SDK reports at `info` is caught the first
  time it fires rather than after it accumulates.
* Every diagnostic in the repository is now either failing, enforced, or suppressed with a reason.
  There is no third state left.

### Negative

* A second build in CI, on one runner.
* A rule raised in `.editorconfig` can now fail a build for something the SDK considers advisory,
  and clearing it is work the SDK did not ask for.

### Risks

* The check reads the compiler's diagnostic log, whose SARIF shape is the SDK's choice. Both shapes
  the SDK emits are read, but a third would need the check updated — and a check that silently read
  nothing would report success forever. Mitigated by failing when no log is found at all.
* Suppression is the cheapest of the three answers and could become the default one. Nothing here
  prevents that; the reason written at the site is what a reviewer reads.

## Follow-up Actions

* Consider whether the check should also read the Sonar scanner's own build, which runs with the
  ratchet disabled and is the one analysis this guard does not cover.

## References

* [ADR-0021](0021-derive-the-build-rule-set-from-the-quality-profile.md) — the decision this
  completes; it brought the Sonar rules into the ratchet and left this severity gap open.
* `tools/analysis/check-diagnostic-floor.sh` — the check, and the measurements behind it.
* `Directory.Build.props` — the ratchet, and the diagnostic log this reads.
* `.editorconfig` — the three rules known to leak today, raised so the ratchet owns them.
