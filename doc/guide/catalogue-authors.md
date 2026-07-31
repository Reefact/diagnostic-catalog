# Publishing a catalogue

For anyone who ships an analyzer, or who wants their team's suppressions checked against rules
nobody else publishes. Everything here is deliberately readable without opening the foundation's
source.

A working version of everything below lives in
[`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self) — this library's own rules,
catalogued by this library's own generator. It is the product applied to itself rather than a
mock-up, and CI fails if it ever stops matching the analyzers it mirrors.

## The whole contract

A rule is a **static class**, marked, exposing **two public string constants**:

```csharp
using DiagnosticCatalog;

[DiagnosticRule]
public static class CTS0001
{
    public const string Id = nameof(CTS0001);
    public const string Category = "Usage";
}
```

That is it. No base class, no interface, nothing to register, no generator to run. If you are
looking for the part you missed, there isn't one.

Three details in that snippet earn their place:

* **`static`** — nothing ever instantiates a rule, and the analyzer rejects a non-static one.
* **`const`, not `static readonly`** — a `static readonly` field has a value at run time but cannot
  be an attribute argument, which is the entire point. This is the mistake people make first.
* **`nameof(CTS0001)`** rather than `"CTS0001"` — it resolves to the containing type's own name, so
  the identifier and the class cannot drift apart. Rename one in the IDE and the other follows.

## The shape to actually ship

Nest the rules in a container, so the use site reads well:

```csharp
namespace Contoso.Analyzers.Suppressions;

public static class ContosoRules
{
    [DiagnosticRule]
    public static class CTS0001
    {
        public const string Id = nameof(CTS0001);
        public const string Category = "Usage";
    }
}
```

```csharp
[SuppressMessage(ContosoRules.CTS0001.Category, ContosoRules.CTS0001.Id, Justification = "...")]
```

**Name the container for the use site, not for the file.** Every suppression pays for that name
twice, and your users cannot shorten it — they can alias it, but the name you pick is the one that
shows up in every code review. `ContosoRules` reads better than
`ContosoAnalyzersDiagnosticRuleDefinitions`.

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

public static class ContosoRules
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
constant, so `ContosoRules.CTS0001.Category` is still valid as an attribute argument and still ends
up as the literal `"Usage"` in the compiled assembly. Nothing downstream changes.

`[DiagnosticCategory]` is optional — the constants work without it. What it buys is that tooling can
tell a category constant from any other string constant in your assembly, so the `DCAT0006` fix can
offer `ContosoCategory.Usage` instead of a bare literal.

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

## Closing the loop: one source of truth

If you own the analyzer as well as the catalogue, feed the descriptor **from the catalogue**:

```csharp
private static readonly DiagnosticDescriptor Rule = new(
    id:                 ContosoRules.CTS0001.Id,
    title:              ContosoRules.CTS0001.Title,
    messageFormat:      ContosoRules.CTS0001.MessageFormat,
    category:           ContosoRules.CTS0001.Category,
    defaultSeverity:    DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description:        ContosoRules.CTS0001.Description,
    helpLinkUri:        ContosoRules.CTS0001.HelpLinkUri);
```

Now the analyzer that *reports* the rule and every suppression that *silences* it read the same
constants. The category your users write is exact by construction rather than by diligence — and
"by diligence" is precisely what fails, because a category is a string that nobody but you
publishes and that nothing verifies.

This is the strongest reason for a first-party project to adopt the convention, and it is something
a third-party catalogue can never offer: a mirror of somebody else's analyzer can only copy what
that analyzer declares today.

## Packaging

Reference the foundation the ordinary way — **not** `PrivateAssets="all"`:

```xml
<PackageReference Include="DiagnosticCatalog" Version="0.1.0" />
```

| Who you are | What you need | How to reference |
| --- | --- | --- |
| **Consumer** — writes suppressions | the analyzers | `DiagnosticCatalog.Analyzers`, `PrivateAssets="all"` |
| **Catalogue author** — declares rules | `[DiagnosticRule]` resolvable *by your own consumers* | ordinary `DiagnosticCatalog` reference |

Hiding the dependency with `PrivateAssets="all"` is the mistake that matters here: your consumers
then cannot resolve `DiagnosticRuleAttribute`, `[DiagnosticRule]` degrades to an error type, and —
this is the bad part — the analyzers find **no rules at all** and report **nothing**. Everything
looks clean. That is the exact failure this library exists to eliminate, so do not reproduce it in
your own package.

### Not taking the dependency at all

If you would rather ship a catalogue with no dependencies whatsoever, declare the attribute yourself:

```csharp
namespace DiagnosticCatalog
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    internal sealed class DiagnosticRuleAttribute : System.Attribute { }
}
```

This is supported and tested, not a trick. The analyzers match the marker by its **fully qualified
name**, never by symbol identity, so your copy is recognised exactly like the real one. It is the
same pattern PolySharp uses for `IsExternalInit`.

### If you reference the analyzers too

A catalogue that references `DiagnosticCatalog.Analyzers` **propagates them to its own consumers**,
so referencing your catalogue is enough to get the checking. That was measured against a real
restore rather than read from NuGet's documentation, which says the opposite:

| Your reference to `DiagnosticCatalog.Analyzers` | The analyzers run for your consumers |
| --- | --- |
| no `PrivateAssets` | **yes** |
| `PrivateAssets="none"` | yes |
| `PrivateAssets="all"` | no |

If you would rather not impose analysis on everyone downstream, say so explicitly with
`PrivateAssets="all"`. **Silence propagates.**

## Versioning: the one rule that will bite you

Constants are **inlined into your consumers at their compile time**. A consumer that referenced
`ContosoRules.CTS0001.Id` did not record a link to your assembly — it copied the string `"CTS0001"`
into its own.

The consequence: **deleting a `const` breaks recompilation** for everyone who used it, and it breaks
it with a bare `CS0117` that names nothing useful. So when a rule is retired upstream, carry it
forward:

```csharp
[DiagnosticRule]
[Obsolete("Retired in Contoso.Analyzers 4.0. No replacement.")]
public static class CTS0001
{
    public const string Id = nameof(CTS0001);
    public const string Category = ContosoCategory.Usage;
}
```

Now a consumer still referencing it gets `CS0618` — which *names the rule and says what happened* —
instead of a compile error that sends them looking for a missing namespace.

The same applies to renaming: a category constant whose name changes breaks every consumer that
referenced it. Pick names you can live with, and see
[ADR-0012](../adr/0012-a-catalogue-never-renames-a-member-it-published.md) for how this repository
holds itself to that.

Beyond that, ordinary SemVer: a new rule is a **minor**, a retired-but-kept rule is a **minor**, and
removing or renaming anything published is a **major**.

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

If you are mirroring at scale, this repository generates three catalogues that way and publishes the
generator as a tool. The Sonar, .NET-analyzers and StyleCop catalogues under `src/` are what the
output looks like with 465, 318 and 193 rules; the method is in §14 of
[the specification](../specification.en.md).

## Where to look next

* [`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self) — the whole of the above,
  generated, shipped, and checked on every pull request.
* [`eng/catalogs.json`](../../eng/catalogs.json) — how each catalogue in this repository declares
  where its rules come from.
* [The diagnostics reference](diagnostics.md) — what your users will be told, and when.
