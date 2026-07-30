# DiagnosticCatalog

Declare analyzer diagnostic rules as strongly referenced constants, so that
`SuppressMessageAttribute` takes compile-checked references instead of magic strings.

## The problem

**Both** arguments of `SuppressMessageAttribute` are magic strings, and nothing
validates either one:

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "...")]
```

They differ only in how they fail. Get the **id** wrong — a typo, or a rule the vendor
later renamed — and the suppression silently does nothing: the warning simply stays,
with nothing pointing at the cause. Get the **category** wrong and *nothing happens at
all, ever*: the .NET platform never reads that argument, so no compiler, analyzer, test
or tool can tell you. And you would not guess it — `S1144`'s category is
`"Major Code Smell"`, not `"Code Smell"` and not `"Maintainability"`.

```csharp
// Fails the build instead, if the rule is ever renamed or retired.
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "...")]
```

Sonar, the .NET CA rules and StyleCop are already packaged as `DiagnosticCatalog.Sonar`,
`DiagnosticCatalog.NetAnalyzers` and `DiagnosticCatalog.StyleCop`. This package is what
you need to declare a catalogue of your own.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog" Version="0.1.0" />
```

Do **not** add `PrivateAssets="all"` if your project publishes a catalogue for others
to consume: the package must flow to them so they can declare rules of their own, and
so that run-time reflection over your catalogue keeps working. The checks themselves
survive an unresolved attribute — the analyzers match on the fully qualified metadata
name `DiagnosticCatalog.DiagnosticRuleAttribute`, which is exactly the silent failure
mode that choice was made to design out — but do not rely on it.

## Declaring a rule

A rule is a static, non-generic class marked `[DiagnosticRule]`, exposing two mandatory
public constants:

```csharp
using DiagnosticCatalog;

namespace JustDummies.Analyzers.Suppressions;

public static class Dummies
{
    [DiagnosticRule]
    public static class JD0007
    {
        public const string Id = nameof(JD0007);
        public const string Category = "Usage";
    }
}
```

Both members must be `const`. A property, a `static readonly` field or a `record`
cannot be used as an attribute argument, which is also why the contract is structural
rather than an interface or a base class.

Keep container names short — every use site pays for them twice. One constraint bounds
the shortening: **never name the container after the first segment of its own
namespace.** A consumer writing `using JustDummies.Analyzers.Suppressions;` resolves
`JustDummies` to the namespace, not to the imported container, and every reference fails
with `CS0234`. The consumer cannot work around it.

## Using a rule

```csharp
using System.Diagnostics.CodeAnalysis;
using JustDummies.Analyzers.Suppressions;

[SuppressMessage(
    Dummies.JD0007.Category,
    Dummies.JD0007.Id,
    Justification = "This member is instantiated by the test infrastructure.")]
public sealed class DummyFactory
{
}
```

## Optional metadata

A rule may carry the remaining `DiagnosticDescriptor` arguments. Every one of these is
a plain string, so this adds no dependency beyond this package:

```csharp
[DiagnosticRule]
public static class JD0007
{
    public const string Id = nameof(JD0007);
    public const string Category = "Usage";
    public const string Title = "Dummy factories should follow the expected convention";
    public const string MessageFormat = "Type '{0}' does not follow the convention";
    public const string Description = "Explains the condition detected by the analyzer.";
    public const string HelpLinkUri = "https://justdummies.io/analyzers/JD0007";
}
```

If you own the analyzer, it can then build its descriptor from the very constants its
suppressions reference — one source of truth for both:

```csharp
using Microsoft.CodeAnalysis;

private static readonly DiagnosticDescriptor Descriptor = new(
    JD0007.Id, JD0007.Title, JD0007.MessageFormat, JD0007.Category,
    DiagnosticSeverity.Warning, isEnabledByDefault: true,
    description: JD0007.Description, helpLinkUri: JD0007.HelpLinkUri);
```

`DiagnosticSeverity` is constant-capable, so a rule *can* also expose
`public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;` — but unlike the
string constants above, that type lives in `Microsoft.CodeAnalysis.Common`, so a rule
declaring it forces a Roslyn dependency on every consumer of the catalogue. Add it only
in a project that already references Microsoft.CodeAnalysis, such as your analyzer
itself. A standalone catalogue package should stay on plain strings.

Localised text (`LocalizableString`, resx-backed descriptors) falls outside the `const`
model; resource files remain the right tool for translated strings.

## What this package is not

This package contains **the attributes only** — `[DiagnosticRule]` and
`[assembly: CatalogSource]`. It performs no checking.

The analyzers that validate rule declarations, verify that `Category` and `Id` come
from the same rule, and offer to replace string literals with catalogue references
ship separately:

```xml
<PackageReference Include="DiagnosticCatalog.Analyzers" Version="..." PrivateAssets="all" />
```

Applying `[DiagnosticRule]` introduces no runtime behaviour. The runtime resolves
attribute types lazily, so `DiagnosticCatalog.dll` is never loaded unless something
reflects over the rule types.

If you want no package dependency at all, the analyzers recognise the attribute by its
fully qualified metadata name. Declaring your own `internal sealed class
DiagnosticRuleAttribute` in the `DiagnosticCatalog` namespace works just as well.

## Recording where a catalogue came from

A catalogue that mirrors somebody else's analyzer is a snapshot. `CatalogSource`
records which upstream release it reflects and when, readable from metadata:

```csharp
[assembly: CatalogSource(
    source:        "SonarAnalyzer.CSharp",
    sourceVersion: "10.31.0.145097",
    generatedOn:   "2026-07-30")]
```

The date is a string because attribute arguments must be compile-time constants and
no date type can be one; the format is ISO 8601, `yyyy-MM-dd`. A first-party catalogue
maintained alongside its own analyzer does not need this — the two ship at one version.

## Documentation

The full design, the diagnostic list and the verified platform behaviour it relies on
are documented in the repository:
[specification](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.en.md)
([français](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.fr.md)).

## License

Apache-2.0
