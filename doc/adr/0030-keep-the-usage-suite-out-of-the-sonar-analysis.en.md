# ADR-0030 | Keep the usage suite out of the Sonar analysis

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0030-keep-the-usage-suite-out-of-the-sonar-analysis.fr.md)

**Status:** Accepted
**Proposed:** 2026-08-04
**Accepted:** 2026-08-05
**Decision Makers:** Reefact

## Context

`tests/DiagnosticCatalog.Usage` is not a test project in the ordinary sense: its **build**
is the assertion. Every other suite compiles a snippet in-process and asserts what the
analyzer reported, which proves the analyzer answers correctly on inputs somebody thought
to write down. It cannot answer the question a 1.0 rests on — does the analyzer stay quiet
on ordinary code it was not shown? A false positive is not found by asserting an
expectation; it is found by writing code that ought to be clean and discovering it is not.

So every file there is code a consumer could reasonably write, and the contract is that it
produces no `DCAT` diagnostic. To make that contract readable, the project's `.csproj`
switches every non-DCAT analyzer off — `EnableNETAnalyzers=false`, `AnalysisLevel=none`,
and `<PackageReference Remove="SonarAnalyzer.CSharp" />` — with the reasoning written next
to it: the files deliberately imitate other people's habits, and a build drowning in
S-rules would bury the one diagnostic the project exists to surface.

**That setting does not reach the Sonar analysis.** `dotnet-sonarscanner begin` attaches
its own `SonarAnalyzer.CSharp`, configured from the server's quality profile, so the
project's `Remove` has no effect on it; and `sonar.yml` builds with
`TreatWarningsAsErrors=false`, because the scanner needs the compilation to complete. The
findings are therefore collected and uploaded in silence. The build is green locally, green
in CI, and the dashboard fills up.

Measured on `main` before this decision, at 138 open issues:

| | |
|---|---|
| In `tests/DiagnosticCatalog.Usage` | **136** (98.5 %) |
| In `tools/tests/*.sh` | 2 (`shelldre:S1192`) |
| In `src/` | **0** |

The rules are exactly the ones the suite is built to trip: `S3400` "declare a constant
instead of this method" ×99 — every rule declaration's `Id` and `Category`; `S101` "rename
`RULE_001`" ×15; `S1186` empty method ×7; `S3903` type outside a named namespace ×2; and
single instances of commented-out code, `[Obsolete]`, and a static constructor.

Both red quality-gate conditions trace to the same directory. The two `S3903` findings are
the project's only two reported **bugs**, which put `new_reliability_rating` at 3 against a
threshold of 1. And the suite contributes 162 lines at **zero** coverage — it asserts by
building and is never executed — which pulls the project to 79.90 % against a threshold
of 80. Excluded, the same analysis reads 83.74 %.

The timing is why this surfaced as a gate failure rather than as quiet debt: the suite
landed on 1–2 August and the new-code baseline is 30 July, so all 136 count as new code.

## Decision

**`tests/DiagnosticCatalog.Usage` is excluded from the SonarQube Cloud analysis**, through
`sonar.exclusions` in `.github/workflows/sonar.yml`, alongside the generated catalogues:

```
/d:sonar.exclusions="**/*.g.cs,**/DiagnosticCatalog.Usage/**"
```

`sonar.exclusions`, not `sonar.coverage.exclusions`: both dimensions are wrong here, the
issues and the coverage, and excluding one would leave the other misreporting.

The pattern is anchored on the directory name rather than on `tests/` so that it does not
depend on where the scanner computes `sonar.projectBaseDir`. The name is unique in the
repository.

## Rationale

The precedent is already in the file. The generated catalogues are excluded because
"nobody can act on an issue reported there". The usage suite is the stronger case: there
the reported shape **is** the assertion. `RULE_001` is not a naming slip that nobody got
round to fixing — it is the input that proves `DCAT0005` does not fire on a name shaped
that way. Renaming it to `Rule001` to satisfy `S101` would not improve the code; it would
delete a test.

So this is not the familiar act of silencing an inconvenient finding. The findings are
correct about the code and wrong about what the code is for, and no per-issue triage fixes
that: there are 136 of them today and every file added to the suite mints more, so
marking them "won't fix" on the server is work with no end and no record in the tree.

This keeps the property ADR-0021 established — a rule is either enforced or its exception
is written down, never quietly absent. The exception is written down twice: in the
workflow, next to the pattern, and here.

**It does not contradict ADR-0024**, which requires the build to fail on any diagnostic the
ratchet cannot see. That decision exists to abolish *reports nobody can act on*, and its
subject is this repository's own code under its own ratchet. The usage suite is already
outside that regime by explicit prior decision — its `.csproj` turns the analyzers off and
promotes `TreatWarningsAsErrors` unconditionally, making its build **stricter** than the
rest of the repository on the one rule family that applies to it. Excluding it from the
server's view serves ADR-0024's purpose rather than undercutting it.

## Consequences

**The suite becomes invisible to Sonar.** A genuine defect there would go unreported by the
dashboard. This is accepted: the project ships nothing, has no consumer and no runtime — it
is never executed — and its only contract, that no `DCAT` diagnostic fires, is enforced by
its own build on every developer machine and in CI, which is a check Sonar was not
performing anyway.

**The quality gate should return to green,** since both failing conditions trace here. The
coverage figure rises from 79.90 % to about 83.74 %, and it is worth being blunt about what
that is: not an improvement in testing, but the removal of 162 lines from a denominator
they never belonged in.

**The remaining two issues are real and stay.** `shelldre:S1192` in
`tools/tests/test-docs-footer.sh` and `tools/tests/test-commit-lint.sh` reports duplicated
string literals in test scripts. They do not affect the gate and this decision does not
touch them.

**A rename of the directory silently un-excludes it.** The exclusion is a path pattern and
nothing binds it to the project. The failure is loud rather than dangerous — the dashboard
would refill — but it would be met on the dashboard rather than in the diff that caused it.

## Follow-up Actions

* Clear the two `shelldre:S1192` findings in `tools/tests`, or record why they stand.
* Move the exclusion with the directory if `tests/DiagnosticCatalog.Usage` is ever renamed.

## References

* [ADR-0021](0021-derive-the-build-rule-set-from-the-quality-profile.en.md) — the build's
  Sonar rule set, and the property this record preserves: enforced, or the exception is
  written down.
* [ADR-0024](0024-fail-on-any-diagnostic-the-ratchet-cannot-see.en.md) — the invariant
  about reports nobody can act on, which this decision serves rather than contradicts.
* [`.github/workflows/sonar.yml`](../../.github/workflows/sonar.yml) — where the exclusion
  lives, with the same reasoning stated next to the pattern.
