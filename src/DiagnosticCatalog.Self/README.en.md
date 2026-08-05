# DiagnosticCatalog.Self

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Self/README.fr.md)

The `DCAT` rules — the ones [`DiagnosticCatalog.Analyzers`](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Analyzers/README.en.md)
reports — as constants you can reference.

It is the library applied to itself. The analyzers that check *your* suppressions publish their own
rules the same way they ask everybody else to, and they do it through the same generator that
produces the Sonar, .NET-analyzers and StyleCop catalogues.

## When you want it

When you suppress a `DCAT` diagnostic and would rather the suppression be checked:

```csharp
using System.Diagnostics.CodeAnalysis;
using DiagnosticCatalog.Self;

// Migrating a large codebase: this file is done last, and the literals here are deliberate.
[SuppressMessage(
    DcatRule.DCAT0006.Category,
    DcatRule.DCAT0006.Id,
    Justification = "Legacy suppressions, migrated in the next pass.")]
public static class LegacyInterop
{
}
```

Without the catalogue you would write `[SuppressMessage("DiagnosticCatalog", "DCAT0006")]` — two
strings nothing checks, which is the exact problem this repository exists to remove. It would have
been odd to leave our own rules as the one place you still had to write them by hand.

Most projects will not need this: `.editorconfig` is the usual way to turn a `DCAT` diagnostic down,
and it takes plain text that no constant can ever be substituted into. Reach for the catalogue when
you are suppressing at a *specific site*, for a reason worth writing down.

## Where it comes from

Generated from the analyzers' own `DiagnosticDescriptor` instances, never from documentation, so the
id and the category are the values the analyzer actually reports. Regenerating it is one command:

```sh
dotnet run --project src/DiagnosticCatalog.Cli -- generate --manifest eng/catalogs.json
```

CI regenerates it on every pull request and fails if the result differs from what is committed — so
a new `DCAT` id cannot ship without the catalogue that publishes it.

## Versioning

This catalogue rides the `lib` train, with the analyzers it mirrors, and that is deliberate: the two
are generated from one source in one repository and must never describe different rule sets. The
other eleven catalogues version independently because an outside vendor sets their pace
([ADR-0015](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0015-a-catalogues-version-runs-on-its-own-line.en.md)); nobody outside sets
this one's.

A retired rule is carried forward as `[Obsolete]` rather than deleted, like everywhere else here:
constants are inlined into your assembly at *your* compile time, so removing one breaks your build
with a message that names nothing useful.

## See also

Twelve sibling catalogues are generated from this repository the same way, each read from one
analyzer's own descriptors — the difference being that theirs belong to somebody else:

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
- [`DiagnosticCatalog.Trimming`](https://www.nuget.org/packages/DiagnosticCatalog.Trimming)
  — the trimming, Native AOT and single-file (`ILxxxx`) warnings.
- [`DiagnosticCatalog.AspNetCore`](https://www.nuget.org/packages/DiagnosticCatalog.AspNetCore)
  — the ASP.NET Core and Blazor (`ASPxxxx`, `BLxxxx`) rules.
- [`DiagnosticCatalog.Syslib`](https://www.nuget.org/packages/DiagnosticCatalog.Syslib)
  — the .NET runtime source-generator (`SYSLIB1xxx`) diagnostics.
- [`DiagnosticCatalog.Roslyn`](https://www.nuget.org/packages/DiagnosticCatalog.Roslyn)
  — the Roslyn analyzer-authoring (`RS1xxx`, `RS2xxx`) rules.
- [`DiagnosticCatalog.PublicApi`](https://www.nuget.org/packages/DiagnosticCatalog.PublicApi)
  — the public-API tracking (`RS00xx`) rules.

**Want a catalogue of your own?** That is what
[the catalogue author's guide](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.en.md) is for, and this package is its
worked example: a static class of constants marked `[DiagnosticRule]`, generated from the analyzer
that reports them. The marker ships in [`DiagnosticCatalog`](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog/README.en.md).

## Documentation

- [**The `DCAT` diagnostics**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/diagnostics.en.md)
  — every rule catalogued here, seen from the side that reports it.
- [**Writing suppressions that the compiler checks**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/writing-suppressions.en.md)
  — how to use these constants, which is the same as for any other catalogue.
- [**Repository architecture**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/architecture.en.md)
  — the self-application loop this package is one half of, and why it runs in one direction.

The [**documentation map**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.en.md)
picks a page by what you are trying to do; every guide exists in English and French.

## Licence

Apache-2.0.
