# DiagnosticCatalog.NetAnalyzers

The **.NET code analysis (CA) rules** as strongly referenced constants, so that
`SuppressMessageAttribute` takes compile-checked references instead of magic strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `Microsoft.CodeAnalysis.NetAnalyzers 10.0.302`
>
> **318 rules, 10 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-07-31.
<!-- mirror:end -->

## Why

Both arguments of a suppression are magic strings, and nothing validates either:

```csharp
[SuppressMessage("Performance", "CA1822", Justification = "...")]
```

Get the id wrong and the suppression silently does nothing — the warning simply stays.
Get the category wrong and **nothing happens at all**, ever: the .NET platform never
reads that argument, so no build, test or tool will tell you.

```csharp
using DiagnosticCatalog.NetAnalyzers;

[SuppressMessage(
    NetAnalyzersRule.CA1822.Category,
    NetAnalyzersRule.CA1822.Id,
    Justification = "Kept as an instance member for the public API contract.")]
```

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.NetAnalyzers" Version="0.1.0" />
```

This package only supplies the constants. The checks that validate rule declarations and
their use sites ship separately in `DiagnosticCatalog.Analyzers`.

## What is in the package

318 rules across 10 categories: `Design`, `Documentation`, `Globalization`,
`Interoperability`, `Maintainability`, `Naming`, `Performance`, `Reliability`,
`Security`, `Usage`.

Every rule carries its help link, because the .NET analyzers populate `HelpLinkUri` on
all 318 descriptors:

```csharp
[DiagnosticRule]
public static class CA1822
{
    public const string Id = nameof(CA1822);
    public const string Category = NetAnalyzersCategory.Performance;
    public const string HelpLinkUri = "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1822";
}
```

Each rule carries its upstream title as a documentation comment, read from the
descriptor in the invariant culture so the generated file does not depend on the machine
that produced it. Rule descriptions are not redistributed — the help link takes you to
them.

## A note on versions

Unlike Sonar or StyleCop, the CA analyzers are **built into the .NET SDK**. Which rules
your project actually gets is governed by your SDK version and by `AnalysisLevel`, not by
a `PackageReference` you control. This catalogue mirrors
`Microsoft.CodeAnalysis.NetAnalyzers 10.0.302`, the latest stable release at generation
time; the assembly records that exactly:

```csharp
[assembly: CatalogSource(
    source:        "Microsoft.CodeAnalysis.NetAnalyzers",
    sourceVersion: "10.0.302",
    generatedOn:   "2026-07-30")]
```

If your SDK ships a newer analyzer set, rules added since 10.0.302 will not be here yet.

## Categories declared once

A catalogue repeats very few distinct categories across very many rules. Each one is
declared once in `NetAnalyzersCategory` and the rules refer to it, so there is a single source per value:

```csharp
[DiagnosticCategory]
public static class NetAnalyzersCategory
{
    public const string Performance = "Performance";
}

[DiagnosticRule]
public static class CA1822
{
    public const string Id = nameof(CA1822);
    public const string Category = NetAnalyzersCategory.Performance;
}
```

A `const` initialised from another `const` is still a compile-time constant, so
`NetAnalyzersRule.CA1822.Category` remains valid as an attribute argument and still folds to
`"Performance"` in metadata. The indirection costs nothing.

`NetAnalyzersCategory` is also usable on its own — IntelliSense on it lists exactly the 10 categories
this analyzer actually uses.

## How it is produced

Not transcribed from documentation. The generator loads the analyzer assemblies,
constructs every `DiagnosticAnalyzer` they contain, and reads the `DiagnosticDescriptor`
instances they actually declare — the only source that cannot have drifted.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.CodeAnalysis.NetAnalyzers --package-version latest \
    --namespace DiagnosticCatalog.NetAnalyzers --container NetAnalyzersRule \
    --output src/DiagnosticCatalog.NetAnalyzers/NetAnalyzersRules.g.cs
```

The package splits its analyzers across `analyzers/dotnet/` (language-neutral, most of
the rules), `analyzers/dotnet/cs/` and `analyzers/dotnet/vb/`. The generator takes the
first two and excludes the third, so no Visual Basic rule leaks into a C# catalogue.

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

This catalogue rides the `netanalyzers` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) and versions independently of
the foundation, so it can follow the .NET SDK's analyzer releases without dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes a `netanalyzers-vX.Y.Z` tag, and the
release workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) with signed build provenance — no long-lived API key exists
anywhere to leak. The packaging half of that pipeline is rehearsed on every pull request, so
a release never exercises it for the first time on a tag.

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a
constant. This package covers the `CAxxxx` analyzer rules only, not `CSxxxx` or `IDExxxx`.

## See also

Three sibling catalogues are generated from this repository the same way, each read from one
analyzer's own descriptors:

- [`DiagnosticCatalog.Sonar`](https://www.nuget.org/packages/DiagnosticCatalog.Sonar)
  — the SonarAnalyzer.CSharp (`Sxxxx`) rules.
- [`DiagnosticCatalog.StyleCop`](https://www.nuget.org/packages/DiagnosticCatalog.StyleCop)
  — the StyleCop.Analyzers (`SAxxxx`) rules.
- [`DiagnosticCatalog.Self`](https://www.nuget.org/packages/DiagnosticCatalog.Self)
  — this library's own `DCATxxxx` rules, for suppressing a diagnostic the catalogue analyzers
  themselves report.

**Want a catalogue of your own?** Your analyzer's rules, or an internal ruleset, are declared
exactly the way these are: a static class of constants marked `[DiagnosticRule]`, referenced by
consumers instead of retyped. That marker ships in
[`DiagnosticCatalog`](https://www.nuget.org/packages/DiagnosticCatalog), the foundation this
catalogue is built on, and its README is the guide.

## Documentation

[Specification](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.en.md)
([français](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.fr.md)).

## License

Apache-2.0. The rule identifiers, categories and help links are facts about a
third-party analyzer, which is itself MIT-licensed.
