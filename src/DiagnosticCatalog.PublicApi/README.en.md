# DiagnosticCatalog.PublicApi

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.PublicApi/README.fr.md)

The **public-API tracking** rules (`RS00xx`) as strongly referenced constants, so that
`SuppressMessageAttribute` takes compile-checked references instead of magic strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0`
>
> **23 rules, 1 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-08-05.
<!-- mirror:end -->

> Unofficial. Not affiliated with, endorsed by, or supported by Microsoft.

## Why

Twenty-three rules, one category — and **four pairs that share a byte-identical title**.

| These two rules | Are both titled |
| --- | --- |
| `RS0022` and `RS0061` | *Constructor make noninheritable base class inheritable.* |
| `RS0026` and `RS0059` | *Do not add multiple public overloads with optional parameters.* |
| `RS0027` and `RS0060` | *API with optional parameter(s) should have the most parameters amongst its public overloads.* |
| `RS0037` and `RS0056` | *Enable tracking of nullability of reference types in the declared API.* |

The first of each pair is about your **public** surface; the second is about your **internal** one.
The analyzer tracks both — `PublicAPI.Shipped.txt` on one side, `InternalAPI.Shipped.txt` on the
other — and the titles say nothing about which is which. Neither do the help links: both members of
every pair point at the same URL.

**Only the id distinguishes them.** Which is the string most likely to be retyped from memory, from
an IDE tooltip showing a title that is not unique, or from a build log two hundred lines up.

```csharp
[SuppressMessage("ApiDesign", "RS0037:Enable tracking of nullability...", ...)]  // which one?
```

Get the id wrong and the suppression silently does nothing — the warning simply stays. Get the
category wrong and **nothing happens at all**, ever: the .NET platform never reads that argument, so
no error, no warning and no failing test will tell you.

```csharp
using DiagnosticCatalog.PublicApi;

[SuppressMessage(
    PublicApiRule.RS0037.Category,
    PublicApiRule.RS0037.Id,
    Justification = "The public surface is annotated; the internal one is tracked but not annotated.")]
```

## Who runs these, and why the answer is different here

Every other catalogue in this family mirrors an analyzer you did not choose — one that arrives
transitively with a test framework, or inside the .NET SDK, or through a targeting pack.
**This one is different, and it is worth saying plainly:** `Microsoft.CodeAnalysis.PublicApiAnalyzers`
is an explicit `PackageReference`. Nobody gets it by accident.

What makes it belong here is what happens *after* that choice. `RS0016` fires **once per member
missing from the declared surface**, so switching the analyzer on over an existing library does not
produce a warning — it produces hundreds, in one build. Most get resolved by writing the API files.
The remainder — a member deliberately left undeclared, a generic the tool renders in a form the file
cannot express — get suppressed in source, with a `Justification`, and they stay there for years.

That is a long-lived population of hand-typed rule ids, in a family where four titles are ambiguous
and the ids run `RS0016`, `RS0017`, `RS0022`, `RS0024` — close together, non-contiguous, and easy to
transpose.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.PublicApi" Version="1.0.0" />
```

This package only supplies the constants. It does not track your API — that is the analyzer's job,
and this catalogue never runs it.

## What is in the package

23 rules in a single category, `ApiDesign`. Every rule carries a help link, though only **two
distinct destinations** exist: nineteen point at the analyzer's shared help page, and the four
optional-parameter rules point at a design document.

The set splits cleanly in two, which is the shape worth knowing:

| Surface | Rules | |
| --- | ---: | --- |
| Public | 11 | `RS0016`, `RS0017`, `RS0022`, `RS0024`, `RS0025`, `RS0026`, `RS0027`, `RS0036`, `RS0037`, `RS0041`, `RS0048` |
| Internal | 11 | `RS0051`–`RS0061` |
| Either | 1 | `RS0050` *API is marked as removed but it exists in source code* |

```csharp
[DiagnosticRule]
public static class RS0016
{
    public const string Id = nameof(RS0016);
    public const string Category = PublicApiCategory.ApiDesign;
    public const string HelpLinkUri = "https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/PublicApiAnalyzers/PublicApiAnalyzers.Help.md";
}
```

## Not the other `RS` rules

Three Microsoft packages issue `RS` rules, and the ids partition cleanly between them:

| Package | Ids | Catalogue |
| --- | --- | --- |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | `RS0016`–`RS0061` | **this one**, all 23 |
| `Microsoft.CodeAnalysis.Analyzers` | `RS1xxx`, `RS2xxx` | [`DiagnosticCatalog.Roslyn`](https://www.nuget.org/packages/DiagnosticCatalog.Roslyn) |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | `RS0030`, `RS0031`, `RS0035` | not catalogued |

Two catalogues rather than one because a catalogue mirrors one package: the manifest takes a single
package id, and `[assembly: CatalogSource]` records a single source and version. What kept them
apart for a while was the icon — a badge carries the catalogue's rule prefix, capped at three
letters, so both wanted `RS` and no icon could tell them apart. A badge whose prefix is already in
service names the catalogue's subject instead, and the prefix stays with the catalogue publishing it
([ADR-0035](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0035-badge-a-shared-prefix-catalogue-with-its-subject.en.md)):
`RS` is `DiagnosticCatalog.Roslyn`'s, and this one wears `API`.

## Categories declared once

`PublicApiCategory` holds each category once, and the rules reference it. With one category that
buys little today; it costs nothing and it is what every catalogue here does, so the day upstream
adds a second one, nothing about the shape changes. It is **internal by design**: a suppression
reaches a category through the rule that carries it, `PublicApiRule.RS0016.Category`, never through
the category constant on its own
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a constant.

Two of these rules are reported against a **project** rather than a syntax node — `RS0048`
*Missing shipped or unshipped public API file* and `RS0058`, its internal twin. Those are answered
by adding the file or by an `.editorconfig` entry, neither of which can take a constant. Where
`[SuppressMessage]` applies, the constants here work; where it does not, no catalogue can help.

## How it is produced

Not transcribed from documentation. The generator reads the analyzer assemblies' metadata for the
types they mark with `[DiagnosticAnalyzer]`, constructs those, and reads the `DiagnosticDescriptor`
instances they actually declare — the only source that cannot have drifted, and what surfaced the
four duplicate titles above. The whole set comes from **one analyzer type** declaring twenty-three
descriptors.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.CodeAnalysis.PublicApiAnalyzers --package-version latest \
    --namespace DiagnosticCatalog.PublicApi --container PublicApiRule \
    --output src/DiagnosticCatalog.PublicApi/PublicApiRules.g.cs
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

This catalogue rides the `publicapi` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md)
and versions independently of the foundation, so it can follow
Microsoft.CodeAnalysis.PublicApiAnalyzers' releases without dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes a `publicapi-vX.Y.Z` tag, and the release
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
