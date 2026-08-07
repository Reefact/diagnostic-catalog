# Changelog

All notable, user-facing changes to the **`publicapi` release train** — the
`DiagnosticCatalog.PublicApi` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following Microsoft.CodeAnalysis.PublicApiAnalyzers' pace never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
The upstream release a given version mirrors is recorded in the package's own metadata by
`[assembly: CatalogSource]` (specification §14.2) — read it from the assembly rather than
inferring it from the number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0`** — unchanged upstream.
<!-- mirror:end -->

_No other change yet._

## [1.0.0] - 2026-08-07

**Mirrors `Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0`.** The first published version of this catalogue.

### Added

* **23 rules** — the `RS00xx` public-API tracking diagnostics — each a static class exposing `Id`,
  `Category` and `HelpLinkUri` as compile-time constants, so `SuppressMessageAttribute` takes checked
  references instead of magic strings.
* **1 category**, `ApiDesign`, declared once on the internal `PublicApiCategory` and reached only
  through the rule that carries it
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).
* Every rule carries the title its `DiagnosticDescriptor` declares as its documentation comment
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).

### Notes

* **Four pairs of rules share a byte-identical title.** `RS0022`/`RS0061`, `RS0026`/`RS0059`,
  `RS0027`/`RS0060` and `RS0037`/`RS0056`. The first of each pair is about the public surface and the
  second about the internal one — the analyzer tracks both, in `PublicAPI.Shipped.txt` and
  `InternalAPI.Shipped.txt` — and nothing in the title says which. Nothing in the help link says it
  either: both members of every pair point at the same URL. Only the id distinguishes them. Reading
  the descriptors rather than the documentation is what surfaced this.
* **You chose these, unlike every sibling.** `Microsoft.CodeAnalysis.PublicApiAnalyzers` is an
  explicit `PackageReference`; it reaches nobody by accident. What it shares with the others is the
  aftermath: `RS0016` fires once per member missing from the declared surface, so adopting the
  analyzer over an existing library produces hundreds of diagnostics in one build, and the ones a
  team cannot resolve that day become long-lived in-source suppressions.
* **23 of the 23 carry a help link, and there are two distinct destinations.** Nineteen point at the
  analyzer's shared help page; the four optional-parameter rules point at a design document.
* **The whole set comes from one analyzer type** declaring twenty-three descriptors.
* **`RS0030`, `RS0031` and `RS0035` are not here.** Those are `BannedApiAnalyzers`, a separate
  package. The `RS1xxx` and `RS2xxx` analyzer-authoring rules are
  [`DiagnosticCatalog.Roslyn`](https://www.nuget.org/packages/DiagnosticCatalog.Roslyn).
* **Where a project-level report is the only answer, no catalogue helps.** `RS0048` and `RS0058` are
  reported against a project rather than a syntax node, and are answered by adding the API file or by
  an `.editorconfig` entry — neither of which takes a constant.
* Requires `DiagnosticCatalog`, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by Microsoft.
Every value in it is read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
