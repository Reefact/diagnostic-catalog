# The testing strategy

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./testing-strategy.fr.md)

For anyone adding a test, or wondering why there are seven test projects. Each one exists because
something the others cannot reach would otherwise fail silently.

Three more projects sit under [`tests/`](../../tests) and assert nothing themselves:
`DiagnosticCatalog.Usage` is a consumer whose build the zero-footprint suite inspects
([ADR-0030](../adr/0030-keep-the-usage-suite-out-of-the-sonar-analysis.en.md)), and
`CatalogGen.AbsentContract` and `CatalogGen.PartialLoadFixture` are analyzer assemblies compiled
for the generator's tests to fail on.

## The seven projects

| Project | Asserts | Runs on |
| --- | --- | --- |
| `DiagnosticCatalog.UnitTests` | The foundation: that the attributes survive into metadata and read back | net10.0 **and the .NET Framework 4.7.2 CLR** |
| `DiagnosticCatalog.ZeroFootprint.UnitTests` | The opposite half: that a suppression leaves no trace in a consumer's build | net10.0 **and the .NET Framework 4.7.2 CLR** |
| `DiagnosticCatalog.Catalogs.UnitTests` | The generated catalogues, and the documents that describe them | net10.0 **and the .NET Framework 4.7.2 CLR** |
| `DiagnosticCatalog.Analyzers.UnitTests` | Every diagnostic and every code fix | net10.0 |
| `CatalogGen.UnitTests` | Acquisition, descriptor reading, naming, emission | net10.0 |
| `DiagnosticCatalog.Cli.UnitTests` | The command tree, the exit codes, what each verb refuses | net10.0 |
| `DiagnosticCatalog.Documentation.UnitTests` | The documentation, against itself and against the code | net10.0 |

Plus a suite `dotnet test` cannot reach at all — see [the shell suite](#the-suite-dotnet-test-cannot-reach).

## The .NET Framework floor

A project joins by importing the shared props and dropping its own `<TargetFramework>`:

```xml
<Import Project="..\build\Net472TestFloor.props" />
```

That import is the whole membership. The Windows-only `framework-floor` CI job discovers every project
carrying it and runs each with `-p:EnableNet472Floor=true -f net472`. The inner build is gated behind
that property, so an ordinary `dotnet build` — and the whole local inner loop — never sees it.

**Which projects join is a decision, not a default.** A test project joins when it exercises a shipped
`netstandard2.0` library, because that is what the floor exists to prove: not that the code *compiles*
against `netstandard2.0`, but that it *runs* on the real 4.7.2 CLR
([ADR-0001](../adr/0001-floor-the-libraries-on-net-framework-4-7-2.en.md)).

The four projects that stay off it cover tooling that never meets that runtime: `dcat` is floored at
net8.0, the analyzers run inside a host compiler, the generator is build-time, and the documentation
tests read Markdown. Running those a second time on another CLR would cost minutes and prove nothing.

## The failure every layer is written against

Each suite is shaped by one characteristic way its kind of test rots.

**Analyzer tests** fail by never running the analyzer. It was not registered, its `SupportedDiagnostics`
is empty, or it threw and Roslyn swallowed the throw as `AD0001` — and every "no diagnostics expected"
test then passes forever, growing more reassuring with each one added.

So `AnalyzerHarness` asserts three things on **every** run, before looking at any expectation:

* the analyzer declares at least one diagnostic;
* the snippet compiles — an uncompilable fixture hands the analyzer error types and turns every
  expectation into a study of nothing;
* nothing reported `AD0001`.

**Negative tests** fail by having no subject. `ZeroFootprintTests` asserts that a suppression left no
trace — an assertion that would pass forever the day its subject stopped being compiled. So the
subject carries a marker attribute of the test's own, and the first assertion is that *that* survived.

**Discovery-driven theories** fail by discovering nothing. `DocumentedSiblingsTests` reads the
catalogues from `eng/catalogs.json`; if the manifest stopped being copied beside the tests, an empty
family would assert nothing at all. So it asserts the family is at least two before asserting anything
about it. Every suite here that discovers its own inputs carries the same guard.

## What each suite covers

### The foundation, and the two halves

`DiagnosticCatalog.UnitTests` and `DiagnosticCatalog.ZeroFootprint.UnitTests` assert **opposite
things about the same subject**, and neither is meaningful alone: one shows the values are readable
when a tool asks for them, the other that they cost the consumer nothing when no tool does.

The zero-footprint project deliberately does **not** define `CODE_ANALYSIS`, so it compiles the way a
consumer's build does.

### The catalogues, and the documents about them

`DiagnosticCatalog.Catalogs.UnitTests` checks the generated output — every rule exposes a non-empty id
and category, every member is a literal constant, identifiers are unique and match their type name,
every category is declared by the catalogue's category class, no container is shadowed by its
namespace, and the provenance records the upstream release.

It also reads the **documents**, because nothing compiles a README: that each catalogue's mirror
banner matches the `CatalogSource` attribute the generator wrote, that each names its siblings and the
foundation, and that a nuget.org address in a README resolves to a package this repository actually
publishes.

### The diagnostics and the fixes

`DiagnosticCatalog.Analyzers.UnitTests` covers each `DCAT` id, each fix, and — the part worth reading
before adding one — each **refusal**. `ADR-0018` asks that a claim about what a fix declines be
testable rather than asserted, so every "no fix is offered here" has a test that fails unless the
diagnostic was still reported.

`MarkerRecognitionTests` is the smallest file with the most at stake: it covers the two cases a symbol
comparison would silently miss — a catalogue declaring its own marker, and a consumer who cannot
resolve the foundation.

### The generator

`CatalogGen.UnitTests` covers acquisition from each source kind, what each refuses, and the emission
properties that make a run reproducible.

### The documentation

`DiagnosticCatalog.Documentation.UnitTests` reads the working tree rather than a staged copy, because
it follows links out of `doc/` and a staged copy would have to reproduce the layout it is checking. The
repository root travels as assembly metadata stamped at build time.

Beyond parity and navigation, two of its assertions leave the documentation entirely: every shipped
`DCAT` is documented and every documented one is shipped; every `dcat` option in the reference exists
on the tool's settings types, and every one the tool exposes appears there. Both compare prose against
compiled truth rather than against another document.

## The suite `dotnet test` cannot reach

```bash
sh tools/tests/run.sh
```

The scripts under `tools/` decide **what a release publishes**. `trains.sh` answers which projects
belong to a train, and the packaging scripts pack exactly what it reports. A project the discovery
misses is silently absent from its own release; one it wrongly finds is published when it must not be.
Neither shows up as a red build, and `dotnet test` cannot reach shell at all.

Tests live in `tools/tests/`, one `test-<script>.sh` per script. Each runs as its own process, sources
`tools/tests/assert.sh`, and **ends with `finish`** — a file that forgets it exits on its last
command's status and reports success however many assertions failed.

The suite is invoked with `sh` rather than `bash`: every script carries a `#!/bin/sh` shebang and is
written to POSIX ([ADR-0013](../adr/0013-write-the-shell-tooling-for-posix-sh-not-bash.en.md)), so
running it under bash would let a bashism pass CI and fail on a contributor's machine.

`tools/packaging/verify-consumption.sh` is the same kind of gap seen from the other end, and it runs
from the release rehearsal rather than from `run.sh` because it needs real `.nupkg` files first. Its
twelve checks restore the packages as a consumer would: that a consumer of a catalogue is checked at
all, that `DiagnosticCatalog.dll` reaches their output folder while the analyzer assemblies do not,
that two catalogues deliver exactly one analyzer instance, and that the flow survives a second hop
through a library — which nothing compiled in-process against project references can observe
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.en.md)).

## Adding a test for a new diagnostic

1. **Write the assertions first**, if the contract is not obvious. The value is the moment it creates:
   if you cannot decide what to assert, that is the point to ask rather than to settle the question
   silently inside the implementation.
2. Add the id to `DiagnosticIds` and a descriptor to `Descriptors`. `RS2008` will require it in
   `AnalyzerReleases.Unshipped.md`, which is also where the documentation tests read the shipped set
   from — so the guide must gain a section for it in **both** languages or the build fails.
3. Put it on the analyzer whose generated-code setting it needs. Use-site diagnostics go on
   `SuppressionUsageAnalyzer`; definition diagnostics on `DiagnosticRuleDefinitionAnalyzer`.
4. Add tests to `DiagnosticCatalog.Analyzers.UnitTests` — including one for anything the fix declines
   to do.
5. Regenerate `DiagnosticCatalog.Self`, because CI compares it and a new id cannot ship without the
   catalogue that publishes it.

## Proving a fix

A `fix` ships with a test **that was seen failing against the unfixed code**. Write the test first, or
write the fix and stash it to watch the test go red — either satisfies it. A test that was never red
cannot tell a fixed bug from one that was never reproduced.

Where a failing test is genuinely impractical — a race, a fix inside a workflow, a defect only
reachable through a third-party service — say so in the pull request and describe how you verified it
instead. Skipping the proof is allowed; skipping it silently is not.

## Where to go next

* [**Repository architecture**](architecture.en.md) — the four independent layers of verification and
  what each reaches.
* [**Inside the generator**](generator-internals.en.md) — what `CatalogGen.UnitTests` is asserting
  about.
* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — the floor, the shell suite, and the commit convention.

---

<div align="center">
<a href="./release-trains.en.md">← Release trains</a> · <a href="./README.en.md">↑ Table of contents</a>
</div>
