# ADR-0005 | Require an enforcing check before any automation merges

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0005-require-an-enforcing-check-before-any-automation-merges.fr.md)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

GitHub's auto-merge is armed by a workflow but performed by GitHub, and only once
the branch's **required** status checks pass. Whether a check is required is a
property of the repository's branch-protection rules, not of the workflow that
armed the merge.

Where no branch-protection rule marks any check as required, arming auto-merge
merges the pull request immediately — before, and regardless of, any check the
repository runs.

This repository is new. It carries workflows but no branch-protection rule, and
the GitHub Actions dependency ecosystem opens update pull requests as soon as it
is configured, which it now is. The window in which an armed merger would act
unchecked therefore opens at once and stays open until a human notices.

The maintainer is the only authority that merges a pull request; no agent merges
or arms a merge on its own work.

The repository's checks are the enforcement for several other decisions: the
commit convention (ADR-0003), the .NET Framework floor (ADR-0001), and the
coding-rules ratchet (ADR-0004) all rest on a check being able to block a merge.

## Decision

No automation in this repository may merge or arm a merge unless an enforcing
required status check is in place, and automation capable of merging ships
disarmed until that check exists.

## Rationale

The safety of an automated merge lives entirely in the required checks. The
workflow that arms it decides only *which* pull requests are eligible; it has no
say in whether anything was verified first. Treating the workflow as the safety
mechanism inverts where the guarantee actually comes from — which is how an
unprotected repository ends up merging unverified changes through a workflow that
looks careful.

The hazard is at its worst precisely now. A young repository looks quiet, the
pull requests are mechanical dependency bumps, and nobody is watching a lane
nobody has used yet. An unbounded, silent window is not a risk to accept on the
assumption that the protection rule will be created soon.

Shipping the automation **disarmed**, rather than not shipping it, keeps its
logic — which updates are eligible, how identity is settled, when a merge is
withdrawn — written, reviewed and versioned while the reasoning is fresh. That
logic is the expensive part; the switch is not. Deferring the whole workflow
would mean writing it later, under the pressure of wanting the lane open.

The rule is stated for *any* automation rather than for the dependency updater
alone because the same inversion is available to every future automation that can
merge or arm a merge. Recording it as a policy means the next such workflow
inherits the answer instead of relitigating it.

The disarming direction is deliberately left ungated. Withdrawing a merge is
always safe, and a fail-safe path that depends on a switch being set is not a
fail-safe path.

## Alternatives Considered

### Ship the automation armed and create the protection rule promptly

Considered because the protection rule is a few minutes of work, and the gap
would in practice be short.

Rejected because the gap is unbounded in principle and silent in practice: the
failure produces no error, no notification and no artifact — merged commits that
nothing verified, indistinguishable afterwards from merges that everything
verified. "Promptly" is not a property the repository can hold.

### Do not ship the automation at all until protection exists

Considered, and it is equally safe. It is the smaller diff, and it removes the
switch entirely.

Rejected because it discards the review of the workflow's own logic, which is the
part worth getting right and the part that is hardest to write later. It also
leaves no record of the decision at the moment it was actually taken.

### Detect the protection rule at run time instead of using a manual switch

Considered because it removes a switch that can be set wrongly, and makes the
guarantee self-checking rather than procedural.

Rejected because reading branch protection requires a token scope broader than
the workflow otherwise needs — widening permissions to check a safety property is
a poor trade — and because the presence of a rule does not prove enforcement: a
rule can exist while marking no check as required. The detection would answer a
question adjacent to the one that matters.

### Rely on requiring a human approval instead of a required check

Considered because an approval is also a gate, and it is the maintainer's
judgement rather than a machine's.

Rejected because it defeats the purpose of the automation: a lane whose point is
to merge routine updates without human attention cannot be gated on human
attention. The check is what can be both automatic and enforcing.

## Consequences

### Positive

* No dependency update can merge before the repository's own checks have run and
  been made to matter.
* The automation's logic exists, reviewed, and is one repository setting away
  from being usable.
* Future merge-capable automation inherits a stated answer rather than repeating
  the analysis.

### Negative

* Dependency updates must be merged by hand until branch protection is
  configured, which is friction on exactly the pull requests the lane exists to
  remove.
* The repository carries a workflow that currently does almost nothing, which is
  a thing a reader must be told rather than discover.

### Risks

* The switch is set before the protection rule exists, re-creating the hazard the
  decision removes. Mitigation: the workflow's own header states the required
  order and names both steps; the switch is documented as the second of two, not
  as a feature toggle.
* The decision is read as being about dependency updates specifically, and a
  future merge-capable automation ships armed. Mitigation: the decision is stated
  for any automation, and this record is what a pull-request ADR check surfaces.

## Follow-up Actions

* Protect `main` and mark the CI checks required, then arm the automation.
* Make the checks that enforce ADR-0001, ADR-0003 and ADR-0004 part of that
  required set.

## References

* [ADR-0001](0001-floor-the-libraries-on-net-framework-4-7-2.en.md),
  [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.en.md),
  [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.en.md) —
  decisions whose enforcement depends on a check being required.
* `.github/workflows/dependabot-automerge.yml` — the disarmed automation and the
  arming procedure.
