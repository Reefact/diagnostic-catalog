# Changelog

All notable, user-facing changes to the **`xunit` release train** — the
`DiagnosticCatalog.Xunit` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following xunit.analyzers' pace never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
The upstream release a given version mirrors is recorded in the package's own metadata by
`[assembly: CatalogSource]` (specification §14.2) — read it from the assembly rather than
inferring it from the number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `xunit.analyzers 1.27.0`** — unchanged upstream.
<!-- mirror:end -->

### Added

* **90 rules** — the `xUnitxxxx` diagnostics — each a static class exposing `Id`,
  `Category` and `HelpLinkUri` as compile-time constants, so `SuppressMessageAttribute`
  takes checked references instead of magic strings.
* **3 categories**, declared once each on the internal `XunitCategory` and reached only
  through the rule that carries them
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)):
  `Usage` on 54 rules, `Assertions` on 32, `Extensibility` on 4.
* Every rule carries the title its `DiagnosticDescriptor` declares as its documentation
  comment, so hovering a constant says what the rule is about
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).

### Notes

* **Every one of the 90 rules carries a help link**, into xunit.net's own rule pages. No
  other catalogue here is complete on both counts — Sonar populates none, and the rest have
  gaps.
* **You already have these analyzers.** `xunit` depends on `xunit.analyzers`, so a test
  project runs them without anybody choosing them. That is what makes their rules the ones
  suppressed in source rather than tuned away in `.editorconfig`: a rule you switched on
  gets configured, a rule that arrives with the framework gets an exception where it is
  wrong.
* **The identifiers keep the vendor's casing** — `xUnit2013`, not `XUnit2013`. A catalogue's
  member name is the identifier a suppression carries, so bending it to C# convention would
  make the constant and the string it stands for disagree
  ([ADR-0012](../../doc/adr/0012-a-catalogue-never-renames-a-member-it-published.en.md)).
* Requires `DiagnosticCatalog`, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by the xUnit.net project.
Every value in it is read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
