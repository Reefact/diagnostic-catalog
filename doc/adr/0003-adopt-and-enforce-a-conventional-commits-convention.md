# ADR-0003 | Adopt and enforce a Conventional Commits convention

**Status:** Proposed
**Proposed:** 2026-07-30
**Decision Makers:** Reefact

## Context

The commit history is the only record of a change that survives the branch, the
pull request and the reviewer's memory. Whoever prepares a release reads it to
decide what the release contains and which version number comes out.

Releases in this repository are partitioned into trains by each commit's scope
(ADR-0002). A commit whose scope is absent matches no train, so it never reaches
any release note or changelog — and it fails silently, producing a green build
and an incomplete release record.

This repository merges pull requests with a **merge commit**. Every commit a
branch carries therefore lands in `main`'s permanent history; a messy branch is
not squashed away on merge.

A local git hook can be bypassed with a single flag, and is not installed by
default on a fresh clone.

Dependabot writes its own commit headers. Their length is driven by the package
name, so a long name alone can overrun a header-length limit, and the bot cannot
amend the message it wrote.

A significant share of the commits in this repository are written by automated
agents, which read the repository's own files to learn its rules and cannot infer
a convention from the surrounding history alone.

## Decision

Every non-merge commit follows a Conventional Commits convention with a closed
list of types and a closed list of scopes, validated by a single linter shared by
the local `commit-msg` hook and a pull-request CI check.

## Rationale

Closed lists are what keep the convention from decaying. An open type list ends
with a catch-all that absorbs everything and means nothing; an open scope list
ends with scopes that name files or classes, which move, rather than components,
which do not. A closed list also makes the convention checkable, and a convention
that cannot be checked is a convention that drifts.

A **single** linter, rather than a hook and a CI job that each implement the
rules, is what makes the two verdicts identical by construction. Two
implementations of the same prose disagree eventually, and the disagreement is
discovered at the worst moment — when a commit that passed locally fails on the
pull request.

Both layers are needed, and neither replaces the other. The hook gives the author
the verdict while the message is still cheap to fix, before the commit exists.
The CI check is the one that cannot be bypassed, and it is required precisely
because the hook can be: it is not installed on a fresh clone and it yields to a
single flag. Enforcing at the pull request rather than at the merge result is
what the merge-commit strategy demands — the individual commits are what reach
`main`, so they are what must be checked.

Requiring a scope on the two version-driving types follows directly from
ADR-0002: those are the commits a release record is built from, and the failure
mode of an unscoped one is silence. Turning a silent omission into a loud
rejection at write time is the only point in the pipeline where the cost of the
fix is near zero.

Exempting Dependabot is not a weakening of the rule but a recognition that the
rule addresses authorship the bot does not have. Its headers are mechanical, it
cannot amend them, and the alternative is a routine dependency update that turns
red for a reason no one can act on.

## Alternatives Considered

### No convention; rely on review to catch poor messages

Considered because it adds no tooling and no friction, and a careful reviewer
does notice an uninformative message.

Rejected because message quality is exactly what a reviewer reading a diff stops
noticing, and because ADR-0002's routing needs a machine-readable scope, not a
well-intentioned one. The failure is also cumulative and invisible: nobody
discovers a decade of unusable history until they need it.

### Squash-merge, and lint only the pull request title

Considered because it makes branch history disposable, so a messy branch costs
nothing and only one line per change needs to conform.

Rejected because it collapses the unit of change. A commit travels alone — it is
cherry-picked, listed in a log, read in isolation later — and squashing replaces
several intentions with one message written by whoever pressed merge rather than
by whoever made each change. It would also make a multi-intention pull request
unrepresentable in the release record.

### Use an off-the-shelf linter such as commitlint

Considered because it is widely used, configurable, and would not have to be
written or maintained here.

Rejected because it would introduce a Node toolchain into a .NET repository for a
single text check — an install step in the hook path, a dependency in the supply
chain, and a second ecosystem for Dependabot to track. The rules that carry the
most weight here (the closed scope list, the coupling to release trains, the
Dependabot exemption) are custom in any case, and a POSIX script has no install
step and behaves identically in the hook and on the runner.

### Enforce only in CI, with no local hook

Considered because it is the layer that actually blocks a merge, and it needs no
per-clone setup.

Rejected because it moves every verdict to after the commits exist, where fixing
a message means an interactive rebase and a force-push rather than an edit. The
CI check remains the authority; the hook is what makes conforming cheap.

## Consequences

### Positive

* The history answers what a branch contains and which version increment it
  implies, without opening a diff.
* Release trains route from the history alone (ADR-0002).
* The convention is stated once, in the contribution guide, and checked by one
  script that the hook and CI both call.
* An agent reading the repository finds the rule written down and the checker
  next to it.

### Negative

* Contributors must run one command per clone to install the hook.
* A rejected message cannot be fixed by a follow-up commit; it requires
  rewriting the branch's history before the merge.
* The scope list is a shared file that must be updated in step with the guide.

### Risks

* The linter and the prose guide drift, so a message the guide allows is
  rejected. Mitigation: the guide states that the linter mirrors it and names the
  file; both are changed in the same commit.
* Dependabot's exemption is identified by commit author, so a rewritten
  Dependabot message loses the exemption. Mitigation: this is the intended
  behaviour — once a human or an agent rewrites the message, it is authored work
  and is linted like any other.

## Follow-up Actions

* Keep the CI check required for merges once branch protection is configured
  (ADR-0005).
* Extend the linter's scope list together with the guide whenever a component or
  a catalog is added.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.md) — why a
  scope is required on `feat` and `fix`.
* [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.md).
* [CONTRIBUTING.md](../../CONTRIBUTING.md) — "Commit messages".
* [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/).
