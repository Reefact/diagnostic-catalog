# DiagnosticCatalog.Xunit

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Xunit/README.fr.md)

The **xunit.analyzers** rules as strongly referenced constants, so that
`SuppressMessageAttribute` takes compile-checked references instead of magic strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `xunit.analyzers 1.27.0`
>
> **90 rules, 3 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-08-05.
<!-- mirror:end -->

> Unofficial. Not affiliated with, endorsed by, or supported by the xUnit.net project.

## Why

Every xUnit test project already runs these analyzers, and almost nobody installed them on
purpose: `xunit` depends on `xunit.analyzers`, so they arrive with the test framework. That
is what makes their rules the ones people actually suppress **in source** — a test that
deliberately asserts on a literal, a theory whose data cannot be inlined, an assertion the
analyzer would rather see written another way. These are local exceptions with a reason,
which is a suppression's job rather than an `.editorconfig` entry's.

```csharp
[SuppressMessage("Assertions", "xUnit2013:Do not use equality check to check for collection size", ...)]
```

Three strings, and nothing checks any of them. Get the id wrong and the suppression silently
does nothing — the warning simply stays. Get the category wrong and **nothing happens at
all**, ever: the .NET platform never reads that argument, so no error, no warning and no
failing test will tell you. Would you have known that `xUnit2013` is `"Assertions"` while
`xUnit1013` is `"Usage"` and `xUnit3000` is `"Extensibility"`?

```csharp
using DiagnosticCatalog.Xunit;

[SuppressMessage(
    XunitRule.xUnit2013.Category,
    XunitRule.xUnit2013.Id,
    Justification = "The count is the subject of this test.")]
```

The day a rule moves to another category, the second version follows it and the first is left
naming a category the rule no longer carries — silently, and for as long as the line survives.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.Xunit" Version="1.0.0" />
```

This package only supplies the constants. The checks that validate rule declarations and
their use sites ship separately in `DiagnosticCatalog.Analyzers`.

## What is in the package

90 rules across 3 categories, and it is the tidiest of the catalogues here: every rule
carries the title its descriptor declares, and **every one of the 90 carries a help link**
into xunit.net's own rule pages.

| Category | Rules | What they are about |
| --- | --- | --- |
| `Usage` | 54 | How tests, theories and their data are declared — the `xUnit1xxx` range |
| `Assertions` | 32 | Assertions that would read better written another way — `xUnit2xxx` |
| `Extensibility` | 4 | Extending the framework itself — `xUnit3xxx` |

```csharp
[DiagnosticRule]
public static class xUnit2013
{
    public const string Id = nameof(xUnit2013);
    public const string Category = XunitCategory.Assertions;
    public const string HelpLinkUri = "https://xunit.net/xunit.analyzers/rules/xUnit2013";
}
```

The identifiers keep the vendor's own casing, `xUnit2013` and not `XUnit2013`, because a
catalogue's member name is the identifier a suppression carries — renaming it to suit C#
convention would make the constant and the string it stands for disagree.

## A note on how you already have these analyzers

You almost certainly do not need to install `xunit.analyzers`: `xunit` depends on it, so a
test project has the rules whether or not anybody asked. This catalogue names them; where
they come from is your test project's business.

That transitive arrival is also why this catalogue exists. A rule you chose to switch on gets
tuned in `.editorconfig`; a rule that arrives with the framework gets suppressed at the one
place it is wrong, with a `Justification` beside the test that earns it.

## Categories declared once

`XunitCategory` holds each category once, and the rules reference it — so a category's
spelling exists in exactly one place. It is **internal by design**: a suppression reaches a
category through the rule that carries it, `XunitRule.xUnit2013.Category`, and never through
the category constant on its own. The two fold to the same string today and stop agreeing the
day xUnit moves the rule
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).

## How it is produced

Not transcribed from documentation. The generator reads the analyzer assemblies' metadata for
the types they mark with `[DiagnosticAnalyzer]`, constructs those, and reads the
`DiagnosticDescriptor` instances they actually declare — the only source that cannot have
drifted.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package xunit.analyzers --package-version latest \
    --namespace DiagnosticCatalog.Xunit --container XunitRule \
    --output src/DiagnosticCatalog.Xunit/XunitRules.g.cs
```

## How it stays current

A nightly workflow regenerates every catalogue from its upstream package and opens a
pull request when anything the catalogue publishes has moved. It never publishes: a category or an id that changed upstream changes
a published contract, and since the platform never reads a suppression's category, a
wrong value merged unreviewed would produce no symptom anywhere. A human reads the diff.

**A rule retired upstream is never deleted.** It is kept and marked `[Obsolete]` naming
the version that dropped it, so a project still referencing it gets a `CS0618` warning
telling it to remove the suppression — rather than a hard error from a member that
vanished. Consumers inline constant values at their own compile time, so deleting one
breaks their recompilation.

## How it reaches nuget.org

This catalogue rides the `xunit` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md)
and versions independently of the foundation, so it can follow xunit.analyzers' releases
without dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes an `xunit-vX.Y.Z` tag, and the
release workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
with signed build provenance — no long-lived API key exists anywhere to leak.

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a
constant. This package covers the `xUnitxxxx` analyzer rules only.

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
third-party analyzer, which is itself Apache-2.0 licensed.
