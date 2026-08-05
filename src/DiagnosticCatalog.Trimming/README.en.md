# DiagnosticCatalog.Trimming

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Trimming/README.fr.md)

The **trimming, Native AOT and single-file warnings** (`ILxxxx`) as strongly referenced constants,
so that `UnconditionalSuppressMessageAttribute` takes compile-checked references instead of magic
strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `Microsoft.NET.ILLink.Tasks 10.0.10`
>
> **77 rules, 3 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-08-05.
<!-- mirror:end -->

> Unofficial. Not affiliated with, endorsed by, or supported by Microsoft.

## Why this catalogue is not like the others

Every other catalogue in this family exists because **nothing reads a suppression's category**. Get
it wrong and no error, no warning and no failing test will ever tell you.

This one is the opposite case, and it is worse. `UnconditionalSuppressMessageAttribute` **is**
parsed — by two different decoders, with two different rules — and an identifier neither of them
accepts is discarded in silence. The linker's decoder is exact about it:

```csharp
if (!(attribute.ConstructorArguments[1].Value is string warningId)
    || warningId.Length < 6
    || !warningId.StartsWith("IL")
    || !int.TryParse(warningId.AsSpan(2, 4), out info.Id))
```

Anything that is not `IL####` is ignored outright. The compile-time trim analyzer implements its
*own* rule — truncate at the first colon, then match exactly — so the two do not even agree on what
they accept.

And unlike a mis-categorised `[SuppressMessage]`, the consequence here is not a warning that quietly
stays. A suppression the linker discarded means the warning it was meant to silence was never
silenced, so the pattern it was covering gets trimmed away — and you find out as a
`TypeLoadException` in production, on a code path nobody exercised before publishing.

## Why you have these warnings, and probably did not ask for them

`PublishTrimmed` and `PublishAot` are opt-in — except that several SDKs set them for you:

| You are building | Trimming analyzer |
| --- | --- |
| **Blazor WebAssembly** | **On, every build.** `Microsoft.NET.Sdk.BlazorWebAssembly` sets `PublishTrimmed` in its own props |
| **MAUI** on iOS/Android | On, Release |
| Anything with `PublishAot` | On — AOT implies trimming |
| A library declaring `IsTrimmable` | On, even though you never publish trimmed yourself |
| An ordinary console, service, or web app | Off, unless you asked |

The switch is not the publish command; it is a **project property**, so
`Microsoft.NET.Sdk.Analyzers.targets` turns `EnableTrimAnalyzer` on at build time. A Blazor
WebAssembly developer sees `IL2026` on every `dotnet build`, having chosen nothing — which is the
same shape as the Roslyn IDE rules under `EnforceCodeStyleInBuild`.

## The two attributes, and which one you need

This is the part worth getting right, because the catalogue serves both and they are not
interchangeable.

```csharp
// Silencing the COMPILE-TIME analyzer — the IL2026 in your build output.
[SuppressMessage(TrimRule.IL2026.Category, TrimRule.IL2026.Id, Justification = "…")]

// Silencing the LINKER, which reads the compiled assembly long after the compiler is gone.
[UnconditionalSuppressMessage(TrimRule.IL2026.Category, TrimRule.IL2026.Id, Justification = "…")]
```

`SuppressMessageAttribute` carries `[Conditional("CODE_ANALYSIS")]`, so it is **not preserved in the
compiled assembly**. ILLink and ILCompiler run after compilation and read suppressions out of IL —
they cannot see it. That is the entire reason `UnconditionalSuppressMessageAttribute` exists: same
shape, no `[Conditional]`, so it survives.

Use the unconditional one when the warning must stay silenced through publish. Reach both through
this catalogue and the identifier is checked either way.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.Trimming" Version="1.0.0" />
```

This package only supplies the constants. The checks that validate rule declarations and their use
sites ship separately in `DiagnosticCatalog.Analyzers` — including **`DCAT0009`**, which reports an
`UnconditionalSuppressMessage` whose identifier is not `IL####`. That diagnostic shipped before this
catalogue did: the check existed, and there were no constants to feed it.

## What is in the package

77 rules across 3 categories, and **not one of them carries a help link** — the analyzer declares
none. There is nothing to click through to, which is exactly where a catalogue earns its keep: the
documentation comment on each constant is the only place the rule's own wording is available at the
point of use.

| Category | Rules | What they are about |
| --- | --- | --- |
| `Trimming` | 64 | Reflection the trimmer cannot follow — the `IL2xxx` range |
| `AOT` | 7 | Code that needs runtime code generation, plus the `FeatureGuard` rules (`IL3050`, `IL4000`) |
| `SingleFile` | 6 | Assembly file paths that do not exist in a single-file bundle (`IL300x`) |

```csharp
[DiagnosticRule]
public static class IL2026
{
    public const string Id = nameof(IL2026);
    public const string Category = TrimCategory.Trimming;
}
```

## Categories declared once

`TrimCategory` holds each category once, and the rules reference it — so a category's spelling
exists in exactly one place. It is **internal by design**: a suppression reaches a category through
the rule that carries it, `TrimRule.IL2026.Category`, and never through the category constant on its
own. The two fold to the same string today and stop agreeing the day a rule moves
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).

## How it is produced

Not transcribed from documentation. The generator reads the analyzer assemblies' metadata for the
types they mark with `[DiagnosticAnalyzer]`, constructs those, and reads the `DiagnosticDescriptor`
instances they actually declare — the only source that cannot have drifted. The analyzer ships
inside `Microsoft.NET.ILLink.Tasks`, the same package the SDK restores when you publish trimmed.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.NET.ILLink.Tasks --package-version latest \
    --namespace DiagnosticCatalog.Trimming --container TrimRule \
    --output src/DiagnosticCatalog.Trimming/TrimRules.g.cs
```

## How it stays current

A nightly workflow regenerates every catalogue from its upstream package and opens a pull request
when something actually moved — added rules, recategorised rules, rules retired upstream. It never
publishes: a category or an id that changed upstream changes a published contract, and a wrong value
merged unreviewed would produce no symptom anywhere. A human reads the diff.

**A rule retired upstream is never deleted.** It is kept and marked `[Obsolete]` naming the version
that dropped it, so a project still referencing it gets a `CS0618` warning telling it to remove the
suppression — rather than a hard error from a member that vanished. Consumers inline constant values
at their own compile time, so deleting one breaks their recompilation.

## How it reaches nuget.org

This catalogue rides the `trimming` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md)
and versions independently of the foundation, so it can follow the SDK's ILLink releases without
dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes a `trimming-vX.Y.Z` tag, and the release
workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) with
signed build provenance — no long-lived API key exists anywhere to leak.

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a constant. This
package covers the `ILxxxx` analyzer rules only.

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
- [`DiagnosticCatalog.AspNetCore`](https://www.nuget.org/packages/DiagnosticCatalog.AspNetCore)
  — the ASP.NET Core and Blazor (`ASPxxxx`, `BLxxxx`) rules.
- [`DiagnosticCatalog.Syslib`](https://www.nuget.org/packages/DiagnosticCatalog.Syslib)
  — the .NET runtime source-generator (`SYSLIB1xxx`) diagnostics.
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
is the normative version of all of it — §9.1 is the one about this attribute.

## License

Apache-2.0. The rule identifiers, categories and titles are read from a Microsoft analyzer, which is
itself MIT-licensed.
