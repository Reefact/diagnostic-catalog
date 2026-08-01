# ADR-0021 | Derive the build's Sonar rule set from the server's quality profile

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0021-derive-the-build-rule-set-from-the-quality-profile.fr.md)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

This repository enforces a warning ratchet: the codebase builds with zero warnings, and CI turns
every warning into an error, so a new one can never merge. The ratchet is stated in
`Directory.Build.props` and its reasoning is stated again in `.editorconfig` — a rule nothing acts
on is how a rule drifts.

The Sonar C# rules sat outside it. They were evaluated in exactly one place: the scanner-hooked
compilation in `.github/workflows/sonar.yml`, which is also the one build in this repository where
the ratchet is deliberately switched off, because the scanner needs the compilation to complete in
order to collect diagnostics and upload them. A contributor — human or agent — therefore met a
Sonar rule after the merge, on a dashboard, and never while writing the code. Forty-six issues
accumulated that way before anyone looked.

The rules the report is scored against live on the server, in a quality profile. The repository
held no record of which rules those were.

The `SonarAnalyzer.CSharp` NuGet package is not that profile. Measured here: its default
configuration leaves `S3776` disabled although the profile activates it, so the four methods the
dashboard reported over the cognitive-complexity threshold produced no local diagnostic until the
rule was given an explicit severity. The divergence runs the other way too — the package at its
current version reports rules the last server analysis did not.

The quality profile bound to this project is SonarSource's built-in "Sonar way": 377 enumerable
active rules, not editable by this organization. It moves when SonarSource ships an analyzer
release, a handful of times a year.

Separately, `dotnet-sonarscanner end` uploads an analysis and returns. It neither waits for the
quality gate nor reads it, and no job carries the verdict, so `sonar.yml` reports success as soon as
the upload succeeds. At the time of writing the gate is red — on `new_coverage` alone, with every
issue condition green.

## Decision

The set of Sonar rules the build enforces is generated from the project's SonarQube Cloud quality
profile and committed, with every exception to it written down in `.editorconfig`.

## Rationale

The ratchet already exists and already works; the only reason Sonar rules escaped it is that the
build did not know which rules to run. Generating that list from the profile is what closes the gap,
and it closes it in the place the repository already puts such rules — the build a contributor runs
— rather than adding a review habit.

Reading the profile rather than trusting the package's defaults is not a refinement, it is the whole
point. The two disagree in both directions, and the measured `S3776` case shows the direction that
matters: a rule the report enforces and the build ignores produces exactly the silence this decision
exists to remove. A generated list is also the only form that can be checked for drift; a
hand-written one would rot the first time SonarSource shipped a release, and nobody would know.

Enforcement is the default because the alternative was measured and does not work. At `suggestion` a
Sonar diagnostic prints nothing in `dotnet build` at any verbosity — it reaches an IDE and a log file
and nobody else — so a generated list at that severity would have been invisible to precisely the
reader it exists for.

The exceptions live in `.editorconfig` rather than in the generated file so that membership stays
generated and every departure from it stays hand-written and reasoned. Two kinds are admitted, and
the distinction is deliberate: a rule whose violations are not yet cleared carries its count and says
"not yet", while a rule this codebase refuses carries its reason. A parked rule is a debt with a
name, not a rule quietly switched off. It follows that parking is temporary by construction — the
better end state for a small deliberate set is a suppression at the site, which keeps the rule
enforced everywhere else.

Regenerating the list can turn the build red, and that is accepted rather than mitigated: a rule the
profile adds has to be cleared or parked deliberately, which is the same bargain the warning ratchet
already strikes.

The gate is read on a schedule rather than waited for in the pull request, because the two questions
are different. Making the scanner wait would couple merge availability to a third-party service —
the analysis runs on every pull request, so an outage would stop every merge, while a red gate today
stops nothing. A scheduled read enforces the verdict and costs a red nightly instead of a frozen
repository. It is also not redundant with the build: the gate measures symbolic-execution rules the
analyzer package does not run, every non-C# rule family, and coverage, duplication and hotspot
review, which no analyzer can answer. The condition that is red today falls in that last class.

The cost accepted is version drift. The package pin follows SonarSource's release line while the
profile follows the server's, so a bump can introduce a rule the report does not yet have. This is
the same shape as the analyzer bumps this repository already takes, and the weekly drift check is
what keeps the two visible to each other rather than silently apart.

## Alternatives Considered

### Leave the Sonar rules to the dashboard

Considered because it is the status quo and costs nothing to keep: the analysis already runs, and
the issues are already listed.

Rejected because the evidence against it is this repository's own history. Forty-six issues reached
`main` under it, including four methods over a complexity threshold, and none of them was seen by
the person who wrote the code at the moment they wrote it. A signal that arrives after the merge is
the failure mode the ratchet was built to prevent.

### Add the analyzer package and accept its default rule set

Considered because it is one line and needs no tooling, no generated file and no scheduled job.

Rejected on measurement: the package's defaults leave `S3776` off, so the largest findings the
dashboard reported would still have merged unseen, and the build and the report would have gone on
disagreeing about which rules exist — the harder failure, because it looks like agreement.

### Make the scanner wait on the quality gate

Considered because it would enforce the verdict at the pull request, where it is cheapest to act on.

Rejected because it couples merge availability to a third-party service: the analysis runs on every
pull request, so a SonarQube Cloud outage would block every merge — a strictly worse failure than
the one being fixed, since a red gate blocks nothing today.

### Have the drift check repair the file itself

Considered because it removes a manual step, and the regeneration is mechanical.

Rejected because it would give a scheduled job write access to the very file that governs which
rules block a merge. Reporting the drift and leaving the regeneration to a human keeps that decision
where decisions belong. Promoting it to open a pull request remains a small change if the trade is
ever judged worth it.

## Consequences

### Positive

* A Sonar rule now fails the build that introduces it, on the contributor's machine and in CI,
  instead of appearing on a dashboard after the merge.
* The build and the report are demonstrably talking about the same rules, and the drift check keeps
  it that way.
* Every rule not enforced is named, with a count or a reason. There is no silent exception.
* The quality gate verdict is finally enforced by something.
* A fork's pull request gets the rules too: the analysis job cannot run without a secret a fork
  cannot read, but the analyzer package needs none.

### Negative

* A new dependency, and a build-time cost on every project.
* Version drift between the package pin and the server's analyzer becomes a thing to manage.
* Regenerating after a profile change can turn CI red, and the work to clear or park it lands on
  whoever regenerates.

### Risks

* A package bump can introduce rules the server does not have yet, turning the build red on findings
  the dashboard is silent about. Mitigated by the drift check making the difference visible, and by
  the parking mechanism giving it somewhere to go.
* The nightly gate check does not block a merge by design, so it can be ignored. It is an alarm, and
  an alarm nobody answers is the failure mode this ADR warns about elsewhere.

## Follow-up Actions

* Clear the 18 parked `S8969` sites and delete the entry.
* Replace the two refusals (`S101`, `S6562`) with suppressions at the sites, so the rules stay
  enforced everywhere else.
* Decide whether the quality gate's `new_coverage` condition is met by raising coverage or by moving
  the threshold. This ADR enforces the verdict; it does not settle it.

## References

* `Directory.Build.props` — the warning ratchet, and where the analyzer and the generated rule set
  are wired in.
* `build/sonar-profile.globalconfig` — the generated rule set.
* `.editorconfig` — the exceptions, and the only place a rule is not enforced.
* `tools/sonar-profile/` — the generator and the gate reader.
* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.en.md) — the same argument for
  the coding rules: a rule stated where no compiler and no agent can read it is enforced by nobody.
* [ADR-0013](0013-write-the-shell-tooling-for-posix-sh-not-bash.en.md) — why the two new scripts are
  POSIX `sh`.
* `Reefact/first-class-errors`, ADR-0062 — the sibling repository where this arrangement was first
  built; this ADR adopts it, with the measurements retaken on this repository.
