# ADR-0013 | Write the shell tooling for POSIX sh, not bash

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0013-write-the-shell-tooling-for-posix-sh-not-bash.fr.md)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

This repository carries shell scripts in three places. `tools/` holds the release
tooling — the release-train table every packaging and release-notes step sources,
the commit linter shared by the local hook and CI, and a small test harness for
them. `.claude/hooks/` holds the agent hooks. `.githooks/` holds the git hooks,
which carry no file extension because git requires the exact hook name.

What `tools/trains.sh` answers is which projects a release publishes. A project
its discovery misses is silently absent from its own release; one it wrongly
finds is published when it must not be. Neither mistake shows up as a red build.

These scripts run in places that do not share an installer: a GitHub-hosted
runner, a maintainer's machine, and a git hook invoked by whatever git the
contributor has. Nothing in the repository installs a shell, and the test harness
under `tools/tests/` deliberately depends on nothing beyond one — no bats, no
package manager.

`local`, `[[ ]]` and arrays are extensions bash and ksh provide; POSIX defines
none of them. The behaviours differ concretely rather than stylistically: dash,
which is `/bin/sh` on the Ubuntu runner images, rejects `[[` outright, while it
accepts `local` as an extension of its own. A shell function's exit status, in
every dialect, is the status of its last command.

Two checks already read these files on every pull request: shellcheck, which
takes the dialect from the shebang and is held to zero findings at every
severity, and the shell test suite, which is executed with `sh` rather than bash
so that a bashism fails here instead of on a leaner machine.

The same files are also submitted to a third-party static analysis service, whose
shell rules are written for bash and take no notice of a shebang. It asks for
`local` in place of a positional parameter, for `[[` in place of `[`, and for an
explicit `return` at the end of every function. On the first analysis of this
repository those three rules accounted for 34 of the 55 open findings.

## Decision

The repository's shell tooling is written to POSIX sh, and every tool that reads
it is configured for that dialect rather than for bash.

## Rationale

The scripts under `tools/` decide what a release publishes, so they have to run
wherever a release runs, without anyone having installed a shell first. POSIX sh
is the only dialect that assumption holds for. Everything else on offer — `local`,
arrays, `[[` — is convenience bought by adding a runtime dependency to the code
with the least tolerance for one.

The convenience is genuinely small at this size. These are a few hundred lines of
string handling over a pipe-separated table; what `local` would buy is scoping
that a naming convention already approximates, and what arrays would buy is a
data structure the scripts do not need.

The constraint is worth recording because it is not self-evident from reading a
script, and because it now binds more than the scripts. A tool that reports bash
advice against a POSIX file is not reporting a defect; it is reporting a dialect
mismatch, and one that can never be resolved — the finding is permanent, and there
are more of those findings than of the actionable ones on the same files. A report
that is mostly noise stops being read, which is the reasoning already behind
holding shellcheck to zero findings at every severity. So the analysis follows the
decision rather than the decision bending to the analysis.

One of those three pieces of advice is worse than noise. A function ending in an
unconditional `return 0` reports success whatever its last command did, and the
last command is where these functions do their work. Applied to the discovery that
decides which projects a release publishes, it converts a broken pipeline into a
successful empty answer — the silent-success failure this repository's tooling is
written to prevent. Taking dialect advice on trust, in a file whose whole purpose
is to make a silent failure impossible, is the specific mistake being designed out.

Recording it also settles the question for the next tool. The decision is a
property of the code, not of any one analyser, so an analyser added later is
configured from the record rather than from an argument had again.

## Alternatives Considered

### Write the tooling in bash and declare it in the shebang

Bash is present on every GitHub-hosted runner and on most developer machines, and
`local`, arrays and `[[` would make the scripts shorter and their scoping
explicit. The bash-dialect findings would then be advice worth taking, and no
analyser would need configuring.

Rejected because it puts a runtime dependency on the code that decides what a
release publishes, in exchange for a saving that is small at this size. The
dependency is invisible until the day something runs in a container that ships
only a POSIX shell, and the failure surfaces as a release that published the wrong
set of projects rather than as a missing interpreter.

### Keep POSIX sh and leave the dialect findings standing

Every suppression is a claim someone has to maintain, and somewhere a real defect
can hide. Leaving the findings open costs nothing mechanically and keeps the
analysis configuration empty.

Rejected because these particular findings can neither be acted on nor go away.
They are permanent, they outnumber the actionable findings on the same files, and
they would be re-triaged by every future reader who does not already know the
dialect. That is the failure mode the zero-findings bar on shellcheck exists to
avoid, arriving through a different door.

### Replace the shell tooling with a program in a language the repository builds

The release logic could be C# in this repository's own toolchain, analysed by the
same analysers as everything else, with no dialect question at all.

Rejected because the scripts run before and around the .NET build — a git hook
fires before anything is restored, and a workflow step reads the train table to
decide what to build. Requiring a restored SDK to answer "which projects does this
train publish" inverts the dependency, and trades a dialect constraint for a
bootstrapping one that is harder to satisfy.

## Consequences

### Positive

* The release tooling runs wherever a POSIX shell exists, including the leaner
  images a release step may be given, with no interpreter to install first.
* The constraint is checked on every pull request by two independent means — the
  dialect shellcheck applies, and the shell the test suite is run with — so it does
  not rest on a contributor remembering it.
* Analysis reports about these files carry only findings that can be acted on,
  which is what keeps them worth reading.

### Negative

* No `local`, no arrays, no `[[`. Function parameters are named by assigning them
  to prefixed globals, which is more verbose and relies on a naming convention
  where a language feature would have done the work.
* Every analyser that reads this repository's shell has to be told the dialect.
  One added later starts by repeating the mismatch, and the configuration is per
  tool rather than declared once.

### Risks

* dash accepts `local`, so the only thing standing between this repository and a
  bashism it would not notice is shellcheck's POSIX dialect. The test suite would
  pass. If the lint job's severity bar were ever relaxed, that guard would stop
  applying **silently**.
* An exclusion scoped by file pattern also covers files that do not exist yet. A
  script added later in another dialect, in the same tree, would inherit the
  exclusion without anyone deciding it should.

## Follow-up Actions

* None. The dialect is enforced by the existing lint workflow and by the shell
  test suite; the per-analyser configuration lives beside the workflow that runs
  the analysis, with the reason for each excluded rule stated there.

## References

* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.en.md) — the
  sibling principle: a rule is recorded where the tooling that enforces it can
  read it, so none rests on attention alone.
* `.github/workflows/lint.yml` — the shellcheck bar and the shell the test suite
  is run with.
* `.github/workflows/sonar.yml` — the excluded bash-dialect rules, each with the
  reason it cannot apply here.
