# Documentation map

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./README.fr.md)

For anyone deciding what to read. The pages are grouped into **tracks**, each a short reading order
of its own for a different reason to be here. A page's previous/next links stay inside its track, so
following one to the end leaves you back at this map rather than partway into somebody else's
chapter.

**Most readers want the first track and nothing else.** The rest are for people publishing a
catalogue, generating one, or working on this repository.

## Using a catalogue

The default track, and the only one you need to reference a catalogue and write suppressions the
compiler checks. Ten minutes end to end is step 2.

<!-- track: using -->

1. [Why magic strings fail](the-problem.en.md) — the two arguments of a suppression, the two
   different ways they fail, and why nothing in the platform can report the worse one.
2. [Getting started](getting-started.en.md) — reference a catalogue, rewrite one suppression, break
   it on purpose and watch the compiler catch it.
3. [Core concepts](concepts.en.md) — rule, catalogue, container, category class, provenance; how
   they nest, which package carries which, and exactly what a reference gives you.
4. [Writing suppressions that the compiler checks](writing-suppressions.en.md) — the full version:
   aliases, the literals you already have, and what this cannot reach.
5. [Configuration](configuration.en.md) — every severity key, the category-wide switch, generated
   code, and the `PrivateAssets` mistake that silences everything.
6. [The zero-footprint guarantee](zero-footprint.en.md) — what reaches the assembly you ship, and
   what the test actually asserts.
7. [When not to use this](when-not-to-use.en.md) — written to talk you out of it where it should.
8. [The alternatives](alternatives.en.md) — a constants file you maintain, `GlobalSuppressions`,
   `#pragma`, a grep before each upgrade, doing nothing.

## Adopting the analyzers on a codebase that already has suppressions

For a migration rather than a first suppression: hundreds of literals, and a way to convert them
that does not mean a week of red builds.

<!-- track: adopting -->

1. [Adopting a catalogue on an existing codebase](adopting-a-catalogue.en.md) — the severity ramp,
   *Fix all occurrences* across a document, a project or the solution, scoping by folder, and what
   order to convert in.
2. [The `DCAT` diagnostics](diagnostics.en.md) — every id you will meet on the way, what triggers
   it, why it exists, and the `.editorconfig` keys that configure it.

## Publishing a catalogue

For an analyzer author, or anyone who owns rules nobody else publishes.

<!-- track: publishing -->

1. [Publishing a catalogue](authoring-a-catalogue.en.md) — the structural contract, the shape to
   actually ship, and declaring categories once.
2. [Closing the loop with your own analyzer](first-party-analyzers.en.md) — feeding your
   `DiagnosticDescriptor` from your own catalogue, and the member that would force Roslyn on every
   consumer.
3. [Versioning a catalogue](versioning-a-catalogue.en.md) — never delete a rule, never rename a
   member, and what each change does to the version number.
4. [Packaging a catalogue](packaging-a-catalogue.en.md) — what to reference, what propagates, and
   what nuget.org does to your README.

## Generating and maintaining a catalogue with `dcat`

For anyone who would rather read an analyzer's descriptors than transcribe them.

<!-- track: generating -->

1. [The `dcat` tool](dcat.en.md) — the four verbs, which source to point it at, and why it reads
   descriptors rather than documentation. Two diagrams.
2. [The `dcat` reference](dcat-reference.en.md) — every command, option and exit code, checked
   against the tool's own settings types.
3. [The catalogue manifest](catalogs-manifest.en.md) — every key of `catalogs.json`.
4. [Keeping a catalogue current](ci-integration.en.md) — `validate` in a pipeline, the nightly
   drift pull request, and why `1` and `2` must be handled differently. One diagram.

## Reference and troubleshooting

For an exact answer, or a symptom.

<!-- track: reference -->

1. [The rule contract](rule-contract.en.md) — the five requirements, how the marker is matched, and
   every syntactic form a use site may take.
2. [Troubleshooting](troubleshooting.en.md) — symptoms first: nothing is reported, `CS0117`,
   `CS0618`, `DCAT0006` on every file at once. One diagram.
3. [FAQ](faq.en.md) — the questions that are not symptoms.
4. [Glossary](glossary.en.md) — every word this documentation uses in a precise sense, including
   what each is *not*.

## Contributing to this repository

The internals track. None of it is needed to *use* any of this.

<!-- track: contributing -->

1. [Repository architecture](architecture.en.md) — the projects, the splits each forced by
   something, the self-application loop, and where each kind of check lives. One diagram.
2. [Inside the generator](generator-internals.en.md) — the path a `dcat` run takes, and what each
   step refuses to do. One diagram.
3. [Release trains](release-trains.en.md) — the fifteen lines, how a project joins one, and the
   cross-train rule that follows. One diagram.
4. [The testing strategy](testing-strategy.en.md) — what each test project asserts, which run on
   the .NET Framework CLR, and the suite `dotnet test` cannot reach.

Plus the two documents that are not guides:

* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — building and testing, the .NET Framework floor,
  release trains, the commit convention, and how to add a catalogue.
* [**doc/CONVENTIONS.en.md**](../CONVENTIONS.en.md) — how these documents are laid out and what the
  documentation tests check about them. Read it before adding a page.

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

The vendor catalogues under `src/` are the same machinery at scale — from 3 rules to 456 — mirroring
other people's analyzers. They are listed in the
[project README](https://github.com/Reefact/diagnostic-catalog#-the-ready-made-catalogues).

---

<div align="center">
<a href="../../README.md">← Project README</a> · <a href="./the-problem.en.md">Start with Why magic strings fail →</a>
</div>
