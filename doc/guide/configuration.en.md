# Configuration

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./configuration.fr.md)

For anyone tuning what the analyzers report in their build. Every knob there is — which is fewer than
you might expect, on purpose.

## There is no configuration format of our own

Everything here is standard: Roslyn's `.editorconfig` keys, and **one** MSBuild property. No
`dcat.json`, no attribute you have to apply, and no options the analyzers read of their own. A team
that already knows how to configure `CA1822` already knows how to configure `DCAT0006`.

That is a decision, not an omission. A proprietary format would be one more file to keep in step with
`.editorconfig`, and the first thing it would have to reimplement is path scoping — which
`.editorconfig` already does, and does better.

The one property is `EnableDiagnosticCatalogAnalyzers`, and it exists because `.editorconfig` cannot
answer the question it answers: whether the analyzers are **loaded into a project at all**. That is a
different question from what they report once loaded, and the two are laid out side by side in
[The three levers](#the-three-levers-and-what-each-one-actually-does) further down.

## Severity, per diagnostic

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.DCAT0006.severity = suggestion
dotnet_diagnostic.DCAT0013.severity = error
```

The accepted values are Roslyn's own: `error`, `warning`, `suggestion`, `silent`, `none`, `default`.

| Id | Default severity | What a team usually wants |
| --- | --- | --- |
| `DCAT0001` | **Error** | keep it — the pair names two different rules, so the line is not doing what it looks like |
| `DCAT0002` | **Error** | keep it — the type claims to be a rule and cannot be used as one |
| `DCAT0003` | **Error** | keep it — without `Id` the rule publishes nothing a suppression can name |
| `DCAT0004` | **Error** | keep it — same contract as `DCAT0003`, other half |
| `DCAT0005` | Info | leave it — there is nothing to repair; `warning` only if you want each such name reviewed |
| `DCAT0006` | **Error** | `suggestion` while migrating an existing codebase, then back |
| `DCAT0007` | **Error** | keep it — a half-migrated suppression is a defect, not a backlog item |
| `DCAT0009` | **Error** | keep it — the trimmer discards that suppression outright, so the line does nothing |
| `DCAT0011` | Warning | `error` if you publish a catalogue — one spelling per category is the point |
| `DCAT0012` | Warning | `error` if you publish a catalogue — the repair is mechanical |
| `DCAT0013` | Warning | `error` if you publish a catalogue and want every name to say its rule |
| `DCAT0014` | **Error** | `suggestion` while an existing codebase writes the reasons it never wrote |
| `DCAT0015` | **Error** | keep it — shipping a catalogue that checks nobody is the failure it names |

**The severity says what kind of defect it is, never who reads the message**
([ADR-0040](../adr/0040-grade-every-dcat-diagnostic-by-what-it-says.en.md)). An error means the
mandatory contract is unmet, the suppression is incorrect or has no effect, or the package does not
deliver what it promises. A warning means the code works today and stays liable to drift (`DCAT0011`,
`DCAT0012`) or misleads whoever reads the use site (`DCAT0013`). `DCAT0005` is `Info` alone: a
legitimate exception nobody can repair, reported so the boundary is visible.

The distinction that matters when you pick a line to write: `DCAT0006` and `DCAT0014` report *work not
yet done*, and everything else reports *something already wrong* — except `DCAT0005`, which reports
something that is right and could not have been written otherwise. Those two are the ones that belong
at `suggestion` for a while. They arrive together, on the day a codebase references a catalogue:
`DCAT0006` on every literal suppression it recognises, `DCAT0014` on every suppression that never
carried a reason, literal or not. Delete each line when its backlog is gone.

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

Set to `none`, it silences every `DCAT` diagnostic in that project. The analyzers still load and still
run; nothing they report survives. That is one of three ways to end up unbothered by them, and the
three are not interchangeable — see [The three levers](#the-three-levers-and-what-each-one-actually-does).

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
| `SuppressionUsageAnalyzer` | `DCAT0001`, `DCAT0006`, `DCAT0007`, `DCAT0009`, `DCAT0014` | **not reported** |
| `DiagnosticRuleDefinitionAnalyzer` | `DCAT0002`–`DCAT0005`, `DCAT0011`–`DCAT0013`, `DCAT0015` | **reported** |

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
<PackageReference Include="DiagnosticCatalog.Sonar" Version="1.0.0" />
```

That single line is the whole of it. The `DCAT` analyzers and their code fixes ship inside
`DiagnosticCatalog`, which every catalogue depends on, so there is no second reference to write and
no `PrivateAssets` to get right on it
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.en.md)). **They are not a
transitive NuGet asset**, and that distinction is the whole of the next section: the assemblies sit
where NuGet resolves nothing, and a three-line file the catalogue packs is what turns them on for the
project that referenced it and for nothing further out. The analysis assemblies never become a
runtime dependency of whatever consumes you: `tools/packaging/verify-consumption.sh` restores the
package as a consumer would and asserts that they stay out of the output folder
`DiagnosticCatalog.dll` does reach.

`PrivateAssets="all"` on the **foundation**, from inside a catalogue you publish, is the one to
avoid, and it costs more than it used to. Your consumers cannot resolve `DiagnosticRuleAttribute` in
their own source, so anyone declaring rules of their own gets `CS0246` until they add a dependency
your package already had — and they are unchecked as well, because one package now means one lever.
[Packaging a catalogue](packaging-a-catalogue.en.md) says what a catalogue owes its consumers; the
same script measures both halves of that failure, the second of them as "a catalogue hiding the
foundation withholds the attribute assembly".

**Declining is not a way of being polite** — for a catalogue. `PrivateAssets="all"` used to mean
"checked by nothing"; it means "does not compile", which the reader meets as a broken package rather
than as a choice you made.

## The three levers, and what each one actually does

Three things stop `DCAT` diagnostics from bothering a project. They are three different behaviours,
they are not substitutes, and the commonest configuration mistake is reaching for one when the
question called for another.

| You want | Write | What actually happens |
| --- | --- | --- |
| this rule quieter, or this folder exempt | `dotnet_diagnostic.DCATxxxx.severity` in `.editorconfig` | the analyzers load and run; **that one diagnostic** is reported at the level you named, per path |
| nothing from this library reported here | `dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = none` | the analyzers load and run; **everything they report** is discarded |
| the analyzers not to run in this project at all | `<EnableDiagnosticCatalogAnalyzers>false</EnableDiagnosticCatalogAnalyzers>` | the analyzer assemblies are **not loaded**; the catalogue's constants and `[DiagnosticRule]` still arrive |
| the analyzers where a catalogue is only reached through a library | `<EnableDiagnosticCatalogAnalyzers>true</EnableDiagnosticCatalogAnalyzers>` | the analyzers are loaded in a project the default would have left alone |

**The `.editorconfig` keys govern what is reported. The property governs what is loaded.** The first
two rows differ from each other only in breadth; the third differs in kind. A project setting the
category to `none` still pays for loading and running the analyzers, still sees them in an IDE's
analyzer list, and turns them back on by deleting one line anywhere in its `.editorconfig` chain. A
project setting the property to `false` has no `DCAT` analyzer in the compilation at all.

Which to reach for follows from that. Silencing a category is the right answer while something is
being migrated, or where a rule genuinely does not apply. Declining the load is the right answer when
a project wants a catalogue's constants and has decided, as policy, that this library does not
analyse it — a generated-code project, a vendored tree, a build somebody else owns.

**The default already answers the question a library author would ask.** Since
[ADR-0038](../adr/0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.en.md) the
analyzers reach the project that referenced a catalogue and stop there, so a **library** needs no
lever at all: an application referencing it is not analysed by a catalogue it never chose, and the
library's author writes nothing to get that. What the property buys is the two exceptions to that
default — a direct consumer that declines, and a project further out that asks.

```xml
<PropertyGroup>
  <!-- keep the catalogue, decline the analysis; [DiagnosticRule] still resolves -->
  <EnableDiagnosticCatalogAnalyzers>false</EnableDiagnosticCatalogAnalyzers>
</PropertyGroup>
```

Both directions are measured by `tools/packaging/verify-consumption.sh` — "a direct consumer can opt
OUT" and "a consumer two hops out can opt IN" — and so is the fact that opting out keeps the attribute
assembly. That last one is what makes declining the load a real alternative rather than the broken
package `PrivateAssets="all"` produces.

## What it costs to have the analyzers on

One number worth knowing, because it decides whether the answer is "nothing".

`DCAT0006` needs to know which rules exist, which means sweeping the metadata of every referenced
assembly that could hold one. That index is built **lazily**, on first use. `DCAT0001`, `DCAT0007`,
`DCAT0009` and `DCAT0014` resolve everything from the attribute in front of them and never touch it —
for `DCAT0007` the rule is named by the argument already migrated, which is what makes its correction
the fully deterministic one.

The consequence: **a project whose suppressions are already catalogue references never pays for the
sweep at all.** The cost lands during migration, which is exactly when there is something to find,
and disappears when there is not.

## What is deliberately not configurable

* **Which rules a catalogue describes.** That is generated from the analyzer's own descriptors, and
  editing it by hand is the drift the generation exists to prevent
  ([ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
* **Whether a suppression is *reasonable*.** No severity setting turns these into a judgement about
  whether silencing a rule at that site was a good idea. `DCAT0014` requires that a `Justification` be
  **present**; what it says is never judged, and no key makes it so.
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
