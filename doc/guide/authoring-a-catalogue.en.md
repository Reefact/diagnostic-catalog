# Publishing a catalogue

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./authoring-a-catalogue.fr.md)

For anyone who ships an analyzer, or who wants their team's suppressions checked against rules
nobody else publishes. Everything here is deliberately readable without opening the foundation's
source.

A working version of everything below lives in
[`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self) — this library's own rules,
catalogued by this library's own generator. It is the product applied to itself rather than a
mock-up, and CI fails if it ever stops matching the analyzers it mirrors.

## The whole contract

A rule is a **static class**, marked, exposing **two public string constants**, one of which reaches a
**declared category**:

```csharp
using DiagnosticCatalog;

[DiagnosticCategory]
internal static class ContosoCategory
{
    public const string Usage = "Usage";
}

[DiagnosticRule]
public static class CTS0001
{
    public const string Id = nameof(CTS0001);
    public const string Category = ContosoCategory.Usage;
}
```

That is it. No base class, no interface, nothing to register, no generator to run. If you are
looking for the part you missed, there isn't one.

Four details in that snippet earn their place:

* **`static`** — nothing ever instantiates a rule, and the analyzer rejects a non-static one.
* **`const`, not `static readonly`** — a `static readonly` field has a value at run time but cannot
  be an attribute argument, which is the entire point. This is the mistake people make first.
* **`nameof(CTS0001)`** rather than `"CTS0001"` — it resolves to the containing type's own name, so
  the identifier and the class cannot drift apart. Rename one in the IDE and the other follows.
* **`ContosoCategory.Usage`** rather than `"Usage"` — one class holds each category once, and
  `[DiagnosticCategory]` is what makes that class visible to tooling. Required, not advisory:
  `DCAT0011` reports the literal. The next section is about the class itself.

## When you get it wrong, the analyzer offers to fix it

`DCAT0002`, `DCAT0003`, `DCAT0004` and `DCAT0011` report a declaration that misses the contract. The
first three carry a fix — **offered only where the repair is already written in the code**, and silent otherwise:

| What you wrote | What is offered |
| --- | --- |
| `public sealed class CTS0001` | *Make 'CTS0001' static* — for a plain class that could carry the keyword: no type parameters, no base type, no instance member, not `partial` |
| `private static readonly string Id = ...` | *Make 'Id' a public constant* — modifiers only; the value is left alone |
| no `Id` member at all | *Declare 'public const string Id'*, written `nameof(CTS0001)` — read off your declaration rather than invented |

`DCAT0011` carries none: the repair is a class that may not exist yet, holding a constant nobody has
named. Nothing is offered when the **value** is what is wrong either — a `const int`, a blank string, a
non-constant initialiser. The code says nothing about what you meant, and a fix that guessed would
produce a rule the compiler accepts and nobody checks.

> **One to think about before pressing it.** *Declare 'public const string Category'* writes `"TODO"`.
> That is a real string, so `DCAT0004` stops being reported the moment you apply it — you have traded
> a warning that named the problem for a marker only a reader will notice, and a wrong category is
> invisible in every build forever. Apply it when you are about to fill it in.

Full detail in [the diagnostics reference](diagnostics.en.md#definition-diagnostics).

## The shape to actually ship

Nest the rules in a container, so the use site reads well:

```csharp
namespace Contoso.Analyzers.Suppressions;

public static class ContosoRule
{
    [DiagnosticRule]
    public static class CTS0001
    {
        public const string Id = nameof(CTS0001);
        public const string Category = ContosoCategory.Usage;
    }
}
```

```csharp
[SuppressMessage(ContosoRule.CTS0001.Category, ContosoRule.CTS0001.Id, Justification = "...")]
```

**Name the container for the use site, not for the file.** Every suppression pays for that name
twice, and your users cannot shorten it — they can alias it, but the name you pick is the one that
shows up in every code review. `ContosoRule` reads better than
`ContosoAnalyzersDiagnosticRuleDefinitions`. The catalogues in this repository are named the same
way, and in the singular: the use site reads `SonarRule.S1144`, one rule, named.

## Declaring your categories once

A real catalogue repeats very few categories across very many rules. The Sonar catalogue in this
repository spends 456 rule declarations on **13** distinct category values. Writing the literal in
every rule is 456 chances for one of them to drift.

```csharp
[DiagnosticCategory]
public static class ContosoCategory
{
    public const string Usage = "Usage";
    public const string Design = "Design";
}

public static class ContosoRule
{
    [DiagnosticRule]
    public static class CTS0001
    {
        public const string Id = nameof(CTS0001);
        public const string Category = ContosoCategory.Usage;   // ← not a literal
    }
}
```

**The indirection is free.** A `const` initialised from another `const` is still a compile-time
constant, so `ContosoRule.CTS0001.Category` is still valid as an attribute argument and still ends
up as the literal `"Usage"` in the compiled assembly. Nothing downstream changes.

`[DiagnosticCategory]` is **required** — `DCAT0011` reports a rule that reaches its category any other
way. The constants would work without it, and that is the point: what the marker buys is that tooling
can tell a category constant from any other string constant in your assembly, so the `DCAT0006` fix can
offer `ContosoCategory.Usage` instead of a bare literal. Unmarked, the class is invisible and the
indirection buys nothing. The decision is
[ADR-0028](../adr/0028-require-every-rule-to-reach-its-category-through-a-declared-constant.en.md).

## Optional metadata, and the one that costs you

A rule may carry more:

```csharp
[DiagnosticRule]
public static class CTS0001
{
    public const string Id = nameof(CTS0001);
    public const string Category = ContosoCategory.Usage;

    public const string Title = "Factories should be named with the 'Factory' suffix";
    public const string MessageFormat = "Type '{0}' is registered as a factory but is not named '...Factory'";
    public const string Description = "Factories are discovered by name at start-up, so one that ...";
    public const string HelpLinkUri = "https://contoso.example/rules/CTS0001";
}
```

Nothing requires these and nothing validates them. They exist because they are exactly the arguments
of `DiagnosticDescriptor` — which is the next section, and the best reason to do any of this.

> **One caveat.** You may be tempted to add
> `public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;`. An enum *is*
> constant-capable, but `DiagnosticSeverity` lives in `Microsoft.CodeAnalysis.Common` — so declaring
> it forces a Roslyn dependency on **every consumer of your catalogue**, including applications that
> only write suppressions. Declare `Severity` in your analyzer project, which already references
> Roslyn. A standalone catalogue package stays on plain strings.

> **A known limit.** Localised text — `LocalizableString`, resx-backed descriptors — cannot be a
> `const` and therefore falls outside this model. The catalogue covers the id and category axis;
> resource files remain the right tool for translated text.

## Three things that get their own page

The contract above is all a catalogue has to satisfy. What surrounds it — feeding your analyzer from
it, publishing it, publishing it again — is where the decisions are, and each has enough of them to
be read on its own:

* [**Closing the loop with your own analyzer**](first-party-analyzers.en.md) — if you own both, the
  `DiagnosticDescriptor` and the suppression can read the same constants, and the category your users
  write becomes exact by construction. Also the one member that would force a Roslyn dependency on
  every consumer of your catalogue.
* [**Versioning a catalogue**](versioning-a-catalogue.en.md) — constants are inlined into your
  consumers at *their* compile time, so deleting one breaks their build with a message that names
  nothing. Never delete a rule; never rename a member; what each change does to your version number.
* [**Packaging a catalogue**](packaging-a-catalogue.en.md) — how to reference the foundation, how to
  ship with no dependency at all, what propagates to your consumers whether you meant it or not, and
  what nuget.org does to your README.

## If you are mirroring somebody else's analyzer

A catalogue that mirrors a third party is a snapshot, and nothing in the compiled assembly would
otherwise say which release it reflects. Record it:

```csharp
[assembly: CatalogSource(
    source:        "Contoso.Analyzers",
    sourceVersion: "4.2.1",
    generatedOn:   "2026-07-31")]
```

The date is a **string**, not a `DateTime`, for the same reason as everything else here: attribute
arguments must be compile-time constants and no date type can be one. Use `yyyy-MM-dd`.

A first-party catalogue maintained beside its own analyzer needs none of this — the two ship from
one repository at one version.

If you are mirroring at scale, this repository generates six catalogues that way and publishes the
generator as a tool. The Sonar, .NET-analyzers and StyleCop catalogues under `src/` are what the
output looks like with 465, 318 and 193 rules; the method is in §14 of
[the specification](../specification.en.md).

## Where to look next

* [`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self) — the whole of the above,
  generated, shipped, and checked on every pull request.
* [`eng/catalogs.json`](../../eng/catalogs.json) — how each catalogue in this repository declares
  where its rules come from.
* [The diagnostics reference](diagnostics.en.md) — what your users will be told, and when.

---

<div align="center">
<a href="./zero-footprint.en.md">← The zero-footprint guarantee</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./first-party-analyzers.en.md">Closing the loop with your own analyzer →</a>
</div>
