# DiagnosticCatalog.Cli — `dcat`

Generates a [DiagnosticCatalog](https://github.com/Reefact/diagnostic-catalog) rule
catalogue from the analyzers you point it at, so that `SuppressMessageAttribute`
takes compile-checked references instead of magic strings.

```bash
dotnet tool install --global DiagnosticCatalog.Cli
```

## Why it reads assemblies

A catalogue's content is derived from the `DiagnosticDescriptor` instances the
analyzer assemblies actually declare — never from the vendor's published
documentation, and never from rule-metadata files shipped beside them.

That is not a preference. Roslyn never reads a suppression's *category*: it
matches on the id alone. A category that is wrong therefore produces no error, no
warning, no failed suppression and no failing test — not at build time, not at
run time, not ever. When a mistake has no symptom, the only defensible source is
one that cannot be mistaken, and the descriptors are that source because they
*are* what the analyzer reports with. (`ADR-0009`.)

The same reasoning is why the tool refuses rather than guesses: if it cannot
construct an analyzer or load an assembly it was given, it emits nothing and
exits non-zero. A catalogue missing a rule is indistinguishable from one whose
vendor retired it, and would publish that rule as `[Obsolete]` — telling your
users something false about somebody else's product.

## Generating from a package

```bash
dcat generate \
  --package SonarAnalyzer.CSharp --package-version latest \
  --namespace MyCompany.Catalog --container SonarRule \
  --output src/MyCompany.Catalog/SonarRules.g.cs
```

`--package-version` accepts an exact version, `latest` (the latest **stable**
release) or `latest-any` (including prereleases). `--version` is left to mean
what it means everywhere else: which version of `dcat` you are running.

**Your sources, not ours.** `dcat` resolves through NuGet's own client, so it
reads the `NuGet.config` hierarchy exactly as `dotnet restore` does — machine,
user, and every folder up from where you run it — and honours the credentials
configured there, including the encrypted and provider-supplied kinds. A package
on a private feed works with no extra flag. Add `--source <name-or-url>` to pin
one feed when several are configured:

```bash
dcat generate --package Vendor.Analyzers --source maison \
  --namespace My.Catalog --container VendorRule \
  --output src/My.Catalog/VendorRules.g.cs
```

## Generating from a package on disk

A `.nupkg` you built, fetched by hand, or keep on a share — anything that never
came through a feed this tool can reach:

```bash
dcat generate \
  --nupkg packages/Vendor.Analyzers.3.1.4.nupkg \
  --namespace My.Catalog --container VendorRule \
  --output src/My.Catalog/VendorRules.g.cs
```

The package names itself: `dcat` reads the id and version out of its `.nuspec`,
not out of the file name — a renamed file must not quietly rewrite what a
catalogue records as the release it was generated from. Pass `--source-name` or
`--source-version` when you know better, which happens when a package is rebuilt
without its version moving.

## Generating from your own project

Point it at the project instead of at its output, and MSBuild works out where the
assembly is:

```bash
dcat generate --project src/My.Analyzers/My.Analyzers.csproj \
  --namespace My.Catalog --container MyRule \
  --output src/My.Catalog/MyRules.g.cs
```

What this removes from a manifest is the `bin/Release/net8.0/` path — the one part
of a catalogue's declaration that says nothing about the catalogue and breaks when
the project retargets, is renamed, or is built somewhere else. The source is
recorded from what the project declares: its `AssemblyName` and its `Version`, not
the numbers stamped into the assembly, because `AssemblyVersion` is routinely
pinned to a major while the release moves.

**It reads; it does not build.** The project must already be built, and `dcat`
says so — naming the path it looked at and the `dotnet build` that would produce
it — rather than building on your behalf. That is what keeps `dcat validate
--project` safe to run against a working copy: it restores nothing, writes no
`obj/`, and touches no output. `--configuration` picks which build to read and
defaults to `Release`. A multi-targeted project is read through `netstandard2.0`
when it builds one, because that is the build a consumer's compiler actually
loads.

Repeat `--project` when rules are split across projects, as an analyzer and its
code fixes often are.

## Generating from a solution

Point it at the solution, and let each project say whether its rules belong in a
catalogue:

```xml
<PropertyGroup>
  <ProducesDiagnosticRules>true</ProducesDiagnosticRules>
</PropertyGroup>
```

```bash
dcat generate --solution MySolution.slnx \
  --namespace My.Catalog --container MyRule \
  --output src/My.Catalog/MyRules.g.cs
```

**Nothing is inferred, and that is the point.** Which of a solution's projects
produce analyzers cannot be told from the outside. Measured on this tool's own
repository, *references `Microsoft.CodeAnalysis`* matches eight projects of which one
is an analyzer; *declares a `DiagnosticAnalyzer`* matches three, and two of those are
fixtures — one written to fail construction, one in an assembly written not to load
whole. Reading the wrong set is not a nuisance here:
a project missed means its rules are absent, an absent rule is indistinguishable
from a retired one, and they would be published as `[Obsolete]` — telling that
vendor's users something false, with nothing anywhere to report it.

So a project joins by saying so, in its own file. The property is read by MSBuild
*evaluation*, so nothing is restored, nothing is built and no `obj/` is written —
which is what keeps `dcat validate --solution` safe against a working copy. As with
`--project`, the projects must already be built, and `--configuration` picks which
build is read.

A solution where **nobody** declares it is refused rather than read as empty:

```
no project in MySolution.slnx declares <ProducesDiagnosticRules>true</ProducesDiagnosticRules>.
Add it to the projects whose analyzers should be catalogued, or name them with --project.
Reading none of them and emitting nothing would report success for a catalogue that was
never generated.
```

The full reasoning — including the six alternatives that were rejected, and why a
heuristic's accuracy cannot be assessed on your solution — is recorded in
[ADR-0023](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0023-acquire-a-solutions-analyzers-by-declaration.en.md).

## Generating from your own analyzers

Point it at assemblies you have already built. Repeat `--assembly` when a
vendor — or you — split rules across several, as StyleCop splits its between the
analyzer and the code-fix assembly:

```bash
dcat generate \
  --assembly src/My.Analyzers/bin/Release/net8.0/My.Analyzers.dll \
  --source-name My.Analyzers --source-version 1.4.0 \
  --namespace My.Catalog --container MyRule \
  --output src/My.Catalog/MyRules.g.cs
```

If your analyzer was built with the SDK it will have a `.deps.json` beside it.
`dcat` reads it and runs the descriptor worker against **your** dependency
graph, so an analyzer compiled against a different Roslyn than the tool carries
is read through its own rather than through ours.

It falls back to the tool's Roslyn when there is no graph — which is what happens
for analyzers unpacked from a NuGet package, since those travel without one — when
your package cache does not hold what the graph asks for, and **when the graph
names no Roslyn at all**. That last one matters because handing a graph over
*replaces* the worker's own rather than extending it: a graph without Roslyn does
not leave the worker with its own, it leaves it with none. A `netstandard2.0`
library's `.deps.json` is exactly that, listing the assembly and nothing else, and
`dcat` says so rather than reading it:

```
resolved MyLib => 1.0.0 (from 1 assembly/assemblies on disk)
  MyLib.deps.json names no Roslyn — reading through this tool's
```

`--source-name` and `--source-version` are worth passing. A catalogue records
which release it was generated from, and that record is what tells one snapshot
from the next: the file is left untouched when neither it nor any rule moved. An
assembly built out of a working copy carries whatever its project last set —
often unchanged across every rebuild — so a catalogue derived from it alone can
claim an unmoved source while its rules move underneath.

## Generating several at once

A manifest declares any number of catalogues, from any kind of source — `package`,
`nupkg`, `projects`, `solution` or `assemblies`, one per entry. Paths inside it are
relative to the manifest, so it works from any directory:

```json
{
  "$schema": "https://raw.githubusercontent.com/Reefact/diagnostic-catalog/main/eng/catalogs.schema.json",
  "catalogs": [
    {
      "package": "SonarAnalyzer.CSharp",
      "namespace": "MyCompany.Catalog",
      "container": "SonarRule",
      "output": "../src/MyCompany.Catalog/SonarRules.g.cs"
    },
    {
      "projects": ["../src/My.Analyzers/My.Analyzers.csproj"],
      "namespace": "My.Catalog",
      "container": "MyRule",
      "output": "../src/My.Catalog/MyRules.g.cs"
    },
    {
      "solution": "../MySolution.slnx",
      "configuration": "Release",
      "namespace": "House.Catalog",
      "container": "HouseRule",
      "output": "../src/House.Catalog/HouseRules.g.cs"
    }
  ]
}
```

```bash
dcat generate --manifest eng/catalogs.json --summary "$RUNNER_TEMP/summary.md"
```

The `$schema` line is worth the two seconds it costs. It documents every key
inside your editor and reports a mistyped one where you typed it — rather than
after a package has been downloaded, which is where `dcat` reports it. `dcat`
names the file, the entry and the key either way:

```
error: catalogs.json: catalogs[2]: "namespace" is missing.
```

`--summary` writes a Markdown report of what changed — rules added,
recategorised, retitled, retired — which is what makes a scheduled regeneration
open a pull request a human can review rather than merge blind.

## Checking a catalogue is still true

`validate` does everything `generate` does and stops one step short: it acquires the
source, reads its descriptors, computes the catalogue that would be written — and writes
nothing. It answers whether what you have on disk still matches what your source declares.

```bash
dcat validate --manifest eng/catalogs.json
```

| Exit | Meaning |
|---|---|
| `0` | Current. |
| `2` | Out of date — regenerate. |
| `1` | Could not be checked: the source would not resolve. Distinct on purpose, so a feed outage is never reported as a drifted contract. |

This is the question no analyzer can answer for you. The `DCAT` diagnostics check that a
catalogue is well formed and correctly used, at compile time, which is the better place
for those — but none of them can check that it is still *current*, because that needs the
vendor's package and a compiler has no business fetching one. And staleness is the failure
with no symptom: a category that moved upstream still compiles, suppresses nothing, and
says nothing.

## Reading a catalogue

`list` and `explain` read a **compiled** catalogue — the assembly from a package you
reference, not a source file you would have to have generated yourself. Nothing in it is
executed: a catalogue declares everything it publishes as metadata constants, so it is
read reflection-only.

```bash
dcat list  ~/.nuget/packages/diagnosticcatalog.stylecop/0.2.1/lib/netstandard2.0/DiagnosticCatalog.StyleCop.dll
dcat explain <that same path> SA1000
```

```
StyleCop.Analyzers.Unstable 1.2.0.556, generated 2026-07-31

id        SA1000
category  StyleCop.CSharp.SpacingRules
help      https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/SA1000.md

[SuppressMessage(
    StyleCopRule.SA1000.Category,
    StyleCopRule.SA1000.Id,
    Justification = "…")]
```

The snippet is the point: it is the line to copy, fully qualified as you would write it.
Both commands state which upstream release the catalogue mirrors and when it was generated
before answering, because a catalogue is a snapshot and its age decides whether its answer
can be trusted.

## Reproducibility

The same upstream release yields the same bytes: rules and categories are
emitted in ordinal order, titles are read culture-invariantly, and a rule the
vendor retired is carried forward as `[Obsolete]` rather than deleted, because
consumers inline `const` values and removing one breaks their recompilation.

Pin `--date yyyy-MM-dd` when you need two runs of the same inputs to be
byte-identical; left unset it stamps today, which only ever reaches the file when
something else changed too.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | The catalogues were generated. |
| `1` | The run could not finish: an upstream package that would not resolve, an analyzer that could not be constructed, an output that could not be written. |
| `2` | `validate` only: the catalogue no longer matches its source. |
| `64` | The command line is wrong. No retry will fix it. |

## Runtime

`dcat` targets **.NET 8** and rolls forward across majors, so one build runs on
.NET 8 and everything newer.

Descriptors are read in a **separate worker process**, which rolls forward to the
*latest* installed major rather than the first one it finds. That is what stops
the floor that makes `dcat` installable from deciding what it can read: an
analyzer built for a newer target still loads, provided that runtime is present.
It also means an analyzer whose construction crashes takes the worker down and
leaves `dcat` to tell you which one — rather than the whole run vanishing.

What that worker carries also decides which languages can be catalogued, and it
carries **C# Roslyn only**. Reading descriptors means *constructing* each analyzer,
and a Visual Basic analyzer derives from types in
`Microsoft.CodeAnalysis.VisualBasic`, which is not there — so `--language` accepts
`cs` and refuses anything else at the command line, rather than after a package has
been downloaded. That is a settled position rather than a gap awaiting work, and
the reasoning is recorded in
[ADR-0020](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0020-a-catalogue-is-generated-for-c-sharp-only.en.md).

Both processes `dcat` spawns — that worker, and MSBuild for `--project` and
`--solution` — are
given a budget and stopped if they outrun it. Constructing an analyzer is
third-party code and is the one step here that can *hang* rather than fail; a
child that wedges would otherwise take the tool with it, leaving a pipeline to run
until its own timeout with nothing to read. The defaults are 10 minutes for a
descriptor read and 2 minutes for a project evaluation, against measured times of
seconds. Set `DCAT_TIMEOUT_SECONDS` to a positive whole number of seconds to give
both longer.

## Documentation

- [**The `dcat` tool**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/dcat.en.md)
  — the four verbs, which source to point it at, and why it reads descriptors rather than
  documentation.
- [**The `dcat` reference**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/dcat-reference.en.md)
  — every command, option and exit code, checked against the tool's own settings types.
- [**The catalogue manifest**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/catalogs-manifest.en.md)
  — every key of `catalogs.json`.
- [**Keeping a catalogue current**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/ci-integration.en.md)
  — `validate` in a pipeline, the nightly drift pull request, and why exit codes `1` and `2`
  must be handled differently.
- [**Publishing a catalogue**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.en.md)
  — what the generated output has to satisfy, if you are about to ship one.

The [**documentation map**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.en.md)
picks a page by what you are trying to do; every guide exists in English and French.

---

Unofficial with respect to every analyzer vendor it reads; not affiliated with or
endorsed by any of them.
