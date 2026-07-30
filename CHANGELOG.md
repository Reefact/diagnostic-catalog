# Changelog

All notable, user-facing changes to the **`lib` release train** — the
DiagnosticCatalog foundation, its analyzers, its CLI and its test-support
package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Each release train keeps its own changelog and versions independently. The rule
that routes a commit to a train — its scope — is in
[CONTRIBUTING.md](CONTRIBUTING.md). The catalog trains (`sonar`,
`netanalyzers`, `stylecop`) will each carry a `CHANGELOG.md` next to their
project once that project exists.

## [Unreleased]

_Nothing yet._

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
