# Changelog

All notable, user-facing changes to the **`codestyle` release train** — the
`DiagnosticCatalog.CodeStyle` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following Roslyn's pace never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
That independence is not a formality here: the upstream package is versioned with the
compiler rather than with the SDK, so its line moves whenever the compiler does. The
upstream release a given version mirrors is recorded in the package's own metadata by
`[assembly: CatalogSource]` (specification §14.2) — read it from the assembly rather than
inferring it from the number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `Microsoft.CodeAnalysis.CSharp.CodeStyle 5.6.0`** — unchanged upstream.
<!-- mirror:end -->

### Added

* **120 rules** — the `IDExxxx` code-style diagnostics — each a static class exposing
  `Id`, `Category` and, on 117 of them, `HelpLinkUri` as compile-time constants, so
  `SuppressMessageAttribute` takes checked references instead of magic strings. Of those
  117 links, 116 point at Microsoft's style-rule reference and one at the Roslyn issue
  that tracks `EnableGenerateDocumentationFile`.
* **3 categories**, declared once each on the internal `CodeStyleCategory` and reached only
  through the rule that carries them
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)):
  `Style` on 116 rules, `CodeQuality` on three, `Compiler` on one.
* **119 rules carry the title** their `DiagnosticDescriptor` declares, as a documentation
  comment, so hovering a constant says what the rule is about
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).
  `RemoveUnnecessaryImportsFixable` declares no title upstream and falls back to its
  identifier and category — the generator states what it read rather than inventing a
  sentence for it.

### Notes

* **The category is the reason to use this one.** The identifiers are famous and the
  categories are not: `IDE0008` is `"Style"`, `IDE0076` is `"CodeQuality"`, `IDE0043` is
  `"Compiler"`. Nothing in the platform reads a suppression's category, so a wrong value
  produces no symptom anywhere, ever.
* **`IDE0079` is absent, deliberately.** *Remove unnecessary suppression* is declared by an
  analyzer carrying no `[DiagnosticAnalyzer]` attribute — the IDE drives it through a
  separate interface, and no build can raise it. A catalogue lists what a consumer's build
  can report
  ([ADR-0031](../../doc/adr/0031-find-analyzers-the-way-the-compiler-finds-them.en.md)).
* **Three identifiers are not `IDExxxx`** and are kept as declared: `IDE0005_gen`,
  `EnableGenerateDocumentationFile` and `RemoveUnnecessaryImportsFixable`.
* **The upstream package is compiler-versioned.** Installing a release that declares a
  newer compiler than the running one is refused with `CS9057`. That limits which release a
  consumer can install; it does not limit what this catalogue can mirror, since the
  generator reads descriptors and runs no analyzer.
* Requires `DiagnosticCatalog`, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by Microsoft. Every value in
it is read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
