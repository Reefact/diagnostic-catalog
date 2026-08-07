# Changelog

All notable, user-facing changes to the **`netanalyzers` release train** — the
`DiagnosticCatalog.NetAnalyzers` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following the .NET SDK's analyzer releases never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
The upstream release a given version mirrors is recorded in the package's own
metadata by `[assembly: CatalogSource]`, so the package version does not encode it
(specification §14.2) — read it from the assembly rather than inferring it from the
number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `Microsoft.CodeAnalysis.NetAnalyzers 10.0.302`** — unchanged upstream.
<!-- mirror:end -->

_No other change yet._

## [1.0.0] - 2026-08-07

**Mirrors `Microsoft.CodeAnalysis.NetAnalyzers 10.0.302`** — unchanged upstream: no rule was added, retired or
recategorised, and every identifier, category and constant is the one the previous
version shipped. The generated catalogue carries one deliberate change of its
own, and it is breaking: see below.

### Changed

* **BREAKING — the `NetAnalyzersCategory` container is now `internal`.** A suppression reaches a
  category through the rule that carries it — `NetAnalyzersRule.CA1000.Category`, unchanged and still
  public — so naming `NetAnalyzersCategory.Design` directly no longer compiles. The two
  spellings fold to the same string today and stop agreeing the day Microsoft moves the
  rule: the rule member follows it, a category named on its own does not, and the
  suppression is left asserting a category the rule no longer carries — which nothing
  reports, ever. The repair is one line, and the compiler points at it
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).
* The README now names the other catalogues and the foundation, so a reader landing
  on this package's page from a search learns the set it belongs to. nuget.org renders
  the README embedded in the package, so republishing is the only way an updated one
  reaches that page.
* The version joins the 1.0 line the whole set moves to with this release. Nothing in
  the mirrored the .NET analyzers rules changed with it.

## [0.2.1] - 2026-07-31

**Mirrors `Microsoft.CodeAnalysis.NetAnalyzers 10.0.302`** — unchanged since 0.2.0: no
rule was added, retired or recategorised, and every identifier, category and help link
is the one 0.2.0 shipped. The assembly is unchanged; only the documents around it moved.

### Changed

* The README now states the mirrored upstream release under the title, where someone
  arriving from nuget.org reads it first, instead of a passing mention halfway down a
  code sample. It matters more here than elsewhere: the CA analyzers ship inside the
  .NET SDK, so that release is what a consumer compares against their own SDK rather
  than a package they chose. This version exists to carry that banner onto the package
  page: nuget.org renders the README embedded in the package, so republishing is the
  only way an updated one reaches a reader.

## [0.2.0] - 2026-07-31

**Mirrors `Microsoft.CodeAnalysis.NetAnalyzers 10.0.302`** — unchanged since 0.1.0: no rule was
added, retired or recategorised, and all 318 of them keep the identifier and the
category they shipped with. What moved is what each rule says about itself.

### Added

* Every rule now carries the title its `DiagnosticDescriptor` declares as its
  documentation comment, so hovering a constant says what the rule is about
  instead of restating the identifier under the cursor. These titles are
  resource-backed, so they are read in the invariant culture: the generated file
  does not depend on the machine that produced it
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).

### Changed

* A rule's category is now documented on its `Category` constant rather than on
  the rule itself, and the help link moved from the rule's summary to a `remarks`
  line beside it. No constant moved: this is a documentation change only, and
  every id, category and help link is unchanged.

## [0.1.0] - 2026-07-31

**Mirrors `Microsoft.CodeAnalysis.NetAnalyzers 10.0.302`** — the first release.

### Added

* **318 rules** — the `CAxxxx` diagnostics — each a static class exposing `Id`,
  `Category` and `HelpLinkUri` as compile-time constants, so
  `SuppressMessageAttribute` takes checked references instead of magic strings.
* **10 categories**, declared once each on `NetAnalyzersCategory` and referenced by
  the rules — so a category's spelling exists in exactly one place.

### Notes

* **Every rule carries its help link.** All 318 descriptors populate
  `HelpLinkUri`, so `NetAnalyzersRule.CA1062.HelpLinkUri` resolves for any rule in
  this catalogue. That is a fact about this upstream release, not a promise: the
  member is emitted only where the descriptor supplies it.
* **Ids, categories and help links only.** Rule titles and descriptions are
  Microsoft's authored prose and are deliberately not redistributed
  ([ADR-0011](../../doc/adr/0011-redistribute-rule-facts-only-never-the-vendors-prose.en.md)).
* **Nothing is checked at compile time by this package alone.** It declares; the
  analyzers that validate declarations and use sites ship separately as
  `DiagnosticCatalog.Analyzers`, which does not exist yet.
* Requires `DiagnosticCatalog` 0.1.0, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by Microsoft. Every
value in it is read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
