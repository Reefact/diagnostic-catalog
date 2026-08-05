# Contributing to DiagnosticCatalog

DiagnosticCatalog is a foundation for defining, generating and validating
strongly referenced diagnostic rule catalogs. A catalog is a contract: a rule
identifier that moves, disappears or changes meaning breaks whoever referenced
it. The history of the repository should be as legible as the catalogs the
library produces. This guide defines how branches, commits and pull request
titles are written here.

Participating here also means accepting the
[Code of Conduct](CODE_OF_CONDUCT.md).

## Building and testing

* The libraries target **`netstandard2.0` and `net10.0`**. `netstandard2.0` is
  the compile contract that reaches .NET Framework consumers; the
  `framework-floor` CI job proves the assemblies actually *run* on .NET
  Framework 4.7.2 (see *The .NET Framework floor* below).
* Build: `dotnet build -c Release`
* Test: `dotnet test -c Release`
* Test the shell tooling: `sh tools/tests/run.sh`

Both `dotnet` commands resolve the solution at the repository root, so they keep
working as projects are added. The shell suite is separate because `dotnet test`
cannot reach it (see *Testing the shell tooling* below).

### The .NET Framework floor

A test project that exercises a shipped `netstandard2.0` library opts into the
floor by importing the shared props and dropping its own `<TargetFramework>`:

```xml
<Import Project="..\build\Net472TestFloor.props" />
```

The `net472` inner build is gated behind `EnableNet472Floor`, so an ordinary
`dotnet build` / `dotnet test` — and the whole local inner loop — never sees it.
The Windows-only `framework-floor` job in `.github/workflows/ci.yml` discovers
every project carrying that import and runs each with
`-p:EnableNet472Floor=true -f net472`.

Test projects that cover `net10.0`-only tooling must **not** import it.

### Testing the shell tooling

The scripts under `tools/` decide what a release publishes: `trains.sh` answers
which projects belong to a train, and the packaging scripts pack exactly what it
reports. A project the discovery misses is silently absent from its own release;
one it wrongly finds is published when it must not be. Neither shows up as a red
build, and `dotnet test` cannot reach shell at all.

    sh tools/tests/run.sh

Tests live in `tools/tests/`, one `test-<script>.sh` per script. Each one runs as
its own process — so a test that changes directory into a fixture cannot leak
that into the next — sources `tools/tests/assert.sh` for `assert_equals` and
`assert_empty`, and **ends with `finish`**. A file that forgets `finish` exits on
its last command's status and reports success however many assertions failed.

The suite runs in CI as *Test the shell tooling*, in
`.github/workflows/lint.yml`. It is invoked with `sh` rather than `bash`: every
script here carries a `#!/bin/sh` shebang and is written to POSIX, so running the
suite under bash would let a bashism pass CI and fail on a contributor's machine.

## Releasing

The trains described under *Scope* below version, tag and publish independently.
The mapping — train id, tag prefix, the scopes it collects, the package label —
lives in [`tools/trains.sh`](tools/trains.sh) and nowhere else.

### How a project joins a train

By declaring it in its own `.csproj`:

```xml
<PropertyGroup>
  <ReleaseTrain>sonar</ReleaseTrain>
</PropertyGroup>
```

That single declaration is the whole membership. It makes the project packable,
gives it an embedded SPDX SBOM (both wired in `Directory.Build.targets`), and is
what `tools/packaging/pack.sh` discovers when it packs the train — nothing lists
the projects a second time, so a renamed or moved project cannot silently drop
out of its own release. A value matching no train fails the pack, on every pull
request, rather than at release time.

### Cross-train dependencies

A project on one train MUST NOT carry a `<ProjectReference>` to a project on
another. The trains publish independently, and `dotnet pack` stamps a
`ProjectReference` at the version being packed — so the reference would declare a
dependency on a version of the other train that was never published, and the
package would be unresolvable for every consumer. Depend on another train through
a `PackageReference` to a version that is actually on nuget.org. The rule is
checked on every pack; decision:
[ADR-0007](doc/adr/0007-depend-across-trains-through-published-packages.en.md).

### Cutting a release

Push a train-prefixed SemVer tag — `lib-v1.2.3`, `sonar-v4.0.0`. The release
workflow resolves the train from the prefix, builds and tests, packs only that
train, attests the artifacts, publishes to NuGet through OIDC trusted publishing,
and creates a GitHub Release whose notes contain only that train's commits.

Two things are worth knowing before the first one:

* **Rehearse it.** `release-dryrun` already packs every train on every pull
  request, and the release workflow itself can be dispatched with `dry_run`
  ticked — which runs everything up to and including the OIDC login and the
  provenance attestation, and skips only the two steps that publish.
* **Build metadata is rejected.** `lib-v1.2.3+build5` is valid SemVer but NuGet
  drops the `+...` from the package identity, so the push would silently become a
  no-op against an already-published `1.2.3`. The workflow fails on it instead.

Adding a train is four edits: a row in `tools/trains.sh`, its scope in the commit
linter and in the tables below, and its tag pattern plus dispatch option in
`.github/workflows/release.yml` — GitHub requires those last two to be literal.

## Adding a catalogue

A catalogue is generated from an analyzer's own descriptors, never hand-written.
Adding one is six steps, and the last three are the ones nothing else would
remind you of — because nothing compiles a README, and nothing reads an icon.

1. **Declare it in the manifest.** One entry in
   [`eng/catalogs.json`](eng/catalogs.json): the upstream package, the namespace,
   the container type, the output path. Both the generator and the nightly
   workflow read that file, so the catalogues are never listed twice.
2. **Create the project** under `src/`, declaring its own `<ReleaseTrain>` (see
   *How a project joins a train*), a `<PackageId>`, and a `PackageReadmeFile`. A
   catalogue rides its own train because it follows its vendor's pace, not the
   foundation's:
   [ADR-0015](doc/adr/0015-a-catalogues-version-runs-on-its-own-line.en.md).
3. **Generate it**:
   `dotnet run --project src/DiagnosticCatalog.Cli -- generate --manifest eng/catalogs.json`,
   or `dcat generate --manifest eng/catalogs.json` with the tool installed.
4. **Give its README and CHANGELOG a mirror block** —
   `<!-- mirror:begin --> … <!-- mirror:end -->`. The generator writes which
   upstream release the catalogue reflects between those markers, and
   `DocumentedMirrorTests` fails a document that carries none: a banner the
   generator cannot reach states nothing. That test lists the catalogues by hand,
   so add the new one to its theory data.
5. **Name the other catalogues and the foundation** in that README, and add the
   catalogue to the repository README. A catalogue's README *is* its page on
   nuget.org, and a package page has no siblings beside it — a reader landing
   there from a search sees that catalogue and nothing else.
   `DocumentedSiblingsTests` reads the manifest from step 1 and fails every
   README that has not heard of the newcomer, in both directions. Name the
   package id; link it only once it is published, since an address cannot be
   pointed at a version that does not exist
   ([ADR-0007](doc/adr/0007-depend-across-trains-through-published-packages.en.md)).
6. **Draw its icon**, as a 512×512 `icon.png` beside the `.csproj`. The badge on
   it carries the **prefix of the rules the catalogue mirrors**, never the
   vendor's name: `S`, `CA`, `IDE`, `SA`. StyleCop's reads `SA` and not `SC`
   because `SA1000` is what a reader types inside `[SuppressMessage(...)]`, and
   at the 128px a nuget.org listing renders, that badge is the whole of what
   distinguishes one catalogue from the next. Start from
   [`assets/icon-template.svg`](assets/icon-template.svg), which carries the
   family mark and leaves the badge text as the one thing to edit.
   `PackageIconTests` fails a catalogue with no icon of its own, one whose icon
   is another catalogue's, and one still wearing the repository's unbadged
   mark — but it never reads the letters, so those are on you and on review.

## Documentation

The repository is written in **English** — source, comments, commit messages,
branch names, pull request titles, issues. [`doc/`](doc/) is the one exception,
and it is bilingual: every document there exists as an `.en.md` / `.fr.md` pair,
with the **English version canonical** wherever the two disagree
([ADR-0022](doc/adr/0022-maintain-every-document-under-doc-in-english-and-french.en.md)).

The project `README.md` is bilingual too, and it is the one pair whose halves do
not sit beside each other: GitHub composes the repository's landing page from a
file called `README.md` at the root and from nothing else, so the English half
carries no language suffix and cannot live under `doc/`. Its French half is
[`doc/README.fr.md`](doc/README.fr.md), which is also that folder's index — the
signpost to the guide, the specification, the records and the conventions is a
section of the README rather than a page behind it
([ADR-0029](doc/adr/0029-pair-the-project-readme-across-the-doc-boundary.en.md)).

The package READMEs under `src/` stay English-only. nuget.org renders one file
per package, offers no language switch, and resolves no relative link — which is
also why those READMEs link outward with absolute addresses.

A page and its translation land in the **same commit**. This is not a style
preference: `tests/DiagnosticCatalog.Documentation.UnitTests` fails a pair
missing a half, a relative link that does not resolve, a page nothing navigates
to, a `DCAT` id documented but never shipped — or shipped and never documented —
a `dcat` option or command that exists in one of the two places only, and a
public type the specification never mentions. Documentation is the artifact where
an omission is least visible, because the reader who cannot find the page is not
in a position to report that it is missing.

Those checks cover the surfaces that can be **enumerated** from something the
build already keeps true — the `AnalyzerReleases` files, the settings types, the
public API files. That is what makes them trustworthy, and it is also their
limit: a build property, a manifest key, a workflow, a hook, a page of the guide
itself can all be added without any of them noticing. Which is why a `feat`
carries a [`Docs:` footer](#the-docs-footer) naming what it documented, or saying
why it documented nothing.

The layout, the language banner, the navigation footer, the diagram rules and
what each check actually asserts are in
[`doc/CONVENTIONS.en.md`](doc/CONVENTIONS.en.md). Read it before adding a page.

Documentation-only changes take the `docs` type, which requires no scope.

## Enabling the commit-message hook

A `commit-msg` hook checks every message against the convention below before it
is recorded. It is versioned under `.githooks/`; enable it once per clone:

```
git config core.hooksPath .githooks
```

The same check runs in CI on every pull request, so a bypassed hook
(`git commit --no-verify`) is caught before merge. Merge commits are exempt.
The check itself lives in `tools/commit-lint/lint-commit-message.sh`, shared by
the hook and CI so the two never diverge.

The hook lets `fixup!`, `squash!`, and `amend!` commits through so you can build
an autosquash rebase; CI rejects them, so squash them away before merge.

## Branches

### Why

A pull request is read against its branch. Two branches carry the same feature.
The first was cut from `origin/main` an hour ago and holds three commits — its
pull request diff *is* the feature, and nothing else. The second was cut from a
local `main` three weeks stale, then revived for a second idea once the first
had merged; its diff carries fifteen commits, twelve already on `main`, and the
reviewer cannot tell the request from the residue.

The branch is not the work. It is the disposable workspace of **one** pull
request — cut fresh from the remote, used once, discarded on merge. Everything
below follows from that.

### The rule

* A branch carries **one pull request**, and that pull request carries one
  coherent unit of work. Work unrelated to that unit MUST take its own branch —
  the branch-level reading of *two intentions, two commits*.
* `main` is written **only** by merge. No commit lands on `main` directly; it
  moves when a reviewed pull request is merged.
* A branch MUST be cut from the **tip of `origin/main`**, freshly fetched —
  never from a local `main` that may lag, nor from another topic branch:

  ```
  git fetch origin
  git switch -c <author>/<short-description> origin/main
  ```
* A branch name MUST take the form `<author>/<short-description>`. The
  `<author>` is the branch owner's GitHub handle — the person or the tool the
  work belongs to: `sylvain/…`, `claude/…`, `dependabot/…`. The
  `<short-description>` MUST be English, lowercase, kebab-case, and name the
  change, not the file it touches: `sylvain/sonar-rule-deprecation`, never
  `sylvain/SonarCatalog.cs`.
* A tool that generates its own branches owns its namespace and keeps its
  native layout beneath it — `dependabot/nuget/Newtonsoft.Json-13.0.1`,
  `renovate/…`. The `<author>/<short-description>` form binds the branches a
  person or an agent cuts by hand; a generator's scheme is the generator's to
  define, and fighting it buys nothing.
* The branch name carries **no type**. The type is a property of each commit,
  checked there by the hook and by CI; a branch gathers commits of several
  types, and a single prefix would name one and hide the rest — the same reason
  a multi-intention pull request title takes no `type:` (see *Pull request
  titles* below). The owner is what the name adds, because the owner is what the
  commits do not carry.
* A branch lives exactly as long as its pull request stays **open**, and MAY be
  reused only for that same request — review fixes, changes asked for on the
  pull request.
* Once the pull request is **merged or closed**, the branch is finished. It MUST
  NOT be revived, not even for follow-up on the same topic: a merged pull
  request cannot describe new work, and a closed one was set aside. Follow-up is
  a new branch, cut fresh from `origin/main`.
* To carry `main`'s progress into an open branch: while the branch is yours
  alone, **rebase** it onto `origin/main`; once others may have based work on
  it, **merge** `origin/main` in instead. Either keeps the branch current
  without rewriting what a collaborator has already pulled.
* Rewriting a branch's history — a force-push, a `git rebase -i` — is fine
  while the branch is **yours alone**, and is how a commit message the lint or
  a reviewer rejected gets fixed, even mid-review: a rejected message cannot be
  corrected by a follow-up commit (see *Commit messages*). Once anyone else may
  have **based work on it**, its history MUST NOT be rewritten — a force-push
  discards what was built on top. Work that is not yours is not yours to
  rewrite or delete.
* Before opening the pull request, **read the branch** against a fresh
  `origin/main`:

  ```
  git fetch origin
  git log  --oneline origin/main..HEAD     # the commits the request adds
  git diff --stat    origin/main...HEAD    # the files it touches
  ```

  If either shows something the request is not about, the branch has drifted —
  split it before review, not after.

### The doctrine

**The branch is the unit of work in progress; the pull request is what it
becomes.** One branch, one pull request, one unit of work — the same one-to-one
the doctrine draws between the commit and its change.

**The name says who, the commits say what.** A branch owns a pull request that
may carry a feature, the refactor that prepared it, and its tests at once; no
single type names it honestly. The type lives on each commit, where the hook
enforces it. The branch name adds the one thing the commits omit — whose work it
is — so `claude/…` and `dependabot/…` are not exceptions but the rule itself,
read the same on a human or a machine.

**A branch is disposable.** Its history is preserved by the merge commit that
lands it; the ref itself is cut fresh and deleted on merge. Nothing of value
lives only on a branch.

**A merged branch is spent.** Reviving it stacks new work on settled history and
forks from a `main` that has moved. The reviewer pays the cost, reading the
residue as if it were the request.

**Cut from the remote, not the local.** A local `main` lags silently; a branch
cut from it drags that lag into every diff. `origin/main`, freshly fetched, is
the only base.

**Unrelated work is a new branch, not a passenger.** A branch that carries two
changes forces a pull request that can describe neither — the branch-level form
of the commit that carries two intentions.

### Anti-patterns

| Branch or move | What is wrong |
|---|---|
| a commit pushed straight to `main` | `main` moves only by merge. Even a one-line fix takes a branch and a pull request. |
| `patch-1`, `my-work`, `tmp` | No owner, and it names nothing. A branch name is read in the pull-request list; it MUST say who owns what. |
| `feat/add-sonar-catalog` | A type in the owner's slot. The type belongs on the commits; the branch prefix is the owner: `sylvain/add-sonar-catalog`. |
| `sylvain/SonarCatalog.cs` | Names a file. It should name the change: `sylvain/sonar-rule-deprecation`. |
| `sylvain/corrige-le-catalogue` | Not English. |
| reviving a merged `claude/add-sonar-catalog` for a follow-up | A merged branch is spent. Cut the follow-up fresh from `origin/main`. |
| a branch cut from a three-week-old local `main` | The pull request diff fills with commits already on `main`. Fetch first; cut from `origin/main`. |
| one branch carrying a feature and an unrelated CI tweak | No single pull request describes both. Two branches, two requests. |
| force-pushing a branch others have built on | Rewrites shared history and discards the work pushed on top. Rewrite only while the branch is yours alone. |

## Commit messages

This section adapts the [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/)
specification. The key words MUST, MUST NOT, SHOULD, and MAY are to be
interpreted as described in [BCP 14](https://www.rfc-editor.org/info/bcp14), and
only when they appear in capitals.

### Why

A release is prepared. One needs to know what a branch contains, what to carry
into it, and which version number comes out.

```
a3f1c2e fix bug
8b41d90 update the catalog
1d0e4aa wip
```

This history teaches nothing. Every question forces a diff open.

```
a3f1c2e fix(sonar): keep deprecated rule ids resolvable
8b41d90 feat(core): emit a rule reference for an unknown catalog entry
1d0e4aa refactor(core): extract catalog lookup into a resolver
```

This one answers three questions without opening a single diff: what the branch
contains, which commit to carry into a release, and whether the version moves
from `1.4.2` to `1.4.3` or to `1.5.0`. That is the reading of the reviewer, and
of whoever prepares the release. Tomorrow it will be a tool's.

### The rule

The rule bears on **each commit**, not on a merge message. A commit travels
alone: it is cherry-picked onto a release branch, listed in a
`git log --oneline`, read in isolation six months later. Its message MUST stand
on its own.

#### Form

```
<type>[(<scope>)][!]: <description>

[body]

[footers]
```

* The commit MUST begin with a type, optionally followed by a scope and a `!`,
  then a colon and a space.
* Everything written in the message MUST be in English — header, body, and
  footers.
* A commit MUST carry a single type, that of its intention. Two independent
  intentions MUST be two commits: the message forces the split that ought to
  happen.

#### Types

Ordered here with the two version-driving types first, then the rest
alphabetically. The list is closed.

| Type | When to use | Minimal effect on the version |
|---|---|---|
| `feat` | A new capability, visible to the consumer of the package | `MINOR` |
| `fix` | The correction of a defective behaviour | `PATCH` |
| `build` | Build system, dependencies, packaging, deployment artefacts | none imposed |
| `chore` | What touches neither production code nor its delivery | none imposed |
| `ci` | Pipeline configuration | none imposed |
| `docs` | Documentation only | none imposed |
| `perf` | A performance gain, at constant observable behaviour | none imposed |
| `refactor` | Restructuring, at constant observable behaviour | none imposed |
| `revert` | The reversal of an earlier commit | per what it reverts |
| `style` | Formatting with no semantic effect | none imposed |
| `test` | Tests only | none imposed |

The type MUST be lowercase and belong to this table. A breaking change carried
by any of these types produces a `MAJOR`.

#### Scope

The scope MAY be provided, and is **required** on `feat` and `fix` (see below).
When present it MUST be lowercase and MUST be one of:

| Scope | Covers |
|---|---|
| `core` | The foundation library — defining, generating and validating catalogs |
| `analyzers` | The Roslyn analyzers **this repository publishes**, and their diagnostics |
| `cli` | The **`dcat` command-line tool** — its command tree, arguments and exit codes |
| `sonar` | The **catalog of SonarQube/SonarAnalyzer rules** |
| `netanalyzers` | The **catalog of Microsoft .NET analyzer (CAxxxx) rules** |
| `stylecop` | The **catalog of StyleCop analyzer rules** |
| `codestyle` | The **catalog of Roslyn IDE code-style (IDExxxx) rules** |
| `cataloggen` | The **generation engine** (`eng/CatalogGen`) — acquiring analyzer assemblies, reading their descriptors, emitting a catalog |

> `analyzers` and `netanalyzers` are close in spelling and far apart in meaning.
> `analyzers` is *code this repository ships* — Roslyn analyzers that enforce our
> own contract. `netanalyzers`, `sonar`, `stylecop` and `codestyle` are *catalogs describing
> somebody else's rules*, which is the product. When in doubt: if the commit
> changes a rule catalog, its scope is the vendor's name.

> `stylecop` and `codestyle` are closer still, and they are two different
> vendors' rules: `stylecop` mirrors the StyleCop.Analyzers project's `SAxxxx`,
> `codestyle` mirrors Roslyn's own `IDExxxx`. The tags differ by the same two
> syllables — `stylecop-v1.2.0` and `codestyle-v1.2.0` publish different packages
> — so read a release tag twice before pushing it. Each is named after the
> package it publishes, which is the rule everywhere here; the resemblance is the
> upstreams' and not this repository's to fix.

This list lives here, in the repository, where a tool can check it — it is the
`SCOPES` list in `tools/commit-lint/lint-commit-message.sh`, and the two MUST be
changed together. So must the train table below and the rows in `tools/trains.sh`:
the two sets are the same set. Every scope the linter accepts routes to exactly
one train, because a scope on no train is silently dropped from the release notes
and the changelog. A scope MUST NOT be a file name or a class name: those move;
the zone they inhabit does not. `fix(core):`, never `fix(RuleCatalog.cs):`.

The scope is load-bearing for the release record. Commits are partitioned into
**release trains** by scope, and each train versions and publishes
independently:

| Train | Scopes | Why it moves at its own pace |
|---|---|---|
| `lib` | `core`, `analyzers` | The foundation. Deliberately very stable — a catalog contract rests on it. |
| `cli` | `cli`, `cataloggen` | The `dcat` tool. Follows Roslyn and upstream package layouts, which move for their own reasons. |
| `sonar` | `sonar` | Follows SonarSource's release cadence. |
| `netanalyzers` | `netanalyzers` | Follows the .NET SDK's analyzer releases. |
| `stylecop` | `stylecop` | Follows StyleCop's releases. |
| `codestyle` | `codestyle` | Follows Roslyn's releases: the upstream package is versioned with the compiler. |

Two scopes ride the `cli` train, and the distinction between them is worth
keeping. `cli` is the shell — the command tree, the arguments, the exit codes;
`cataloggen` is the engine behind it — obtaining analyzer assemblies, reading the
descriptors they declare, emitting the catalog. They are one published package,
so they version together, but a change to how descriptors are read and a change
to how a command line is parsed are different facts about it, and the release
record reads better for saying which.

`cataloggen` belonged to no train until the generator was published: it produced
the catalogs and was never packed, so nothing it did could move a version.
ADR-0017 changed that premise rather than the reasoning — the engine now reaches
users inside `dcat`, so its corrections reach them too. What did not change is
why it stays off `lib`: the foundation's version must say something about the
foundation, and generation work reaches none of its consumers.

A catalog moves when its vendor ships rules; the foundation moves when its own
contract changes. Folding them into one version number would force a release of
everything each time one vendor published, and would make the foundation's
version say nothing about the foundation. A commit's scope decides which train's
release notes and changelog it lands in.

Because of that, **`commit-lint` requires a scope on the two version-driving
types, `feat` and `fix`**, and rejects an unscoped one at the `commit-msg` hook
and in CI: it would match no train and be **silently dropped from every release
note and changelog**, vanishing from the release record. Every other type keeps
the scope optional — what genuinely belongs to no component stays unscoped
(repository infrastructure: the solution, `Directory.Build.props`, the
workflows, `.gitignore`, `CLAUDE.md`; repository-wide documentation), and those
use non-version-driving types anyway: `ci: …`, `docs: …`, `chore: …`.

When one atomic change crosses several components, the commit MUST carry all
their scopes, comma-separated with no space and ordered alphabetically. The
order is alphabetical so a given pair is always written the same way, and found
again with a single `git log --grep`.

```
fix(core,sonar): resolve a rule reference through its replacement id
```

#### Description

* It MUST be in the imperative present: `add`, not `added` nor `adds`. The
  description completes one sentence — *If applied, this commit will …* — and
  only the imperative fits it: *…will add a deprecation reason*.
* It MUST begin with a lowercase letter and MUST NOT end with a period. The
  header line is not a sentence; it is a title.
* The full header line — type, optional scope, optional `!`, colon and
  description — MUST fit in 72 characters. Beyond that, once the abbreviated
  hash is prefixed, it overflows the 80 columns of a terminal in a
  `git log --oneline`.

#### Body

The body MAY be provided, after a blank line. It explains **why** the change
happens — the constraint, the symptom, the trade-off. The *what* is already in
the diff; repeating it is noise.

When that why is not readable from the diff, the body SHOULD be provided.
Abstaining is paid for six months later, on a commit no one can interpret any
more.

#### Footers

Footers MAY be provided, after a blank line. Each footer MUST take the form
`Token: value`. The token MUST be words separated by hyphens, **each word
capitalized**: `Co-Authored-By`, `Reviewed-By`, `Refs`, `Reverts`.
`BREAKING CHANGE` is the sole exception to this form.

> This "every word capitalized" casing is a deliberate departure from the usual
> single-initial convention. It exists so that hand-written footers stay
> consistent with the trailers this repository's automated commits already
> carry — `Co-Authored-By`, `Claude-Session`. One rule for every footer beats
> two.

When an issue exists, its number MUST live in a `Refs:` footer, and MUST NOT
appear in the description — the description states the change, not where it was
requested. The footer carries the key (`#142`), never the URL: a commit message
is not rewritten, and the number survives what an address does not. (A tooling
footer such as `Claude-Session` is a URL by nature, and is exempt from that last
point.)

A commit is **not** the place to close an issue. Closing is a
repository-workflow concern: put `Closes #142` in the pull request description,
and GitHub closes the issue on merge. The commit itself stays neutral, carrying
at most a `Refs:`.

#### The `Docs:` footer

A `feat` MUST carry a `Docs:` footer. It names the documentation the commit
changed:

```
feat(cli): report which catalogues a manifest run rewrote

Docs: doc/guide/dcat-reference.en.md, doc/guide/dcat-reference.fr.md
```

or it says, in words, why the commit changed none:

```
feat(core): widen the descriptor cache

Docs: none — the cache is internal; nothing a consumer can name has moved
```

The rule follows from what `feat` means here — *a new capability, visible to the
consumer of the package*. A capability the consumer can see and cannot read
about is either undocumented or mistyped, and the footer makes the author say
which. `Docs: none` without a reason is refused: an exemption nobody can judge is
a hole. The reason is a sentence in the permanent record, which is what makes it
reviewable.

Every entry MUST be a Markdown path relative to the repository root, and under
[`doc/`](doc/) a page and its translation are named **together** — the parity
test sees two files that both exist and cannot tell that only one was updated
([ADR-0022](doc/adr/0022-maintain-every-document-under-doc-in-english-and-french.en.md)).

The footer is **one line**, however long that line gets. Both checks below read
the first line matching `Docs: ` and no further, so wrapping the list to fit a
72-column log hides every path below the fold from both of them — and they would
then report success on a list they had read part of. Folding it is refused rather
than supported, in the linter and in the resolver: a rejection you can read beats
a check that quietly does less. A trailing comma is refused for the same reason,
being a list with something after it wherever that something went.

The footer is checked twice, and the split is deliberate. Its *shape* is checked
by the commit linter, so the `commit-msg` hook reports a missing or malformed
footer before the commit is recorded. Whether the files it names were really
touched is a question about a commit, not about a message — the hook runs before
the commit exists, and on `git commit --amend` the index holds only the reword —
so `tools/commit-lint/check-docs-footer.sh` answers it in CI, and can be run by
hand on any commit:

```
tools/commit-lint/check-docs-footer.sh --commit HEAD
```

Only `feat` is bound. A `fix` restores behaviour the documentation already
promises, so the honest answer would nearly always be "none", and a field whose
usual value is "nothing" stops being read. A `fix` that *does* change what is
documented may carry the footer; nothing forbids it. The decision, its
alternatives and what it deliberately does not guarantee are in
[ADR-0025](doc/adr/0025-bind-every-feature-commit-to-the-documentation-it-changed.en.md).

#### Breaking changes

A breaking change MUST be signalled twice: by a `!` placed just before the
colon, and by a `BREAKING CHANGE:` footer in capitals.

```
feat(core)!: return an unresolved reference instead of throwing

BREAKING CHANGE: resolving an unknown rule id yields an unresolved reference
where it used to throw. Callers must handle the reference instead of catching.
```

The `!` is what one sees in a `git log --oneline`. The footer is what one reads
when it is time to migrate. The two have different readers; neither replaces the
other.

What is breaking reads on the **published contract**, not on internal code. In
this repository that contract explicitly includes **rule identifiers and catalog
entry keys**: a consumer references them symbolically, so renaming or removing
one is a breaking change of the catalog that carries it. Renaming an `internal`
type breaks nothing.

#### Reverts

A revert commit MUST carry the `revert` type, repeat the description of the
reverted commit, and reference its SHA in a `Reverts:` footer.

```
revert(sonar): drop the rules removed in SonarAnalyzer 10.30

Reverts: b36765a
```

A revert's effect on the version is qualified like any commit's: from the
consumer, on the published contract. Reverting a change not yet released
neutralizes its effect. Removing a capability already released is a breaking
change, and the commit MUST then carry the `!` and the `BREAKING CHANGE` footer.

### The doctrine

**The issue is the unit of the request, the commit the unit of the change.** An
issue produces as many commits as it carries intentions: the feature, the
refactor that prepared it, the fix found in review. Each carries its own type,
all carry the same `Refs:`.

**The type is the intention, not the content of the diff.** A feature arrives
with its tests, its API documentation, its sample: the commit stays a `feat`.
`test` and `docs` designate a change that touches *only* tests, *only*
documentation. Splitting a `feat` into five commits because it spans five
directories manufactures commits that do not compile alone.

**`feat` or `fix` is decided from outside the component.** The criterion is not
the size of the diff, it is what the consumer of the package observes. Three
lines that restore the promised behaviour are a `fix`. One line that opens a new
capability is a `feat`.

**`refactor` and `perf` make a promise: observable behaviour does not change.** A
`refactor` that fixes a bug in passing is a mislabelled `fix` — and the
correction becomes invisible to whoever prepares the release.

**`chore` is not the bin.** Everything that fits nowhere lands there, and the
type ends up meaning nothing. Before writing `chore`, reread the table.

**What is breaking reads on the published contract**, not on internal code. A
renamed `internal` type breaks nothing. A changed return type, a rule
identifier, a catalog entry key — those do.

**The wrong type is fixed before the merge.** A `git rebase -i` rewrites the
message while the commit has not reached a shared branch. After that, the cost
of the correction exceeds the cost of the error: leave it and move on.

**The version number is decided by reading the history.** Whoever prepares the
release reads the increment there, within one train: a lone `fix` gives a
`PATCH`, a `feat` imposes at least a `MINOR`, a breaking change imposes a
`MAJOR`.

**Who decides what.** The author of the commit chooses the type and the scope.
The reviewer of the pull request refuses a non-conforming message as they refuse
non-conforming code. The maintainers own the list of scopes and the list of
types.

### Examples

**A feature, with scope and issue.**

```
feat(sonar): carry the rule's clean-code attribute

Refs: #142
```

**A fix whose why is not readable from the diff.**

```
fix(core): compare rule ids with the invariant culture

Ids were compared with the host's culture, so a Turkish-locale machine
resolved `CA1062` and `ca1062` as different entries while CI resolved them
as one. CI and developers disagreed on the very same commit.

Refs: #128
```

**A refactor, promising nothing but iso-behaviour.** Neither body nor footer:
the diff says it all.

```
refactor(core): extract catalog lookup into a resolver
```

**A breaking change, with the migration instruction.**

```
feat(core)!: return an unresolved reference instead of throwing

BREAKING CHANGE: resolving an unknown rule id yields an unresolved reference
where it used to throw. Callers must handle the reference instead of catching.

Refs: #150
```

### Anti-patterns

Ordered as the rules are: type, scope, description, body, breaking, issue.

| Message | What is wrong |
|---|---|
| `chore: handle an unknown rule id` | A `fix` in disguise. The release preparer will not see it, and the version will not move when it should. |
| `feat: refactor the catalog reader` | The type contradicts the description. One of the two is lying. |
| `fix(core): fix the resolver and add a CI cache` | Two changes, two commits. No version can describe this one. |
| `fix(RuleCatalog.cs): formatting` | The scope names a file. It designates a zone: `core`. |
| `feat(sonar, core): carry the clean-code attribute` | A space after the comma, and the order is not alphabetical. Two spellings for one pair — write `feat(core,sonar):`. |
| `fix(core): Fixed the null dereference.` | Capital, past tense, trailing period. Three form rules broken, one useful word. |
| `feat(core): add support` | Support for what? The description must stand alone in a `git log`. |
| `fix(core): change line 42 of RuleCatalog` | The description names a line. It should name a change. |
| `feat(netanalyzers): add the CA rules` — meaning our own analyzer | Wrong scope. `netanalyzers` is the catalog of Microsoft's rules; an analyzer we publish is `analyzers`. |
| `feat(core)!: return an unresolved reference` — no footer | The `!` warns; it migrates no one. |
| `feat(core): add rule resolution (#142)` | The issue eats the 72 characters of the description. Its place is a footer. |
| `refs: #142` | Lowercase token. The footer token is `Refs`. |

### Adoption

This guide is the rule for commits in this repository. Deviating from it
requires a justification — an ADR under [`doc/adr/`](doc/adr/), or an update to
this guide. The convention itself is recorded as
[ADR-0003](doc/adr/0003-adopt-and-enforce-a-conventional-commits-convention.en.md),
and the release trains that make the scope load-bearing as
[ADR-0002](doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md).

It applies from its adoption on, to every commit created after. Prior history is
not rewritten.

Enforcement is layered. A `commit-msg` hook (versioned under `.githooks/`,
enabled once per clone) refuses a non-conforming message at write time, and —
because a local hook can be bypassed — a CI check re-runs the same validation on
every pull request, so a bypassed hook is still caught before the merge. Merge
commits, generated by GitHub, are exempt.

### Credits

This section adapts the [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/)
specification, published under [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/).

## Pull request titles

The convention above governs each commit. A pull request needs a line of its
own, and it is not the same object: the commit is the unit of the change, the
**pull request the unit of the request** — the relation the doctrine already
draws between the commit and the issue. A pull request MAY therefore gather
several commits, of several types.

Its title is read in three places: the list of open pull requests, the
`Merge pull request #NN` commit GitHub writes when the branch lands (this
repository merges with a merge commit), and the draft of the release notes. It
earns the same care as a commit header. Unlike a commit, it is **not** linted;
it stands on the review, as the code does.

### The rule

* The title MUST be in **English**, like everything else recorded here.
* It MUST name the **whole** pull request, not one of its commits. The per-commit
  types live in the commits, where the hook and CI check them; the title says
  what the branch delivers.
* Its shape follows from how many intentions the pull request carries:
  * **One intention** — the branch does a single kind of thing. The title MUST
    mirror the commit header it collapses to:
    `<type>[(<scope>)][!]: <description>`, under the very rules of the section
    above — imperative present, lowercase after the colon, no trailing period. A
    one-commit pull request's title is that commit's header, verbatim.
  * **Several intentions** — the branch carries a feature and the refactor that
    prepared it, or a fix and the test that pins it. The title MUST NOT borrow a
    single `type:` prefix: it would name one commit and hide the rest. It states
    the subject in plain words, as a title — an initial capital, no trailing
    period. A topical prefix (`Release supply chain: …`) is welcome; a
    Conventional-Commits type is not, unless it is honestly the only one.
* Keep the title within the **72 characters** a commit header targets, so the
  pull request list shows it whole.
* The issue reference lives in the **description**, never the title: `Closes #NN`
  when the pull request closes the issue, so GitHub closes it on merge;
  `Refs: #NN` otherwise. The title states the change, not where it was asked. A
  breaking change is signalled the same way it is on a commit — the `!` and the
  `BREAKING CHANGE:` note ride the commit, and the template's "Breaking change"
  box repeats it — not the title.

### Examples

| Title | Why it fits |
|---|---|
| `ci: add dependency review on pull requests` | One intention. The title is the commit header. |
| `feat(sonar): carry the rule's clean-code attribute` | One intention, scoped. The issue it closes lives in the description, not here. |
| `Adopt and enforce a Conventional Commits convention` | The guide, the hook, and the CI gate — several commits of several types. A plain title names them all. |
| `Release supply chain: build provenance + embedded SBOM` | Several intentions under one topic. A topical prefix carries it; no single `type:` would be honest. |

### Anti-patterns

| Title | What is wrong |
|---|---|
| `feat: various improvements` | A type on a grab-bag. Either it is one intention — name it — or it is several, and `feat:` hides them. |
| `fix(core): Fixed the null dereference.` | The single-intention form, wearing the commit header's own faults: capital, past tense, trailing period. |
| `Add rule resolution (#142)` | The issue number belongs in the description's `Closes`/`Refs`, where GitHub reads it — not eating the title. |
| `Corrige la résolution des règles` | Not English. |
