# The `dcat` reference

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./dcat-reference.fr.md)

For anyone who already knows what the tool does and needs the exact behaviour. Every command, every
option, every exit code. [The tour](dcat.en.md) is the place to start if you do not.

Everything below is checked against the tool's own settings types by
`tests/DiagnosticCatalog.Documentation.UnitTests` — an option documented here that `dcat` does not
declare fails the build, and so does one it declares and this page omits.

## Commands

| Command | Description |
| --- | --- |
| `dcat generate` | Generate a catalogue from a NuGet package, a project, a solution, or analyzer assemblies on disk. |
| `dcat validate` | Check that a catalogue still matches its source, without writing anything. |
| `dcat list <CATALOGUE>` | List the rules a compiled catalogue publishes. |
| `dcat explain <CATALOGUE> <RULE-ID>` | Explain one rule, and print the suppression that references it. |
| `dcat --help` | The command tree. Also `dcat <command> --help`. |
| `dcat --version` | Which version of the tool is installed. |

`list` and `explain` take a path to a **compiled** assembly, not to a source file.

## Naming a source

`generate` and `validate` need exactly one source. Naming more than one is refused rather than
resolved by precedence — a tool that silently picked would make the mistake invisible.

| Option | What it names |
| --- | --- |
| `--package <ID>` | The NuGet package whose analyzers to read. |
| `--package-version <VERSION>` | Which release of `--package`: an exact version, `latest` (latest **stable**), or `latest-any` (including prereleases). |
| `--source <NAME-OR-URL>` | Which configured feed to read `--package` from. Defaults to every enabled source in `NuGet.config`. |
| `--nupkg <PATH>` | A `.nupkg` already on disk. Its `.nuspec` names the source unless you say otherwise. |
| `--project <PATH>` | A project that produces analyzers, **already built**. Repeat to read several together. |
| `--solution <PATH>` | A solution; reads the projects in it that declare `ProducesDiagnosticRules`. **Already built.** |
| `--assembly <PATH>` | An analyzer assembly already on disk. Repeat to read several together. |
| `--configuration <NAME>` | Which configuration of `--project` or `--solution` to read. Defaults to `Release`. |
| `--language <LANG>` | Which language's analyzers to read out of a package. Only `cs` can be read today. |
| `--manifest <PATH>` | Generate every catalogue declared in a manifest. Paths inside it are relative to the manifest. |

**`--package-version`, not `--version`.** On a .NET tool `--version` is universally read as "which
version of the tool am I running", and a switch answering a different question under the name
everybody already knows would be a trap laid for the first user.

**A solution needs a declaration.** `--solution` reads the projects that carry
`<ProducesDiagnosticRules>true</ProducesDiagnosticRules>`, and refuses a solution where none does
rather than emitting an empty catalogue. Why declaration rather than discovery — with the measured
numbers — is in [the tour](dcat.en.md#--solution-and-why-it-needs-a-declaration).

**A multi-targeted project is read through `netstandard2.0`** when it builds one, because that is the
build a consumer's compiler actually loads.

## Naming a destination

| Option | What it sets |
| --- | --- |
| `--namespace <NAMESPACE>` | The namespace the generated catalogue declares. |
| `--container <NAME>` | The name of the static class holding the rules. |
| `--output <PATH>` | Where to write the generated C# source. |

Name the container in the **singular**: the use site reads `SonarRule.S1144` — one rule, named. Your
users pay for that name twice per suppression and cannot shorten it.

## Recording provenance

| Option | What it records |
| --- | --- |
| `--source-name <NAME>` | What to record as the source. Defaults to the package's id, the project's assembly name, or the first assembly's. |
| `--source-version <VERSION>` | What to record as the source's release. Defaults to the package's version, the project's, or the assembly's. |
| `--date <yyyy-MM-dd>` | `generate` only. The generation date to stamp. Pin it to make regenerating the same inputs byte-identical. |

**Pass `--source-name` and `--source-version` when reading `--assembly`.** A catalogue records which
release it was generated from, and that record is what tells one snapshot from the next. An assembly
built out of a working copy carries whatever its project last set — often unchanged across every
rebuild — so a catalogue derived from it alone can claim an unmoved source while its rules move
underneath.

## Reporting

| Option | What it does |
| --- | --- |
| `--summary <PATH>` | Write a Markdown report of what changed — rules added, recategorised, retitled, retired. |

`--summary` is what makes a scheduled regeneration open a pull request a human can review rather than
merge blind. [Keeping a catalogue current](ci-integration.en.md) is the pattern it serves.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | The catalogues were generated, or `validate` found them current. |
| `1` | The run could not finish: an upstream package that would not resolve, an analyzer that could not be constructed, an output that could not be written. |
| `2` | `validate` only: the catalogue no longer matches its source. |
| `64` | The command line is wrong. No retry will fix it. |

`1` and `2` are distinct **on purpose**. A feed outage and a drifted contract need different
responses, and a pipeline that could not tell them apart would either retry a real drift or open a
pull request for a network blip.

`64` is the conventional `EX_USAGE`. Branch on it when a job should fail loudly rather than retry.

## Where packages come from

`dcat` resolves through NuGet's own client, so it reads the `NuGet.config` hierarchy exactly as
`dotnet restore` does — machine, user, and every folder up from where you run it — and honours the
credentials configured there, **including the encrypted and provider-supplied kinds**, which cannot
be read by hand at all ([ADR-0019](../adr/0019-resolve-packages-through-the-users-own-nuget-configuration.en.md)).

A package on a private feed therefore works with no extra flag. `--source` pins one feed when several
are configured.

## How descriptors are read

Descriptors are read in a **separate worker process**, and three properties follow from that.

**It rolls forward to the latest installed major**, rather than the first one it finds. That is what
stops the floor that makes `dcat` installable from deciding what it can read: an analyzer built for a
newer target still loads, provided that runtime is present.

**Your analyzer's own dependency graph is used when it has one.** If the analyzer was built with the
SDK it has a `.deps.json` beside it; `dcat` reads it and runs the worker against **your** graph, so an
analyzer compiled against a different Roslyn than the tool carries is read through its own.

It falls back to the tool's Roslyn when there is no graph — which is what happens for analyzers
unpacked from a NuGet package, since those travel without one — when your package cache does not hold
what the graph asks for, and **when the graph names no Roslyn at all**. That last one matters because
handing a graph over *replaces* the worker's own rather than extending it: a graph without Roslyn does
not leave the worker with its own, it leaves it with none. A `netstandard2.0` library's `.deps.json`
is exactly that, and `dcat` says so rather than reading it:

```text
resolved MyLib => 1.0.0 (from 1 assembly/assemblies on disk)
  MyLib.deps.json names no Roslyn — reading through this tool's
```

**A crash is attributable.** An analyzer whose construction throws takes the worker down and leaves
`dcat` to tell you which one — rather than the whole run vanishing.

## Timeouts

Both processes `dcat` spawns — the descriptor worker, and MSBuild for `--project` and `--solution` —
are given a budget and stopped if they outrun it.

| Step | Default |
| --- | --- |
| A descriptor read | 10 minutes |
| A project evaluation | 2 minutes |

Against measured times of seconds. Constructing an analyzer is third-party code and is the one step
here that can *hang* rather than fail; a child that wedged would otherwise take the tool with it,
leaving a pipeline to run until its own timeout with nothing to read.

Set `DCAT_TIMEOUT_SECONDS` to a positive whole number of seconds to give both longer.

## Reproducibility

The same upstream release yields the same bytes:

* rules and categories are emitted in **ordinal** order;
* titles are read **culture-invariantly**;
* a rule the vendor retired is carried forward as `[Obsolete]` rather than deleted, because consumers
  inline `const` values and removing one breaks their recompilation
  ([ADR-0010](../adr/0010-carry-a-retired-rule-forward-as-obsolete.en.md)).

Pin `--date` when two runs of the same inputs must be byte-identical. Left unset it stamps today,
which only ever reaches the file when something else changed too — so a night where upstream did not
move produces no diff.

## Runtime

`dcat` targets **.NET 8** and rolls forward across majors, so one build runs on .NET 8 and everything
newer.

## Where to go next

* [**The catalogue manifest**](catalogs-manifest.en.md) — every key of `catalogs.json`.
* [**Keeping a catalogue current**](ci-integration.en.md) — these exit codes in a pipeline.
* [**Versioning a catalogue**](versioning-a-catalogue.en.md) — what to do with what `--summary`
  reports.

---

<div align="center">
<a href="./dcat.en.md">← The dcat tool</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./catalogs-manifest.en.md">The catalogue manifest →</a>
</div>
