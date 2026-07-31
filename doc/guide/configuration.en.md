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
dotnet_diagnostic.DCAT0001.severity = error
dotnet_diagnostic.DCAT0006.severity = suggestion
```

The accepted values are Roslyn's own: `error`, `warning`, `suggestion`, `silent`, `none`, `default`.

| Id | Default | What a team usually wants |
| --- | --- | --- |
| `DCAT0001` | Warning | `error` — the pair names two different rules, so the line is not doing what it looks like |
| `DCAT0002` | Warning | `error` if you publish a catalogue; irrelevant otherwise |
| `DCAT0003` | Warning | `error` if you publish a catalogue |
| `DCAT0004` | Warning | `error` if you publish a catalogue |
| `DCAT0006` | Warning | `suggestion` while migrating, `error` once converted |
| `DCAT0007` | Warning | `error` — a half-migrated suppression is a defect, not a backlog item |
| `DCAT0009` | Warning | `error` — the trimmer discards that suppression outright |

The distinction that matters when you pick: `DCAT0006` reports *work not yet done*, and the other six
report *something already wrong*. Only the first belongs at `suggestion` for a while.

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

**You do not have to configure this, and the default is not uniform.** The package ships two analyzer
classes because `ConfigureGeneratedCodeAnalysis` is per-**analyzer** rather than per-diagnostic, and
the two groups need opposite settings:

| Analyzer | Diagnostics | On generated code |
| --- | --- | --- |
| `SuppressionUsageAnalyzer` | `DCAT0001`, `DCAT0006`, `DCAT0007`, `DCAT0009` | **not reported** |
| `DiagnosticRuleDefinitionAnalyzer` | `DCAT0002`, `DCAT0003`, `DCAT0004` | **reported** |

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
| You write suppressions | `DiagnosticCatalog.Sonar` (or another catalogue) | ordinary reference |
| You write suppressions and want the checks | `DiagnosticCatalog.Analyzers` | `PrivateAssets="all"` |
| You publish a catalogue | `DiagnosticCatalog` | **ordinary reference — never `PrivateAssets="all"`** |

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="0.1.0" />
<PackageReference Include="DiagnosticCatalog.Analyzers" Version="0.1.0" PrivateAssets="all" />
```

`PrivateAssets="all"` on the analyzers is right: analysis assemblies must not become runtime
dependencies of whatever consumes you.

`PrivateAssets="all"` on the **foundation**, from inside a catalogue you publish, is the mistake that
matters. Your consumers then cannot resolve `DiagnosticRuleAttribute`, `[DiagnosticRule]` degrades to
an error type, the analyzers find **no rules at all**, and everything looks clean. That is the exact
silent failure this library exists to remove.

If a catalogue references the analyzers, they reach that catalogue's own consumers — measured against
a real restore rather than read from NuGet's documentation, which says the opposite:

| A catalogue referencing `DiagnosticCatalog.Analyzers` with | The analyzers run for its consumers |
| --- | --- |
| no `PrivateAssets` | **yes** |
| `PrivateAssets="none"` | yes |
| `PrivateAssets="all"` | no |

**Silence propagates.** If you would rather not impose analysis downstream, say so explicitly.

## What it costs to have the analyzers on

One number worth knowing, because it decides whether the answer is "nothing".

`DCAT0006` and `DCAT0007` need to know which rules exist, which means sweeping the metadata of every
referenced assembly that could hold one. That index is built **lazily**, on first use. `DCAT0001` and
`DCAT0009` resolve everything from the attribute in front of them and never touch it.

The consequence: **a project whose suppressions are already catalogue references never pays for the
sweep at all.** The cost lands during migration, which is exactly when there is something to find,
and disappears when there is not.

## What is deliberately not configurable

* **Which rules a catalogue describes.** That is generated from the analyzer's own descriptors, and
  editing it by hand is the drift the generation exists to prevent
  ([ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.md)).
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
<a href="./adopting-a-catalogue.en.md">← Adopting a catalogue on an existing codebase</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./zero-footprint.en.md">The zero-footprint guarantee →</a>
</div>
