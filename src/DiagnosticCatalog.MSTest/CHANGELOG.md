# Changelog

All notable, user-facing changes to the **`mstest` release train** — the
`DiagnosticCatalog.MSTest` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following MSTest.Analyzers' pace never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
The upstream release a given version mirrors is recorded in the package's own metadata by
`[assembly: CatalogSource]` (specification §14.2) — read it from the assembly rather than
inferring it from the number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `MSTest.Analyzers 4.3.3`** — unchanged upstream.
<!-- mirror:end -->

### Added

* **62 rules** — the `MSTESTxxxx` diagnostics — each a static class exposing `Id`,
  `Category` and `HelpLinkUri` as compile-time constants, so `SuppressMessageAttribute`
  takes checked references instead of magic strings.
* **3 categories**, declared once each on the internal `MSTestCategory` and reached only
  through the rule that carries them
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)):
  `Usage` on 46 rules, `Design` on 14, `Performance` on 2.
* Every rule carries the title its `DiagnosticDescriptor` declares as its documentation
  comment, so hovering a constant says what the rule is about
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).

### Notes

* **MSTest has no assertion category.** xUnit files an assertion rule under `Assertions` and
  NUnit under `Assertion`; MSTest splits them by the kind of mistake instead — `MSTEST0037`
  *Use proper 'Assert' methods* is `Usage`, `MSTEST0032` *Assertion condition is always true*
  is `Design`. Knowing the framework is therefore not enough to guess the category, and nothing
  in the platform reads a suppression's category, so a wrong one costs no error and no warning,
  ever. That is the case this catalogue exists for.
* **Every one of the 62 rules carries a help link**, into Microsoft Learn. Only the xUnit and
  NUnit catalogues here are as complete.
* **You probably have these analyzers already, and did not choose them.**
  `MSTest.TestFramework` depends on `MSTest.Analyzers`, so the `MSTest` meta-package and
  `MSTest.TestAdapter` both bring them in — the same shape as xUnit's, and unlike NUnit's,
  which arrive through the project template instead.
* **Some of these rules contradict each other, deliberately.** `MSTEST0019` prefers
  `TestInitialize` over constructors and `MSTEST0020` prefers the reverse; `MSTEST0021` and
  `MSTEST0022` do the same for `Dispose` and `TestCleanup`. Whichever style a project picks,
  the other rule of the pair is one it will keep answering for.
* Requires `DiagnosticCatalog`, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by Microsoft.
Every value in it is read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
