# Documentation map

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./README.fr.md)

For anyone deciding what to read. This page is organised by what you are trying to **do**, not by
how the code is arranged.

## I write `[SuppressMessage(...)]` and want it checked

The common case, and the one that needs no knowledge of anything else here.

1. [**Writing suppressions that the compiler checks**](writing-suppressions.en.md) — reference a
   catalogue, write the suppression against constants, migrate the literals you already have, and
   see what it costs at run time (nothing).

Then, when a diagnostic appears:

* [The `DCAT` diagnostics](diagnostics.en.md) — what each one means, and how to configure its
  severity.

## I ship an analyzer, or own rules nobody else publishes

1. [**Publishing a catalogue**](authoring-a-catalogue.en.md) — the structural contract, the shape to
   actually ship, declaring categories once, packaging, and the versioning rule that will bite you
   if you skip it.
2. [The `DCAT` diagnostics](diagnostics.en.md) — what your users will be told, and when.

## I saw a `DCATxxxx` and want to know what it means

* [**The `DCAT` diagnostics**](diagnostics.en.md) — every id, what triggers it, why it exists, and
  the `.editorconfig` keys that configure it.

## I want the reasoning, not the instructions

The guides above state what to do and say why in a sentence. Where a decision needed an argument, it
is recorded once and linked rather than repeated:

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

Everything in this folder is threaded in one order, and each page's footer carries the previous and
the next. Following it end to end takes you from a single suppression to publishing a catalogue of
your own:

1. [Writing suppressions that the compiler checks](writing-suppressions.en.md)
2. [Publishing a catalogue](authoring-a-catalogue.en.md)
3. [The `DCAT` diagnostics](diagnostics.en.md)

---

<div align="center">
<a href="../../README.md">← Project README</a> · <a href="./writing-suppressions.en.md">Start with Writing suppressions →</a>
</div>
