# Changelog

All notable, user-facing changes to the **`bannedapi` release train** — the
`DiagnosticCatalog.BannedApi` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following Microsoft.CodeAnalysis.BannedApiAnalyzers' pace never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
The upstream release a given version mirrors is recorded in the package's own metadata by
`[assembly: CatalogSource]` (specification §14.2) — read it from the assembly rather than
inferring it from the number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `Microsoft.CodeAnalysis.BannedApiAnalyzers 5.6.0`** — unchanged upstream.
<!-- mirror:end -->

### Added

* **3 rules** — `RS0030`, `RS0031` and `RS0035` — each a static class exposing `Id`, `Category` and,
  where the descriptor declares one, `HelpLinkUri` as compile-time constants, so
  `SuppressMessageAttribute` takes checked references instead of magic strings.
* **1 category**, `ApiDesign`, declared once on the internal `BannedApiCategory` and reached only
  through the rule that carries it
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).
* Every rule carries the title its `DiagnosticDescriptor` declares as its documentation comment
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).

### Notes

* **The smallest catalogue here, and the rule count says nothing about the volume.** `RS0030` fires
  once per call site of whatever a `BannedSymbols.txt` bans. Three ids can mean thousands of
  diagnostics and, after the migration, a long tail of in-source suppressions that all name the same
  id — which is exactly where a typo survives review.
* **Two of the three carry a help link.** `RS0035` declares none, so no `HelpLinkUri` constant is
  emitted for it. That is the vendor's descriptor, read rather than assumed.
* **`RS0031` is reported against `BannedSymbols.txt`**, not against code, so no
  `[SuppressMessage]` reaches it. The answer there is to fix the duplicate line.
* **Nobody runs this by accident.** `Microsoft.CodeAnalysis.BannedApiAnalyzers` is an explicit
  `PackageReference`, and it does nothing until somebody writes the file that says what to ban. Like
  `DiagnosticCatalog.PublicApi`, it is here for what a deliberate adoption leaves behind rather than
  for how it arrives.
* **This completes the `RS` family.** `RS00xx` public-API tracking is
  [`DiagnosticCatalog.PublicApi`](https://www.nuget.org/packages/DiagnosticCatalog.PublicApi);
  `RS1xxx` and `RS2xxx` analyzer authoring is
  [`DiagnosticCatalog.Roslyn`](https://www.nuget.org/packages/DiagnosticCatalog.Roslyn). The badges
  `BAN`, `API` and `RS` are what tell the three icons apart.
* **`ApiDesign` is declared by two catalogues.** This one and `DiagnosticCatalog.PublicApi` mirror
  packages that use the same category string. They stay separate constants on separate containers,
  so one vendor recategorising does not move the other.
* Requires `DiagnosticCatalog`, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by Microsoft.
Every value in it is read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
