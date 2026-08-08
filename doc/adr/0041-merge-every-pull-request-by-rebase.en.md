# ADR-0041 | Merge every pull request by rebase, never a merge commit

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0041-merge-every-pull-request-by-rebase.fr.md)

**Status:** Accepted
**Proposed:** 2026-08-08
**Accepted:** 2026-08-08
**Decision Makers:** Reefact

## Context

This repository merged pull requests with a merge commit until August 2026. `main`'s history is now
linear: the merge commits are gone from it, and each pull request's commits appear in sequence beside
every other commit.

GitHub offers three ways to close a pull request, and they differ in what reaches the base branch. A
merge commit adds one commit that names the branch and holds its commits under it, leaving their
identities untouched. A rebase replays each commit onto the base and adds nothing, giving every
replayed commit a new identity. A squash replaces the whole branch with a single commit.

Three existing decisions depend on what survives a merge:

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.en.md) partitions releases into
  trains by commit **scope**, so a release note and a changelog entry are built from the scopes the
  individual commits carry.
* [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.en.md) requires a Conventional
  Commits header on every non-merge commit, enforced by a local hook and by a required CI job. Its
  Context states the merge-commit strategy as a fact, and its Rationale argues from it that
  enforcement belongs at the pull request rather than at the merge result.
* [ADR-0025](0025-bind-every-feature-commit-to-the-documentation-it-changed.en.md) binds a feature
  commit to the documentation it changed, and its Rationale likewise cites a repository that
  "merges with a merge commit".

A merge commit carries no Conventional Commits scope. The commit linter exempts it, the CI job
filters it out, and the release-notes tooling skips it — so under the previous strategy `main`
accumulated commits that reached no release note and no changelog.

`CONTRIBUTING.md` requires a branch's history to be tidy before it merges: autosquash placeholders
squashed away, a conforming header on every commit, one intention per commit. `AGENTS.md` makes
reaching that state an acting agent's standing duty, `CLAUDE.md` inlines the rule, and a repository
hook reports on it. Of that endpoint, two parts are mechanically enforced — pending `fixup!`,
`squash!` and `amend!` placeholders, and headers the linter rejects — while "one intention per
commit" and scaffolding commits remain a human judgement.

An autosquash placeholder is written to be rewritten: `git rebase --autosquash` folds it into the
commit it names, and that rebase happens on the branch, before the merge.

When this repository's history was relinearised, every commit on `main` received a new identity.
A commit referenced by its identifier before that point no longer resolves.

## Decision

Every pull request is merged by rebase — its commits replayed one by one onto `main` — and neither a
merge commit nor a squash is used.

## Rationale

The release record is unaffected, which is what makes the change safe to adopt at all. Trains are
built from the scopes the individual commits carry, and a rebase preserves those commits exactly as a
merge commit did: same messages, same order, same one-intention-per-commit granularity. ADR-0002 and
ADR-0003 keep working without amendment, and ADR-0003's argument that enforcement belongs at the pull
request rather than at the merge result holds unchanged — the individual commits are still what reach
`main`.

What changes is what *else* survives, and it argues for the decision rather than against it. A merge
commit was a wrapper: it named the branch, held its commits together, and let a reader recover the
pull request as a unit long afterwards. A rebase leaves no wrapper at all. The commits themselves
become the only record that the branch ever existed, which is the strongest possible reason to hold
them to a standard — a messy branch is no longer merely admitted to `main`, it *becomes* part of
`main`, commit by commit, with nothing left to mark where it began or ended. The rule that a branch
is tidied before it merges is therefore more critical under this decision than under the one it
replaces, and any reading that treats a rebase as the more forgiving strategy has it backwards.

The autosquash case shows the sharpened stakes concretely. Under a merge commit, a placeholder that
slipped through landed unlinted but stayed attached to a branch a reader could still reconstruct.
Replayed by rebase, it becomes an ordinary commit of `main` naming a commit it was supposed to be
folded into — and there is nothing left to fold it into. That is why the CI job refuses one outright
rather than warning, and the refusal is worth more now than it was.

Against that, a linear history is the one bisect and blame read most cleanly: one sequence, no
side branches to descend into, no commit whose diff is the union of somebody else's work. And it
removes from `main` the one class of commit that carried no scope and could reach no release note.

The cost is accepted knowingly: the history stops recording that a set of commits arrived together.
That grouping was real information, and nothing in the commits replaces it. It survives only outside
the history — in the pull request itself, and in whatever the commits choose to reference.

## Alternatives Considered

### Keep merging with a merge commit

Considered because it is what three accepted records already assume, because it preserves the pull
request as a unit inside the history, and because keeping it would have cost nothing to write down.

Rejected because the grouping it preserves is rarely the thing anyone reads, while its costs are
paid on every merge: a commit in permanent history that carries no scope, reaches no release note and
is exempt from the convention every other commit obeys, plus a branching shape that bisect and blame
have to descend into. The wrapper is a weak substitute for commits that are individually clean, and
requiring the latter is already the rule.

### Squash every pull request into one commit

Considered because it makes branch history disposable — a messy branch would cost nothing, and only
one message per change would need to conform.

Rejected for the reason ADR-0003 already gave when it weighed the same option: squashing collapses
the unit of change. A commit travels alone, and replacing several intentions with one message
written at merge time would make a multi-intention pull request unrepresentable in the release
record that ADR-0002 builds from scopes. It is also the opposite trade to the one made here — this
decision raises what a commit must be worth, and squashing would remove the question.

### Allow more than one strategy and choose per pull request

Considered because some pull requests genuinely are one intention and would read well squashed,
while others benefit from keeping their commits.

Rejected because the strategy is a property the tooling reasons about, not a per-merge preference.
The commit linter, the release-notes tooling and the history-hygiene rule each state what reaches
`main`; a strategy chosen at merge time would make that statement conditional on a decision nobody
records, and the weakest available choice would set the real standard.

## Consequences

### Positive

* `main` reads as a single sequence, which is the shape `git bisect` and `git blame` navigate most
  directly.
* Every commit in `main` now carries a Conventional Commits header and a scope the release tooling
  can read. The one exempt class — the merge commit — no longer exists.
* The release record is unchanged: trains are still built from the individual commits' scopes.
* The tidy-before-a-pull-request rule gains a plainer justification than it had. It no longer rests
  on "a messy branch reaches `main`" but on the stronger fact that a messy branch *becomes* `main`.

### Negative

* Nothing in the history records that a set of commits arrived together. A pull request is no longer
  represented by a commit of its own, and recovering its boundaries means leaving the history.
* Every commit on a branch is rewritten when it merges, so an identifier quoted before the merge —
  in an issue, a review, a changelog entry, an agent's notes — does not resolve on `main` afterwards.
* Two accepted records, ADR-0003 and ADR-0025, state the previous strategy in their Context. Neither
  decision changes and neither is edited, so the base carries a premise that is now historical.

### Risks

* The rule that now carries more weight is the one enforced least completely. Placeholders and
  non-conforming headers are caught mechanically; "one intention per commit" and scaffolding commits
  are not, and they are exactly what a rebase makes permanent. Mitigated by `AGENTS.md` making the
  review a standing duty rather than a reminder, and by the repository hook raising it unprompted —
  but the mitigation is a habit, not a gate.
* A contributor who force-pushes a branch after review sees the same commits rewritten twice, once
  by their own rebase and once by the merge, which makes a review comment pinned to a line of a
  specific commit easy to strand.

## Follow-up Actions

* The four places that justified the tidy-history rule by the merge-commit strategy — `CLAUDE.md`,
  `AGENTS.md`, the commit linter's CI-mode comment and the history-hygiene hook's header — were
  corrected in the pull request that precedes this record.
* Decide whether the historical premise in ADR-0003 and ADR-0025 is worth reconciling. Neither
  decision changed, and an accepted ADR is not edited in place, so the options are to leave both as
  the dated records they are or to note the change from here — not to rewrite them.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.en.md) — the release trains built
  from the scopes the individual commits carry, which is what a rebase must preserve.
* [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.en.md) — the convention on
  every non-merge commit, the enforcement layers, and the squash-merge alternative weighed there
  first.
* [ADR-0025](0025-bind-every-feature-commit-to-the-documentation-it-changed.en.md) — the feature
  commit bound to its documentation, whose Rationale cites the previous strategy.
* [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — the endpoint a branch's history has to reach before
  it merges.
* [`AGENTS.md`](../../AGENTS.md) — "Tidying history before a pull request", the standing duty this
  decision raises the stakes of.
