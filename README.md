# DiagnosticCatalog

|  |  |
| :-- | :-- |
| **Build** | [![ci](https://github.com/Reefact/diagnostic-catalog/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Reefact/diagnostic-catalog/actions/workflows/ci.yml) |
| **Quality** | [![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=reefact_diagnostic-catalog&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=reefact_diagnostic-catalog) [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=reefact_diagnostic-catalog&metric=coverage)](https://sonarcloud.io/summary/new_code?id=reefact_diagnostic-catalog) |
| **Security** | [![codeql](https://github.com/Reefact/diagnostic-catalog/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/Reefact/diagnostic-catalog/actions/workflows/codeql.yml) [![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/Reefact/diagnostic-catalog/badge)](https://securityscorecards.dev/viewer/?uri=github.com/Reefact/diagnostic-catalog) |
| **Package** | [![NuGet](https://img.shields.io/nuget/vpre/DiagnosticCatalog?logo=nuget)](https://www.nuget.org/packages/DiagnosticCatalog) ![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4) |
| **Project** | [![License](https://img.shields.io/github/license/Reefact/diagnostic-catalog)](LICENSE) [![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-fe5196?logo=conventionalcommits&logoColor=white)](https://www.conventionalcommits.org) |

---

**Stop writing analyzer suppressions as magic 🪄 strings.**

## 🚨 The problem

**Both** arguments of `SuppressMessageAttribute` are magic strings, and nothing validates
either one:

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "...")]
```

They differ only in how they fail.

Get the **id** wrong — a typo, or a rule the vendor later renamed — and the suppression
silently does nothing: the warning simply stays, with nothing pointing at the cause.

Get the **category** wrong and *nothing happens at all, ever*: the .NET platform never
reads that argument, so no compiler, analyzer, test or tool can tell you. And you would
not guess it — `S1144`'s category is `"Major Code Smell"`, not `"Code Smell"` and not
`"Maintainability"`. StyleCop makes the point harder still: `SA1000` lives in
`"StyleCop.CSharp.SpacingRules"`.

## 💡 The approach

Declare each rule once, as a static class of compile-time constants, and reference those
constants everywhere else:

```csharp
// Fails the build instead, the day the rule is renamed or retired.
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "...")]
```

A renamed rule becomes a build error. A rule the vendor retires is kept and marked
`[Obsolete]`, so an upgrade tells you to drop the suppression instead of breaking the
build outright. And the category has exactly one published source of truth, read from the
analyzer's own `DiagnosticDescriptor` rather than retyped from memory.

## 📦 What is in the box

| Package | What it gives you |
| --- | --- |
| **`DiagnosticCatalog`** | The `[DiagnosticRule]` and `[assembly: CatalogSource]` markers. This is what you reference to declare a catalogue **of your own** — for your analyzers, or for an internal ruleset. |
| **`DiagnosticCatalog.Sonar`** | The [SonarAnalyzer.CSharp](https://github.com/SonarSource/sonar-dotnet) rules, ids and categories read from the analyzers' own descriptors. |
| **`DiagnosticCatalog.NetAnalyzers`** | The .NET code analysis (`CAxxxx`) rules, same treatment. |
| **`DiagnosticCatalog.StyleCop`** | The [StyleCop.Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) (`SAxxxx`) rules, same treatment. |

The three vendor catalogues are **generated**, never hand-written: `eng/CatalogGen` reads
the `DiagnosticDescriptor` instances the upstream analyzers actually declare, and a
[nightly job](.github/workflows/nightly-catalogs.yml) opens a pull request when an upstream
release moves an id or a category. Only facts are redistributed — ids, categories, help
links. Rule titles and descriptions are the vendors' authored prose and are deliberately
left out ([ADR-0011](doc/adr/0011-redistribute-rule-facts-only-never-the-vendors-prose.md)).

They are unofficial, and affiliated with none of those projects.

## 🚧 Project status

**Nothing is published on nuget.org yet.** The install snippets below describe the shape
of the thing, not something you can restore today.

The foundation ships first, on its own: the catalogue packages carry a project reference
to it, and turning that into a package reference requires a published version to point at
([ADR-0007](doc/adr/0007-depend-across-trains-through-published-packages.md)). Until that
first release, the catalogues build and are tested, but publish nothing.

| | Status |
| --- | --- |
| `DiagnosticCatalog` | Built and tested; **first to ship**, on the `lib` train. |
| `DiagnosticCatalog.Sonar` / `.NetAnalyzers` / `.StyleCop` | Built and tested; unblocked by the first `lib` release. |
| `DiagnosticCatalog.Analyzers` | **Specified, not built yet** — the diagnostics that validate declarations and use sites. |

Referencing the foundation alone declares rules; it performs **no checking**. That part is
the analyzer package, and it does not exist yet.

## 🏁 Getting started

Add the foundation:

```xml
<PackageReference Include="DiagnosticCatalog" Version="0.1.0" />
```

Declare a rule as a static, non-generic class marked `[DiagnosticRule]`, with two
mandatory public constants:

```csharp
using DiagnosticCatalog;

namespace JustDummies.Analyzers.Suppressions;

public static class Dummies
{
    [DiagnosticRule]
    public static class JD0007
    {
        public const string Id = nameof(JD0007);
        public const string Category = "Usage";
    }
}
```

Then reference it at the use site:

```csharp
[SuppressMessage(
    Dummies.JD0007.Category,
    Dummies.JD0007.Id,
    Justification = "This member is instantiated by the test infrastructure.")]
public sealed class DummyFactory
{
}
```

Both members must be `const`: a property, a `static readonly` field or a `record` cannot
be an attribute argument. That is also why the contract is structural rather than an
interface or a base class — see
[ADR-0008](doc/adr/0008-express-a-rule-as-a-marked-static-class-of-constants.md).

Per-package guides:
[`DiagnosticCatalog`](src/DiagnosticCatalog/README.md) ·
[`.Sonar`](src/DiagnosticCatalog.Sonar/README.md) ·
[`.NetAnalyzers`](src/DiagnosticCatalog.NetAnalyzers/README.md) ·
[`.StyleCop`](src/DiagnosticCatalog.StyleCop/README.md)

## 🎯 When it is a good fit

Reach for this when suppressions are load-bearing rather than incidental:

- a codebase that suppresses analyzer rules routinely, and wants the suppressions to break
  when a rule moves;
- an analyzer author who wants their own rules referenced symbolically by consumers;
- a team standardising on Sonar, the .NET CA rules or StyleCop across several
  repositories;
- an upgrade path where an analyzer package bump must surface renamed and retired rules
  instead of silently voiding suppressions.

A handful of suppressions in one project does not need any of this.

## 🛠️ Supported platforms

The libraries target **`netstandard2.0`** and **`net10.0`**. The `netstandard2.0` floor is
not a compile-time claim only: CI runs the test suite on the real .NET Framework 4.7.2
CLR ([ADR-0001](doc/adr/0001-floor-the-libraries-on-net-framework-4-7-2.md)).

Applying `[DiagnosticRule]` introduces no runtime behaviour. The runtime materialises
custom attributes lazily, so `DiagnosticCatalog.dll` is never actually loaded unless
something reflects over the rule types.

## 🔍 Supply chain

Releases publish through [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
with signed build provenance and an embedded SBOM
([ADR-0006](doc/adr/0006-publish-through-trusted-publishing-with-provenance-and-an-sbom.md)).
Packages are versioned in independent [release trains](CONTRIBUTING.md), so a Sonar
release does not move the foundation's version. Verification details are in
[SECURITY.md](SECURITY.md).

## 📚 Documentation

- **[Specification](doc/specification.en.md)** — the full design: the rule contract, the
  generator, the analyzer diagnostics, packaging, and the platform behaviour it all relies
  on. A courtesy French translation is kept at
  [`specification.fr.md`](doc/specification.fr.md); the English version is canonical.
- **[Architecture decisions](doc/adr/)** — the lasting decisions and why they were taken.
- **[CONTRIBUTING.md](CONTRIBUTING.md)** — commit convention, release trains, the .NET
  Framework floor, and how to add a catalogue.
- **[CHANGELOG.md](CHANGELOG.md)** — user-facing changes to the `lib` train.

## 🐛 Feedback and contributing

Found a bug, or want a catalogue that is not here yet? Open an issue on the
[issue tracker](https://github.com/Reefact/diagnostic-catalog/issues). Contributions are
welcome — start with [CONTRIBUTING.md](CONTRIBUTING.md).

For security vulnerabilities, follow the private process in [SECURITY.md](SECURITY.md).

## 📄 License

[Apache-2.0](LICENSE)
