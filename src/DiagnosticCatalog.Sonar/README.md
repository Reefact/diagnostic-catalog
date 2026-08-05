# DiagnosticCatalog.Sonar

The **SonarAnalyzer.CSharp** rules as strongly referenced constants, so that
`SuppressMessageAttribute` takes compile-checked references instead of magic strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `SonarAnalyzer.CSharp 10.31.0.145097`
>
> **456 rules, 13 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-07-31.
<!-- mirror:end -->

> Unofficial. Not affiliated with, endorsed by, or supported by SonarSource. "Sonar" and
> "SonarQube" are trademarks of SonarSource S.A.

## Why

Both arguments of a Sonar suppression are magic strings, and neither is checked:

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "...")]
```

Get the id wrong and the suppression silently does nothing — the warning simply stays.
Get the category wrong and **nothing happens at all**, forever: the .NET platform never
reads that argument, so no build, test or tool will ever tell you. And you would not
guess it: `S1144`'s category is `"Major Code Smell"`, not `"Code Smell"`, not
`"Maintainability"`.

```csharp
using DiagnosticCatalog.Sonar;

[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Instantiated through reflection by the DI container.")]
```

A rule that upstream renames or retires now breaks the build instead of leaving a dead
suppression behind.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="0.1.0" />
```

This package only supplies the constants. The checks that validate rule declarations and
their use sites ship separately in `DiagnosticCatalog.Analyzers`.

## Usage

```csharp
using System.Diagnostics.CodeAnalysis;
using DiagnosticCatalog.Sonar;

public sealed class Repository
{
    [SuppressMessage(
        SonarRule.S1481.Category,
        SonarRule.S1481.Id,
        Justification = "Placeholder retained for the upcoming migration step.")]
    public int Compute()
    {
        int unused = 42;
        return 1;
    }
}
```

Type `SonarRule.` and IntelliSense lists every rule; type `S1481` and it filters to it.

## What is in the package

456 rules, covering 13 categories:

| | Blocker | Critical | Major | Minor | Info |
| --- | --- | --- | --- | --- | --- |
| Bug | ✓ | ✓ | ✓ | ✓ | |
| Code Smell | ✓ | ✓ | ✓ | ✓ | ✓ |
| Vulnerability | ✓ | ✓ | ✓ | ✓ | |

Each rule exposes exactly the two mandatory constants — Sonar's descriptors carry no help
links (0 of 465), so none are invented here:

```csharp
[DiagnosticRule]
public static class S1144
{
    public const string Id = nameof(S1144);
    public const string Category = SonarCategory.MajorCodeSmell;
}
```

**Ids, categories and titles.** All three are read from the analyzers themselves. The
title is SonarSource's own sentence, carried as the rule's documentation comment so that
hovering a constant says what the rule is about. Rule descriptions are their
documentation and are deliberately not redistributed here — follow the rule id to
[rules.sonarsource.com](https://rules.sonarsource.com/csharp/) for those.

## Categories declared once

A catalogue repeats very few distinct categories across very many rules. Each one is
declared once in `SonarCategory` and the rules refer to it, so there is a single source per value:

```csharp
[DiagnosticCategory]
public static class SonarCategory
{
    public const string MajorCodeSmell = "Major Code Smell";
}

[DiagnosticRule]
public static class S1144
{
    public const string Id = nameof(S1144);
    public const string Category = SonarCategory.MajorCodeSmell;
}
```

A `const` initialised from another `const` is still a compile-time constant, so
`SonarRule.S1144.Category` remains valid as an attribute argument and still folds to
`"Major Code Smell"` in metadata. The indirection costs nothing.

`SonarCategory` is also usable on its own — IntelliSense on it lists exactly the 13 categories
this analyzer actually uses.

## How it is produced

Not transcribed from documentation, and not from rule-metadata JSON. The generator loads
`SonarAnalyzer.CSharp`, constructs the analyzers it marks with `[DiagnosticAnalyzer]`, and reads the
`DiagnosticDescriptor` instances they actually declare. That is the only source that
cannot be wrong — and because the platform never validates a category, a value copied
from documentation that had drifted would produce no symptom anywhere.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package SonarAnalyzer.CSharp --package-version latest \
    --namespace DiagnosticCatalog.Sonar --container SonarRule \
    --output src/DiagnosticCatalog.Sonar/SonarRules.g.cs
```

Nine entries are deliberately excluded: `S9999-cpd`, `S9999-log`, `S9999-metadata`,
`S9999-metrics`, `S9999-symbolRef`, `S9999-telemetry`, `S9999-testMethodDeclaration`,
`S9999-token-type` and `S9999-warning`. They carry an empty category because they are
internal metrics and telemetry channels rather than suppressable diagnostics.

## Which upstream release this mirrors

The assembly records its own provenance, readable from metadata without the source:

```csharp
[assembly: CatalogSource(
    source:        "SonarAnalyzer.CSharp",
    sourceVersion: "10.31.0.145097",
    generatedOn:   "2026-07-30")]
```

```csharp
var source = typeof(SonarRule.S1144).Assembly
    .GetCustomAttributes<CatalogSourceAttribute>()
    .Single();
// source.SourceVersion => "10.31.0.145097"
// source.GeneratedOn   => "2026-07-30"
```

A catalogue is a snapshot: upstream adds, retires and recategorises rules with every
release. If your project references a much newer `SonarAnalyzer.CSharp` than
`SourceVersion` says, this catalogue may be missing rules.

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

This catalogue rides the `sonar` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) and versions independently of
the foundation, so it can follow SonarSource's release pace without dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes a `sonar-vX.Y.Z` tag, and the
release workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) with signed build provenance — no long-lived API key exists
anywhere to leak. The packaging half of that pipeline is rehearsed on every pull request, so
a release never exercises it for the first time on a tag.

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a
constant. This package covers Sonar's `Sxxxx` analyzer rules only.

## See also

Six sibling catalogues are generated from this repository the same way, each read from one
analyzer's own descriptors:

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

Apache-2.0. The rule identifiers and categories are facts about a third-party analyzer;
this package is not a derivative of SonarSource's rule descriptions.
