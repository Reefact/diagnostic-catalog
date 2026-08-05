# Changelog

All notable, user-facing changes to the **`trimming` release train** — the
`DiagnosticCatalog.Trimming` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following the SDK's ILLink releases never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
The upstream release a given version mirrors is recorded in the package's own metadata by
`[assembly: CatalogSource]` (specification §14.2) — read it from the assembly rather than
inferring it from the number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `Microsoft.NET.ILLink.Tasks 10.0.10`** — unchanged upstream.
<!-- mirror:end -->

### Added

* **77 rules** — the `ILxxxx` trimming, Native AOT and single-file diagnostics — each a static class
  exposing `Id` and `Category` as compile-time constants, so `UnconditionalSuppressMessageAttribute`
  takes checked references instead of magic strings.
* **3 categories**, declared once each on the internal `TrimCategory` and reached only through the
  rule that carries them
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)):
  `Trimming` on 64 rules, `AOT` on 7, `SingleFile` on 6.
* Every rule carries the title its `DiagnosticDescriptor` declares as its documentation comment
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)) —
  which matters more here than anywhere else, because there is nowhere else to read it.

### Notes

* **This catalogue inverts the usual argument.** The others exist because nothing reads a
  suppression's category. `UnconditionalSuppressMessageAttribute` **is** read, by two decoders that
  do not agree: the linker requires `IL` followed by four parseable digits and discards anything
  else, while the compile-time analyzer truncates at the first colon and matches exactly
  (specification §9.1). A discarded suppression does not leave a warning behind — it leaves the
  pattern it was covering to be trimmed away, and the symptom is a `TypeLoadException` in
  production.
* **`DCAT0009` shipped before these constants did.** That diagnostic already reports an
  `UnconditionalSuppressMessage` whose identifier is not `IL####`. The check existed and there was
  nothing to feed it.
* **Not one of the 77 rules carries a help link.** The analyzer declares none, so the documentation
  comment on each constant is the only place the rule's own wording is available at the point of
  use. Every other catalogue here has somewhere to click through to; this one does not.
* **You probably have these analyzers already, and did not choose them.** Blazor WebAssembly's SDK
  sets `PublishTrimmed` in its own props, and `Microsoft.NET.Sdk.Analyzers.targets` reads that to
  set `EnableTrimAnalyzer` — so the warnings appear on every build, not at publish. The same holds
  for MAUI, for `PublishAot`, and for any library declaring `IsTrimmable`.
* **Two attributes, one catalogue.** `[SuppressMessage]` silences the compile-time analyzer;
  `[UnconditionalSuppressMessage]` silences the linker, because the former carries
  `[Conditional("CODE_ANALYSIS")]` and is not preserved in the compiled assembly. Both take the
  same constants.
* Requires `DiagnosticCatalog`, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by Microsoft.
Every value in it is read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
