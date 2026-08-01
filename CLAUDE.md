# DiagnosticCatalog — guide for Claude Code

DiagnosticCatalog is a .NET foundation for defining, generating and validating
strongly referenced diagnostic rule catalogs. Keep changes aligned with that
goal: a rule reference is a **contract**, and it should stay resolvable,
documented, and close to the code that uses it.

## Language

* The repository language is **English** by default: source code, code comments,
  commit messages, branch names, PR titles and descriptions, and issues.
* You may reply to me in French in the chat. Outside [`doc/`](doc/), never write
  repository content in French.
* **[`doc/`](doc/) is bilingual**, and it is the only place that is. Every
  document there exists as an `.en.md` / `.fr.md` pair — the guides, the
  specification, the decision records. **English is canonical**: where the two
  disagree, the English version wins. A page and its translation land in the same
  commit; `tests/DiagnosticCatalog.Documentation.UnitTests` fails a pair that is
  missing a half, a link that does not resolve, or a page nothing navigates to.
  The layout, the language banner and the navigation footer are specified in
  [`doc/CONVENTIONS.en.md`](doc/CONVENTIONS.en.md); the decision is
  [ADR-0022](doc/adr/0022-maintain-every-document-under-doc-in-english-and-french.en.md).
* The package READMEs under `src/` stay **English-only**. nuget.org renders one
  file per package, offers no language switch, and resolves no relative link.

## Build & test

* The libraries target **`netstandard2.0` and `net10.0`**.
* Build: `dotnet build -c Release`
* Test: `dotnet test -c Release`
* Both commands resolve the solution at the repository root, so they keep
  working as projects are added. Do not hardcode a solution file name in a
  script or a workflow.
* A test project that exercises a shipped `netstandard2.0` library MUST import
  `build/Net472TestFloor.props` and drop its own `<TargetFramework>`; that is
  what puts it in the `framework-floor` CI job, which runs it on the real .NET
  Framework 4.7.2 CLR. Test projects covering `net10.0`-only tooling MUST NOT
  import it. See CONTRIBUTING.md, "The .NET Framework floor".
* Only report tests as passing if you actually ran the corresponding command.
* If you did not run a relevant command, say so explicitly.

### Proving a fix

* **A `fix` ships with a test that was seen failing against the unfixed code.**
  This constrains the **evidence**, not the order of work: write the test first,
  or write the fix and stash it to watch the test go red — either satisfies it.
  A test that was never red cannot tell a fixed bug from one that was never
  reproduced, and a test written after the fix, in the same breath, tends to
  encode what the code does rather than what it should do. Report the failure you
  observed; never assert it from memory.
* When a failing test is genuinely impractical — a race, a fix inside a workflow,
  a defect only reachable through a third-party service — **say so in the pull
  request and describe how you verified the fix instead.** Skipping the proof is
  allowed; skipping it silently is not.
* Scope: this binds the `fix` type, which per CONTRIBUTING.md means the
  correction of a defective *behaviour*. A documentation or tooling change takes
  another type and carries no such obligation.
* When adding behaviour whose contract is not obvious, write the assertions that
  define it before implementing. This one is advice, not a rule. Its value is the
  moment it creates: if you cannot decide what to assert, that is the point to
  ask — not to settle the question silently inside the implementation.

### Documenting a feature

* **A `feat` carries a `Docs:` footer.** It names the documentation the commit
  changed — `Docs: doc/guide/dcat-reference.en.md, doc/guide/dcat-reference.fr.md`
  — or it says why it changed none: `Docs: none — <reason>`. A reason is
  required; `Docs: none` alone is refused. Under [`doc/`](doc/) a page and its
  translation are named **together**, because the parity test sees two files that
  both exist and cannot tell that only one was updated.
* This binds `feat` only. A `fix` restores behaviour the documentation already
  promises; it may carry the footer, and nothing requires it. Full rule in
  [`CONTRIBUTING.md`](CONTRIBUTING.md) ("The `Docs:` footer"); the decision, and
  what it deliberately does not guarantee, is
  [ADR-0025](doc/adr/0025-bind-every-feature-commit-to-the-documentation-it-changed.en.md).
* The footer's **shape** is checked by the commit linter, at the hook and in CI.
  Whether the files it names were really touched is checked in CI by
  `tools/commit-lint/check-docs-footer.sh`, which you can run yourself on any
  commit: `tools/commit-lint/check-docs-footer.sh --commit HEAD`.
* Do not reach for the exemption to avoid writing a page. The documentation tests
  cover only what they can enumerate — the `DCAT` ids, the `dcat` options and
  commands, the public API. Everything else a feature adds (a build property, a
  manifest key, a workflow, a hook) has no check but this footer, so "none" on
  one of those is a claim nothing will contradict and everything rests on it
  being true.

## Release trains

Commits are partitioned into **release trains by scope**, and each train
versions and publishes independently:

| Train | Scopes | Pace |
|---|---|---|
| `lib` | `core`, `analyzers` | The foundation. Deliberately very stable. |
| `cli` | `cli`, `cataloggen` | The `dcat` tool. Follows Roslyn and upstream package layouts. |
| `sonar` | `sonar` | Follows SonarSource's releases. |
| `netanalyzers` | `netanalyzers` | Follows the .NET SDK's analyzer releases. |
| `stylecop` | `stylecop` | Follows StyleCop's releases. |

This is why `commit-lint` **requires a scope on `feat` and `fix`**: an unscoped
one matches no train and is silently dropped from the release notes and the
changelog. The scope list and the train table name the same set, in both
directions: `cataloggen` joined the `cli` train when the generator was published
inside `dcat` (ADR-0017), and `testing` was dropped once it was clear it named a
test-support package nobody was going to build. So there is neither a scope that
reaches no release note nor a train that promises a package that does not exist.
The full table, with the
`analyzers` / `netanalyzers` distinction and why the shell and the engine keep
separate scopes, is in [`CONTRIBUTING.md`](CONTRIBUTING.md).

Two rules follow, and both are checked on every pull request by the release
rehearsal:

* A project joins a train by declaring `<ReleaseTrain>` in its own `.csproj`.
  That declaration is the whole membership — it also makes the project packable
  and gives it an embedded SBOM. Never add a project to a list somewhere else.
* A project on one train MUST NOT carry a `<ProjectReference>` to a project on
  another: `dotnet pack` would stamp a dependency on a version of the other train
  that was never published. Depend on another train through a `PackageReference`
  to a released version (ADR-0007).

## Change guidelines

* Keep changes small, focused, and aligned with the requested task.
* Do not introduce new dependencies without a clear reason. Every version is
  pinned centrally in `Directory.Packages.props` (Central Package Management):
  a `<PackageReference>` carries no `Version`.
* Do not make public API changes unless they are required by the task.
* Treat renamed or removed **rule identifiers and catalog entry keys** as
  breaking changes unless explicitly stated otherwise: a consumer references
  them symbolically, which is the whole point of the library.
* Preserve compatibility with **`netstandard2.0`**. In particular, a shipped
  library must not rely on `IsExternalInit` (records, `init` accessors): the
  polyfill under `build/` is a test-only concession, and a consumer compiling
  against .NET Framework would otherwise have to supply the marker itself.

## Coding rules

Rules you must apply to code you write. They are written out here, rather than
delegated to a ReSharper/Rider `.DotSettings` artifact, because that file is
read by Rider and by nothing else — no compiler, no CI job, and no agent.
Pointing at it reads like an instruction without being one. This list is the
extensible home for such rules; each one states how it is checked, so none of
them rests on attention alone.

* **Write the type; never `var`.** The only exception is a declaration C# gives
  no other spelling, which in practice means an anonymous type (`new { ... }`).
  This is checked twice: `.claude/hooks/coding-rules.sh` reports it on the edit
  itself, and the build reports it as `IDE0008`, which CI turns into an error.
  A pull request carrying one does not merge.

* **Do not reformat code you did not change.** Reformatting buries the real
  change and drifts away from whatever layout the surrounding file already has.
  Touch the lines the task requires and leave their neighbours alone, even when
  the surrounding alignment already looks stale.

## Architecture decisions (ADRs)

Before finalizing a pull request, check the change against the ADR base under
[`doc/adr/`](doc/adr/). This is **advisory**: produce a recommendation, never a
blocker. Full procedure in [`AGENTS.md`](AGENTS.md) ("Architecture decisions");
format and conventions in [`doc/adr/README.en.md`](doc/adr/README.en.md). The
essentials, inlined so they hold even if `AGENTS.md` is not read:

* An ADR records a **significant, lasting decision** — one a future maintainer
  would question. Test: *if the implementation changed but the decision stood,
  the ADR should not need editing.* Most pull requests need none; the **check**
  is the habit, the **ADR** is the exception.
* **Create** — a new lasting decision (public API contract, cross-cutting
  invariant, supported-platform floor, dependency or security/compatibility
  policy): draft one ADR per decision as `Status: Proposed`, index it, and link
  it from the PR.
* **Supersede** — the change replaces a recorded decision: draft the successor as
  `Proposed`; never edit an accepted ADR in place or flip its status yourself.
* **Alert** — the change contradicts an accepted ADR: flag it in the PR
  description (`⚠️ Conflicts with ADR-NNNN`); do not proceed silently.
* You **draft and propose**; you never accept, supersede, or deprecate an ADR —
  the maintainer decides, exactly as no agent merges a pull request. When unsure
  whether a change is significant enough, say so and let `@reefact` judge.

## Git and pull requests

* Follow `.github/pull_request_template.md` for every pull request.
* Do not open a pull request unless I explicitly ask for one.
* PR titles, descriptions, commits, and branch names must be written in English.
* Write every commit message per [`CONTRIBUTING.md`](CONTRIBUTING.md):
  Conventional Commits, a closed type list, the scopes
  `analyzers, cataloggen, cli, core, netanalyzers, sonar, stylecop`, an imperative
  header within 72 characters, a `Docs:` footer on every `feat` (see *Documenting
  a feature*), and `Refs: #NN` in a footer when a GitHub issue exists
  (issue-closing keywords belong in the PR description, not the commit).
* Write every pull request title per [`CONTRIBUTING.md`](CONTRIBUTING.md): name
  the whole change in English; a single-intention PR mirrors its commit header
  (`type(scope): description`), a multi-intention PR uses a short descriptive
  title, and issue references stay in the description, not the title.
* Enable the local commit-message hook once per clone with
  `git config core.hooksPath .githooks`; the same check runs in CI on every pull
  request.
* Before opening a pull request — and after pushing more commits to an open one
  — read the branch against a fresh `origin/main` and, if the history is messy
  (pending `fixup!`/`squash!`, wip/typo/"address review" commits, headers the
  lint rejects, one change split across non-standalone commits or two folded
  into one), **propose** a cleanup and rewrite only after I approve — while the
  branch is yours alone, with `git push --force-with-lease`, leaving the diff
  against `origin/main` unchanged. This repository merges with a merge commit,
  so a messy branch reaches `main`. Full rule in [`AGENTS.md`](AGENTS.md)
  ("Tidying history before a pull request"); the `/tidy-history` command runs it.
* In PR descriptions, do not invent testing results. Only check items that were
  actually run.

## Responding to pull request review feedback

When you act on review feedback on a pull request, follow the escalation rules
in [`AGENTS.md`](AGENTS.md) ("Responding to review feedback"). The essentials,
inlined so they hold even if `AGENTS.md` is not read:

* If you agree and the fix is clear and local, implement it, push, and reply
  `Resolved in <sha>`.
* If you believe a finding is wrong, reply with the concrete technical reason
  and mention `@reefact` to arbitrate — do not argue with the reviewer bot.
* If a finding needs a human judgement (architecture, a trade-off, an ambiguous
  requirement, a security or compatibility policy), mention `@reefact` and wait.
* Never mention both the reviewer bot and `@reefact` on the same thread; cap at
  two fix/re-review cycles, then escalate to `@reefact`.
* No agent merges a pull request or enables auto-merge on it — the human
  maintainer merges.
