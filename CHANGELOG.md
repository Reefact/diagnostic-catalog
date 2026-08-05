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
* [`codestyle`](src/DiagnosticCatalog.CodeStyle/CHANGELOG.md)

## [Unreleased]

The first preview of the whole set, prepared and not yet published: the foundation
moves from 0.1.0 to a 1.0 line, and this train ships two packages for the first
time — the analyzers that check the contract, and the catalogue of their own rules.

The `lib` train's last published version is `0.1.0`. Nothing below has a tag, an
`AnalyzerReleases.Shipped.md` entry or a `PublicAPI.Shipped.txt` line, which is why
it sits here rather than under a version heading: an entry dated as released is a
claim a consumer will read as one.

### Added

* **`DiagnosticCatalog.Analyzers`** — the checking. A suppression whose two arguments do not name one
  rule's `Category` and that same rule's `Id` (`DCAT0001`), a rule declaration that fails the
  structural contract (`DCAT0002`–`DCAT0004`), a rule type whose name cannot say its identifier
  (`DCAT0005`) or could and does not (`DCAT0013`), string literals a catalogue reference would
  replace (`DCAT0006`), a suppression left half migrated (`DCAT0007`), an
  `UnconditionalSuppressMessage` the trimmer silently discards (`DCAT0009`), a category that reaches
  no declared constant (`DCAT0011`), and an identifier written as a literal where `nameof` would not
  drift (`DCAT0012`). The
  [diagnostics guide](doc/guide/diagnostics.en.md) is the inventory, and is held to the shipped set
  by the documentation tests; a count written here would be a second inventory that nothing checks.
  The assemblies are
  build-time only and never reach a consumer's output, which
  [a real restore asserts](tools/packaging/verify-consumption.sh) rather than the package merely
  claiming it.

  `DCAT0001`, `DCAT0006` and `DCAT0007` ship as **errors**. They are what a consumer references the
  package for, and a codebase where half the suppressions are magic strings does not have the
  guarantee — it has it where somebody remembered. Those addressed to a catalogue's *author*
  (`DCAT0002`–`DCAT0004`, `DCAT0011`–`DCAT0013`) stay warnings, and so does `DCAT0009`. `DCAT0005`
  alone is `Info`: it is the one rule reporting something its author cannot act on. Every severity is
  overridable per id and per path in `.editorconfig`
  ([ADR-0027](doc/adr/0027-ship-the-use-site-diagnostics-as-errors.en.md)); the
  [configuration guide](doc/guide/configuration.en.md) gives the one line that downgrades `DCAT0006`
  while an existing codebase migrates.

  An identifier or category hoisted into a named constant — the form the guide promotes so a second
  suppression can reuse it — resolves to the rule it was initialised from rather than to its value.
  One hop, and only from a declaring type that is not itself a rule.

  The fixes for a rule *declaration* (§12.4) are each offered only where the repair is already
  written in the code — a class that could carry `static`, a member whose modifiers are wrong but
  whose value is not, an absent `Id` whose type name supplies it. Where the value itself is what is
  missing or wrong, the diagnostic is reported with no fix, and that refusal is asserted case by case
  ([ADR-0018](doc/adr/0018-a-code-fix-never-decides-what-only-the-author-can.en.md)). The one exception
  is the `Category` placeholder §12.4 specifies: `"TODO"` is not blank, so applying it stops
  `DCAT0004` being reported. It is a literal, so `DCAT0011` reports it instead — the marker is not
  silent, and the rule that replaces the warning is the one asking for the category to be declared
  where the catalogue declares its categories. The diagnostics guide says so where somebody about to
  press it will read it.

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
