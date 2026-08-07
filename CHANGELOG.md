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
* [`xunit`](src/DiagnosticCatalog.Xunit/CHANGELOG.md)
* [`nunit`](src/DiagnosticCatalog.NUnit/CHANGELOG.md)
* [`mstest`](src/DiagnosticCatalog.MSTest/CHANGELOG.md)
* [`trimming`](src/DiagnosticCatalog.Trimming/CHANGELOG.md)
* [`aspnetcore`](src/DiagnosticCatalog.AspNetCore/CHANGELOG.md)
* [`syslib`](src/DiagnosticCatalog.Syslib/CHANGELOG.md)
* [`roslyn`](src/DiagnosticCatalog.Roslyn/CHANGELOG.md)
* [`publicapi`](src/DiagnosticCatalog.PublicApi/CHANGELOG.md)
* [`bannedapi`](src/DiagnosticCatalog.BannedApi/CHANGELOG.md)

## [Unreleased]

_Nothing yet._

## [1.0.1] - 2026-08-07

**Use this rather than 1.0.0**, whose packages cannot be loaded. Same foundation, same
source; what changes is that the packages now contain the assemblies they claim.

### Fixed

* **`DiagnosticCatalog 1.0.0` was published around an assembly stamped `0.0.0.0`**, while
  `DiagnosticCatalog.Self 1.0.0`, packed in the same run, was stamped `1.0.0.0` and recorded
  a reference to `DiagnosticCatalog 1.0.0.0`. The two cannot bind, so loading a catalogue
  raised a `FileNotFoundException` naming a version that was published and does not exist.

  The bytes were stale, not misdeclared. `release.yml` builds, then **tests**, then packs
  without rebuilding — and in the middle, `DiagnosticCatalog.Packaging.IntegrationTests`
  packs the foundation from source at `0.0.0-pkgtest` to restore it the way a consumer
  would, rebuilding `src/DiagnosticCatalog` into the shared `bin/Release` on its way past.
  The release then numbered those leftovers. The suite is right to pack from source; nothing
  told the release it was no longer packing its own build.

  `tools/packaging/pack.sh` now compiles what it packs, so no step that ran earlier can
  decide what a release ships. A second check in `Directory.Build.targets` (`DCATPACK001`)
  fails any train project whose package version and assembly version disagree at the source.

## [1.0.0] - 2026-08-07

**Broken — do not use; see 1.0.1.** The packages are numbered correctly and contain an
assembly identity that no consumer can bind to. The release below describes what 1.0.0 was
meant to deliver, and what 1.0.1 delivers.

The whole set: the foundation moves from 0.1.0 to the 1.0 line and now carries the
analyzers that check the contract, and the train ships a catalogue of their own
rules for the first time.

### Added

* **The checking**, now inside the `DiagnosticCatalog` package (ADR-0037). A suppression whose two
  arguments do not name one rule's `Category` and that same rule's `Id` (`DCAT0001`), a rule
  declaration that fails the structural contract (`DCAT0002`–`DCAT0004`), a rule type whose name
  cannot say its identifier (`DCAT0005`) or could and does not (`DCAT0013`), string literals a
  catalogue reference would replace (`DCAT0006`), a suppression left half migrated (`DCAT0007`), an
  `UnconditionalSuppressMessage` the trimmer silently discards (`DCAT0009`), a category that reaches
  no declared constant (`DCAT0011`), an identifier written as a literal where `nameof` would not
  drift (`DCAT0012`), a suppression that never says why it is there — any
  suppression, a literal one included (`DCAT0014`) — and a catalogue package that publishes rules
  and turns no analyzer on for whoever references it (`DCAT0015`). The
  [diagnostics guide](doc/guide/diagnostics.en.md) is the inventory, and is held to the shipped set
  by the documentation tests; a count written here would be a second inventory that nothing checks.
  The assemblies are
  build-time only and never reach a consumer's output, which
  [a real restore asserts](tools/packaging/verify-consumption.sh) rather than the package merely
  claiming it.

  **Nine of the thirteen ship as errors**, and the tier is decided by what the diagnostic says about
  the code rather than by who reads the message
  ([ADR-0040](doc/adr/0040-grade-every-dcat-diagnostic-by-what-it-says.en.md), superseding the
  audience split of [ADR-0027](doc/adr/0027-ship-the-use-site-diagnostics-as-errors.en.md) and the
  provisional warning [ADR-0039](doc/adr/0039-require-a-justification-on-every-suppression.en.md)
  gave `DCAT0014`). An **error** means this library's mandatory contract is unmet, the suppression is
  incorrect or has no effect, or the package does not deliver what it promises: the suppression
  itself (`DCAT0001`, `DCAT0006`, `DCAT0007`), the structural contract a rule declaration must meet
  (`DCAT0002`–`DCAT0004`), a suppression every tool in the chain discards (`DCAT0009`), a missing
  justification (`DCAT0014`), and a catalogue package that checks nobody (`DCAT0015`). A **warning**
  means the code works today and stays liable to drift or misleads its reader — `DCAT0011`,
  `DCAT0012`, `DCAT0013`. `DCAT0005` alone is `Info`: the one rule reporting something its author
  cannot act on. Every severity is overridable per id and per path in `.editorconfig`; the
  [configuration guide](doc/guide/configuration.en.md) gives the two lines that downgrade `DCAT0006`
  and `DCAT0014` while an existing codebase catches up. Both arrive on the first build after the
  reference: `DCAT0006` on every literal suppression a catalogue can match, `DCAT0014` on every
  suppression that never said why it exists — matched or not, migrated or not.

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

### Changed

* **The analyzers ship inside `DiagnosticCatalog` rather than beside it.** The 0.1.0 notes below
  announced them as a package of their own, `DiagnosticCatalog.Analyzers`; that package identity is
  gone, and the assemblies now travel in the foundation's `dcat-analyzers/` folder next to its
  `lib/`. The project, the assembly and the namespace keep their names — only the packaging moved
  ([ADR-0037](doc/adr/0037-ship-the-analyzers-inside-the-foundation-package.en.md)). Nothing breaks
  for anyone: `DiagnosticCatalog.Analyzers` was never published, so no `.csproj` anywhere names it.

  What that buys is the reason for it. Every catalogue package already depends on
  `DiagnosticCatalog` and may not hide it, so referencing **any** catalogue —
  `DiagnosticCatalog.Sonar`, `.Xunit`, any of the thirteen — now delivers the diagnostics and the
  code fixes, with no second reference to write, to review, or to remember when the fourteenth
  catalogue is added. Someone who wants the checking without a catalogue references
  `DiagnosticCatalog` itself. Before this, thirteen catalogues shipped with nothing checking their
  consumers.

  Measured rather than assumed, by
  [`tools/packaging/verify-consumption.sh`](tools/packaging/verify-consumption.sh) on every pull
  request: a catalogue delivers the analyzers to its own consumers, a consumer of two catalogues is
  handed exactly one analyzer instance rather than one per catalogue, and the analyzer stops there.

* **The checks stop at the project that references a catalogue.** An application referencing a
  library that took a catalogue for its own suppressions is no longer analysed by it — it chose
  neither, and `DCAT0006` is an error, so under the arrangement above its build failed on its own
  suppressions with nothing in its own project file to point at. The analyzer assemblies moved out
  of `analyzers/dotnet/cs/`, where NuGet resolves them as an asset and an asset flows down the whole
  graph, into `dcat-analyzers/`, where only the foundation's own
  `buildTransitive/DiagnosticCatalog.targets` reaches them; each catalogue packs a three-line
  `build/<its id>.props` that turns them on, which NuGet imports for a direct reference and for
  nothing further out
  ([ADR-0038](doc/adr/0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.en.md)).

  Consumers of the catalogues published here need change nothing. A **third-party** catalogue must
  now ship that props file to have its consumers checked —
  [Packaging a catalogue](doc/guide/packaging-a-catalogue.en.md#ship-the-opt-in-that-checks-your-consumers)
  has it.

* **`EnableDiagnosticCatalogAnalyzers` turns the analysis on or off from the consuming project.**
  `false` keeps a catalogue and declines its diagnostics, which one package could not previously
  offer — `PrivateAssets="all"` withheld `[DiagnosticRule]` along with them, so the only opt-out was
  silencing the category in `.editorconfig`. `true` asks for the checks from a project that reaches
  a catalogue only through a library. Both directions are measured.

* **`PrivateAssets="all"` on a catalogue's reference to the foundation now withholds
  `[DiagnosticRule]` as well as the analyzers.** One package means one lever, so declining to
  impose analysis is no longer a polite opt-out: a consumer written the ordinary way stops
  compiling rather than merely going unchecked, which is the §7.2 failure
  [the troubleshooting guide](doc/guide/troubleshooting.en.md) already describes. No catalogue in
  this repository is written that way; the check that measures it is in the script named above.

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
