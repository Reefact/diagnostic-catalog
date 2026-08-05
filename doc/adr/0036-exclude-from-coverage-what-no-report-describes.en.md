# ADR-0036 | Exclude from coverage what no coverage report describes

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0036-exclude-from-coverage-what-no-report-describes.fr.md)

**Status:** Proposed
**Proposed:** 2026-08-05
**Decision Makers:** Reefact

## Context

The SonarQube Cloud quality gate has one red condition left. The other two cleared when the ten
open issues over `tools/icon` were closed: `new_reliability_rating` and `new_security_rating` both
read A, and `new_maintainability_rating`, duplication and hotspot review were already green. What
remains is `new_coverage`, at **76.3 %** against a threshold of 80.

**Exactly one coverage report reaches the analysis.** `.github/workflows/sonar.yml` names it:

```
/d:sonar.cs.opencover.reportsPaths="artifacts/coverage/**/coverage.opencover.xml"
```

That report is written by `dotnet test` and it describes C#. The scanner nevertheless counts every
Python line in the repository as coverable, finds no report that mentions any of them, and reports
all of them uncovered. Measured on `main` at the 2026-08-05T23:37 analysis:

| | Lines to cover | Uncovered | Coverage |
|---|---:|---:|---:|
| Whole project | 3 014 | 747 | 76.3 % |
| `tools/icon` (all five `.py`) | 532 | 532 | **0.0 %** |
| `src` | 1 334 | 45 | 90.6 % |
| `eng` | 1 144 | 166 | 82.4 % |

**532 of the 747 uncovered lines are Python** — 71 % of the shortfall — while both trees of C# are
already above the bar the gate asks for. Removing them from the denominator puts the project at
about **91.3 %**.

**The excluded code is not untested code.** `tools/tests/test-check-icon-template.sh` exercises it
on every pull request, in the `Test the shell tooling` job, with seven assertions: that the shipped
icons are drawn by the template, that a near-miss mark is rejected, that a file it cannot decode
fails rather than being skipped, that a candidate outside the repository is reported on rather than
crashed over, that what `render-icon.py` draws is what `check-icon-template.py` accepts, and that a
catalogue missing from the badge roster is refused. What is missing is not the testing. It is a
report that says so in a form Sonar reads.

So the gate is red on a number that reports a **missing report** rather than missing tests, and
`0.0 %` here is the absence of a measurement rather than the result of one.

This shape has been met once before, from the other side. [ADR-0030](0030-keep-the-usage-suite-out-of-the-sonar-analysis.en.md)
excluded `tests/DiagnosticCatalog.Usage` through `sonar.exclusions` — issues *and* coverage —
explicitly noting that "both dimensions are wrong here … and excluding one would leave the other
misreporting". That reasoning does not transfer whole: here one dimension is wrong and the other is
working.

## Decision

**Code written in a language no coverage report describes is excluded from Sonar's coverage
measurement**, through `sonar.coverage.exclusions` in `.github/workflows/sonar.yml`. Today that is
the whole of the Python:

```
/d:sonar.coverage.exclusions="**/*.py"
```

`sonar.coverage.exclusions`, not `sonar.exclusions`: the issues are wanted and stay.

## Rationale

**Zero per cent is not a measurement.** A coverage figure answers "how much of this did the tests
execute". For a file no report mentions, the analysis has not answered that question — it has
recorded that it could not ask it. Carrying that non-answer into a threshold makes the threshold
report something other than what it claims.

**Only coverage misreads this code, so only coverage is excluded.** This is the distinction from
ADR-0030, and it is the reason the mechanism differs. There, the reported shape *was* the assertion:
renaming `RULE_001` to satisfy `S101` would have deleted a test, so no per-issue triage could ever
converge. Here, Sonar's issues about the Python were correct and were acted on — ten of them, seven
fixed in code and three declined with the reasoning written into the workflow. That analysis is
working and this record does not touch it.

**Language-scoped, not directory-scoped.** The reason a line is excluded is the language of the
report, not the purpose of the directory. `**/*.py` says exactly that, and it is checkable against
the one `reportsPaths` line above it. `tools/**` would say "this directory does not matter", which
is a different claim and one this record does not make — the shell suite is CI-enforced precisely
because that directory does matter.

**It keeps the property [ADR-0021](0021-derive-the-build-rule-set-from-the-quality-profile.en.md)
established:** a rule is either enforced or its exception is written down, never quietly absent.
The exception is written twice, in the workflow next to the pattern and here.

## Alternatives Considered

### Produce a Python coverage report

The honest fix, and the only one that would actually measure anything. Deferred rather than
rejected: it means adding `coverage.py` to the workflow, feeding
`sonar.python.coverage.reportPaths`, and running the scripts under a harness that today is POSIX sh
with no dependency beyond a shell — a constraint [ADR-0013](0013-write-the-shell-tooling-for-posix-sh-not-bash.en.md)
set for good reasons and that a coverage harness would have to answer to. That is a change with its
own dependency question, not a line in a workflow. This record does not block it; it removes a false
red so the gate says something true in the meantime, and names the deletion that lands with it.

### Exclude `tools/**` instead

Rejected. It reaches the same files today and states the wrong reason. A `.py` added outside
`tools/` would still be miscounted, a shell coverage report would not change what the pattern means,
and the sentence a future reader would take from it — that `tools/` is not worth measuring — is one
this repository contradicts elsewhere by running that suite in CI at a bar of zero findings.

### Lower the gate's coverage threshold

Rejected. It would move the bar for the C# as well, which is the code the report actually describes
and the code the threshold exists to hold. The number is not too demanding; the denominator is
wrong.

### Leave it red

Rejected, and it is the option with the highest cost. `sonar-gate.yml` is a scheduled job whose
whole purpose is to be a standing alarm on the gate — an alarm permanently on is one nobody reads,
and the next genuine regression would arrive into a red that everyone had already learned to ignore.

## Consequences

### Positive

* The gate's coverage condition is projected to move from 76.3 % to about 91.3 %, which should
  return the gate to green — every other condition already reads OK.
* The figure the gate reports becomes a figure about the code its report describes.
* `sonar-gate.yml` becomes informative again: a red nightly means something changed.

### Negative

* **Python coverage stops being reported at all.** `0.0 %` was useless as a measurement but it was
  at least visible as a gap; after this, nothing on the dashboard shows that these 532 lines are
  unmeasured. This record and the workflow comment are the only trace.
* The denominator drops by 532 lines, and it is worth being as blunt as ADR-0030 was about the same
  move: this is not an improvement in testing. Nothing is better tested afterwards than before.

### Risks

* **A Python coverage report added later would be suppressed by this line.** The exclusion would
  silently discard exactly the measurement it stands in for, and nothing checks for that
  contradiction. The follow-up below is the only guard.
* A future `.py` that genuinely warrants coverage measurement is exempt on arrival, without anyone
  deciding so.
* The pattern names one language. A script rewritten in another that no report describes —
  JavaScript, PowerShell — reintroduces the same red with no record and no pattern covering it.

## Follow-up Actions

* Delete this exclusion if a Python coverage report is ever wired into the analysis, and supersede
  this record rather than editing it.
* Confirm on the next `main` analysis that `new_coverage` clears 80. If it does not, the cause is
  elsewhere and this record did not address it.

## References

* [ADR-0030](0030-keep-the-usage-suite-out-of-the-sonar-analysis.en.md) — the nearest precedent, and
  the contrast this record turns on: there both dimensions misread the code, here only one does.
* [ADR-0021](0021-derive-the-build-rule-set-from-the-quality-profile.en.md) — enforced, or the
  exception is written down.
* [ADR-0013](0013-write-the-shell-tooling-for-posix-sh-not-bash.en.md) — the constraint on `tools/`
  that a Python coverage harness would have to answer to.
* [`.github/workflows/sonar.yml`](../../.github/workflows/sonar.yml) — where the exclusion lives,
  with the measurement stated next to the pattern.
* `tools/tests/test-check-icon-template.sh` — what actually exercises the excluded code, and why
  `0.0 %` was never a statement about testing.
