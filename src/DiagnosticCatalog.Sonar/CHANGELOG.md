# Changelog

All notable, user-facing changes to the **`sonar` release train** — the
`DiagnosticCatalog.Sonar` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following SonarSource's pace never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
The upstream release a given version mirrors is recorded in the package's own
metadata by `[assembly: CatalogSource]`, so the package version does not encode it
(specification §14.2) — read it from the assembly rather than inferring it from the
number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `SonarAnalyzer.CSharp 10.31.0.145097`** — unchanged upstream.
<!-- mirror:end -->

_No other change yet._

## [1.0.0-preview.1] - 2026-08-01

**Mirrors `SonarAnalyzer.CSharp 10.31.0.145097`** — unchanged upstream: no rule was added, retired or
recategorised, and every identifier, category and constant is the one the previous
version shipped. The generated catalogue is byte-identical.

### Changed

* The README now names the other catalogues and the foundation, so a reader landing
  on this package's page from a search learns the set it belongs to. nuget.org renders
  the README embedded in the package, so republishing is the only way an updated one
  reaches that page.
* The version joins the 1.0 line the whole set moves to with this preview. Nothing in
  the mirrored SonarAnalyzer rules changed with it.

## [0.2.1] - 2026-07-31

**Mirrors `SonarAnalyzer.CSharp 10.31.0.145097`** — unchanged since 0.2.0: no rule was
added, retired or recategorised, and every identifier, category and constant is the
one 0.2.0 shipped. The assembly is unchanged; only the documents around it moved.

### Changed

* The README now states the mirrored upstream release under the title, where someone
  arriving from nuget.org reads it first, instead of a passing mention halfway down a
  code sample. This version exists to carry that banner onto the package page:
  nuget.org renders the README embedded in the package, so republishing is the only
  way an updated one reaches a reader.

## [0.2.0] - 2026-07-31

**Mirrors `SonarAnalyzer.CSharp 10.31.0.145097`** — unchanged since 0.1.0: no rule was
added, retired or recategorised, and all 456 of them keep the identifier and the
category they shipped with. What moved is what each rule says about itself.

### Added

* Every rule now carries the title its `DiagnosticDescriptor` declares as its
  documentation comment, so hovering a constant says what the rule is about
  instead of restating the identifier under the cursor. This catalogue gains the
  most from it: SonarAnalyzer.CSharp populates no help link, so until now a rule
  had nothing at all to say for itself
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).

### Changed

* A rule's category is now documented on its `Category` constant rather than on
  the rule itself, where the title has taken its place. No constant moved: this is
  a documentation change only, and every id and category is unchanged.

## [0.1.0] - 2026-07-31

**Mirrors `SonarAnalyzer.CSharp 10.31.0.145097`** — the first release.

### Added

* **456 rules**, each a static class exposing `Id` and `Category` as compile-time
  constants, so `SuppressMessageAttribute` takes checked references instead of
  magic strings.
* **13 categories**, declared once each on `SonarCategory` and referenced by the
  rules — so a category's spelling exists in exactly one place.

### Notes

* **No `HelpLinkUri`.** SonarAnalyzer.CSharp's `DiagnosticDescriptor` instances do
  not populate that field, so no rule in this catalogue exposes one. The member is
  emitted only where the descriptor actually supplies it, and here that is nowhere.
  The other two catalogues in this repository do carry help links; this one cannot
  invent them.
* **Ids and categories only.** Rule titles and descriptions are SonarSource's
  authored prose and are deliberately not redistributed
  ([ADR-0011](../../doc/adr/0011-redistribute-rule-facts-only-never-the-vendors-prose.en.md)).
* **Nothing is checked at compile time by this package alone.** It declares; the
  analyzers that validate declarations and use sites ship separately as
  `DiagnosticCatalog.Analyzers`, which does not exist yet.
* Requires `DiagnosticCatalog` 0.1.0, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by SonarSource.
"Sonar" and "SonarQube" are trademarks of SonarSource S.A. Every value in it is
read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
