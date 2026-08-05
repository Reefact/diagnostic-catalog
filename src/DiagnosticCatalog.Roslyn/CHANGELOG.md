# Changelog

All notable, user-facing changes to the **`roslyn` release train** — the
`DiagnosticCatalog.Roslyn` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following Microsoft.CodeAnalysis.Analyzers' pace never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
The upstream release a given version mirrors is recorded in the package's own metadata by
`[assembly: CatalogSource]` (specification §14.2) — read it from the assembly rather than
inferring it from the number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `Microsoft.CodeAnalysis.Analyzers 5.6.0`** — unchanged upstream.
<!-- mirror:end -->

### Added

* **52 rules** — the `RS1xxx` and `RS2xxx` analyzer-authoring diagnostics — each a static class
  exposing `Id`, `Category` and, where the descriptor declares one, `HelpLinkUri` as compile-time
  constants, so `SuppressMessageAttribute` takes checked references instead of magic strings.
* **9 categories**, declared once each on the internal `RoslynCategory` and reached only through the
  rule that carries them
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)):
  `MicrosoftCodeAnalysisCorrectness` on 20 rules, `MicrosoftCodeAnalysisDesign` on 10,
  `MicrosoftCodeAnalysisReleaseTracking` on 9, `MicrosoftCodeAnalysisPerformance` on 4,
  `Correctness` on 3, `MicrosoftCodeAnalysisCompatibility` on 2,
  `MicrosoftCodeAnalysisDocumentation` on 2, `MicrosoftCodeAnalysisLocalization` on 1 and `Library`
  on 1.
* Every rule carries the title its `DiagnosticDescriptor` declares as its documentation comment
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).

### Notes

* **Two categories break the pattern the other seven follow.** Seven read `MicrosoftCodeAnalysis`
  plus a word. `RS1010`, `RS1011` and `RS1016` are plain `Correctness` while twenty of their
  neighbours are `MicrosoftCodeAnalysisCorrectness` — the same concept, spelled two ways inside one
  package, with nothing to tell them apart. `RS1023` is `Library`, a category with one rule in it.
  Reading the descriptors rather than the documentation is what surfaced this.
* **You run these without asking.** `Microsoft.CodeAnalysis.Analyzers` arrives transitively through
  `Microsoft.CodeAnalysis.CSharp`, so referencing the Roslyn APIs to write an analyzer, a code fix,
  a source generator or an analyzer test brings all 52 along — the same shape as xUnit's and
  MSTest's.
* **13 of the 52 carry a help link.** Seven of those thirteen point at one shared release-tracking
  page, so the distinct destinations are fewer still.
* **`RS00xx` is not here**, and that is a decision nobody has made yet rather than an oversight.
  `Microsoft.CodeAnalysis.PublicApiAnalyzers` (23 rules) and `BannedApiAnalyzers` (3) also issue
  `RS` rules; the ids partition cleanly, but a badge carries the rule prefix (ADR-0032) capped at
  three letters (ADR-0033), so several `RS` catalogues would want the same two letters while
  `PackageIconTests` asserts no two icons match. Merging them into one catalogue has no manifest
  shape either — `package` is a single string, unlike `projects` and `assemblies`.
* **Do not confuse this with `DiagnosticCatalog.Analyzers`**, which ships this library's own `DCAT`
  diagnostics and does the checking. Same word, opposite role.
* **Where `#pragma` is the only answer, no catalogue helps.** Several `RS` rules are reported against
  an assembly or a project file rather than a syntax node — `RS1036`, `RS1038`, `RS2008` — and
  `#pragma warning disable` takes bare identifiers, never a constant. This repository silences three
  of them that way in its own tests.
* Requires `DiagnosticCatalog`, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by Microsoft.
Every value in it is read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
