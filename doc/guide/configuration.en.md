# Configuration

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./configuration.fr.md)

For anyone tuning what the analyzers report in their build. Every knob there is — which is fewer than
you might expect, on purpose.

## There is no configuration format

Everything here is standard Roslyn. No `dcat.json`, no MSBuild property, no attribute you have to
apply, and no options the analyzers read of their own. A team that already knows how to configure
`CA1822` already knows how to configure `DCAT0006`.

That is a decision, not an omission. A proprietary format would be one more file to keep in step with
`.editorconfig`, and the first thing it would have to reimplement is path scoping — which
`.editorconfig` already does, and does better.

## Severity, per diagnostic

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.DCAT0009.severity = error
dotnet_diagnostic.DCAT0006.severity = suggestion
```

The accepted values are Roslyn's own: `error`, `warning`, `suggestion`, `silent`, `none`, `default`.

| Id | Default | What a team usually wants |
| --- | --- | --- |
| `DCAT0001` | **Error** | keep it — the pair names two different rules, so the line is not doing what it looks like |
| `DCAT0002` | Warning | `error` if you publish a catalogue; irrelevant otherwise |
| `DCAT0003` | Warning | `error` if you publish a catalogue |
| `DCAT0004` | Warning | `error` if you publish a catalogue |
| `DCAT0005` | Info | leave it — there is nothing to repair; `warning` only if you want each such name reviewed |
| `DCAT0006` | **Error** | `suggestion` while migrating an existing codebase, then back |
| `DCAT0007` | **Error** | keep it — a half-migrated suppression is a defect, not a backlog item |
| `DCAT0009` | Warning | `error` — the trimmer discards that suppression outright |
| `DCAT0011` | Warning | `error` if you publish a catalogue — one spelling per category is the point |
| `DCAT0012` | Warning | `error` if you publish a catalogue — the repair is mechanical |
| `DCAT0013` | Warning | `error` if you publish a catalogue and want every name to say its rule |

The distinction that matters when you pick: `DCAT0006` reports *work not yet done*, and the others
report *something already wrong* — except `DCAT0005`, which reports something that is right and could
not have been written otherwise. Only the first belongs at `suggestion` for a while — and it is what
a codebase with existing literal suppressions wants on the day it references the package, since the
default turns every one of them into a build error. Delete the line when the last literal is gone.

The three use-site defaults are errors on purpose. A codebase where half the suppressions are magic
strings does not have the guarantee this library exists to provide; it has it where someone
remembered. A warning would leave that to memory.

## Severity, for all of them at once

Every `DCAT` diagnostic is in the category `DiagnosticCatalog`, so Roslyn's category switch reaches
them as a group:

```ini
dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = error
```

Useful as a floor with a per-id exception on top — the per-id key wins:

```ini
dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = error
dotnet_diagnostic.DCAT0006.severity = suggestion
```

The category switch is also the answer for a reader who wants a catalogue's constants and none of its
diagnostics. Since the checking ships inside the package every catalogue depends on, no arrangement
of references delivers one without the other, and `none` on the category is what expresses that
choice.

## Severity, per path

`.editorconfig` sections are ordinary path patterns, and the most specific match wins. This is how a
migration runs project by project without a flag day:

```ini
[*.cs]
dotnet_diagnostic.DCAT0006.severity = suggestion

[src/Billing/**.cs]
dotnet_diagnostic.DCAT0006.severity = error

[src/Legacy.Interop/**.cs]
dotnet_diagnostic.DCAT0006.severity = none
```

[Adopting a catalogue](adopting-a-catalogue.en.md) is the strategy this supports.

## Generated code

**You do not have to configure this, and the default is not uniform.** The checking is written as
two analyzer classes because `ConfigureGeneratedCodeAnalysis` is per-**analyzer** rather than
per-diagnostic, and the two groups need opposite settings:

| Analyzer | Diagnostics | On generated code |
| --- | --- | --- |
| `SuppressionUsageAnalyzer` | `DCAT0001`, `DCAT0006`, `DCAT0007`, `DCAT0009` | **not reported** |
| `DiagnosticRuleDefinitionAnalyzer` | `DCAT0002`–`DCAT0005`, `DCAT0011`–`DCAT0013` | **reported** |

A suppression inside a generated file is not the author's to fix, so reporting it would flood every
generated file with work nobody can do. A *rule declaration* inside a generated file is the opposite
case: the catalogues this repository publishes are generated, and checking them is the main thing
that analyzer exists for.

What Roslyn counts as generated, without you saying anything: a file named `*.g.cs`,
`*.generated.cs`, `TemporaryGeneratedFile_*.cs`, or a type carrying `[GeneratedCode]`. To declare a
file yourself:

```ini
[src/Legacy/Interop.cs]
generated_code = true
```

## Package references

Not `.editorconfig`, but the configuration people get wrong most often.

| Who you are | Reference | How |
| --- | --- | --- |
| You write suppressions | `DiagnosticCatalog.Sonar` (or another catalogue) | ordinary reference — the checks come with it |
| You want the checks and no catalogue | `DiagnosticCatalog` | ordinary reference |
| You want a catalogue and not the analysis | that catalogue | ordinary reference, plus `EnableDiagnosticCatalogAnalyzers=false` |
| You publish a catalogue | `DiagnosticCatalog` | **ordinary reference — never `PrivateAssets="all"`** — plus the opt-in props |
| You publish a library that references a catalogue | that catalogue | nothing; your consumers are not checked by it |

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="0.1.0" />
```

That single line is the whole of it. The `DCAT` analyzers and their code fixes ship inside
`DiagnosticCatalog`, which every catalogue depends on, so there is no second reference to write and
no `PrivateAssets` to get right on it
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.en.md)). The analysis
assemblies still do not become a runtime dependency of whatever consumes you:
`tools/packaging/verify-consumption.sh` restores the package as a consumer would and asserts that
they stay out of the output folder `DiagnosticCatalog.dll` does reach.

`PrivateAssets="all"` on the **foundation**, from inside a catalogue you publish, is the one to
avoid, and it costs more than it used to. Your consumers cannot resolve `DiagnosticRuleAttribute` in
their own source, so anyone declaring rules of their own gets `CS0246` until they add a dependency
your package already had — and they are unchecked as well, because one package now means one lever.
[Packaging a catalogue](packaging-a-catalogue.en.md) says what a catalogue owes its consumers; the
same script measures both halves of that failure, the second of them as "hiding the foundation also
withholds the attribute assembly".

**Declining is not a way of being polite** — for a catalogue. `PrivateAssets="all"` used to mean
"checked by nothing"; it means "does not compile", which the reader meets as a broken package rather
than as a choice you made.

**The property is the lever, and it is the consumer's.** Since
[ADR-0038](../adr/0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.en.md) the
analyzers reach the project that referenced a catalogue and stop there, so a **library** needs no
lever at all: an application referencing it is not analysed by a catalogue it never chose. What
`EnableDiagnosticCatalogAnalyzers` buys is the two exceptions, and it is a plain MSBuild property:

```xml
<PropertyGroup>
  <!-- keep the catalogue, decline the analysis; [DiagnosticRule] still resolves -->
  <EnableDiagnosticCatalogAnalyzers>false</EnableDiagnosticCatalogAnalyzers>
</PropertyGroup>
```

Set it to `true` instead and a project is checked by a catalogue it reaches only through a library.
Both directions are measured — "a direct consumer can opt OUT" and "a consumer two hops out can opt
IN" — and so is the fact that opting out keeps the attribute assembly, which is what makes it a real
alternative to silencing `DCAT0006` in `.editorconfig`.

## What it costs to have the analyzers on

One number worth knowing, because it decides whether the answer is "nothing".

`DCAT0006` needs to know which rules exist, which means sweeping the metadata of every referenced
assembly that could hold one. That index is built **lazily**, on first use. `DCAT0001`, `DCAT0007`
and `DCAT0009` resolve everything from the attribute in front of them and never touch it — for
`DCAT0007` the rule is named by the argument already migrated, which is what makes its correction
the fully deterministic one.

The consequence: **a project whose suppressions are already catalogue references never pays for the
sweep at all.** The cost lands during migration, which is exactly when there is something to find,
and disappears when there is not.

## What is deliberately not configurable

* **Which rules a catalogue describes.** That is generated from the analyzer's own descriptors, and
  editing it by hand is the drift the generation exists to prevent
  ([ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
* **Whether a suppression is *reasonable*.** No severity setting turns these into a judgement about
  whether silencing a rule at that site was a good idea. `Justification` is where that goes.
* **`#pragma warning disable` and `.editorconfig` severity keys.** Not a setting — a limit. Both take
  bare text outside the C# compilation model, so no constant can ever be substituted into either.

## Where to go next

* [**The zero-footprint guarantee**](zero-footprint.en.md) — what any of this costs the assembly you
  ship.
* [**The `DCAT` diagnostics**](diagnostics.en.md) — what each id means before you decide its severity.
* [**Adopting a catalogue**](adopting-a-catalogue.en.md) — the severity ramp these keys are for.

---

<div align="center">
<a href="./writing-suppressions.en.md">← Writing suppressions that the compiler checks</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./zero-footprint.en.md">The zero-footprint guarantee →</a>
</div>
