# DiagnosticCatalog

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog/README.fr.md)

Declare analyzer diagnostic rules as strongly referenced constants, so that
`SuppressMessageAttribute` takes compile-checked references instead of magic strings.

One package, both halves: the attributes a catalogue is declared with, and the `DCAT`
analyzers and code fixes that check what you write against it.

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

Sonar, the .NET CA rules, StyleCop, the Roslyn IDE rules and xUnit's are already packaged as
`DiagnosticCatalog.Sonar`, `DiagnosticCatalog.NetAnalyzers`, `DiagnosticCatalog.StyleCop`
`DiagnosticCatalog.CodeStyle` and `DiagnosticCatalog.Xunit`. This package is what you need to
declare a catalogue of your own — and referencing any of those already brings it, and the
checks it carries, along with them.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog" Version="0.1.0" />
```

Do **not** add `PrivateAssets="all"` if your project publishes a catalogue for others
to consume. One package carries both halves, so hiding it hides both: your consumers
lose `[DiagnosticRule]`, which they need to declare rules of their own and which
run-time reflection over your catalogue resolves, and they lose the checks along with
it — a consumer written the ordinary way stops compiling rather than merely going
unchecked. Both halves of that are measured against a real restore by
`tools/packaging/verify-consumption.sh`, in the checks
`a catalogue hiding the foundation delivers no analyzer either` and
`a catalogue hiding the foundation withholds the attribute assembly`.

A catalogue also packs `build/<its own package id>.props`, setting
`EnableDiagnosticCatalogAnalyzers`, and that is what delivers the analyzers to its
consumers — the check `a catalogue delivers the analyzer to its own consumer`. NuGet
imports a package's `build/` folder for a direct reference and for nothing further out,
so the checks reach the project that referenced the catalogue and stop there: an
application referencing a **library** that took a catalogue for its own suppressions is
not analysed by a catalogue it never chose, and the library writes nothing to arrange
that
([ADR-0038](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.en.md)).
[Packaging a catalogue](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/packaging-a-catalogue.en.md)
has the file.

A consuming project overrules that in either direction with the same property:
`false` keeps the catalogue and declines the analysis, `true` asks for the checks from
further out than a direct reference.

## Declaring a rule

A rule is a static, non-generic class marked `[DiagnosticRule]`, exposing two mandatory
public constants. The category must reach a constant declared in a class marked
`[DiagnosticCategory]`:

```csharp
using DiagnosticCatalog;

namespace JustDummies.Analyzers.Suppressions;

[DiagnosticCategory]
internal static class DummiesCategory
{
    public const string Usage = "Usage";
}

public static class Dummies
{
    [DiagnosticRule]
    public static class JD0007
    {
        public const string Id = nameof(JD0007);
        public const string Category = DummiesCategory.Usage;
    }
}
```

Both members must be `const`. A property, a `static readonly` field or a `record`
cannot be used as an attribute argument, which is also why the contract is structural
rather than an interface or a base class.

The category class earns its place on a catalogue of any size: very few distinct
categories are spread over very many rules, and declaring each once is what keeps a
single spelling per value. The marker is what makes that class legible to tooling, so a
fix can offer the named constant in place of a literal. A rule reaching its category any
other way is reported as `DCAT0011`.

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
    public const string Category = DummiesCategory.Usage;
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

## The checks that come with it

The `DCAT` analyzers and their code fixes ship **inside this package**, under
`analyzers/dotnet/cs/` beside `lib/`. There is nothing else to reference: they arrive with
the foundation, and the foundation arrives with every catalogue built on it.

They check two things: that a rule **declaration** satisfies the structural contract — its
shape, its `Id`, its `Category`, how that category is reached and what its type name says —
and that a **suppression** referencing one is coherent: two arguments that do not name one
rule's `Category` and that same rule's `Id`, a half-migrated suppression mixing a reference
with a literal, a literal that a catalogue reference would replace, and an
`UnconditionalSuppressMessage` the trimmer discards.

A project that consumes a catalogue and declares no rules of its own sees the second set
only. The declaration diagnostics report on types marked `[DiagnosticRule]` and return
immediately on everything else.

An analysis assembly never becomes a runtime dependency of the consuming application:
`tools/packaging/verify-consumption.sh` restores this package the way a consumer does and
asserts that `DiagnosticCatalog.Analyzers.dll` and `DiagnosticCatalog.CodeFixes.dll` stay out
of the output folder while `DiagnosticCatalog.dll` reaches it. Applying `[DiagnosticRule]`
adds no runtime behaviour either — the runtime resolves attribute types lazily, so
`DiagnosticCatalog.dll` is never loaded unless something reflects over the rule types.

The analyzers never need the attribute *type*, only its name: they match
`DiagnosticCatalog.DiagnosticRuleAttribute` by its fully qualified metadata name. A project
declaring its own `internal sealed class DiagnosticRuleAttribute` in the `DiagnosticCatalog`
namespace is therefore checked exactly like one that took the package. What that does not do
is deliver the analyzers — those arrive with this package, and a project that has hidden it
has neither.

## Migrating an existing codebase

Adopting a catalogue is not a quiet change: the use-site diagnostics are errors by default
(`DCAT0001`, `DCAT0006` and `DCAT0007`), so a literal suppression a catalogue reference would
replace fails the build rather than warning. The code fix that rewrites it is how a codebase
adopts a catalogue in practice:

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "kept for reflection")]
// becomes
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "kept for reflection")]
```

*Fix all occurrences* applies it across a document, project or solution in one step, and the
`using` the reference needs is added for you. Everything else in the attribute is left exactly
as written — `Justification`, `Scope`, `Target` and `MessageId` are yours.

Two behaviours worth knowing before you run it:

* **The friendly-name suffix is dropped.** Visual Studio writes
  `"S1144:Unused private members should be removed"`; the fix recognises that form and replaces
  the whole thing with the reference. The prose lived in the suppression only because there was
  nothing else to hold it — the rule's own documentation has it now.
* **When two catalogues describe the same rule, no fix is offered.** The diagnostic still
  appears, so nothing is hidden, but choosing between them is yours to make.

A suppression left half migrated — one reference, one literal — is reported too, and completed
from the rule the migrated argument already names:

```csharp
[SuppressMessage(SonarRule.S1144.Category, "S1144", Justification = "kept for reflection")]
// becomes
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "kept for reflection")]
```

Only the literal is rewritten; whatever spelling you chose for the other side, an alias
included, is left alone. And if the literal names something the referenced rule does not —
`"S9999"` beside `SonarRule.S1144.Category` — you get the diagnostic and no fix. Completing
that one would silence a different rule than the one silenced today, which is a decision for
you and not for a lightbulb.

## When the two arguments name different rules

That one gets **two** fixes and no recommendation:

```text
Use SonarRule.S1144.Id        — keep the category, correct the identifier
Use SonarRule.S2094.Category  — keep the identifier, correct the category
```

Only you know which half was the typo, so neither is offered as the default. Worth knowing
while you choose: Roslyn matches a suppression on the **identifier alone** and never looks at
the category, so correcting the category leaves what is suppressed exactly as it is, while
correcting the identifier changes it.

## Fixes for a rule written by hand

A catalogue is normally generated, and generated code satisfies the contract by construction.
When you write one yourself, code fixes are there for the mechanical part:

```csharp
[DiagnosticRule]
public sealed class JD0007                      // → Make 'JD0007' static
{
    private static readonly string Id = "JD0007";   // → Make 'Id' a public constant
                                                    // → Declare 'public const string Category'
}
```

Each is offered **only where the repair is already written in the code**. `static` is not
offered to a generic type, to a `struct`, or to a class holding an instance member — the
keyword would not compile there, and removing what blocks it is a change to your design rather
than a repair of it. A `partial` class is refused too: the parts the fix cannot see are the
ones that decide.

The member repairs correct modifiers and never the value. A `const int Id`, a blank string, an
initialiser that is not constant — those are reported with no fix, because the code says
nothing about what you meant.

> **The one to think about before pressing it.** *Declare 'public const string Category'*
> writes `"TODO"`. That is a real string, so `DCAT0004` stops being reported — you have swapped
> a warning for a marker. A category nobody fills in is wrong forever and invisible in every
> build, because Roslyn matches a suppression on its identifier alone. `Id` is different: it is
> written `nameof(JD0007)`, read off the declaration rather than invented.

## What the analyzers do not do

They do not validate an arbitrary string. `[SuppressMessage("Usage", "S1144")]` with the wrong
category matches no known rule, and nothing is reported — the mechanism that makes a wrong
category impossible is the constant itself, which the compiler checks. These analyzers get you
to the constants and keep you there.

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

## See also

Every rule catalogue built on this package is listed in one place, generated from the analyzers'
own descriptors rather than hand-written. If you run one of those analyzers, its rules do not need
declaring:

**[The ready-made catalogues](https://github.com/Reefact/diagnostic-catalog#-the-ready-made-catalogues)**

They are also worth reading as worked examples of the contract above: a container of rules, the
categories declared once, and the upstream release the whole thing mirrors recorded in
`[assembly: CatalogSource]`.

For the contract explained from scratch rather than by example, see
[the catalogue author's guide](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.en.md).

## Documentation

For declaring a catalogue, in the order the work happens:

- [**Publishing a catalogue**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.en.md)
  — the structural contract, the shape to actually ship, declaring categories once, and the
  versioning rule that will bite you if you skip it.
- [**Closing the loop with your own analyzer**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/first-party-analyzers.en.md)
  — feeding your `DiagnosticDescriptor` from your own catalogue, and the member that would
  force Roslyn on every consumer.
- [**Versioning a catalogue**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/versioning-a-catalogue.en.md)
  — never delete a rule, never rename a member, and what each change does to the number.
- [**Packaging a catalogue**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/packaging-a-catalogue.en.md)
  — what to reference, what propagates to your consumers, and what nuget.org does to your
  README.

For the checks this package brings with it:

- [**The `DCAT` diagnostics**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/diagnostics.en.md)
  — every id these analyzers report, what triggers it, why it exists, whether a code fix is
  offered, and the `.editorconfig` key that configures it.
- [**Configuration**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/configuration.en.md)
  — severities, the category-wide switch, generated code, and the `PrivateAssets` mistake
  that silences everything.
- [**Adopting a catalogue on an existing codebase**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/adopting-a-catalogue.en.md)
  — the severity ramp and what order to convert in, when the migration above is large.
- [**The rule contract**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/rule-contract.en.md)
  — the five requirements a declaration is checked against, and every syntactic form a use
  site may take.
- [**Troubleshooting**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/troubleshooting.en.md)
  — by symptom, starting with "nothing is reported at all".

The [**documentation map**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.en.md)
picks a page by what you are trying to do; every guide exists in English and French. The
[**specification**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.en.md)
is the normative version of all of it, including the verified platform behaviour the design
relies on.

## License

Apache-2.0
