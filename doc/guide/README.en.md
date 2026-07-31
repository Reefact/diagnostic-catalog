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
* [The `DCAT` diagnostics](diagnostics.en.md) — what your users will be told, and when.

## I saw a `DCATxxxx` and want to know what it means

* [**The `DCAT` diagnostics**](diagnostics.en.md) — every id, what triggers it, why it exists, and
  the `.editorconfig` keys that configure it.

## I want the vocabulary

* [**Core concepts**](concepts.en.md) — rule, catalogue, container, category class, provenance; how
  they nest, which package carries which, and exactly what a reference gives you today.

## I want the reasoning, not the instructions

The guides state what to do and say why in a sentence. Where a decision needed an argument, it is
recorded once and linked rather than repeated:

* [**The specification**](../specification.en.md) — the canonical design document: the rule
  contract, the platform behaviour it relies on, the generator, the analyzer diagnostics, packaging.
  Normative, and longer than any guide.
* [**The architecture decision records**](../adr/) — the lasting decisions and why they were taken.
  Start with [ADR-0008](../adr/0008-express-a-rule-as-a-marked-static-class-of-constants.md) (why a
  rule is a marked static class of constants) and
  [ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.md) (why catalogue
  content is read from descriptors and never from documentation).

## I want to see it working rather than read about it

The worked example is [`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self): this
library's own `DCAT` rules, catalogued by this library's own generator, published on the same train
as the analyzers they mirror. It is not a mock-up — it is the product applied to itself, and CI
fails if it ever stops describing the analyzers that ship beside it.

The three vendor catalogues under `src/` are the same machinery at scale — 465, 318 and 193 rules —
mirroring other people's analyzers.

## I am contributing to this repository

* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — building and testing, the .NET Framework floor,
  release trains, the commit convention, and how to add a catalogue.
* [**doc/CONVENTIONS.en.md**](../CONVENTIONS.en.md) — how these documents are laid out and what the
  documentation tests check about them. Read it before adding a page.

## Suggested reading order

Every page in this folder is threaded in one order, and each footer carries the previous and the
next. Following it end to end takes you from a single suppression to publishing a catalogue of your
own:

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
11. [The `DCAT` diagnostics](diagnostics.en.md)

---

<div align="center">
<a href="../../README.md">← Project README</a> · <a href="./getting-started.en.md">Start with Getting started →</a>
</div>
