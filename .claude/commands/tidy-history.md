---
description: "Assess origin/main..HEAD and, if messy, propose then (on approval) perform a commit-history cleanup before or on a pull request"
argument-hint: "[base-ref]"
---

Tidy the commit history of the current branch **before it reaches `main`**, per
`CONTRIBUTING.md` ("Branches", "Commit messages") and `AGENTS.md` ("Tidying
history before a pull request").

This repository merges pull requests with a **merge commit**: every commit on
the branch lands in `main`'s history, so cleaning it up before merge is not
cosmetic.

Base to compare against: `origin/main`. If a base ref was passed (`$ARGUMENTS`),
use it in place of `origin/main` everywhere below.

## 1 — Read the history (change nothing yet)

Run these and show me the output:

```
git fetch origin
git log --oneline --no-merges origin/main..HEAD
git diff --stat origin/main...HEAD
git status --short
```

Then confirm the branch is **yours alone**: if anyone else may have based work
on it, STOP — its history MUST NOT be rewritten (CONTRIBUTING.md, "Branches");
offer only follow-up-commit fixes.

## 2 — Assess

Judge every commit in `origin/main..HEAD`. Flag for cleanup:

- pending autosquash placeholders — `fixup!`, `squash!`, `amend!` (CI rejects them);
- headers that fail the convention — pipe each through the repository's own
  linter: `git log -1 --format=%B <sha> | tools/commit-lint/lint-commit-message.sh --ci -`;
- a commit that only fixes / rewords / reverts an earlier commit of this same
  branch ("wip", "typo", "address review", a commit and its own revert);
- one logical change scattered across commits that don't each stand alone, or two
  unrelated intentions folded into one commit.

If nothing is flagged, tell me the history reads clean and **stop** — do not
rewrite for its own sake.

## 3 — Propose (rewrite nothing yet)

If something is flagged, show me a plan as a table — for each commit:
keep / squash-into / reword-to / drop / reorder — plus the resulting
`git log --oneline`, every rewritten header conforming to CONTRIBUTING.md
(`<type>[(scope)][!]: <imperative, lowercase, no trailing period, ≤72 chars>`,
scope from `analyzers, cataloggen, cli, core, netanalyzers, sonar, stylecop`). Then **ask
me to approve.** Run no history-rewriting command before I say go.

## 4 — Rewrite (only after I approve, only while the branch is yours alone)

- prefer `git rebase --autosquash origin/main` when the cleanup is fixup!/squash!
  folding;
- otherwise perform the approved rebase against `origin/main`
  (reword / squash / drop / reorder);
- never touch a commit already on `origin/main` — `origin/main..HEAD` is the only
  range you may rewrite.

## 5 — Verify, then publish

- **The tree must not move.** Prove the cleanup changed only messages and
  grouping, never code: `git range-diff origin/main <old-head> HEAD`, and confirm
  `git diff origin/main...HEAD` is identical to before.
- re-lint every resulting commit:
  `git log -1 --format=%B <sha> | tools/commit-lint/lint-commit-message.sh --ci -`;
- publish with `git push --force-with-lease` — **never** a bare
  `git push --force`.

Report the before/after `git log --oneline` and confirm the diff is unchanged.
Do not open, merge, or enable auto-merge on the pull request — that stays with
the maintainer.
