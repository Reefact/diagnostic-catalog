# Changelog

All notable, user-facing changes to the **`lib` release train** — the
DiagnosticCatalog foundation, its analyzers, and the catalogue of their own
rules — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Each release train keeps its own changelog and versions independently. The rule
that routes a commit to a train — its scope — is in
[CONTRIBUTING.md](CONTRIBUTING.md). The other trains carry theirs next to their
project:

* [`cli`](src/DiagnosticCatalog.Cli/CHANGELOG.md)
* [`sonar`](src/DiagnosticCatalog.Sonar/CHANGELOG.md)
* [`netanalyzers`](src/DiagnosticCatalog.NetAnalyzers/CHANGELOG.md)
* [`stylecop`](src/DiagnosticCatalog.StyleCop/CHANGELOG.md)

## [Unreleased]

### Added

* **`DiagnosticCatalog.Analyzers`** — the checking. Seven diagnostics and seven code fixes: a
  suppression whose category and id come from two different rules (`DCAT0001`), a rule declaration
  that fails the structural contract (`DCAT0002`–`DCAT0004`), string literals a catalogue reference
  would replace (`DCAT0006`), a suppression left half migrated (`DCAT0007`), and an
  `UnconditionalSuppressMessage` the trimmer silently discards (`DCAT0009`). The assemblies are
  build-time only and never reach a consumer's output, which
  [a real restore asserts](tools/packaging/verify-consumption.sh) rather than the package merely
  claiming it.

  The three fixes for a rule *declaration* (§12.4) are each offered only where the repair is already
  written in the code — a class that could carry `static`, a member whose modifiers are wrong but
  whose value is not, an absent `Id` whose type name supplies it. Where the value itself is what is
  missing or wrong, the diagnostic is reported with no fix, and that refusal is asserted case by case
  ([ADR-0018](doc/adr/0018-a-code-fix-never-decides-what-only-the-author-can.en.md)). The one exception
  is the `Category` placeholder §12.4 specifies: `"TODO"` is not blank, so applying it stops
  `DCAT0004` being reported. The diagnostics guide says so where somebody about to press it will
  read it.

* **`DiagnosticCatalog.Self`** — those `DCAT` rules as a catalogue, generated from the analyzers'
  own descriptors by this repository's own generator. It rides this train rather than one of its
  own, because a catalogue describing a different rule set from the analyzer shipped beside it is
  precisely the silent mismatch the library exists to prevent; CI regenerates it on every pull
  request and fails if the committed file has gone stale.

* **Guides** for [consumers](doc/guide/writing-suppressions.en.md), for
  [catalogue authors](doc/guide/authoring-a-catalogue.en.md), and a
  [reference for every `DCAT` diagnostic](doc/guide/diagnostics.en.md) including its `.editorconfig`
  configuration.

### Fixed

* The catalogue generator can no longer rename a category constant it has already
  published. A category arriving upstream that both flattened to an existing
  identifier and sorted before it would have taken that name, pushing the
  incumbent onto a numbered suffix — breaking every consumer that referenced it,
  through an unattended nightly run
  ([ADR-0012](doc/adr/0012-a-catalogue-never-renames-a-member-it-published.en.md)).

  **No shipped assembly changes.** `eng/CatalogGen` declares no `<ReleaseTrain>` of
  its own — it is bundled into the `dcat` tool, which rides the `cli` train
  ([ADR-0017](doc/adr/0017-publish-the-generator-as-a-cli-on-its-own-release-train.en.md))
  — so nothing this train publishes moved. The entry appears here because the
  commit's `core` scope routes it to `lib`. What it protects is the catalogues, not
  this package.

## [0.1.0] - 2026-07-30

The first release of the foundation, and the one the catalogue trains were waiting
for: a catalogue package cannot depend on this one through a `PackageReference`
until a version of it exists (ADR-0007).

### Added

* `[DiagnosticRule]` — marks a static class as one analyzer diagnostic rule,
  exposing that rule's `Id` and `Category` as compile-time constants, so that
  `SuppressMessageAttribute` takes checked references instead of magic strings.
* `[DiagnosticCategory]` — marks the class that declares a catalogue's categories
  once each, which the rules then refer to.
* `[assembly: CatalogSource]` — records which upstream analyzer release a generated
  catalogue mirrors, and when, readable from metadata without the source.

### Notes

* **Attributes only.** Referencing this package declares rules; it performs no
  checking. The analyzers that validate declarations and use sites will ship
  separately as `DiagnosticCatalog.Analyzers`, which does not exist yet.
* Targets `netstandard2.0` and `net10.0`. The floor is exercised on the real .NET
  Framework 4.7.2 CLR in CI, not merely claimed at compile time (ADR-0001).
* `0.1.0` rather than `1.0.0` on purpose: the public API above is small and stable,
  but the surface this foundation is meant to carry is not complete while the
  analyzers are missing. The version says so.
