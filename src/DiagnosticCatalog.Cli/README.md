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
is read through its own rather than through ours. It falls back to the tool's
Roslyn when there is no graph, or when your package cache does not hold what the
graph asks for — which is what happens for analyzers unpacked from a NuGet
package, since those travel without one.

`--source-name` and `--source-version` are worth passing. A catalogue records
which release it was generated from, and that record is what tells one snapshot
from the next: the file is left untouched when neither it nor any rule moved. An
assembly built out of a working copy carries whatever its project last set —
often unchanged across every rebuild — so a catalogue derived from it alone can
claim an unmoved source while its rules move underneath.

## Generating several at once

A manifest declares any number of catalogues, from either kind of source. Paths
inside it are relative to the manifest, so it works from any directory:

```json
{
  "catalogs": [
    {
      "package": "SonarAnalyzer.CSharp",
      "namespace": "MyCompany.Catalog",
      "container": "SonarRule",
      "output": "../src/MyCompany.Catalog/SonarRules.g.cs"
    },
    {
      "assemblies": ["../src/My.Analyzers/bin/Release/net8.0/My.Analyzers.dll"],
      "sourceName": "My.Analyzers",
      "sourceVersion": "1.4.0",
      "namespace": "My.Catalog",
      "container": "MyRule",
      "output": "../src/My.Catalog/MyRules.g.cs"
    }
  ]
}
```

```bash
dcat generate --manifest eng/catalogs.json --summary "$RUNNER_TEMP/summary.md"
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

This is the question no analyzer can answer for you. `DCAT0001`–`DCAT0007` check that a
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

---

Unofficial with respect to every analyzer vendor it reads; not affiliated with or
endorsed by any of them.
