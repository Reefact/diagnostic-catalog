# Versioning a catalogue

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./versioning-a-catalogue.fr.md)

For anyone who has published a catalogue and is about to publish it again. One property of `const`
decides almost everything on this page, and it is not obvious.

## The property everything follows from

A `const` is **not** read from your assembly at run time. The compiler substitutes its value at every
use site, in the *consumer's* compilation.

```mermaid
flowchart LR
    subgraph YOURS["Contoso.Rules 1.0"]
        C["const string Id = \"CTS0001\""]
    end
    subgraph THEIRS["Acme.App, compiled against it"]
        U["[SuppressMessage(..., \"CTS0001\")]<br/><i>the literal, copied in</i>"]
    end
    C -- "at Acme's compile time" --> U
    THEIRS -. "no link back" .-> YOURS
```

A consumer who wrote `ContosoRule.CTS0001.Id` did not record a reference to your assembly. They copied
the string `"CTS0001"` into their own, and nothing connects the two afterwards.

Two consequences, and they pull in opposite directions:

* **Shipping a new value does not reach anyone** until they recompile. A catalogue is not a runtime
  configuration you can correct in place.
* **Deleting a member breaks recompilation** for everyone who used it — and breaks it with a bare
  `CS0117` that names a type and a missing member and explains nothing.

## Never delete a rule

When a vendor retires a rule, the temptation is to drop it from the catalogue. Do not:

```csharp
[DiagnosticRule]
[Obsolete("Retired in Contoso.Analyzers 4.0. No replacement.")]
public static class CTS0001
{
    public const string Id = nameof(CTS0001);
    public const string Category = ContosoCategory.Usage;
}
```

A consumer still referencing it now gets `CS0618` — which **names the rule and says what happened** —
instead of a compile error that sends them looking for a missing namespace or a bad `using`.

That difference is the whole point of the convention
([ADR-0010](../adr/0010-carry-a-retired-rule-forward-as-obsolete.en.md)). The alternative is not "a
cleaner catalogue"; it is an upgrade that fails for a reason nobody can read.

Name the release that dropped it. The consumer's next question is always "when, and is there a
replacement", and the obsolete message is the only place they will look.

## Never rename a member

The same reasoning, and it catches people twice as often because a rename feels safe.

A category constant whose *name* changes breaks every consumer that referenced it, with the same
useless `CS0117`. That includes tidying `ContosoCategory.CodeSmells` into
`ContosoCategory.CodeSmell` — an improvement everywhere except in the one place it is a published
contract.

This repository holds itself to it
([ADR-0012](../adr/0012-a-catalogue-never-renames-a-member-it-published.en.md)), and the case that
forced the decision is worth knowing because it was not a human's mistake: a new category arriving
upstream, whose flattened identifier collided with an existing one and sorted before it, would have
taken that name and pushed the incumbent onto a numbered suffix — renaming a published member,
through an unattended nightly run.

**Pick names you can live with.** They are as public as the rule ids.

## What a version number should say

Ordinary SemVer, with the shape a catalogue actually has:

| Change | Version |
| --- | --- |
| A new rule | **minor** — additive; nobody's code stops compiling |
| A rule retired upstream, carried forward as `[Obsolete]` | **minor** — a warning is not a break |
| A rule's *category* changed upstream | **minor**, and worth a release note — the value your consumers inline changes |
| Removing anything published | **major** |
| Renaming anything published | **major** |
| Regenerating against a new upstream release, nothing moved | **no release** |

The third row is the one that catches people. A recategorisation changes what your consumers compile
into their assemblies and produces no error anywhere — it is exactly the class of silent change this
library exists to surface, so surface it in your notes even though SemVer does not force you to.

The last row is not laziness. The generator compares its own previous output and leaves the file
untouched when nothing moved, `generatedOn` stamp included, so a night where upstream did not move
produces no diff and no release.

## Your version is not the vendor's

A catalogue mirroring `SonarAnalyzer.CSharp 10.31.0` is **not** version `10.31.0`.

It runs on its own line ([ADR-0015](../adr/0015-a-catalogues-version-runs-on-its-own-line.en.md)), for a
reason that becomes obvious the first time you need it: you will publish a fix to the catalogue —
a metadata correction, a packaging change, a title that was dropped — with the upstream release
unchanged. If the numbers are tied, that release has no number available.

Which upstream release a catalogue reflects belongs in the assembly, not in the version:

```csharp
[assembly: CatalogSource(
    source:        "Contoso.Analyzers",
    sourceVersion: "4.2.1",
    generatedOn:   "2026-07-31")]
```

That is also what makes the pair readable from the outside: `dcat list` and `dcat explain` state which
release a catalogue mirrors and when it was generated **before** answering anything, because a
snapshot's age decides whether its answer can be trusted.

In this repository each catalogue rides its own [release train](../../CONTRIBUTING.md), so a Sonar
release never drags the foundation's version along, and vice versa.

## Prereleases, when the vendor is on one

If the analyzer you mirror publishes its real work on a prerelease line, mirror that line rather than
a stale stable tag. StyleCop is the case that settled it here
([ADR-0016](../adr/0016-mirror-stylecops-prerelease-line.en.md)): its stable release is years behind what
everybody actually runs, and a catalogue reflecting it would describe rules its users do not have and
omit the ones they do.

State it in the README rather than leaving it to be discovered. A consumer choosing between two
packages needs to know which one describes the analyzer they run.

## Where to go next

* [**Packaging a catalogue**](packaging-a-catalogue.en.md) — how to reference the foundation, and what
  reaches your consumers whether you meant it or not.
* [**Closing the loop with your own analyzer**](first-party-analyzers.en.md) — if you own both, the
  values can stop being two transcriptions of one string.
* [**Core concepts**](concepts.en.md#provenance-a-catalogue-is-a-snapshot) — what provenance records
  and why the date is a `string`.

---

<div align="center">
<a href="./first-party-analyzers.en.md">← Closing the loop with your own analyzer</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./packaging-a-catalogue.en.md">Packaging a catalogue →</a>
</div>
