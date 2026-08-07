# Changelog

All notable, user-facing changes to the **`aspnetcore` release train** — the
`DiagnosticCatalog.AspNetCore` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following ASP.NET Core's pace never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
The upstream release a given version mirrors is recorded in the package's own metadata by
`[assembly: CatalogSource]` (specification §14.2) — read it from the assembly rather than
inferring it from the number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `Microsoft.AspNetCore.App.Ref 10.0.10`** — unchanged upstream.
<!-- mirror:end -->

_No other change yet._

## [1.0.0] - 2026-08-07

**Mirrors `Microsoft.AspNetCore.App.Ref 10.0.10`.** The first published version of this catalogue.

### Added

* **35 rules** — the `ASPxxxx` and `BLxxxx` diagnostics — each a static class exposing `Id`,
  `Category` and, where the descriptor declares one, `HelpLinkUri` as compile-time constants, so
  `SuppressMessageAttribute` takes checked references instead of magic strings.
* **3 categories**, declared once each on the internal `AspNetCoreCategory` and reached only through
  the rule that carries them
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)):
  `Usage` on 32 rules, `Encapsulation` on 2, `Security` on 1.
* Every rule carries the title its `DiagnosticDescriptor` declares as its documentation comment
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).

### Notes

* **Nobody installed these analyzers, and nobody can uninstall them.** They arrive inside the shared
  framework, and the web SDK references that framework — no `PackageReference` names them. Of every
  catalogue here, this is the one whose rules a project is least able to opt out of.
* **`ASP0026` is the whole `Security` category**, and it is worth knowing on its own: *`[Authorize]`
  overridden by `[AllowAnonymous]` from farther away*. An `[AllowAnonymous]` on a base class or an
  outer scope silently wins over an `[Authorize]` written closer to the endpoint. A suppression of
  that rule is load-bearing, and the argument naming its category is one nothing in the platform
  ever checks.
* **Two prefixes, one package.** `ASPxxxx` is ASP.NET Core proper (26 rules); `BLxxxx` is Blazor
  components (9). They ship together in the framework, so they are catalogued together. The icon
  badge reads `ASP`, the majority prefix
  ([ADR-0032](../../doc/adr/0032-badge-a-catalogues-icon-with-its-rule-prefix.en.md)).
* **The source is a package, not the SDK on the build machine.** `Microsoft.AspNetCore.App.Ref` is
  the targeting pack and it is published on nuget.org — that is how the SDK acquires it. So the
  mirrored release is one a consumer can look up, and the entry needs no special manifest shape.
  The pack also carries source generators, which declare no rule of their own here; reading the
  whole pack yields exactly the rules the two analyzer assemblies do.
* **26 of the 35 rules carry a help link.** The nine without are Blazor's `BLxxxx`, whose
  descriptors declare none.
* Requires `DiagnosticCatalog`, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by Microsoft.
Every value in it is read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
