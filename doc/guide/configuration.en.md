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
| `DCAT0014` | Warning | `suggestion` while an existing codebase catches up, then `error` |

The distinction that matters when you pick: `DCAT0006` and `DCAT0014` report *work not yet done*, and
the others report *something already wrong* — except `DCAT0005`, which reports something that is right
and could not have been written otherwise. Those two are the ones that belong at `suggestion` for a
while, and both usually do. They arrive together, on the day a codebase references the package:
`DCAT0006` turns every literal suppression it recognises into a build error, and `DCAT0014` reports
every suppression that never carried a reason, literal or not. Delete each line when its backlog is
gone.

Three of the five use-site defaults are errors on purpose. A codebase where half the suppressions are
magic strings does not have the guarantee this library exists to provide; it has it where someone
remembered. A warning would leave that to memory. The other two say something narrower — a suppression
the trimmer discards (`DCAT0009`), and one that never says why it is there (`DCAT0014`) — and both
report lines that resolve correctly, which is why they arrive quieter and are worth raising once your
codebase is clean.

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
| `SuppressionUsageAnalyzer` | `DCAT0001`, `DCAT0006`, `DCAT0007`, `DCAT0009`, `DCAT0014` | **not reported** |
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
| You write suppressions | `DiagnosticCatalog.Sonar` (or another catalogue) | ordinary reference |
| You write suppressions and want the checks | `DiagnosticCatalog.Analyzers` | `PrivateAssets="all"` |
| You publish a catalogue | `DiagnosticCatalog` | **ordinary reference — never `PrivateAssets="all"`** |

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="0.1.0" />
<PackageReference Include="DiagnosticCatalog.Analyzers" Version="0.1.0" PrivateAssets="all" />
```

`PrivateAssets="all"` on the analyzers is right: analysis assemblies must not become runtime
dependencies of whatever consumes you.

`PrivateAssets="all"` on the **foundation**, from inside a catalogue you publish, is the one to
avoid. Your consumers then cannot resolve `DiagnosticRuleAttribute` in their own source, so anyone
declaring rules of their own gets `CS0246` until they add a dependency your package already had.
The analyzers still find the rules in your catalogue — that is asserted, and
[packaging a catalogue](packaging-a-catalogue.en.md) says what the guarantee rests on — so the
failure is loud rather than silent. Avoid it anyway: it misstates what your package needs.

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
