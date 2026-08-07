# Changelog

All notable, user-facing changes to the **`stylecop` release train** — the
`DiagnosticCatalog.StyleCop` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following StyleCop.Analyzers' pace never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
That independence matters more here than elsewhere: this upstream moves rarely, and
a catalogue tied to its numbering would have had no version available for a fix of
its own. The upstream release a given version mirrors is recorded in the package's
own metadata by `[assembly: CatalogSource]` (specification §14.2) — read it from the
assembly rather than inferring it from the number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `StyleCop.Analyzers.Unstable 1.2.0.556`** — unchanged upstream.
<!-- mirror:end -->

_No other change yet._

## [1.0.0] - 2026-08-07

**Mirrors `StyleCop.Analyzers.Unstable 1.2.0.556`** — unchanged upstream: no rule was added, retired or
recategorised, and every identifier, category and constant is the one the previous
version shipped. The generated catalogue carries one deliberate change of its
own, and it is breaking: see below.

### Changed

* **BREAKING — the `StyleCopCategory` container is now `internal`.** A suppression reaches a
  category through the rule that carries it — `StyleCopRule.SA0001.Category`, unchanged and still
  public — so naming `StyleCopCategory.StyleCopCSharpSpecialRules` directly no longer compiles. The two
  spellings fold to the same string today and stop agreeing the day StyleCop moves the
  rule: the rule member follows it, a category named on its own does not, and the
  suppression is left asserting a category the rule no longer carries — which nothing
  reports, ever. The repair is one line, and the compiler points at it
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).
* The README now names the other catalogues and the foundation, so a reader landing
  on this package's page from a search learns the set it belongs to. nuget.org renders
  the README embedded in the package, so republishing is the only way an updated one
  reaches that page.
* The version joins the 1.0 line the whole set moves to with this release. Nothing in
  the mirrored StyleCop rules changed with it.

## [0.3.0] - 2026-07-31

**Mirrors `StyleCop.Analyzers.Unstable 1.2.0.556`** — the `1.2.0-beta` line, where every
earlier version mirrored `StyleCop.Analyzers 1.1.118`. That stable was published in April
2019 and the project has never left beta since, so the catalogue was describing a release
almost nobody installs
([ADR-0016](../../doc/adr/0016-mirror-stylecops-prerelease-line.en.md)).

If you are on `1.1.118`, stay on **0.2.0**, the last version to mirror it.

### Added

* Four rules the beta line declares and the stable does not: `SA1141` (use tuple
  syntax), `SA1142` (refer to tuple fields by name), `SA1316` (tuple element names
  should use correct casing) and `SA1414` (tuple types in signatures should have
  element names).

### Changed

* **`SA1413` changed category** — `StyleCop.CSharp.ReadabilityRules` becomes
  `StyleCop.CSharp.MaintainabilityRules`. This is the change to read twice: the
  platform never validates a suppression's category, so a wrong one produces no
  error, no warning and no failing test, ever. Anyone running the beta with a
  stable-based catalogue has been passing the wrong string for this rule and had
  no way to know.
* No rule was removed: the stable's 193 are all still here, and the constant of
  every one of them is unchanged.

## [0.2.0] - 2026-07-31

**Mirrors `StyleCop.Analyzers 1.1.118`** — unchanged since 0.1.0: no rule was
added, retired or recategorised, and all 193 of them keep the identifier and the
category they shipped with. What moved is what each rule says about itself.

### Added

* Every rule now carries the title its `DiagnosticDescriptor` declares as its
  documentation comment, so hovering a constant says what the rule is about
  instead of restating the identifier under the cursor
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).

### Changed

* A rule's category is now documented on its `Category` constant rather than on
  the rule itself, and the help link moved from the rule's summary to a `remarks`
  line beside it. No constant moved: this is a documentation change only, and
  every id, category and help link is unchanged.

## [0.1.0] - 2026-07-31

**Mirrors `StyleCop.Analyzers 1.1.118`** — the first release.

### Added

* **193 rules** — the `SAxxxx` diagnostics — each a static class exposing `Id`,
  `Category` and `HelpLinkUri` as compile-time constants, so
  `SuppressMessageAttribute` takes checked references instead of magic strings.
* **8 categories**, declared once each on `StyleCopCategory` and referenced by the
  rules — so a category's spelling exists in exactly one place.

### Notes

* **The categories are the reason to use this one.** StyleCop's are the least
  guessable of any catalogue here — `SA1000` lives in
  `"StyleCop.CSharp.SpacingRules"`, not `"Spacing"` — and since nothing in the
  platform reads a suppression's category, a wrong value produces no symptom
  anywhere. `StyleCopCategory.StyleCopCSharpSpacingRules` is the spelling, once.
* **The common prefix is deliberately kept.** Every category here begins with
  `StyleCop.CSharp.`, and stripping it would read better. It stays because the
  common prefix changes the day upstream adds a category outside it, which would
  rename every existing constant at once and break every consumer that referenced
  one (specification §23.1).
* **Every rule carries its help link.** All 193 descriptors populate `HelpLinkUri`.
* **Ids, categories and help links only.** Rule titles and descriptions are the
  StyleCop.Analyzers project's authored prose and are deliberately not
  redistributed
  ([ADR-0011](../../doc/adr/0011-redistribute-rule-facts-only-never-the-vendors-prose.en.md)).
* **Nothing is checked at compile time by this package alone.** It declares; the
  analyzers that validate declarations and use sites ship separately as
  `DiagnosticCatalog.Analyzers`, which does not exist yet.
* Requires `DiagnosticCatalog` 0.1.0, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by the
StyleCop.Analyzers project. Every value in it is read from the analyzers' own
`DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
