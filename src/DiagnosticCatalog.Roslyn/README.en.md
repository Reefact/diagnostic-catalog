# DiagnosticCatalog.Roslyn

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Roslyn/README.fr.md)

The **Roslyn analyzer-authoring** rules (`RS1xxx`, `RS2xxx`) as strongly referenced constants, so
that `SuppressMessageAttribute` takes compile-checked references instead of magic strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `Microsoft.CodeAnalysis.Analyzers 5.6.0`
>
> **52 rules, 9 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-08-05.
<!-- mirror:end -->

> Unofficial. Not affiliated with, endorsed by, or supported by Microsoft.

## Why

Nine categories, and **two of them break the pattern the other seven follow**.

Seven read `MicrosoftCodeAnalysis` followed by a word: `MicrosoftCodeAnalysisCorrectness`,
`MicrosoftCodeAnalysisDesign`, `MicrosoftCodeAnalysisReleaseTracking`. Nobody types those from
memory, but at least they are guessable once you have seen one.

Then there is this:

| Rule | Category |
| --- | --- |
| `RS1001` *Missing diagnostic analyzer attribute* | `MicrosoftCodeAnalysisCorrectness` |
| `RS1010` *Create code actions should have a unique EquivalenceKey* | **`Correctness`** |
| `RS1011` *Use code actions that have a unique EquivalenceKey* | **`Correctness`** |
| `RS1016` *Code fix providers should provide FixAll support* | **`Correctness`** |
| `RS1023` *Upgrade MSBuildWorkspace* | **`Library`** |

The same concept, correctness, spelled two ways in **one package** — twenty rules under the long
form and three under the short one, with nothing to tell you which is which. `Library` is a category
with exactly one rule in it.

```csharp
[SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1016:...", ...)]   // wrong, and silent
```

Get the id wrong and the suppression silently does nothing — the warning simply stays. Get the
category wrong and **nothing happens at all**, ever: the .NET platform never reads that argument, so
no error, no warning and no failing test will tell you.

```csharp
using DiagnosticCatalog.Roslyn;

[SuppressMessage(
    RoslynRule.RS1016.Category,
    RoslynRule.RS1016.Id,
    Justification = "The fixer is deliberately single-document; FixAll would be wrong here.")]
```

## Who runs these without asking

`Microsoft.CodeAnalysis.Analyzers` reaches a project **transitively**, through
`Microsoft.CodeAnalysis.CSharp`. Reference the Roslyn APIs to write an analyzer, a code fix, a
source generator or an analyzer test, and these fifty-two rules come with them — the same shape as
xUnit's and MSTest's analyzers arriving with their test frameworks.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.Roslyn" Version="1.0.0" />
```

This package only supplies the constants. The checks that validate rule declarations and their use
sites ship separately in `DiagnosticCatalog.Analyzers` — which, despite the similar name, is a
different thing entirely: it holds this library's own `DCAT` diagnostics, not Roslyn's `RS` ones.

## What is in the package

52 rules across 9 categories. Thirteen carry a help link; the rest declare none.

| Category | Rules | What they are about |
| --- | --- | --- |
| `MicrosoftCodeAnalysisCorrectness` | 20 | Registering actions, analyzer attributes, descriptor construction |
| `MicrosoftCodeAnalysisDesign` | 10 | The shape an analyzer or fixer is expected to have |
| `MicrosoftCodeAnalysisReleaseTracking` | 9 | `AnalyzerReleases.Shipped.md` and its unshipped twin — the `RS2xxx` range |
| `MicrosoftCodeAnalysisPerformance` | 4 | Work an analyzer should not do per-compilation |
| `Correctness` | 3 | `EquivalenceKey` and FixAll support — the short-form outlier |
| `MicrosoftCodeAnalysisCompatibility` | 2 | Interfaces only Roslyn may implement |
| `MicrosoftCodeAnalysisDocumentation` | 2 | Analyzer documentation |
| `MicrosoftCodeAnalysisLocalization` | 1 | Localizable descriptor arguments |
| `Library` | 1 | `RS1023`, alone |

```csharp
[DiagnosticRule]
public static class RS1016
{
    public const string Id = nameof(RS1016);
    public const string Category = RoslynCategory.Correctness;
}
```

## Not the `RS00xx` rules

Three Microsoft packages issue `RS` rules, and this catalogue holds one of them:

| Package | Ids | Here? |
| --- | --- | --- |
| `Microsoft.CodeAnalysis.Analyzers` | `RS1xxx`, `RS2xxx` | **yes**, all 52 |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | `RS0016`–`RS002x` | no |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | `RS0030`, `RS0031`, `RS0035` | no |

The ids partition cleanly, so there is no ambiguity about which rule lives where. The reason the
other two are absent is the icon: a catalogue's badge carries its rule prefix
([ADR-0032](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0032-badge-a-catalogues-icon-with-its-rule-prefix.en.md))
capped at three letters
([ADR-0033](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0033-cap-the-badge-at-three-letters.en.md)),
so three catalogues of `RS` rules would want the same two letters and no icon could tell them apart.
Merging them instead has no shape in the manifest, which takes one package per catalogue. That is a
decision somebody has to make before those 26 rules can be catalogued at all; this package does not
pretend to have made it.

## Categories declared once

`RoslynCategory` holds each category once, and the rules reference it — so a category's spelling
exists in exactly one place, which for `MicrosoftCodeAnalysisReleaseTracking` is worth more than
usual. It is **internal by design**: a suppression reaches a category through the rule that carries
it, `RoslynRule.RS1016.Category`, and never through the category constant on its own. The two fold
to the same string today and stop agreeing the day a rule moves
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a constant.

That limit bites harder here than elsewhere, and it is worth being straight about. Several `RS`
rules are reported against a whole assembly or a project file rather than a syntax node — `RS1036`
*Specify EnforceExtendedAnalyzerRules*, `RS1038` *Compiler extensions should target netstandard2.0*,
`RS2008` *Enable analyzer release tracking* — and the usual answer to those is `#pragma` or an
`.editorconfig` entry, neither of which can take a constant. This repository silences three of them
that way in its own tests. Where `[SuppressMessage]` does apply, the constants here work; where it
does not, no catalogue can help.

## How it is produced

Not transcribed from documentation. The generator reads the analyzer assemblies' metadata for the
types they mark with `[DiagnosticAnalyzer]`, constructs those, and reads the `DiagnosticDescriptor`
instances they actually declare — the only source that cannot have drifted, which is what surfaced
the two off-pattern categories above.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.CodeAnalysis.Analyzers --package-version latest \
    --namespace DiagnosticCatalog.Roslyn --container RoslynRule \
    --output src/DiagnosticCatalog.Roslyn/RoslynRules.g.cs
```

## How it stays current

A nightly workflow regenerates every catalogue from its upstream package and opens a pull request
when something actually moved — added rules, recategorised rules, rules retired upstream. It never
publishes: a category or an id that changed upstream changes a published contract, and since the
platform never reads a suppression's category, a wrong value merged unreviewed would produce no
symptom anywhere. A human reads the diff.

**A rule retired upstream is never deleted.** It is kept and marked `[Obsolete]` naming the version
that dropped it, so a project still referencing it gets a `CS0618` warning telling it to remove the
suppression — rather than a hard error from a member that vanished. Consumers inline constant values
at their own compile time, so deleting one breaks their recompilation.

## How it reaches nuget.org

This catalogue rides the `roslyn` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md)
and versions independently of the foundation, so it can follow Microsoft.CodeAnalysis.Analyzers'
releases without dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes a `roslyn-vX.Y.Z` tag, and the release
workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) with
signed build provenance — no long-lived API key exists anywhere to leak.

## See also

Twelve sibling catalogues are generated from this repository the same way, each read from one
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
- [`DiagnosticCatalog.MSTest`](https://www.nuget.org/packages/DiagnosticCatalog.MSTest)
  — the MSTest.Analyzers (`MSTESTxxxx`) rules.
- [`DiagnosticCatalog.Trimming`](https://www.nuget.org/packages/DiagnosticCatalog.Trimming)
  — the trimming, Native AOT and single-file (`ILxxxx`) warnings.
- [`DiagnosticCatalog.AspNetCore`](https://www.nuget.org/packages/DiagnosticCatalog.AspNetCore)
  — the ASP.NET Core and Blazor (`ASPxxxx`, `BLxxxx`) rules.
- [`DiagnosticCatalog.Syslib`](https://www.nuget.org/packages/DiagnosticCatalog.Syslib)
  — the .NET runtime source-generator (`SYSLIB1xxx`) diagnostics.
- [`DiagnosticCatalog.PublicApi`](https://www.nuget.org/packages/DiagnosticCatalog.PublicApi)
  — the public-API tracking (`RS00xx`) rules.
- [`DiagnosticCatalog.Self`](https://www.nuget.org/packages/DiagnosticCatalog.Self)
  — this library's own `DCATxxxx` rules, for suppressing a diagnostic the catalogue analyzers
  themselves report.

**Want a catalogue of your own?** Your analyzer's rules, or an internal ruleset, are declared exactly
the way these are: a static class of constants marked `[DiagnosticRule]`, referenced by consumers
instead of retyped. That marker ships in
[`DiagnosticCatalog`](https://www.nuget.org/packages/DiagnosticCatalog), the foundation this
catalogue is built on, and its README is the guide. If you are here because you write analyzers,
that guide is aimed at you.

## Documentation

For using a catalogue, in the order the work happens:

- [**Getting started**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/getting-started.en.md)
  — ten minutes: reference this package, rewrite one suppression, break it on purpose and watch the
  compiler catch it.
- [**Writing suppressions that the compiler checks**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/writing-suppressions.en.md)
  — the full version, including migrating the literals you already have.
- [**Publishing a catalogue**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.en.md)
  — the structural contract, and how to ship one for your own analyzer's rules.
- [**Configuration**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/configuration.en.md)
  — every severity key, the category-wide switch, and the `PrivateAssets` mistake that silences
  everything.
- [**Troubleshooting**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/troubleshooting.en.md)
  — by symptom: nothing is reported, `CS0117`, `CS0618` after an upgrade.

The [**documentation map**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.en.md)
picks a page by what you are trying to do; every guide exists in English and French. The
[**specification**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.en.md)
is the normative version of all of it.

## License

Apache-2.0. The rule identifiers, categories, titles and help links are read from a Microsoft
analyzer, which is itself MIT-licensed.
