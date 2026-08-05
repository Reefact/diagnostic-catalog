# DiagnosticCatalog.CodeStyle

The **Roslyn IDE code-style rules** as strongly referenced constants, so that
`SuppressMessageAttribute` takes compile-checked references instead of magic strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `Microsoft.CodeAnalysis.CSharp.CodeStyle 5.6.0`
>
> **120 rules, 3 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-08-05.
<!-- mirror:end -->

> Unofficial. Not affiliated with, endorsed by, or supported by Microsoft.

## Why

The `IDExxxx` rules are the ones most projects meet first, because they arrive with the
.NET SDK rather than with a package: turn on `EnforceCodeStyleInBuild`, give a rule a
severity in `.editorconfig`, and it starts failing builds. What almost nobody knows is
which **category** each one belongs to.

```csharp
[SuppressMessage("Style", "IDE0008:Use explicit type", Justification = "...")]
```

Three strings, and nothing checks any of them. Get the id wrong and the suppression
silently does nothing — the warning simply stays. Get the category wrong and **nothing
happens at all**, ever: the .NET platform never reads that argument, so no error, no
warning and no failing test will tell you. Would you have known that `IDE0008` is
`"Style"` but `IDE0076` is `"CodeQuality"`, and that `IDE0043` is `"Compiler"`?

```csharp
using DiagnosticCatalog.CodeStyle;

[SuppressMessage(
    CodeStyleRule.IDE0008.Category,
    CodeStyleRule.IDE0008.Id,
    Justification = "The generated shape is clearer with var here.")]
```

The day a rule moves to another category, the second version follows it and the first
keeps compiling while it quietly stops matching.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.CodeStyle" Version="1.0.0" />
```

This package only supplies the constants. The checks that validate rule declarations and
their use sites ship separately in `DiagnosticCatalog.Analyzers`.

## What is in the package

120 rules across 3 categories:

| Category | Rules |
| --- | --- |
| `Style` | 116 |
| `CodeQuality` | `IDE0064`, `IDE0076`, `IDE0077` |
| `Compiler` | `IDE0043` |

119 of the 120 carry the title their descriptor declares; `RemoveUnnecessaryImportsFixable`
declares none, so it documents itself with its identifier and category instead. 117 carry a
help link, 116 of them into Microsoft's style-rule reference and one — `EnableGenerateDocumentationFile`
— into the Roslyn issue that tracks it:

```csharp
[DiagnosticRule]
public static class IDE0008
{
    public const string Id = nameof(IDE0008);
    public const string Category = CodeStyleCategory.Style;
    public const string HelpLinkUri =
        "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0008";
}
```

**Three identifiers are not of the `IDExxxx` shape**, and they are here because the
analyzers declare them: `IDE0005_gen` (the generated-code half of *using directive is
unnecessary*), `EnableGenerateDocumentationFile` (*set MSBuild property
`GenerateDocumentationFile` to `true`*, which is what makes `IDE0005` work in a build) and
`RemoveUnnecessaryImportsFixable`. A catalogue reports what its upstream declares rather
than what would look tidy.

**`IDE0079` is not here, and its absence is deliberate.** *Remove unnecessary suppression*
is declared by an analyzer that carries no `[DiagnosticAnalyzer]` attribute: the IDE drives
it through a separate interface, and no compiler ever loads it — with the rule set to
`warning` and code-style enforcement on, a build reports it on an unnecessary suppression
not at all. A catalogue exists to make a suppression's arguments checkable, and a rule no
build can raise is a reference this package cannot make mean anything
([ADR-0031](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0031-find-analyzers-the-way-the-compiler-finds-them.en.md)).

## A note on versions

`Microsoft.CodeAnalysis.CSharp.CodeStyle` is versioned with the **compiler**, not with the
SDK. A release that declares a newer compiler than the one running is refused outright:

```
warning CS9057: Analyzer assembly '...' cannot be used because it references version
'5.6.0.0' of the compiler, which is newer than the currently running version '5.0.0.0'.
```

That constrains which release *you* can install, and it is why this catalogue rides a train
of its own rather than sharing anyone else's pace. It does not constrain what is read here:
the generator reads descriptors and runs no analyzer, so a catalogue can mirror a release
that your compiler would decline to load.

**You most likely do not need that package at all.** The same analyzers reach almost every
project through the .NET SDK, where `EnforceCodeStyleInBuild` turns them on and
`.editorconfig` sets their severity. This catalogue names the rules; where they come from is
your build's business.

## Categories declared once

`CodeStyleCategory` holds each category once, and the rules reference it — so a category's
spelling exists in exactly one place. It is **internal by design**: a suppression reaches a
category through the rule that carries it, `CodeStyleRule.IDE0008.Category`, and never through
the category constant on its own. The two fold to the same string today and stop agreeing
the day Roslyn moves the rule
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).

## How it is produced

Not transcribed from documentation. The generator reads the analyzer assemblies' metadata
for the types they mark with `[DiagnosticAnalyzer]`, constructs those, and reads the
`DiagnosticDescriptor` instances they actually declare — the only source that cannot have
drifted.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.CodeAnalysis.CSharp.CodeStyle --package-version latest \
    --namespace DiagnosticCatalog.CodeStyle --container CodeStyleRule \
    --output src/DiagnosticCatalog.CodeStyle/CodeStyleRules.g.cs
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

This catalogue rides the `codestyle` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md)
and versions independently of the foundation, so it can follow Roslyn's releases without
dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes a `codestyle-vX.Y.Z` tag, and the
release workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
with signed build provenance — no long-lived API key exists anywhere to leak.

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a
constant. The `Compiler` category above is not an exception to that: `IDE0043` is an
analyzer rule that Roslyn files under that category, not a `CSxxxx` diagnostic.

Many `IDExxxx` rules are configured in `.editorconfig` rather than suppressed in source,
and that is usually the better tool: a severity applies to a whole project, where a
suppression applies to one member. This package is for the cases where the exception is
local and deserves a `Justification` next to the code.

## See also

Ten sibling catalogues are generated from this repository the same way, each read from one
analyzer's own descriptors:

- [`DiagnosticCatalog.Sonar`](https://www.nuget.org/packages/DiagnosticCatalog.Sonar)
  — the SonarAnalyzer.CSharp (`Sxxxx`) rules.
- [`DiagnosticCatalog.NetAnalyzers`](https://www.nuget.org/packages/DiagnosticCatalog.NetAnalyzers)
  — the .NET code analysis (`CAxxxx`) rules.
- [`DiagnosticCatalog.StyleCop`](https://www.nuget.org/packages/DiagnosticCatalog.StyleCop)
  — the StyleCop.Analyzers (`SAxxxx`) rules.
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
