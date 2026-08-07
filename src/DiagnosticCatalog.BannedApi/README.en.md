# DiagnosticCatalog.BannedApi

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.BannedApi/README.fr.md)

The **banned-API** rules (`RS0030`, `RS0031`, `RS0035`) as strongly referenced constants, so that
`SuppressMessageAttribute` takes compile-checked references instead of magic strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `Microsoft.CodeAnalysis.BannedApiAnalyzers 5.6.0`
>
> **3 rules, 1 category**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-08-05.
<!-- mirror:end -->

> Unofficial. Not affiliated with, endorsed by, or supported by Microsoft.

## Why

Three rules. The smallest catalogue in this family by a wide margin — and the rule count says
nothing at all about how much of it you will meet.

`RS0030` *Do not use banned APIs* fires **once per call site of whatever you banned**. Nothing about
the package decides that; a `BannedSymbols.txt` you wrote does. Ban `DateTime.Now` across a codebase
that has been calling it for eight years and you get one diagnostic per call, all of them `RS0030`.
The ones you migrate that afternoon disappear. The ones behind a serialisation format, a third-party
signature or a release you have not cut yet get a suppression with a justification, and they stay.

That is the whole case for the constants: a rule with three ids and thousands of sites is exactly
where a typo in the id survives review, because no reviewer is reading the fourteenth suppression as
carefully as the first.

```csharp
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "…")]
```

Get the id wrong and the suppression silently does nothing — the warning simply stays. Get the
category wrong and **nothing happens at all**, ever: the .NET platform never reads that argument, so
no error, no warning and no failing test will tell you.

```csharp
using DiagnosticCatalog.BannedApi;

[SuppressMessage(
    BannedApiRule.RS0030.Category,
    BannedApiRule.RS0030.Id,
    Justification = "The wire format pins this overload; migrating it is a breaking change.")]
```

## Who runs these

Nobody by accident. `Microsoft.CodeAnalysis.BannedApiAnalyzers` is an explicit `PackageReference`,
and it does nothing at all until somebody writes the `BannedSymbols.txt` that tells it what to ban.

It is here for the same reason `DiagnosticCatalog.PublicApi` is: what a team adopts deliberately, it
then lives with for years. A ban is adopted precisely *because* the API is still being called, so
the suppressions arrive with the ban and outlive whoever wrote them.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.BannedApi" Version="1.0.0" />
```

The constants are all this package declares — what is banned is `BannedSymbols.txt`, which this
catalogue never reads. That reference is still all you need to be checked: this package depends
on `DiagnosticCatalog`, which carries the `DCAT` analyzers and code fixes beside its attributes, so
the checks on rule declarations and their use sites arrive with it. A literal suppression a
catalogue reference would replace is an error by default, and a code fix rewrites it for you.

## What is in the package

3 rules in a single category, `ApiDesign`:

| Rule | What it reports |
| --- | --- |
| `RS0030` | A call to a symbol listed in `BannedSymbols.txt`. The one you will meet. |
| `RS0031` | The banned-symbols file itself lists something twice. |
| `RS0035` | An internal symbol reached from outside its restricted namespace. |

**Two of the three carry a help link; `RS0035` declares none.** That is the vendor's descriptor, not
an omission here — the catalogue emits `HelpLinkUri` only where one exists, so the constant is
absent rather than empty.

```csharp
[DiagnosticRule]
public static class RS0030
{
    public const string Id = nameof(RS0030);
    public const string Category = BannedApiCategory.ApiDesign;
    public const string HelpLinkUri = "https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.BannedApiAnalyzers/BannedApiAnalyzers.Help.md";
}
```

## The `RS` family, now complete

Three Microsoft packages issue `RS` rules, and all three are catalogued:

| Package | Ids | Catalogue |
| --- | --- | --- |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | `RS0030`, `RS0031`, `RS0035` | **this one** |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | `RS0016`–`RS0061` | [`DiagnosticCatalog.PublicApi`](https://www.nuget.org/packages/DiagnosticCatalog.PublicApi) |
| `Microsoft.CodeAnalysis.Analyzers` | `RS1xxx`, `RS2xxx` | [`DiagnosticCatalog.Roslyn`](https://www.nuget.org/packages/DiagnosticCatalog.Roslyn) |

The ids partition cleanly, so no rule is ambiguous about where it lives. Three catalogues rather
than one because a catalogue mirrors one package: the manifest takes a single package id, and
`[assembly: CatalogSource]` records a single source and version. Their badges — `BAN`, `API`, `RS` —
are what tells the three icons apart. A badge carries the rule prefix, capped at three letters, and
all three would otherwise want `RS`; when the prefix is already in service the badge names the
catalogue's subject instead, and the prefix stays with the catalogue publishing it
([ADR-0035](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0035-badge-a-shared-prefix-catalogue-with-its-subject.en.md)).

**This catalogue and `DiagnosticCatalog.PublicApi` declare the same category string**, `ApiDesign`,
from two different packages. They are separate constants on separate containers, which is what
keeps them independent the day one vendor moves.

## Categories declared once

`BannedApiCategory` holds each category once, and the rules reference it. It is **internal by
design**: a suppression reaches a category through the rule that carries it,
`BannedApiRule.RS0030.Category`, never through the category constant on its own
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a constant.

`RS0031` is reported against `BannedSymbols.txt` rather than against code, so no attribute reaches
it; the answer there is to fix the duplicate line. Where `[SuppressMessage]` applies — which for
`RS0030` is every call site — the constants here work.

## How it is produced

Not transcribed from documentation. The generator reads the analyzer assemblies' metadata for the
types they mark with `[DiagnosticAnalyzer]`, constructs those, and reads the `DiagnosticDescriptor`
instances they actually declare — which is how the missing help link on `RS0035` is a measured fact
rather than a guess.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.CodeAnalysis.BannedApiAnalyzers --package-version latest \
    --namespace DiagnosticCatalog.BannedApi --container BannedApiRule \
    --output src/DiagnosticCatalog.BannedApi/BannedApiRules.g.cs
```

## How it stays current

A nightly workflow regenerates every catalogue from its upstream package and opens a pull request
when anything the catalogue publishes has moved. It never
publishes: a category or an id that changed upstream changes a published contract, and since the
platform never reads a suppression's category, a wrong value merged unreviewed would produce no
symptom anywhere. A human reads the diff.

**A rule retired upstream is never deleted.** It is kept and marked `[Obsolete]` naming the version
that dropped it, so a project still referencing it gets a `CS0618` warning telling it to remove the
suppression — rather than a hard error from a member that vanished. Consumers inline constant values
at their own compile time, so deleting one breaks their recompilation.

## How it reaches nuget.org

This catalogue rides the `bannedapi` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md)
and versions independently of the foundation, so it can follow
Microsoft.CodeAnalysis.BannedApiAnalyzers' releases without dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes a `bannedapi-vX.Y.Z` tag, and the release
workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) with
signed build provenance — no long-lived API key exists anywhere to leak.

## See also

Every catalogue this repository publishes is listed in one place — pick the one that matches an
analyzer you run:

**[The ready-made catalogues](https://github.com/Reefact/diagnostic-catalog#-the-ready-made-catalogues)**

**Want a catalogue of your own?** Your analyzer's rules, or an internal ruleset, are declared exactly
the way these are: a static class of constants marked `[DiagnosticRule]`, referenced by consumers
instead of retyped. That marker ships in
[`DiagnosticCatalog`](https://www.nuget.org/packages/DiagnosticCatalog), the foundation this catalogue is built
on, and its README is the guide.

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
