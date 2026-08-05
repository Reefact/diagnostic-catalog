# DiagnosticCatalog.NUnit

The **NUnit.Analyzers** rules as strongly referenced constants, so that
`SuppressMessageAttribute` takes compile-checked references instead of magic strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `NUnit.Analyzers 4.14.0`
>
> **99 rules, 3 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-08-05.
<!-- mirror:end -->

> Unofficial. Not affiliated with, endorsed by, or supported by the NUnit project.

## Why

Almost every NUnit test project runs these analyzers without anybody having decided to:
`dotnet new nunit` writes `NUnit.Analyzers` into the project file it generates, beside
`NUnit` itself. They are not a transitive dependency — `NUnit` declares none — they simply
arrive with the template and stay.

That is what makes their rules the ones people suppress **in source**. A rule you switched
on gets tuned in `.editorconfig`; a rule that came with the template gets an exception at
the one place it is wrong, with a `Justification` beside the test that earns it.

```csharp
[SuppressMessage("Assertion", "NUnit2007:The actual value should not be a constant", ...)]
```

Three strings, and nothing checks any of them. Get the id wrong and the suppression silently
does nothing — the warning simply stays. Get the category wrong and **nothing happens at
all**, ever: the .NET platform never reads that argument, so no error, no warning and no
failing test will tell you. Would you have known that NUnit's category is `"Assertion"`
singular, where xUnit's is `"Assertions"` plural?

```csharp
using DiagnosticCatalog.NUnit;

[SuppressMessage(
    NUnitRule.NUnit2007.Category,
    NUnitRule.NUnit2007.Id,
    Justification = "The constant is the subject of this test.")]
```

The day a rule moves to another category, the second version follows it and the first keeps
compiling while it quietly stops matching.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.NUnit" Version="1.0.0" />
```

This package only supplies the constants. The checks that validate rule declarations and
their use sites ship separately in `DiagnosticCatalog.Analyzers`.

## What is in the package

99 rules across 3 categories, and **every one of the 99 carries both the title its descriptor
declares and a help link** into NUnit's own rule pages — a completeness only the xUnit
catalogue here matches.

| Category | Rules | What they are about |
| --- | --- | --- |
| `Assertion` | 59 | Assertions, and the classic-model constraints that replace them — the `NUnit2xxx` range |
| `Structure` | 38 | How tests, fixtures and their parameters are declared — `NUnit1xxx` |
| `Style` | 2 | How an assertion is written rather than whether it is right — `NUnit3xxx` |

```csharp
[DiagnosticRule]
public static class NUnit2007
{
    public const string Id = nameof(NUnit2007);
    public const string Category = NUnitCategory.Assertion;
    public const string HelpLinkUri = "https://github.com/nunit/nunit.analyzers/tree/master/documentation/NUnit2007.md";
}
```

## The category is `Assertion`, not `Assertions`

If you also run xUnit somewhere, this is the trap worth naming. xUnit files its assertion
rules under `"Assertions"` and NUnit files its own under `"Assertion"` — one letter apart,
on two analyzers a solution may well run side by side. Nothing in the platform reads that
argument, so getting it wrong costs no error and no warning, ever. Reaching it through
`NUnitRule.NUnit2007.Category` removes the question.

## Categories declared once

`NUnitCategory` holds each category once, and the rules reference it — so a category's
spelling exists in exactly one place. It is **internal by design**: a suppression reaches a
category through the rule that carries it, `NUnitRule.NUnit2007.Category`, and never through
the category constant on its own. The two fold to the same string today and stop agreeing the
day NUnit moves the rule
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).

## How it is produced

Not transcribed from documentation. The generator reads the analyzer assemblies' metadata for
the types they mark with `[DiagnosticAnalyzer]`, constructs those, and reads the
`DiagnosticDescriptor` instances they actually declare — the only source that cannot have
drifted.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package NUnit.Analyzers --package-version latest \
    --namespace DiagnosticCatalog.NUnit --container NUnitRule \
    --output src/DiagnosticCatalog.NUnit/NUnitRules.g.cs
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

This catalogue rides the `nunit` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md)
and versions independently of the foundation, so it can follow NUnit.Analyzers' releases
without dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes an `nunit-vX.Y.Z` tag, and the
release workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
with signed build provenance — no long-lived API key exists anywhere to leak.

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a
constant. This package covers the `NUnitxxxx` analyzer rules only.

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
- [`DiagnosticCatalog.MSTest`](https://www.nuget.org/packages/DiagnosticCatalog.MSTest)
  — the MSTest.Analyzers (`MSTESTxxxx`) rules.
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
