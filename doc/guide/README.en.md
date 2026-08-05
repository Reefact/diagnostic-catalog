# Documentation map

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./README.fr.md)

For anyone deciding what to read. This page is organised by what you are trying to **do**, not by
how the code is arranged.

## I am finding out whether this is for me

* [**Getting started**](getting-started.en.md) — ten minutes: reference a catalogue, rewrite one
  suppression, break it on purpose and watch the compiler catch it.
* [**Why magic strings fail**](the-problem.en.md) — the two arguments of a suppression, the two
  different ways they fail, and why nothing in the platform can report the worse one.
* [**When not to use this**](when-not-to-use.en.md) — written to talk you out of it where it should.
* [**The alternatives**](alternatives.en.md) — a constants file you maintain, `GlobalSuppressions`,
  `#pragma`, a grep before each upgrade, doing nothing.

## I write `[SuppressMessage(...)]` and want it checked

The common case, and the one that needs no knowledge of anything else here.

* [**Writing suppressions that the compiler checks**](writing-suppressions.en.md) — reference a
  catalogue, write the suppression against constants, migrate the literals you already have, and see
  what it costs at run time (nothing).
* [**Adopting a catalogue on an existing codebase**](adopting-a-catalogue.en.md) — the severity ramp,
  *Fix all occurrences*, scoping by folder, and what order to convert in.
* [**Configuration**](configuration.en.md) — every severity key, the category-wide switch, generated
  code, and the `PrivateAssets` mistake that silences everything.
* [**The zero-footprint guarantee**](zero-footprint.en.md) — what reaches the assembly you ship, and
  what the test actually asserts.
* [Core concepts](concepts.en.md) — if a word in that guide is unfamiliar.
* [The `DCAT` diagnostics](diagnostics.en.md) — when one of them appears.

## I ship an analyzer, or own rules nobody else publishes

* [**Publishing a catalogue**](authoring-a-catalogue.en.md) — the structural contract, the shape to
  actually ship, declaring categories once, packaging, and the versioning rule that will bite you if
  you skip it.
* [**Closing the loop with your own analyzer**](first-party-analyzers.en.md) — feeding your
  `DiagnosticDescriptor` from your own catalogue, and the member that would force Roslyn on every
  consumer.
* [**Versioning a catalogue**](versioning-a-catalogue.en.md) — never delete a rule, never rename a
  member, and what each change does to the version number.
* [**Packaging a catalogue**](packaging-a-catalogue.en.md) — what to reference, what propagates, and
  what nuget.org does to your README.
* [The `DCAT` diagnostics](diagnostics.en.md) — what your users will be told, and when.

## I generate a catalogue rather than hand-writing one

* [**The `dcat` tool**](dcat.en.md) — the four verbs, which source to point it at, and why it reads
  descriptors rather than documentation. Two diagrams.
* [**The `dcat` reference**](dcat-reference.en.md) — every command, option and exit code, checked
  against the tool's own settings types.
* [**The catalogue manifest**](catalogs-manifest.en.md) — every key of `catalogs.json`.
* [**Keeping a catalogue current**](ci-integration.en.md) — `validate` in a pipeline, the nightly
  drift pull request, and why `1` and `2` must be handled differently. One diagram.

## I saw a `DCATxxxx` and want to know what it means

* [**The `DCAT` diagnostics**](diagnostics.en.md) — every id, what triggers it, why it exists, and
  the `.editorconfig` keys that configure it.

## I need a reference, not a tutorial

* [**The rule contract**](rule-contract.en.md) — the five requirements, how the marker is matched, and
  every syntactic form a use site may take.
* [**Troubleshooting**](troubleshooting.en.md) — symptoms first: nothing is reported, `CS0117`,
  `CS0618`, `DCAT0006` on every file at once. One diagram.
* [**FAQ**](faq.en.md) — the questions that are not symptoms.
* [**Glossary**](glossary.en.md) — every word this documentation uses in a precise sense.

## I want the vocabulary

* [**Core concepts**](concepts.en.md) — rule, catalogue, container, category class, provenance; how
  they nest, which package carries which, and exactly what a reference gives you today.
* [**Glossary**](glossary.en.md) — the same words, defined one by one, including what each is *not*.

## I want the reasoning, not the instructions

The guides state what to do and say why in a sentence. Where a decision needed an argument, it is
recorded once and linked rather than repeated:

* [**The specification**](../specification.en.md) — the canonical design document: the rule
  contract, the platform behaviour it relies on, the generator, the analyzer diagnostics, packaging.
  Normative, and longer than any guide.
* [**The architecture decision records**](../adr/) — the lasting decisions and why they were taken.
  Start with [ADR-0008](../adr/0008-express-a-rule-as-a-marked-static-class-of-constants.en.md) (why a
  rule is a marked static class of constants) and
  [ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md) (why catalogue
  content is read from descriptors and never from documentation).

## I want to see it working rather than read about it

The worked example is [`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self): this
library's own `DCAT` rules, catalogued by this library's own generator, published on the same train
as the analyzers they mirror. It is not a mock-up — it is the product applied to itself, and CI
fails if it ever stops describing the analyzers that ship beside it.

The ten vendor catalogues under `src/` are the same machinery at scale — from 13 rules to 465 —
mirroring other people's analyzers.

## I am contributing to this repository

The four pages below are the internals track: they explain how the repository is put together, and
none of them is needed to *use* any of this.

* [**Repository architecture**](architecture.en.md) — the eight projects, the four splits each forced
  by something, the self-application loop, and where each kind of check lives. One diagram.
* [**Inside the generator**](generator-internals.en.md) — the path a `dcat` run takes, and what each
  step refuses to do. One diagram.
* [**Release trains**](release-trains.en.md) — the twelve lines, how a project joins one, and the
  cross-train rule that follows. One diagram.
* [**The testing strategy**](testing-strategy.en.md) — what each of the seven test projects asserts,
  which run on the .NET Framework CLR, and the suite `dotnet test` cannot reach.

Plus the two documents that are not guides:

* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — building and testing, the .NET Framework floor,
  release trains, the commit convention, and how to add a catalogue.
* [**doc/CONVENTIONS.en.md**](../CONVENTIONS.en.md) — how these documents are laid out and what the
  documentation tests check about them. Read it before adding a page.

## Suggested reading order

Every page in this folder is threaded in one order, and each footer carries the previous and the
next. Following it end to end takes you from a single suppression to publishing a catalogue of your
own, and then into the repository itself:

1. [Getting started](getting-started.en.md)
2. [Why magic strings fail](the-problem.en.md)
3. [Core concepts](concepts.en.md)
4. [When not to use this](when-not-to-use.en.md)
5. [The alternatives](alternatives.en.md)
6. [Writing suppressions that the compiler checks](writing-suppressions.en.md)
7. [Adopting a catalogue on an existing codebase](adopting-a-catalogue.en.md)
8. [Configuration](configuration.en.md)
9. [The zero-footprint guarantee](zero-footprint.en.md)
10. [Publishing a catalogue](authoring-a-catalogue.en.md)
11. [Closing the loop with your own analyzer](first-party-analyzers.en.md)
12. [Versioning a catalogue](versioning-a-catalogue.en.md)
13. [Packaging a catalogue](packaging-a-catalogue.en.md)
14. [The `dcat` tool](dcat.en.md)
15. [The `dcat` reference](dcat-reference.en.md)
16. [The catalogue manifest](catalogs-manifest.en.md)
17. [Keeping a catalogue current](ci-integration.en.md)
18. [The `DCAT` diagnostics](diagnostics.en.md)
19. [The rule contract](rule-contract.en.md)
20. [Troubleshooting](troubleshooting.en.md)
21. [FAQ](faq.en.md)
22. [Glossary](glossary.en.md)

Then, for contributors only:

23. [Repository architecture](architecture.en.md)
24. [Inside the generator](generator-internals.en.md)
25. [Release trains](release-trains.en.md)
26. [The testing strategy](testing-strategy.en.md)

---

<div align="center">
<a href="../../README.md">← Project README</a> · <a href="./getting-started.en.md">Start with Getting started →</a>
</div>
