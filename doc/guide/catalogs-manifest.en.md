# The catalogue manifest

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./catalogs-manifest.fr.md)

For anyone generating more than one catalogue, or generating one more than once. Every key of
`catalogs.json`, and the two lines at the top that are worth more than they look.

## What it is for

A manifest declares any number of catalogues, of any source kind, in one file:

```bash
dcat generate --manifest eng/catalogs.json
dcat validate --manifest eng/catalogs.json
```

The point is not brevity. It is that the list becomes **data the repository owns** rather than
arguments duplicated across a script, a scheduled workflow and somebody's shell history. In this
repository, `eng/catalogs.json` is read by `dcat` and by the nightly workflow, and by
`DocumentedSiblingsTests` — which discovers the catalogues from it, so a catalogue declared there
enters the documentation obligations before it exists.

## The shape

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
    }
  ]
}
```

**Every path inside is relative to the manifest**, not to your working directory. That is what makes
`dcat generate --manifest eng/catalogs.json` behave the same from the repository root, from `eng/`,
and from a CI job that starts somewhere else.

An empty `catalogs` array is refused. Generating nothing and exiting `0` would read to a scheduled job
exactly like a run that succeeded.

## The `$schema` line

Worth the two seconds it costs. It documents every key inside your editor and reports a mistyped one
**where you typed it** — rather than after a package has been downloaded, which is where `dcat`
reports it.

`dcat` names the file, the entry and the key either way:

```text
error: catalogs.json: catalogs[2]: "namespace" is missing.
```

## Every key

Three are required: `namespace`, `container`, `output`. The rest name a source or adjust behaviour.

### Naming a source

| Key | Type | Default | What it names |
| --- | --- | --- | --- |
| `package` | string | — | A package id to resolve from the configured NuGet sources. |
| `version` | string | `latest` | Which release of `package`: an exact version, `latest` (latest **stable**), or `latest-any` (including prereleases). |
| `source` | string | every enabled source | Which configured feed to resolve `package` from, by name or URL. |
| `nupkg` | string | — | A `.nupkg` already on disk. Its `.nuspec` names the source unless `sourceName`/`sourceVersion` say otherwise. |
| `projects` | array of string | — | Projects that produce analyzers. **They must already be built; nothing here builds them.** Several when rules are split across projects, as an analyzer and its code fixes often are. |
| `solution` | string | — | A solution. Reads the projects in it that declare `ProducesDiagnosticRules`; they must already be built. |
| `assemblies` | array of string | — | Analyzer assemblies already on disk. Several when a vendor splits its rules across them. |
| `configuration` | string | `Release` | Which configuration of `projects` or `solution` to read. |
| `language` | string | `cs` | Which language's analyzers to read out of a package. |

The manifest key is **`version`**, where the command line is `--package-version`. On a command line
`--version` already means "which version of the tool"; inside an entry there is no such collision.

### Naming a destination

| Key | Type | What it sets |
| --- | --- | --- |
| `namespace` | string | The namespace the generated catalogue declares. |
| `container` | string | The static class holding the rules. |
| `output` | string | Where the generated C# is written. |

**`container` names two types, not one.** A name ending in `Rule` also names the category class:
`SonarRule` gives `SonarCategory`. That is why the singular matters beyond style — the plural would
produce `SonarRulesCategory`.

### Recording provenance

| Key | Type | What it records |
| --- | --- | --- |
| `sourceName` | string | What to record as the source. Defaults to the package's own id, the project's assembly name, or the first assembly's name. |
| `sourceVersion` | string | What to record as the source's release. Defaults to the package's version, the project's declared `Version`, or the first assembly's. |

**`sourceVersion` is worth setting when a source on disk keeps a version that stands still while its
rules move.** An assembly built out of a working copy carries whatever its project last set, often
unchanged across every rebuild, so a catalogue derived from it alone can claim an unmoved source while
its content changes underneath — and the record that tells one snapshot from the next stops telling
you anything.

### `$comment`, and why it is in the schema

Both the manifest and each entry accept `$comment`, as a string or an array of lines. JSON has no
comment syntax, and a manifest that cannot explain itself accumulates entries nobody dares change:

```json
{
  "$comment": [
    "StyleCop's stable release is years behind what teams actually run,",
    "so this mirrors the prerelease line (ADR-0016)."
  ],
  "package": "StyleCop.Analyzers.Unstable",
  "version": "latest-any"
}
```

## On `language`

Only `cs` can be read today. Constructing a Visual Basic analyzer needs a Roslyn the descriptor worker
does not carry, so a run would refuse **after downloading the package** — which is why the key exists
rather than the tool guessing.

One behaviour worth knowing: selecting a language **excludes the others** rather than keeping only its
folder. Most rules often sit in a language-neutral assembly, and a selection that kept only
`cs/` would silently drop them.

## What `--summary` adds

```bash
dcat generate --manifest eng/catalogs.json --summary "$RUNNER_TEMP/summary.md"
```

A Markdown report of what changed — rules added, recategorised, retitled, retired — across every
entry. It is what turns a scheduled regeneration into a pull request a human can review rather than
merge blind. [Keeping a catalogue current](ci-integration.en.md) is the pattern.

## Where to go next

* [**Keeping a catalogue current**](ci-integration.en.md) — the manifest in a scheduled job.
* [**The `dcat` reference**](dcat-reference.en.md) — the same options on the command line.
* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — how a catalogue is added to this repository, starting
  with its manifest entry.

---

<div align="center">
<a href="./dcat-reference.en.md">← The dcat reference</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./ci-integration.en.md">Keeping a catalogue current →</a>
</div>
