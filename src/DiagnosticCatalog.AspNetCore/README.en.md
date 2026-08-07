# DiagnosticCatalog.AspNetCore

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.AspNetCore/README.fr.md)

The **ASP.NET Core and Blazor** analyzer rules (`ASPxxxx`, `BLxxxx`) as strongly referenced
constants, so that `SuppressMessageAttribute` takes compile-checked references instead of magic
strings.

<!-- mirror:begin -->
> ## 🪞 Mirrors `Microsoft.AspNetCore.App.Ref 10.0.10`
>
> **35 rules, 3 categories**, every identifier and category read
> from that release's own analyzers. Regenerated 2026-08-05.
<!-- mirror:end -->

> Unofficial. Not affiliated with, endorsed by, or supported by Microsoft.

## Why

Every ASP.NET Core project runs these analyzers, and **nobody installed them** — not because people
do not bother, but because there is nothing to install. They arrive inside the shared framework, and
the web SDK references that framework. No `PackageReference` names them, and none can be removed.

That is what makes their rules the ones people suppress **in source**. A rule you switched on gets
tuned in `.editorconfig`; a rule that came with the framework gets an exception at the one place it
is wrong, with a `Justification` beside the code that earns it.

```csharp
[SuppressMessage("Usage", "ASP0018:Unused route parameter", Justification = "…")]
```

Three strings, and nothing checks any of them. Get the id wrong and the suppression silently does
nothing — the warning simply stays. Get the category wrong and **nothing happens at all**, ever: the
.NET platform never reads that argument, so no error, no warning and no failing test will tell you.

```csharp
using DiagnosticCatalog.AspNetCore;

[SuppressMessage(
    AspNetCoreRule.ASP0018.Category,
    AspNetCoreRule.ASP0018.Id,
    Justification = "The parameter is read by the model binder, not by the handler.")]
```

The day a rule moves to another category, the second version follows it and the first is left
naming a category the rule no longer carries — silently, and for as long as the line survives.

## The one you do not want to get wrong

`ASP0026` is the only `Security` rule in the set, and it reports this:

> **`[Authorize]` overridden by `[AllowAnonymous]` from farther away.**

An `[AllowAnonymous]` on a base class or an outer scope silently wins over an `[Authorize]` written
closer to the endpoint — the opposite of what almost everyone reads the code to mean. If a project
ever suppresses that one, the suppression is load-bearing in the strongest sense, and the argument
naming its category is `"Security"` — a value nothing in the platform will ever check.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.AspNetCore" Version="1.0.0" />
```

That is the only reference you need. This package depends on `DiagnosticCatalog`, which carries
the `DCAT` analyzers and code fixes beside its attributes, so referencing this catalogue is what
switches on the checks that validate rule declarations and their use sites. A literal suppression
a catalogue reference would replace is an error by default, and a code fix rewrites it for you.

## What is in the package

35 rules across 3 categories, 26 of the 35 carrying the help link their descriptor declares.

| Category | Rules | What they are about |
| --- | --- | --- |
| `Usage` | 32 | Minimal APIs, routing, `WebApplicationBuilder` migration, header access, Blazor render trees |
| `Encapsulation` | 2 | Blazor component parameters that must be public, and settable (`BL0001`, `BL0004`) |
| `Security` | 1 | `ASP0026`, above |

**Two prefixes, one package.** `ASPxxxx` is ASP.NET Core proper — 26 rules, mostly minimal APIs and
routing. `BLxxxx` is Blazor components — 9 rules about parameters, render trees and persisted state.
They ship together in the framework, so they are catalogued together; the icon badge reads `ASP`
because a badge carries the majority prefix
([ADR-0032](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0032-badge-a-catalogues-icon-with-its-rule-prefix.en.md)).

```csharp
[DiagnosticRule]
public static class ASP0026
{
    public const string Id = nameof(ASP0026);
    public const string Category = AspNetCoreCategory.Security;
    public const string HelpLinkUri = "https://learn.microsoft.com/aspnet/core/diagnostics/asp0026";
}
```

## Categories declared once

`AspNetCoreCategory` holds each category once, and the rules reference it — so a category's spelling
exists in exactly one place. It is **internal by design**: a suppression reaches a category through
the rule that carries it, `AspNetCoreRule.ASP0026.Category`, and never through the category constant
on its own. The two fold to the same string today and stop agreeing the day a rule moves
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).

## How it is produced

Not transcribed from documentation. The generator reads the analyzer assemblies' metadata for the
types they mark with `[DiagnosticAnalyzer]`, constructs those, and reads the `DiagnosticDescriptor`
instances they actually declare — the only source that cannot have drifted.

The analyzers ship inside **`Microsoft.AspNetCore.App.Ref`**, the ASP.NET Core targeting pack, which
is an ordinary package on nuget.org — that is how the SDK itself acquires it. So the mirrored release
is a package version, one a consumer can look up and install, rather than whatever happened to be on
the machine that generated the file.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.AspNetCore.App.Ref --package-version latest \
    --namespace DiagnosticCatalog.AspNetCore --container AspNetCoreRule \
    --output src/DiagnosticCatalog.AspNetCore/AspNetCoreRules.g.cs
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

## A note on versions

The rules a project actually gets are governed by its **shared framework**, which its target
framework selects — not by a package reference it controls. This catalogue mirrors a targeting-pack
release, and the assembly records exactly which one in `[assembly: CatalogSource]`. If your app
targets an older ASP.NET Core than the version recorded there, rules added since will be present in
the catalogue and absent from your build; referencing one still compiles, and the suppression simply
never matches anything.

## How it reaches nuget.org

This catalogue rides the `aspnetcore` [release train](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md)
and versions independently of the foundation, so it can follow ASP.NET Core's releases without
dragging anything else along.

Publishing is not part of the nightly. A maintainer pushes an `aspnetcore-vX.Y.Z` tag, and the
release workflow packs the package, embeds an SPDX SBOM, and publishes through NuGet
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) with
signed build provenance — no long-lived API key exists anywhere to leak.

## Limits

`[SuppressMessage]` cannot suppress **compiler** warnings — `CS0219` and friends need
`#pragma warning disable`, which takes bare identifiers and so can never reference a constant. This
package covers the `ASPxxxx` and `BLxxxx` analyzer rules only.

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
