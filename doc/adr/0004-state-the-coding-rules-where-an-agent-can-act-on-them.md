# ADR-0004 | State the coding rules where an agent can act on them

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

The maintainer's established practice records code style in a ReSharper/Rider
`.DotSettings` file. That file is read by Rider and by nothing else: no compiler
reads it, no CI job reads it, and no automated agent can open it usefully.

In the sibling repository that this repository's conventions come from, the
project guide delegated its style rules to that file — "follow it". Measured
outcome: the explicit-type rule drifted to 203 violations. The instruction read
like one without being actionable for any reader that could not open the file.

A significant share of the code here is written by automated agents. They never
open an IDE, and they learn the repository's rules by reading its files.

Roslyn can express a subset of these rules as `IDE*` diagnostics configured
through `.editorconfig` — but only when the SDK property that runs the code-style
analyzers during a build is set. Without it, `.editorconfig` is read and nothing
reports anything at build time.

Another subset has no Roslyn equivalent at all: the column alignment of
consecutive declarations, file layout patterns, region conventions. No tool
available to a contributor without Rider can reproduce them.

The repository already promotes every warning to an error in CI.

## Decision

A coding rule that contributors are held to is stated in the project guide and
enforced by at least one mechanism that runs outside an IDE; no such rule rests
on a Rider-only artifact.

## Rationale

A rule only one tool can read is enforced only while that tool is open. That is
not a hypothesis here: it is the measured history of the exact rule this
repository inherits. Writing the rule where every reader can find it — a human
without Rider, an agent, a reviewer reading the guide — is what makes it a rule
rather than a preference the IDE happens to apply.

Enforcement is layered because the layers have different latencies and catch
different misses. An edit-time hook reports the violation while the author is
still in the file. The build reports the same thing to whoever compiles,
including a contributor with no hook installed. CI turns it into an error, which
is where it stops being negotiable. Each layer covers the reader the previous one
does not reach, and all three report the same rule.

Keeping the build's report a warning locally, and an error only in CI, is
deliberate: an inner loop that refuses to compile a half-finished refactoring
makes iteration hostile, while a warning that CI will promote costs nothing to
ignore for ten minutes and nothing to fix before pushing. The ratchet is where
the rule becomes binding, and it is placed after the author has stopped working,
not during.

The cost accepted is duplication: the rule is stated in prose and configured in
`.editorconfig`, and if a `.DotSettings` file is added it will state it a third
time. The duplication is kept honest by all copies saying the same thing, and by
the guide naming which file is the checkable one.

The rules with no Roslyn equivalent are not abandoned; they are demoted. They may
live in a Rider artifact, but they are then formatting the IDE applies, not rules
a contributor is held to — which is why the guide pairs this decision with a
standing instruction not to reformat code one did not change. Without that
pairing, a contributor without Rider would drift the layout on every touched
file, and a formatter would bury real changes under reflowing.

## Alternatives Considered

### Keep the `.DotSettings` authoritative and point contributors at it

Considered because it is the existing practice, it needs no new file, and Rider
reproduces the repository's layout exactly — which no other tool does.

Rejected because it is the arrangement whose failure is already measured. It
leaves every reader without Rider — including every agent — unable to comply, and
turns the guide's instruction into one that cannot be followed.

### Run a formatter in CI and let it rewrite the code

Considered because it removes the question entirely: the code is normalized
whatever anyone writes.

Rejected on two grounds. A formatter patching behind the author removes the
author's own output from correction, so nothing is learned and the next commit
repeats the mistake. And the available formatter cannot reproduce the layout
conventions the repository's style encodes, so it would not converge on the
repository's style — it would drift away from it while appearing to enforce it.

### Enforce only in CI, and state nothing in the guide

Considered as the smallest mechanism that still blocks a violation from merging.

Rejected because the feedback then arrives once the pull request is open, on code
already written, which is the arrangement that let the rule drift. A rule
discovered by a red check is a rule nobody was told.

### Enforce only through the edit-time hook

Considered because it gives the fastest feedback and reaches the agents that
write much of the code.

Rejected because it reaches only the agents whose harness runs the hook. A human
contributor, another tool, or a bypassed hook would meet nothing, and the hook
blocks nothing on the way in.

## Consequences

### Positive

* Every reader — human, agent, compiler, CI — can find the rule and act on it.
* A violation is reported at the edit, at the build and at the merge gate, with
  the same wording.
* Adding a rule is a defined operation: state it in the guide, and name how it is
  checked.

### Negative

* The same rule is stated in more than one file, and the copies must be changed
  together.
* The rules Roslyn cannot express are not enforced anywhere, and rely on the
  do-not-reformat instruction to stay stable.
* Contributors who compile locally see a warning that CI will treat as fatal, so
  a clean local build is not proof of a clean CI build.

### Risks

* The prose rule and the `.editorconfig` severity drift, so the guide describes a
  rule the build does not report. Mitigation: the guide states, per rule, how it
  is checked, so a rule with no stated check is visibly incomplete.
* The list grows into a style manual nobody reads. Mitigation: the guide admits a
  rule only when it states its enforcement mechanism, which bounds the list to
  what is actually checked.

## Follow-up Actions

* State the enforcement mechanism next to every rule added to the guide.
* Reassess if a `.DotSettings` file is introduced: it may carry layout, never a
  rule contributors are held to.

## References

* [CLAUDE.md](../../CLAUDE.md) — "Coding rules".
* `.editorconfig`, `Directory.Build.props` — the build-time enforcement.
* `.claude/hooks/coding-rules.sh` — the edit-time report.
