# AGENTS.md — DiagnosticCatalog

Instructions for automated agents (OpenAI Codex and others) in this repository.
Two roles are covered: **writing code** and **reviewing pull requests**.

## Project orientation (code changes)

- A .NET foundation for defining, generating and validating strongly referenced
  diagnostic rule catalogs. A rule reference is a contract.
- Libraries target **`netstandard2.0` and `net10.0`**.
- Build: `dotnet build -c Release` — Test: `dotnet test -c Release`. Both resolve
  the solution at the repository root; do not hardcode a solution file name.
- A test project exercising a shipped `netstandard2.0` library imports
  `build/Net472TestFloor.props` and drops its own `<TargetFramework>`; that is
  what enrolls it in the Windows `framework-floor` CI job, which runs it on the
  real .NET Framework 4.7.2 CLR. Test projects covering `net10.0`-only tooling
  must not import it.
- Repository language is **English** (code, comments, commits, PRs, issues, and
  review comments).
- Package versions are pinned centrally in `Directory.Packages.props`; a
  `<PackageReference>` carries no `Version`.
- Keep changes small and focused. Treat renamed or removed **rule identifiers
  and catalog entry keys** as breaking changes — a consumer references them
  symbolically.
- A shipped library must not rely on `IsExternalInit` (records, `init`
  accessors): the polyfill under `build/` is a test-only concession for the
  net472 floor.
- **Write the type; never `var`**, except where C# gives no other spelling
  (anonymous types). The build reports it as `IDE0008` and CI turns it into an
  error.
- **A `fix` ships with a test that was seen failing against the unfixed code.**
  This constrains the evidence, not the order of work: write the test first, or
  write the fix and stash it to watch the test go red. A test that was never red
  cannot tell a fixed bug from one that was never reproduced. Where a failing
  test is genuinely impractical (a race, a fix inside a workflow), say so in the
  pull request and describe how the fix was verified instead — the proof may be
  skipped, never skipped silently. Full wording in
  [`CLAUDE.md`](CLAUDE.md), "Proving a fix".

## Release trains (code changes)

Commits are partitioned into release trains **by scope**, and each train
publishes independently: `lib` (`core`, `analyzers`), `cli` (`cli`,
`cataloggen`), `sonar` (`sonar`), `netanalyzers` (`netanalyzers`), `stylecop`
(`stylecop`). A `feat` or
`fix` without a scope matches no train and is silently dropped from the release
record, which is why `commit-lint` rejects it. See `CONTRIBUTING.md`, "Scope" —
note in particular that `analyzers` means *analyzers this repository publishes*,
while `netanalyzers` means *the catalog of Microsoft's CA rules*.

## Architecture decisions (code changes)

Before finalizing a pull request, check it against the ADR base under
[`doc/adr/`](doc/adr/) (format and conventions:
[`doc/adr/README.md`](doc/adr/README.md)). An ADR records a **significant,
lasting decision** — one a future maintainer would ask "why did they do it this
way?" about — not every change. Apply the README's test: *if the implementation
changed but the decision stood, the ADR should not need editing.* Most pull
requests embark no such decision; the **check** is mandatory, the **ADR** is not.

The check has three outcomes — state the result in the pull request description:

- **Create** — the pull request embarks a new lasting decision (a public API
  contract, a cross-cutting invariant, a supported-platform floor, a dependency
  or security/compatibility policy, and the like). Draft one ADR per decision
  from `template.md` with **`Status: Proposed`**, add it to the index in
  `README.md`, and link it from the pull request.
- **Supersede** — the decision replaces one already recorded. Never edit the
  existing ADR in place or change its status yourself: name it in the pull
  request, draft the successor as `Proposed`, and leave the status flip to the
  maintainer. Accepted ADRs are immutable historical records.
- **Alert** — the pull request contradicts an accepted ADR. Do not proceed
  silently: flag it in the description — `⚠️ Conflicts with ADR-NNNN (<title>)` —
  with the precise conflict, and let the maintainer decide (accept it as a
  supersession, or change the code).

An agent **drafts and proposes**; it never accepts, supersedes, or deprecates an
ADR on its own authority — that is the maintainer's call, exactly as no agent
merges a pull request. When it is genuinely unclear whether a change is
significant enough, or whether it supersedes an existing ADR, say so in the pull
request and let `@reefact` judge rather than guessing.

## Tidying history before a pull request (acting agent)

This governs the agent that *prepares* a branch for review, not the reviewer.
This repository merges pull requests with a **merge commit**, so every commit a
branch carries lands in `main`'s history — a messy branch is not squashed away
on merge, it pollutes protected history for good. `CONTRIBUTING.md` already
fixes the endpoint (autosquash placeholders squashed before merge, a conforming
header on every commit, one intention per commit); this section makes the agent
*reach* it **on its own initiative**.

At two moments, read the branch against a freshly fetched `origin/main`:
**before opening a pull request**, and **after pushing further commits to an
already-open one**.

```
git fetch origin
git log --oneline origin/main..HEAD
```

Judge whether the history reads clean. Treat these as **messy**, worth proposing
a cleanup for:

- autosquash placeholders still pending — `fixup!`, `squash!`, `amend!` (CI
  rejects them);
- a commit that only fixes, rewords, or reverts an earlier commit of the *same*
  branch — "wip", "typo", "address review", a commit and its own revert;
- a header that fails the convention — run each through the repository's own
  linter, `git log -1 --format=%B <sha> | tools/commit-lint/lint-commit-message.sh --ci -`;
- one logical change scattered across commits that do not each stand alone, or
  two unrelated intentions folded into one commit (CONTRIBUTING.md, "Commit
  messages").

When it reads clean, say so in one line and proceed. When it is messy,
**propose** a concrete plan — which commits to squash, reword, drop, or reorder,
and the resulting `git log --oneline` shape — and rewrite only after an explicit
go-ahead. The endpoint is the maintainer's to approve: no agent rewrites a
branch on its own authority any more than it merges one.

Hard constraints on the rewrite itself (CONTRIBUTING.md, "Branches"):

- Rewrite history **only while the branch is yours alone**. Once anyone may have
  based work on it, a force-push discards that work — leave the history and say
  why.
- Publish with `git push --force-with-lease`, never a bare `--force`: the lease
  refuses the push if the remote moved under you.
- Never touch a commit already on `main`; `origin/main..HEAD` is the only range
  you may rewrite.
- This tidies history, not code. The diff against `origin/main` MUST be identical
  before and after — prove it with `git range-diff origin/main <old-head> HEAD`
  (only messages and grouping move, never the tree).

For Claude Code the mechanics are packaged: the `/tidy-history` command runs the
assessment and, on approval, the rewrite; a hook (`.claude/`, on pull-request
creation and after each committing or pushing git command) flags the CI-fatal
signals so the check is never skipped. Other agents apply the rule by hand.
Either way the judgement — *is this messy?* — and the decision to rewrite stay
here.

## Review guidelines (pull request reviews)

READ THIS BEFORE REVIEWING. The rules below are mandatory.

### Output format — mandatory

Every inline comment MUST use exactly this shape, with nothing around it:

```text
<label> [(decorations)]: <subject on one line>

<optional discussion>
```

In this shape, `< >` marks a placeholder to replace and `[ ]` marks an optional
part — write neither the angle brackets nor the square brackets literally.
Decorations, when present, go in parentheses (for example `(security)`).

- The entire comment is written in **English** — label, decorations, subject and
  discussion. Code identifiers, API names and exception messages are quoted verbatim.
- Never publish an unlabelled comment.
- Exactly **one label** and **one independent finding** per comment. At most two decorations.
- Do **NOT** add a severity/priority prefix — no `P0`, `P1`, `P2`, `P3`,
  `critical`, `major`, `minor`, anywhere in the comment. Blocking status is
  carried only by the label and the `(blocking)` / `(non-blocking)` decoration.
- No introduction or conclusion around the comment. Place it on the smallest
  relevant code range. Do not repeat the same finding on multiple lines.

Canonical example:

```text
issue (security): The raw connection string is copied into the diagnostic context and reaches the logs.

`RuleViolation.Create` stores the full connection string in the context, and the log sink
serializes every entry. The password therefore appears verbatim in any aggregated log
output.

Store only the host, or a redacted form, in the context — never the credential itself.
```

### Labels (one per comment)

- `issue:` confirmed defect that must be addressed — *blocking*.
- `todo:` small, obvious, local, non-debatable required change — *blocking*.
- `chore:` mandatory process step before merge; name the command/file — *blocking*.
- `question:` code looks suspicious but evidence is insufficient to assert a defect — *non-blocking*.
- `suggestion:` concrete optional improvement (never for incorrect code — use `issue:`) — *non-blocking*.
- `nitpick:` purely subjective, optional preference; should be rare — *non-blocking*.
- `note:` relevant information, no change expected — *non-blocking*.
- `thought:` design/architecture observation out of scope; must state no change is required here — *non-blocking*.
- `praise:` genuinely good and worth preserving; explain what and why — *non-blocking*.

Override a default only when the finding genuinely differs, e.g.
`suggestion (blocking):` or `issue (non-blocking):`. Never restate a default
(`issue (blocking):`, `nitpick (non-blocking):`).

Allowed decorations: `(blocking)`, `(non-blocking)`, `(if-minor)`, `(security)`,
`(perf)`, `(test)`, `(archi)`. One normally, never more than two.

### What to report (priority order)

Correctness → security → data integrity → regressions → public API / compatibility
→ concurrency / reliability → significant performance → missing tests for a
demonstrated risk → violations of an explicit repository rule (e.g. `var` where
the type must be written out, a shipped library relying on `IsExternalInit`).

Do NOT report: formatter-enforced style, issues already flagged by an analyzer
this repository runs, naming already enforced by tooling, speculative problems
with no execution path, broad refactors unrelated to the PR, personal style
presented as a requirement, or pre-existing issues the PR does not materially
affect.

If there is no relevant finding, approve without manufacturing comments.

### Final summary

Keep it concise. Report only: the number of blocking findings, the number of
non-blocking findings, and the main risk areas. Do not restate every inline
comment. If nothing was found, state clearly that no blocking issue was found.
The summary is not a Conventional Comment and needs no label.

## Responding to review feedback (acting agent)

This section governs the agent that *fixes* a pull request in response to a
review (for example `@claude`), not the reviewer. The human maintainer `@reefact`
is the only authority that merges a pull request; no agent merges, and no agent
enables auto-merge on its own pull request.

For each review finding, take exactly one route:

- **You agree and the fix is clear and local** — implement it, push, and reply on
  the thread with `Resolved in <sha>`. You MAY ask the reviewer for a single
  confirming re-review; never open a back-and-forth.
- **You believe the finding is wrong** — reply on the thread with the concrete
  technical reason and mention `@reefact` to arbitrate. Do not ping the reviewer
  bot to argue: a peer reviewer has no authority to settle the disagreement.
- **The finding needs a human judgement** — architecture, a product trade-off, an
  ambiguous requirement, a security or compatibility policy — mention `@reefact`
  and wait. Do not decide unilaterally.

Rules:

- Never mention both the reviewer bot and `@reefact` on the same thread: a bot
  round-trip or a human decision, never both.
- At most two fix / re-review cycles per finding. If it is still open after that,
  stop and mention `@reefact` instead of continuing.
- Keep replies short and factual; the diff is the record.
