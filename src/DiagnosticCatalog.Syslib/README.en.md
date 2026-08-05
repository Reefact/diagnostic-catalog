# DiagnosticCatalog.Syslib

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Syslib/README.fr.md)

The **.NET runtime source-generator diagnostics** (`SYSLIB1xxx`) as strongly referenced constants,
so that `SuppressMessageAttribute` takes compile-checked references instead of magic strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `Microsoft.NETCore.App.Ref 10.0.10`
>
> **13 rules, 4 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-08-05.
<!-- mirror:end -->

> Unofficial. Not affiliated with, endorsed by, or supported by Microsoft.

## Why

Thirteen rules is the smallest catalogue here, and one of them is the reason it exists.

`SYSLIB1090`'s category is **`ComInterfaceGenerator`**.

Not `Interoperability`, which is what its four closest neighbours use. Not `Usage`, not `Design` —
the name of the generator assembly that happens to declare it. Every other category in every
catalogue in this family is a concept a person could arrive at: `Usage`, `Security`, `Performance`,
`Trimming`, `Assertion`. This one is an implementation detail that leaked into a published contract,
carried by exactly one rule.

```csharp
[SuppressMessage("Interoperability", "SYSLIB1090:...", ...)]   // wrong, and nothing says so
```

Get the id wrong and the suppression silently does nothing — the warning simply stays. Get the
category wrong and **nothing happens at all**, ever: the .NET platform never reads that argument, so
no error, no warning and no failing test will tell you.

```csharp
using DiagnosticCatalog.Syslib;

[SuppressMessage(
    SyslibRule.SYSLIB1090.Category,
    SyslibRule.SYSLIB1090.Id,
    Justification = "The interface is only ever marshalled by the legacy path.")]
```

The day that category is corrected upstream — and it looks like the sort of thing that gets
corrected — the second version follows it and the first keeps compiling while it quietly stops
matching.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.Syslib" Version="1.0.0" />
```

This package only supplies the constants. The checks that validate rule declarations and their use
sites ship separately in `DiagnosticCatalog.Analyzers`.

## What is in the package

13 rules across 4 categories, and **all 13 carry the help link their descriptor declares**.

| Category | Rules | What they are about |
| --- | --- | --- |
| `Usage` | 6 | Marshaller shape and validity for `LibraryImport` — `SYSLIB1055`–`SYSLIB1061` |
| `Interoperability` | 5 | Converting to `LibraryImport` and to the generated COM interface, and COM hosting |
| `Performance` | 1 | `SYSLIB1045`, *Convert to `GeneratedRegexAttribute`* |
| `ComInterfaceGenerator` | 1 | `SYSLIB1090`, above |

```csharp
[DiagnosticRule]
public static class SYSLIB1045
{
    public const string Id = nameof(SYSLIB1045);
    public const string Category = SyslibCategory.Performance;
    public const string HelpLinkUri = "https://learn.microsoft.com/dotnet/fundamentals/syslib-diagnostics/syslib1045";
}
```

## Which SYSLIB rules these are

The `SYSLIB` prefix covers two unrelated things, and only one of them is here.

* **`SYSLIB1xxx` — source-generator diagnostics.** What this package holds. They come from real
  analyzers with real `DiagnosticDescriptor` instances, and `[SuppressMessage]` silences them.
* **`SYSLIB0xxx` — obsoletion warnings.** `SYSLIB0001` and its siblings are raised by the compiler
  from `[Obsolete]` on the API itself. No analyzer declares them, so no descriptor exists to read
  and none appears here.

The ids are not contiguous for the same reason a vendor's ids are never contiguous — the runtime
allocates them across generators, and only the ones that survived to a shipped release are declared.

## Categories declared once

`SyslibCategory` holds each category once, and the rules reference it — so a category's spelling
exists in exactly one place. It is **internal by design**: a suppression reaches a category through
the rule that carries it, `SyslibRule.SYSLIB1090.Category`, and never through the category constant
on its own. The two fold to the same string today and stop agreeing the day a rule moves
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).

## How it is produced

Not transcribed from documentation. The generator reads the analyzer assemblies' metadata for the
types they mark with `[DiagnosticAnalyzer]`, constructs those, and reads the `DiagnosticDescriptor`
instances they actually declare — the only source that cannot have drifted.

The generators ship inside **`Microsoft.NETCore.App.Ref`**, the .NET runtime targeting pack, which is
an ordinary package on nuget.org — that is how the SDK itself acquires it. So the mirrored release is
a package version a consumer can look up, rather than whatever happened to be installed on the
machine that generated the file.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.NETCore.App.Ref --package-version latest \
    --namespace DiagnosticCatalog.Syslib --container SyslibRule \
    --output src/DiagnosticCatalog.Syslib/SyslibRules.g.cs
```

Six generator assemblies are read and ten of their types declare a rule. The whole pack is read
rather than a hand-picked subset, so a generator that gains its first rule is caught by the nightly
instead of waiting for somebody to notice.

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

## A note on versions

The rules a project actually gets are governed by its **shared framework**, which its target
framework selects — not by a package reference it controls. This catalogue mirrors a targeting-pack
release, and the assembly records exactly which one in `[assembly: CatalogSource]`. If your app
targets an older runtime than the version recorded there, rules added since will be present in the
catalogue and absent from your build; referencing one still compiles, and the suppression simply
never matches anything.

## How it reaches nuget.org

This catalogue rides the `syslib` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md)
and versions independently of the foundation, so it can follow the runtime's releases without
dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes a `syslib-vX.Y.Z` tag, and the release
workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) with
signed build provenance — no long-lived API key exists anywhere to leak.

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a constant. That
is also why the `SYSLIB0xxx` obsoletions are out of reach: they are compiler warnings raised from
`[Obsolete]`, not analyzer diagnostics. This package covers the `SYSLIB1xxx` analyzer rules only.

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
- [`DiagnosticCatalog.Roslyn`](https://www.nuget.org/packages/DiagnosticCatalog.Roslyn)
  — the Roslyn analyzer-authoring (`RS1xxx`, `RS2xxx`) rules.
- [`DiagnosticCatalog.PublicApi`](https://www.nuget.org/packages/DiagnosticCatalog.PublicApi)
  — the public-API tracking (`RS00xx`) rules.
- [`DiagnosticCatalog.Self`](https://www.nuget.org/packages/DiagnosticCatalog.Self)
  — this library's own `DCATxxxx` rules, for suppressing a diagnostic the catalogue analyzers
  themselves report.

**Want a catalogue of your own?** Your analyzer's rules, or an internal ruleset, are declared exactly
the way these are: a static class of constants marked `[DiagnosticRule]`, referenced by consumers
instead of retyped. That marker ships in
[`DiagnosticCatalog`](https://www.nuget.org/packages/DiagnosticCatalog), the foundation this
catalogue is built on, and its README is the guide.

## Documentation

For using a catalogue, in the order the work happens:

- [**Getting started**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/getting-started.en.md)
  — ten minutes: reference this package, rewrite one suppression, break it on purpose and watch the
  compiler catch it.
- [**Writing suppressions that the compiler checks**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/writing-suppressions.en.md)
  — the full version, including migrating the literals you already have.
- [**Adopting a catalogue on an existing codebase**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/adopting-a-catalogue.en.md)
  — the severity ramp, *Fix all occurrences*, scoping by folder, and what order to convert in.
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
