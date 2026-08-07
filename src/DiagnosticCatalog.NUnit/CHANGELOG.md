# Changelog

All notable, user-facing changes to the **`nunit` release train** — the
`DiagnosticCatalog.NUnit` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following NUnit.Analyzers' pace never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
The upstream release a given version mirrors is recorded in the package's own metadata by
`[assembly: CatalogSource]` (specification §14.2) — read it from the assembly rather than
inferring it from the number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `NUnit.Analyzers 4.14.0`** — unchanged upstream.
<!-- mirror:end -->

_No other change yet._

## [1.0.0] - 2026-08-07

**Mirrors `NUnit.Analyzers 4.14.0`.** The first published version of this catalogue.

### Added

* **99 rules** — the `NUnitxxxx` diagnostics — each a static class exposing `Id`,
  `Category` and `HelpLinkUri` as compile-time constants, so `SuppressMessageAttribute`
  takes checked references instead of magic strings.
* **3 categories**, declared once each on the internal `NUnitCategory` and reached only
  through the rule that carries them
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)):
  `Assertion` on 59 rules, `Structure` on 38, `Style` on 2.
* Every rule carries the title its `DiagnosticDescriptor` declares as its documentation
  comment, so hovering a constant says what the rule is about
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).

### Notes

* **`Assertion`, not `Assertions`.** NUnit files its assertion rules under the singular and
  xUnit under the plural — one letter apart, on two analyzers a solution may well run side
  by side. Nothing in the platform reads a suppression's category, so getting it wrong costs
  no error and no warning, ever. That is the case this catalogue exists for.
* **Every one of the 99 rules carries a help link**, into NUnit's own rule pages. Only the
  xUnit catalogue here is as complete.
* **You probably have these analyzers already, and did not choose them.** `dotnet new nunit`
  writes `NUnit.Analyzers` into the project file beside `NUnit`. Unlike xUnit's, they are not
  a transitive dependency — `NUnit` declares none — they arrive with the template and stay.
* Requires `DiagnosticCatalog`, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by the NUnit project.
Every value in it is read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
