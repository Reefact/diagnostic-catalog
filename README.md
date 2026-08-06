<p align="center">
  <img src="icon.png" width="128" alt="">
</p>

# DiagnosticCatalog

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](doc/README.fr.md)

<!-- dcat-doc:missing SonarRule.S1145 the reference the reader is asked to break on purpose; the compile error is the point -->

|  |  |
| :-- | :-- |
| **Build** | [![ci](https://github.com/Reefact/diagnostic-catalog/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Reefact/diagnostic-catalog/actions/workflows/ci.yml) |
| **Quality** | [![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=reefact_diagnostic-catalog&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=reefact_diagnostic-catalog) [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=reefact_diagnostic-catalog&metric=coverage)](https://sonarcloud.io/summary/new_code?id=reefact_diagnostic-catalog) |
| **Security** | [![codeql](https://github.com/Reefact/diagnostic-catalog/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/Reefact/diagnostic-catalog/actions/workflows/codeql.yml) [![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/Reefact/diagnostic-catalog/badge)](https://securityscorecards.dev/viewer/?uri=github.com/Reefact/diagnostic-catalog) |
| **Package** | [![NuGet](https://img.shields.io/nuget/vpre/DiagnosticCatalog?logo=nuget)](https://www.nuget.org/packages/DiagnosticCatalog) ![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4) |
| **Project** | [![License](https://img.shields.io/github/license/Reefact/diagnostic-catalog)](LICENSE) [![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-fe5196?logo=conventionalcommits&logoColor=white)](https://www.conventionalcommits.org) |

---

**Analyzer suppressions written as constants the compiler checks, with a reason that is not optional.**

## 🚨 The problem

`[SuppressMessage("Major Code Smell", "S1144")]` is two magic strings and an optional reason, and
the .NET platform validates none of the three.

Get the **identifier** wrong and the suppression matches nothing: the warning comes back, or does
not, depending on whether the code that raised it still exists. Get the **category** wrong and
nothing happens at all, ever — no compiler, analyzer, test or tool reads that argument, so none of
them is in a position to tell you. And you would not guess it: `S1144` is a `Major Code Smell`, not
a `Code Smell`; `SA1000` lives in `StyleCop.CSharp.SpacingRules`.

Leave the **justification** out and the decision is gone for good. The warning is silenced, so
there is nothing left to re-examine, and the reason it was acceptable lived only in the head of
whoever wrote the line.

## 💡 The approach

Declare each rule once, as a static class of compile-time constants, and reference those constants
everywhere else. A mistyped reference stops the build, where a mistyped string compiles happily
into a suppression that does nothing. A rule the vendor retires is kept and marked `[Obsolete]`, so
an upgrade warns you rather than breaking recompilation. And the category has exactly one published
source of truth, read from the analyzer's own `DiagnosticDescriptor` rather than retyped from
memory.

The reason stops being optional at the same time: `DCAT0014` requires that a `Justification` be
**present**. What it says is never judged — that is a human question, and a tool scoring prose
would be wrong in both directions.

## 🔁 Before and after

<!-- dcat-doc:missing-justification the "before" half is the incorrect form this page contrasts; the "after" half carries the reason -->

```csharp
// Before — two strings nothing validates, and a reason nothing asks for.
[SuppressMessage("Major Code Smell", "S1144")]
private ReportSerializer() { }

// After — two constants the compiler resolves, and a reason the build requires.
[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Invoked by the serializer through reflection.")]
private ReportSerializer() { }
```

Break the reference on purpose — write `SonarRule.S1145` — and the build stops with `CS0117`, where
the string it replaced would have compiled into a suppression that quietly did nothing.

## 🏁 Install it

One reference, to the catalogue matching an analyzer you already run:

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="1.0.0" />
```

[Getting started](doc/guide/getting-started.en.md) is the ten-minute version of the whole thing,
with the reference broken on purpose so you see the difference in two builds.

## ✅ What that one reference gives you

Referencing a catalogue automatically enables the checks and code fixes in that project.

* **Constants** for every rule that analyzer publishes — identifiers, categories, help links, and
  the rule's own title on hover, so the prose you used to paste into a suppression has a home.
* **Analyzers** that report the suppressions you have not converted, a pair naming two different
  rules, and a suppression the trimmer would discard.
* **Code fixes** that rewrite a literal pair into a reference and add the `using`, one occurrence at
  a time or across a whole solution with *Fix all occurrences*.
* **A justification on every suppression**, required rather than suggested.
* **No analysis assembly at run time.** The analyzers run inside the compiler and nowhere else, and
  the constants are folded to their values before your assembly is written.

Where that checking stops, and how a project asks for it or declines it, is
[Configuration](doc/guide/configuration.en.md). What a catalogue owes its own consumers is
[Packaging a catalogue](doc/guide/packaging-a-catalogue.en.md).

## 📦 The ready-made catalogues

You almost certainly do not need to write one. Reference the catalogue that matches an analyzer you
already run:

<!-- catalogue-index:begin -->

| Package | Catalogues the rules of | Ids |
| --- | --- | --- |
| **`DiagnosticCatalog.Sonar`** | [SonarAnalyzer.CSharp](https://github.com/SonarSource/sonar-dotnet) | `Sxxxx` |
| **`DiagnosticCatalog.NetAnalyzers`** | .NET code analysis, the rules the SDK ships | `CAxxxx` |
| **`DiagnosticCatalog.StyleCop`** | [StyleCop.Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) | `SAxxxx` |
| **`DiagnosticCatalog.CodeStyle`** | Roslyn's IDE code style — what `.editorconfig` configures and `EnforceCodeStyleInBuild` turns on | `IDExxxx` |
| **`DiagnosticCatalog.Xunit`** | [xunit.analyzers](https://github.com/xunit/xunit.analyzers), which every xUnit test project already runs since `xunit` depends on them | `xUnitxxxx` |
| **`DiagnosticCatalog.NUnit`** | [NUnit.Analyzers](https://github.com/nunit/nunit.analyzers), which `dotnet new nunit` writes into the project file it generates | `NUnitxxxx` |
| **`DiagnosticCatalog.MSTest`** | [MSTest.Analyzers](https://github.com/microsoft/testfx), which every MSTest project already runs since `MSTest.TestFramework` depends on them | `MSTESTxxxx` |
| **`DiagnosticCatalog.Trimming`** | The trimming, Native AOT and single-file warnings, which Blazor WebAssembly, MAUI and `PublishAot` turn on for every build | `ILxxxx` |
| **`DiagnosticCatalog.AspNetCore`** | ASP.NET Core and Blazor, which every web project runs and none can uninstall since they ship inside the shared framework | `ASPxxxx`, `BLxxxx` |
| **`DiagnosticCatalog.Syslib`** | The .NET runtime source generators — `LibraryImport`, the COM and regex generators, JSON source generation | `SYSLIB1xxx` |
| **`DiagnosticCatalog.Roslyn`** | Analyzer authoring, which arrives with `Microsoft.CodeAnalysis.CSharp` for anyone writing an analyzer or a code fix | `RS1xxx`, `RS2xxx` |
| **`DiagnosticCatalog.PublicApi`** | [PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers), for a library tracking its surface in `PublicAPI.Shipped.txt` | `RS00xx` |
| **`DiagnosticCatalog.BannedApi`** | [BannedApiAnalyzers](https://github.com/dotnet/roslyn-analyzers), for a codebase banning an API in `BannedSymbols.txt` | `RS0030`, `RS0031`, `RS0035` |

<!-- catalogue-index:end -->

Every one of them is **generated** from the analyzer's own descriptors, never hand-written, and each
package's own page states which upstream release it currently mirrors. The `DCAT` rules this library
reports are catalogued the same way, as `DiagnosticCatalog.Self`, so suppressing one of them is a
checked reference too. Rules nobody has catalogued are not out of reach either: `DiagnosticCatalog`
on its own is what you reference to declare a catalogue for your own analyzers or an internal
ruleset — [Publishing a catalogue](doc/guide/authoring-a-catalogue.en.md) is that path end to end.

These catalogues are unofficial. They are not affiliated with, endorsed by, or supported by
SonarSource, Microsoft, the StyleCop.Analyzers project, xUnit.net, or the NUnit project. "Sonar" and
"SonarQube" are trademarks of SonarSource S.A.

## 🧭 When to use it, and when not

Worth it when:

* you have suppressions today, and expect to have more;
* several of them name the same rule, so a vendor renaming it touches many files at once;
* you upgrade analyzer packages and want an upgrade to tell you what moved;
* you want to answer "where is this rule suppressed, and why?" with *Find All References* rather
  than a text search.

Not worth it when:

* a handful of suppressions sit in one project and nobody is adding more;
* you silence rules through `#pragma warning disable` or `.editorconfig` alone — neither can take a
  constant, and no version of this will change that;
* you want a tool to judge whether a suppression was *reasonable*. That stays a human question.

[When not to use this](doc/guide/when-not-to-use.en.md) is written to talk you out of it where it
should, and [the alternatives](doc/guide/alternatives.en.md) covers what else there is.

## 📖 Documentation

* [**Getting started**](doc/guide/getting-started.en.md) — ten minutes, one reference, one
  deliberate mistake.
* [**Adopting a catalogue**](doc/guide/adopting-a-catalogue.en.md) — migrating a codebase that
  already suppresses, and the order to convert in.
* [**The `DCAT` diagnostics**](doc/guide/diagnostics.en.md) — every id, what triggers it, and what
  its severity means.
* [**Configuration**](doc/guide/configuration.en.md) — severity keys, and the three levers that
  decide what runs where.
* [**Publishing a catalogue**](doc/guide/authoring-a-catalogue.en.md) and
  [**packaging one**](doc/guide/packaging-a-catalogue.en.md) — for your own analyzers or an internal
  ruleset.
* [**The `dcat` tool**](doc/guide/dcat.en.md) — the generator that writes every catalogue above.
* [**Troubleshooting**](doc/guide/troubleshooting.en.md) — by symptom: nothing reported, `CS0117`,
  `CS0618`, `DCAT0006` on every file at once.

The [documentation map](doc/guide/README.en.md) picks a page by what you are trying to do, and every
page there exists in English and in French. The [specification](doc/specification.en.md) is the
normative version, and the [decision records](doc/adr/) carry the reasoning behind the design.

## 🤝 Contributing and security

Found a bug, or want a catalogue that is not here yet? Open an issue on the
[issue tracker](https://github.com/Reefact/diagnostic-catalog/issues) — there is a form for each.
Contributions are welcome: start with [CONTRIBUTING.md](CONTRIBUTING.md) and with the
[Code of Conduct](CODE_OF_CONDUCT.md) that everyone taking part here accepts.

Releases publish with signed build provenance and an embedded SPDX SBOM. For security
vulnerabilities, follow the private process in [SECURITY.md](SECURITY.md), which also carries the
verification details.

## 📄 License

[Apache-2.0](LICENSE)
