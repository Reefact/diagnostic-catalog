# Changelog

All notable, user-facing changes to the **`syslib` release train** — the
`DiagnosticCatalog.Syslib` package — are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation and of the other catalogues,
so following the .NET runtime's pace never drags anything else along
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md)).
The upstream release a given version mirrors is recorded in the package's own metadata by
`[assembly: CatalogSource]` (specification §14.2) — read it from the assembly rather than
inferring it from the number below.

## [Unreleased]

<!-- mirror:begin -->
**Mirrors `Microsoft.NETCore.App.Ref 10.0.10`** — unchanged upstream.
<!-- mirror:end -->

### Added

* **13 rules** — the `SYSLIB1xxx` source-generator diagnostics — each a static class exposing `Id`,
  `Category` and `HelpLinkUri` as compile-time constants, so `SuppressMessageAttribute` takes
  checked references instead of magic strings.
* **4 categories**, declared once each on the internal `SyslibCategory` and reached only through the
  rule that carries them
  ([ADR-0026](../../doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)):
  `Usage` on 6 rules, `Interoperability` on 5, `Performance` on 1, `ComInterfaceGenerator` on 1.
* Every rule carries the title its `DiagnosticDescriptor` declares as its documentation comment
  ([ADR-0014](../../doc/adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)),
  and all 13 carry a help link into Microsoft Learn.

### Notes

* **`SYSLIB1090`'s category is `ComInterfaceGenerator`** — the name of the generator assembly that
  declares it, not a concept. Its four closest neighbours use `Interoperability`. Every other
  category across every catalogue here is something a person could arrive at by thinking; this one
  is an implementation detail that reached a published contract, carried by exactly one rule. It is
  the single best reason this small catalogue exists.
* **`SYSLIB0xxx` is not here, and cannot be.** Those are obsoletion warnings the compiler raises
  from `[Obsolete]` on the API itself, not analyzer diagnostics — no descriptor declares them, so
  there is nothing to read and nothing `[SuppressMessage]` could silence.
* **The source is a package, not the SDK on the build machine.** `Microsoft.NETCore.App.Ref` is the
  runtime targeting pack and it is published on nuget.org, which is how the SDK acquires it. The
  mirrored release is therefore one a consumer can look up.
* **The whole pack is read, not a chosen subset.** Six generator assemblies contribute — LibraryImport,
  the COM interface generator and its shared source generation, the JavaScript JSImport generator,
  System.Text.Json's generator and the regex generator — and ten of their types declare a rule.
  Reading all of them means a generator that gains its first rule is caught by the nightly rather
  than waiting for somebody to notice.
* **The smallest catalogue in the family.** Thirteen rules against Sonar's 456. Size was never the
  argument: a category nobody can guess costs the same whether it sits among thirteen rules or four
  hundred.
* Requires `DiagnosticCatalog`, which reaches you transitively.
* Targets `netstandard2.0` and `net10.0`.

### Unofficial

This package is not affiliated with, endorsed by, or supported by Microsoft.
Every value in it is read from the analyzers' own `DiagnosticDescriptor` instances
([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
