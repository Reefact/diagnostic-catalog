# DiagnosticCatalog.MSTest

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.MSTest/README.fr.md)

The **MSTest.Analyzers** rules as strongly referenced constants, so that
`SuppressMessageAttribute` takes compile-checked references instead of magic strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `MSTest.Analyzers 4.3.3`
>
> **62 rules, 3 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-08-05.
<!-- mirror:end -->

> Unofficial. Not affiliated with, endorsed by, or supported by Microsoft.

## Why

Every MSTest project runs these analyzers, and almost nobody installed them:
`MSTest.TestFramework` depends on `MSTest.Analyzers`, so the `MSTest` meta-package and
`MSTest.TestAdapter` both drag them in. They arrive with the test framework.

That is what makes their rules the ones people suppress **in source**. A rule you switched on
gets tuned in `.editorconfig`; a rule that arrived with the framework gets an exception at the
one place it is wrong, with a `Justification` beside the test that earns it.

MSTest sharpens this further than the other test frameworks do, because it ships rules that
**contradict each other on purpose**: `MSTEST0019` prefers `TestInitialize` methods over
constructors and `MSTEST0020` prefers constructors over `TestInitialize` methods; `MSTEST0021`
prefers `Dispose` over `TestCleanup` and `MSTEST0022` prefers the reverse. You pick a side, and
the other rule of the pair is one you will be answering for the life of the project.

```csharp
[SuppressMessage("Usage", "MSTEST0037:Use proper 'Assert' methods", ...)]
```

Three strings, and nothing checks any of them. Get the id wrong and the suppression silently
does nothing — the warning simply stays. Get the category wrong and **nothing happens at all**,
ever: the .NET platform never reads that argument, so no error, no warning and no failing test
will tell you.

```csharp
using DiagnosticCatalog.MSTest;

[SuppressMessage(
    MSTestRule.MSTEST0037.Category,
    MSTestRule.MSTEST0037.Id,
    Justification = "The overload this rule suggests does not exist for this comparer.")]
```

The day a rule moves to another category, the second version follows it and the first is left
naming a category the rule no longer carries — silently, and for as long as the line survives.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.MSTest" Version="1.0.0" />
```

This package only supplies the constants. The checks that validate rule declarations and
their use sites ship separately in `DiagnosticCatalog.Analyzers`.

## What is in the package

62 rules across 3 categories, and **every one of the 62 carries both the title its descriptor
declares and a help link** into Microsoft Learn — a completeness only the xUnit and NUnit
catalogues here match.

| Category | Rules | What they are about |
| --- | --- | --- |
| `Usage` | 46 | Using the framework correctly — assertions, attributes, data sources, async |
| `Design` | 14 | How a test class is shaped: fixtures, lifecycle, what is public |
| `Performance` | 2 | Parallelisation, and blocking calls in test code |

```csharp
[DiagnosticRule]
public static class MSTEST0037
{
    public const string Id = nameof(MSTEST0037);
    public const string Category = MSTestCategory.Usage;
    public const string HelpLinkUri = "https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0037";
}
```

## Where MSTest files an assertion rule, and why you cannot guess it

If you run more than one test framework across a solution — a migration, a mixed monorepo — this
is the trap worth naming. Take one concept, "this assertion is wrong", and ask each framework
what category it belongs to:

| | The category for an assertion rule |
| --- | --- |
| xUnit | `Assertions` |
| NUnit | `Assertion` |
| **MSTest** | **`Usage` *or* `Design`, depending on the rule** |

xUnit and NUnit differ by one letter. MSTest does not have an assertion category at all: it
splits them by what kind of mistake they catch. `MSTEST0037` *Use proper 'Assert' methods* is
`Usage`; `MSTEST0032` *Assertion condition is always true* is `Design`; `MSTEST0025` *Use
'Assert.Fail' instead of an always-failing assert* is `Design` too.

So even knowing the framework does not tell you the answer — you have to know the rule. And
nothing in the platform reads that argument, so a wrong one costs no error and no warning,
ever. Reaching it through `MSTestRule.MSTEST0037.Category` removes the question.

## Categories declared once

`MSTestCategory` holds each category once, and the rules reference it — so a category's
spelling exists in exactly one place. It is **internal by design**: a suppression reaches a
category through the rule that carries it, `MSTestRule.MSTEST0037.Category`, and never through
the category constant on its own. The two fold to the same string today and stop agreeing the
day MSTest moves the rule
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).

## How it is produced

Not transcribed from documentation. The generator reads the analyzer assemblies' metadata for
the types they mark with `[DiagnosticAnalyzer]`, constructs those, and reads the
`DiagnosticDescriptor` instances they actually declare — the only source that cannot have
drifted.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package MSTest.Analyzers --package-version latest \
    --namespace DiagnosticCatalog.MSTest --container MSTestRule \
    --output src/DiagnosticCatalog.MSTest/MSTestRules.g.cs
```

## How it stays current

A nightly workflow regenerates every catalogue from its upstream package and opens a
pull request when something actually moved — added rules, recategorised rules, rules
retired upstream. It never publishes: a category or an id that changed upstream changes
a published contract, and since the platform never reads a suppression's category, a
wrong value merged unreviewed would produce no symptom anywhere. A human reads the diff.

**A rule retired upstream is never deleted.** It is kept and marked `[Obsolete]` naming
the version that dropped it, so a project still referencing it gets a `CS0618` warning
telling it to remove the suppression — rather than a hard error from a member that
vanished. Consumers inline constant values at their own compile time, so deleting one
breaks their recompilation.

## How it reaches nuget.org

This catalogue rides the `mstest` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md)
and versions independently of the foundation, so it can follow MSTest.Analyzers' releases
without dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes an `mstest-vX.Y.Z` tag, and the
release workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
with signed build provenance — no long-lived API key exists anywhere to leak.

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a
constant. This package covers the `MSTESTxxxx` analyzer rules only.

## See also

Eleven sibling catalogues are generated from this repository the same way, each read from one
analyzer's own descriptors:

- [`DiagnosticCatalog.Sonar`](https://www.nuget.org/packages/DiagnosticCatalog.Sonar)
  — the SonarAnalyzer.CSharp (`Sxxxx`) rules.
- [`DiagnosticCatalog.NetAnalyzers`](https://www.nuget.org/packages/DiagnosticCatalog.NetAnalyzers)
  — the .NET code analysis (`CAxxxx`) rules.
- [`DiagnosticCatalog.StyleCop`](https://www.nuget.org/packages/DiagnosticCatalog.StyleCop)
  — the StyleCop.Analyzers (`SAxxxx`) rules.
- [`DiagnosticCatalog.CodeStyle`](https://www.nuget.org/packages/DiagnosticCatalog.CodeStyle)
  — the Roslyn IDE code-style (`IDExxxx`) rules.
- [`DiagnosticCatalog.Xunit`](https://www.nuget.org/packages/DiagnosticCatalog.Xunit)
  — the xunit.analyzers (`xUnitxxxx`) rules.
- [`DiagnosticCatalog.NUnit`](https://www.nuget.org/packages/DiagnosticCatalog.NUnit)
  — the NUnit.Analyzers (`NUnitxxxx`) rules.
- [`DiagnosticCatalog.Trimming`](https://www.nuget.org/packages/DiagnosticCatalog.Trimming)
  — the trimming, Native AOT and single-file (`ILxxxx`) warnings.
- [`DiagnosticCatalog.AspNetCore`](https://www.nuget.org/packages/DiagnosticCatalog.AspNetCore)
  — the ASP.NET Core and Blazor (`ASPxxxx`, `BLxxxx`) rules.
- [`DiagnosticCatalog.Syslib`](https://www.nuget.org/packages/DiagnosticCatalog.Syslib)
  — the .NET runtime source-generator (`SYSLIB1xxx`) diagnostics.
- [`DiagnosticCatalog.Roslyn`](https://www.nuget.org/packages/DiagnosticCatalog.Roslyn)
  — the Roslyn analyzer-authoring (`RS1xxx`, `RS2xxx`) rules.
- [`DiagnosticCatalog.Self`](https://www.nuget.org/packages/DiagnosticCatalog.Self)
  — this library's own `DCATxxxx` rules, for suppressing a diagnostic the catalogue analyzers
  themselves report.

**Want a catalogue of your own?** Your analyzer's rules, or an internal ruleset, are declared
exactly the way these are: a static class of constants marked `[DiagnosticRule]`, referenced by
consumers instead of retyped. That marker ships in
[`DiagnosticCatalog`](https://www.nuget.org/packages/DiagnosticCatalog), the foundation this
catalogue is built on, and its README is the guide.

## Documentation

For using a catalogue, in the order the work happens:

- [**Getting started**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/getting-started.en.md)
  — ten minutes: reference this package, rewrite one suppression, break it on purpose and
  watch the compiler catch it.
- [**Writing suppressions that the compiler checks**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/writing-suppressions.en.md)
  — the full version, including migrating the literals you already have.
- [**Adopting a catalogue on an existing codebase**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/adopting-a-catalogue.en.md)
  — the severity ramp, *Fix all occurrences*, scoping by folder, and what order to convert in.
- [**Configuration**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/configuration.en.md)
  — every severity key, the category-wide switch, and the `PrivateAssets` mistake that
  silences everything.
- [**Troubleshooting**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/troubleshooting.en.md)
  — by symptom: nothing is reported, `CS0117`, `CS0618` after an upgrade.

The [**documentation map**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.en.md)
picks a page by what you are trying to do; every guide exists in English and French. The
[**specification**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.en.md)
is the normative version of all of it.

## License

Apache-2.0. The rule identifiers, categories, titles and help links are read from a
third-party analyzer, which is itself MIT-licensed.
