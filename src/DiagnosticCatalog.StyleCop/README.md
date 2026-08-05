# DiagnosticCatalog.StyleCop

The **StyleCop.Analyzers** rules as strongly referenced constants, so that
`SuppressMessageAttribute` takes compile-checked references instead of magic strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `StyleCop.Analyzers.Unstable 1.2.0.556`
>
> **197 rules, 8 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-07-31.
<!-- mirror:end -->

> Unofficial. Not affiliated with or endorsed by the StyleCop.Analyzers project.

## Why

StyleCop makes the point better than most analyzers: its categories are namespace-shaped
strings nobody would ever guess.

```csharp
[SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1000", Justification = "...")]
```

Get the id wrong and the suppression silently does nothing — the warning simply stays.
Get the category wrong and **nothing happens at all**, ever: the .NET platform never
reads that argument, so nothing will ever tell you. Would you have written
`"StyleCop.CSharp.SpacingRules"` from memory? Or `"Spacing"`, or `"Style"`?

```csharp
using DiagnosticCatalog.StyleCop;

[SuppressMessage(
    StyleCopRule.SA1000.Category,
    StyleCopRule.SA1000.Id,
    Justification = "Generated code follows a different spacing convention.")]
```

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.StyleCop" Version="0.1.0" />
```

This package only supplies the constants. The checks that validate rule declarations and
their use sites ship separately in `DiagnosticCatalog.Analyzers`.

## What is in the package

193 rules across 8 categories, all of them of the `StyleCop.CSharp.*Rules` shape:
`DocumentationRules`, `LayoutRules`, `MaintainabilityRules`, `NamingRules`,
`OrderingRules`, `ReadabilityRules`, `SpacingRules`, `SpecialRules`.

Every rule carries its help link, because StyleCop populates `HelpLinkUri` on all 193
descriptors:

```csharp
[DiagnosticRule]
public static class SA1000
{
    public const string Id = nameof(SA1000);
    public const string Category = StyleCopCategory.StyleCopCSharpSpacingRules;
    public const string HelpLinkUri = "https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/SA1000.md";
}
```

Each rule carries its upstream title as a documentation comment. Rule descriptions are
not redistributed — the help link takes you to them.

## A note on versions

This catalogue mirrors **StyleCop.Analyzers.Unstable 1.2.0.556** — the `1.2.0-beta` line,
which is what projects actually install.

That is a deliberate choice, and it is worth stating plainly. StyleCop.Analyzers' latest
*stable* release is `1.1.118`, published in **April 2019**; the project has lived on
`1.2.0-beta` ever since. Mirroring the stable meant mirroring a release almost nobody
runs, and it was not merely incomplete — `SA1413` is declared under
`StyleCop.CSharp.ReadabilityRules` in the stable and under
`StyleCop.CSharp.MaintainabilityRules` in the beta. A consumer on the beta writing
`StyleCopRule.SA1413.Category` from a stable-based catalogue would get the wrong string,
and nothing in their build would ever say so. Being current beats being nominally stable
when a category is wrong either way (ADR-0016).

Note the package id: `StyleCop.Analyzers` 1.2.0-beta is a metapackage carrying no analyzer
assembly of its own — the rules live in `StyleCop.Analyzers.Unstable`, whose own versions
carry no prerelease tag.

**If you are on the stable `1.1.118`**, use `DiagnosticCatalog.StyleCop` **0.2.0**, the
last version to mirror it. Its 193 rules are a subset of the 197 here, none was removed,
and only `SA1413` changed category.

The assembly records exactly what it mirrors:

```csharp
[assembly: CatalogSource(
    source:        "StyleCop.Analyzers.Unstable",
    sourceVersion: "1.2.0.556",
    generatedOn:   "2026-07-31")]
```

## Categories declared once

A catalogue repeats very few distinct categories across very many rules. Each one is
declared once in `StyleCopCategory` and the rules refer to it, so there is a single source per value:

```csharp
[DiagnosticCategory]
public static class StyleCopCategory
{
    public const string StyleCopCSharpSpacingRules = "StyleCop.CSharp.SpacingRules";
}

[DiagnosticRule]
public static class SA1000
{
    public const string Id = nameof(SA1000);
    public const string Category = StyleCopCategory.StyleCopCSharpSpacingRules;
}
```

A `const` initialised from another `const` is still a compile-time constant, so
`StyleCopRule.SA1000.Category` remains valid as an attribute argument and still folds to
`"StyleCop.CSharp.SpacingRules"` in metadata. The indirection costs nothing.

`StyleCopCategory` is also usable on its own — IntelliSense on it lists exactly the 8 categories
this analyzer actually uses.

## How it is produced

Not transcribed from documentation. The generator loads the analyzer assemblies,
constructs the analyzers they mark with `[DiagnosticAnalyzer]`, and reads the `DiagnosticDescriptor`
instances they actually declare — the only source that cannot have drifted.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package StyleCop.Analyzers.Unstable --package-version latest \
    --namespace DiagnosticCatalog.StyleCop --container StyleCopRule \
    --output src/DiagnosticCatalog.StyleCop/StyleCopRules.g.cs
```

## How it stays current

A nightly workflow regenerates every catalogue from its upstream package and opens a
pull request when something actually moved — added rules, recategorised rules, rules
retired upstream. It never publishes: a category or an id that changed upstream changes
a published contract, and since the platform never reads a suppression's category, a
wrong value merged unreviewed would produce no symptom anywhere. A human reads the diff.

Nights where upstream has not moved produce nothing at all: the generator compares its
own previous output and leaves the file untouched, `generatedOn` included.

**A rule retired upstream is never deleted.** It is kept and marked `[Obsolete]` naming
the version that dropped it, so a project still referencing it gets a `CS0618` warning
telling it to remove the suppression — rather than a hard error from a member that
vanished. Consumers inline constant values at their own compile time, so deleting one
breaks their recompilation.

To regenerate every catalogue at once:

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate --manifest eng/catalogs.json
```

## How it reaches nuget.org

This catalogue rides the `stylecop` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) and versions independently of
the foundation, so it can follow StyleCop.Analyzers' releases without dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes a `stylecop-vX.Y.Z` tag, and the
release workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) with signed build provenance — no long-lived API key exists
anywhere to leak. The packaging half of that pipeline is rehearsed on every pull request, so
a release never exercises it for the first time on a tag.

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a
constant. This package covers the `SAxxxx` analyzer rules only.

## See also

Six sibling catalogues are generated from this repository the same way, each read from one
analyzer's own descriptors:

- [`DiagnosticCatalog.Sonar`](https://www.nuget.org/packages/DiagnosticCatalog.Sonar)
  — the SonarAnalyzer.CSharp (`Sxxxx`) rules.
- [`DiagnosticCatalog.NetAnalyzers`](https://www.nuget.org/packages/DiagnosticCatalog.NetAnalyzers)
  — the .NET code analysis (`CAxxxx`) rules.
- [`DiagnosticCatalog.CodeStyle`](https://www.nuget.org/packages/DiagnosticCatalog.CodeStyle)
  — the Roslyn IDE code-style (`IDExxxx`) rules.
- [`DiagnosticCatalog.Xunit`](https://www.nuget.org/packages/DiagnosticCatalog.Xunit)
  — the xunit.analyzers (`xUnitxxxx`) rules.
- [`DiagnosticCatalog.NUnit`](https://www.nuget.org/packages/DiagnosticCatalog.NUnit)
  — the NUnit.Analyzers (`NUnitxxxx`) rules.
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

Apache-2.0. The rule identifiers, categories and help links are facts about a
third-party analyzer, which is itself MIT-licensed.
