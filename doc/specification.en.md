# DiagnosticCatalog — Foundation Library Specification

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./specification.fr.md)

For anyone who needs the normative answer rather than the shortest one. This is the canonical
version: when it and the French translation diverge, this document wins. The
[guides](guide/README.en.md) teach the same material and are the better place to start.

---

## 1. Document status

This specification is the normative definition of `DiagnosticCatalog` as it ships
at **1.0**. Everything here describes the library, the analyzers, the generator
and the packaging as they exist in this repository; where a statement and the
code disagree, the code is what is right and the statement is the defect.

The name is settled: `DiagnosticCatalog` is what the packages are published
under, and it is not provisional (Appendix B1).

It is a living document rather than a changelog. It is amended when the design
changes, and the reasoning behind a change is recorded once as an
[architecture decision record](adr/) rather than as a revision note here — which
is what keeps this document a statement of the current design instead of a stack
of revisions somebody has to reconcile while reading.

Two appendices carry what the body leans on.
[Appendix A](#appendix-a--verified-platform-behaviour) records every behavioural
claim about the .NET platform against the source it was checked in, and
[Appendix B](#appendix-b--decisions-taken) the design questions that were open
while the library was being built, each with the answer it was given.

---

## 2. Vision

`DiagnosticCatalog` provides a .NET convention and the tooling that turns
analyzer diagnostics into strongly structured, discoverable references.

### 2.1 The problem

**Both** arguments of `SuppressMessageAttribute` are magic strings:

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "...")]
```

Nothing validates either one. They differ only in *how* they fail, and each
failure mode is bad in its own way:

| Argument | When it is wrong | What tells you |
| --- | --- | --- |
| `checkId` | The suppression does nothing. The diagnostic keeps being reported. | Only the symptom — a warning you believed was handled, with nothing pointing at the cause. In a codebase carrying a warning backlog, nobody notices. |
| `category` | Nothing happens. The suppression works exactly as intended. | **Nothing, ever.** The platform never reads this value (§3.2), so no compiler, no analyzer, no test and no tool can report the mistake. |

A wrong `checkId` leaves a **dead suppression**: it looks like a deliberate,
justified engineering decision while providing no protection whatsoever. That
happens whenever the id carries a typo (`S1441` for `S1144`), an upstream
analyzer renames or retires a rule, a suppression is copy-pasted between rules,
or a suppression outlives the code it was written for.

A wrong `category` is the quieter defect: it never breaks anything, so it is
never found. And it is not guessable — `S1144`'s category is
`"Major Code Smell"`, not `"Code Smell"` and not `"Maintainability"` (§14).
Nothing in the toolchain will ever correct you.

### 2.2 The fix

Replace the string literals:

```csharp
[SuppressMessage(
    "Major Code Smell",
    "S1144",
    Justification = "Instantiated through reflection by the DI container.")]
```

with references that the compiler resolves and IntelliSense discovers:

```csharp
[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Instantiated through reflection by the DI container.")]
```

A renamed or removed rule now breaks the build instead of silently
deactivating a suppression.

### 2.3 What each argument gains

Both arguments gain from a catalogue, but not the same thing, and the difference
drives the priorities of this specification:

| Argument | What the catalogue buys you | Why |
| --- | --- | --- |
| `checkId` | **Correctness.** A stale or misspelled id becomes a compile error instead of a silent no-op. | This is the value proposition of the library. |
| `category` | **A source of truth.** The authoritative value is published once, so nobody guesses and nobody drifts. | Nothing else can ever tell you the value is wrong (§2.1, §3.2). |

### 2.4 Structural coherence

The foundation additionally verifies that the category and the identifier used
in a suppression attribute belong to the *same* rule. Because the platform
ignores the category entirely (§3.2), a mismatched pair is **undetectable by any
other means** — which is precisely why an analyzer is the only place this check
can live. It is a hygiene guarantee, not a functional fix.

### 2.5 Scope

`DiagnosticCatalog` contains no catalogue specific to Sonar, Microsoft,
StyleCop, JustDummies or FirstClassErrors. It defines only the shared model, the
conventions and the checks.

---

## 3. Technical foundations

### 3.1 Attribute arguments must be constants

C# attribute arguments must be determinable at compile time. Identifiers and
categories exposed by a catalogue must therefore be `const string`. A property,
a `record`, a static instance or a `static readonly` field cannot replace those
constants inside an attribute.

The public model therefore **cannot** be founded on an abstract class or an
interface imposing `Id` and `Category` properties. The contract must be:

* structural;
* materialised by constants;
* identified by a marker attribute;
* validated by a Roslyn analyzer.

### 3.2 The platform ignores the category

`SuppressMessageAttribute` exposes exactly **one** constructor,
`(string category, string checkId)`. Both parameters are required, positional
and non-nullable — the category cannot be omitted.

It is however **never used for matching**. Roslyn's
`SuppressMessageAttributeState` states it outright:

> *Ignore the category parameter because it does not identify the diagnostic and
> category information can be obtained from diagnostics themselves.*

Three consequences shape this specification:

1. A wrong category is functionally harmless — and therefore never caught by
   anything. It is the ideal target for an analyzer (§2.4).
2. The catalogue's job on the category axis is to **publish the authoritative
   value**, not to make suppression work.
3. Any generated catalogue must derive its categories from the real
   `DiagnosticDescriptor` of the target analyzer, never from a guess: an
   inaccurate value will never be reported by anything (§25.4).

### 3.3 The `checkId` carries an optional friendly name

Roslyn truncates `checkId` at the first colon:

```csharp
var separatorIndex = info.Id.IndexOf(':');
if (separatorIndex != -1)
{
    info.Id = info.Id.Remove(separatorIndex);
}
```

So `"S1144:Unused private members should be removed"` matches the `S1144`
diagnostic. **This is the form Visual Studio generates** through its
built-in *Suppress → In Source* code fix, so it dominates existing codebases —
exactly the code this library is meant to migrate. Literal detection must
normalise it (§11.6).

### 3.4 The attribute is absent from compiled metadata

`SuppressMessageAttribute` is declared `[Conditional("CODE_ANALYSIS")]` in the
BCL. Unless the `CODE_ANALYSIS` preprocessor symbol is defined, the compiler
**does not emit it into the assembly at all** — reflecting over a suppressed
member returns nothing:

```csharp
[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
[Conditional("CODE_ANALYSIS")]
public sealed class SuppressMessageAttribute : Attribute
```

Three consequences:

1. Roslyn reads suppressions from the **semantic model of the compilation being
   built**, never from metadata. Nothing in §10 or §13 depends on emission, so
   the analysis path is unaffected.
2. Verifying constant folding by reflection (§21.5) requires the test project to
   define `CODE_ANALYSIS`. Without it the assertion silently reads `null`.
3. It is the reason `UnconditionalSuppressMessageAttribute` exists at all
   (§9.1), and it splits the footprint question **per attribute**, not per
   library:

   * `SuppressMessageAttribute` — nothing survives. The attribute is omitted and
     the referenced values are folded into constants, so the shipped assembly
     carries no trace of the suppression at all.
   * `UnconditionalSuppressMessageAttribute` — the opposite, by design. It
     carries no `[Conditional]` precisely so that it *is* preserved, and it is
     emitted with the catalogue's category and id folded in as literal strings.

   Verified on one member carrying both attributes, compiled without
   `CODE_ANALYSIS`: reflection returns `null` for the former and a populated
   attribute reading `CheckId='IL2026'`, `Category='Trimming'` for the latter.

`DiagnosticRuleAttribute` must therefore **never** be made `[Conditional]`. Rule
discovery across assembly boundaries (§13) reads that marker from referenced
metadata; a conditional marker would make every catalogue shipped as a package
invisible to the analyzer.

### 3.5 The developer never types these values

The realistic workflow is not "type the attribute by hand":

1. Roslyn reports `JD0007`. The IDE offers *Suppress `JD0007` → in Source*. The
   built-in fix inserts the literals, **with the exact category taken from the
   rule's `DiagnosticDescriptor`** and the `:Title` suffix.
2. `DCAT0006` then offers *Use a diagnostic catalog reference*, rewriting those
   literals into catalogue references.

Neither value is ever hand-written, and step 1 supplies authoritative starting
values. This is why **`DCAT0006` and its code fix are the primary entry point of
the product**, not an optional extra (§24).

It also scopes the category-discoverability claim honestly: inside Visual
Studio, the built-in fix already inserts the right category. The catalogue's
value on that axis is for other editors, for `dotnet build` workflows, and for
hand-written `GlobalSuppressions.cs` files.

### 3.6 Forward compatibility

[dotnet/runtime#68153](https://github.com/dotnet/runtime/issues/68153) proposes
a category-less constructor for both suppression attributes, with the same
reasoning as §3.2. It is **still open, with no decision**. Nothing in this
design needs to anticipate it: should it ship, the catalogue model survives
unchanged and only `DCAT0001` becomes moot.

---

## 4. Goals

The library must make it possible:

1. to define a diagnostic rule following a shared convention;
2. to use that rule in `SuppressMessageAttribute`;
3. to use trim/AOT rules in `UnconditionalSuppressMessageAttribute` (§9);
4. to guarantee that `Category` and `Id` come from the same rule;
5. to detect invalid rule definitions;
6. to replace string literals with catalogue references;
7. to detect partially migrated suppressions (one reference, one literal);
8. to provide code fixes whenever the correction is unambiguous;
9. to be consumed by public or internal catalogues;
10. to work with hand-written or generated rules;
11. to feed a `DiagnosticDescriptor` from the same constants (§15.2);
12. to introduce no runtime behaviour in the consuming application.

---

## 5. Non-goals

The first version must not:

* replace `SuppressMessageAttribute`;
* introduce a proprietary suppression attribute;
* decide whether a suppression is functionally legitimate;
* assess the semantic quality of a justification — §11.14 requires one to be
  written and reads no further than its length;
* download third-party vendor catalogues automatically;
* ship the Sonar, Microsoft or StyleCop rules themselves;
* impose a base class on rules;
* provide a runtime rule engine;
* change the severity of the targeted analyzers;
* disable a diagnostic automatically;
* generate a justification automatically.

### 5.1 Out of reach by construction

Two suppression mechanisms cannot benefit from this library, and the
documentation must say so plainly rather than leave readers to discover it:

| Mechanism | Why it is out of reach |
| --- | --- |
| `#pragma warning disable JD0007` | Takes bare identifiers, not expressions. No constant can be substituted. |
| `dotnet_diagnostic.JD0007.severity` in `.editorconfig` | Configuration keys are plain text, outside the C# compilation model. |

The foundation checks the structural coherence of a suppression, never its
business or technical relevance.

---

## 6. Solution layout

```text
DiagnosticCatalog/
├── src/
│   ├── DiagnosticCatalog/                  → lib, ships the attributes
│   ├── DiagnosticCatalog.Analyzers/        → analyzer assemblies
│   ├── DiagnosticCatalog.CodeFixes/        → code fix assemblies, bundled into the above
│   ├── DiagnosticCatalog.Cli/              → the dcat tool (§14.1)
│   ├── DiagnosticCatalog.Self/             → this library's own rules, catalogued
│   └── DiagnosticCatalog.<Vendor>/         → one generated catalogue each (§14)
├── eng/
│   ├── catalogs.json                       → which catalogues exist, and their sources
│   ├── catalogs.schema.json                → what that manifest accepts
│   ├── CatalogGen/                         → generator, never shipped (§14.1)
│   └── CatalogGen.Worker/                  → loads analyzers out of process
├── tests/
│   ├── *.UnitTests/                        → one per shipped project, plus the documentation set
│   ├── DiagnosticCatalog.Usage/            → a consumer whose BUILD is the assertion
│   └── CatalogGen.*Fixture/                → assemblies the generator is pointed at
├── tools/                                  → release, commit and icon tooling (POSIX sh; ADR-0013)
├── build/                                  → shared MSBuild props, including the net472 floor
├── assets/                                 → the icon template every catalogue is drawn from
└── doc/
```

The catalogues are not listed here. There are more of them than of anything else, they arrive
faster than any other kind of project, and [`eng/catalogs.json`](../eng/catalogs.json) already
names every one — a second roster is one that goes stale between a reader opening this page and
finishing it.

A package per publishable project, not one for the whole repository — see §16 for the rationale,
and *Release trains* in [`CONTRIBUTING.md`](../CONTRIBUTING.md) for which of them version
together. There is no separate `.Package` project: each package is produced by the project that
owns its content, which is what makes `<ReleaseTrain>` the whole of a project's membership.

### 6.1 Analyzer release tracking

Both analyzer projects must ship `AnalyzerReleases.Shipped.md` and
`AnalyzerReleases.Unshipped.md`. Without them the Roslyn analyzer SDK reports
`RS2008` for every declared diagnostic.

---

## 7. The public rule model

### 7.1 Marker attribute

```csharp
namespace DiagnosticCatalog;

/// <summary>
/// Identifies a static type that represents a diagnostic rule.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class,
    AllowMultiple = false,
    Inherited = false)]
public sealed class DiagnosticRuleAttribute : Attribute
{
}
```

The attribute marks a class as a diagnostic rule. It suppresses nothing and
changes no compiler behaviour.

The attribute deliberately carries **no arguments**. Putting the id and category
on the attribute would duplicate the constants without removing the need for
them, since one attribute's arguments cannot be referenced from another
attribute.

It must also never be made `[Conditional]`. Rule discovery reads the marker from
referenced assembly metadata (§13); a conditional marker would make every
catalogue distributed as a package invisible to the analyzer (§3.4).

### 7.2 Matching by metadata name

The analyzer recognises the attribute by its **fully qualified metadata name**,
`DiagnosticCatalog.DiagnosticRuleAttribute`, regardless of which assembly
declares it.

This is a load-bearing decision, not an implementation detail. It means a
catalogue may either reference `DiagnosticCatalog` or declare its
own `internal sealed class DiagnosticRuleAttribute` in that namespace — the
`IsExternalInit` / PolySharp pattern — and remain entirely dependency-free.

It also eliminates a silent failure mode. Were matching based on symbol
identity, a catalogue whose consumers cannot resolve
`DiagnosticCatalog.dll` would see `[DiagnosticRule]` degrade to an
error type; the analyzer would find no rules at all and **every check would go
quiet with no diagnostic reported**.

As a fallback, the analyzer may also accept the purely structural shape — a
static nested class exposing `const string Id` and `const string Category` —
with the attribute serving as the explicit, preferred opt-in signal.

### 7.3 Minimal definition

A valid rule is a static class marked `[DiagnosticRule]` exposing two public
constants, the second of which reaches a category declared per §8.5:

```csharp
[DiagnosticCategory]
internal static class JdCategory
{
    public const string Usage = "Usage";
}

[DiagnosticRule]
public static class JD0007
{
    public const string Id = nameof(JD0007);
    public const string Category = JdCategory.Usage;
}
```

A catalogue declares that container once, and every sample below refers to this
one rather than repeating the declaration — which is §8.5's requirement applied
to this document.

The full canonical form nests rules inside a container class:

```csharp
namespace JustDummies.Analyzers.Suppressions;

public static class JustDummiesRules
{
    [DiagnosticRule]
    public static class JD0007
    {
        public const string Id = nameof(JD0007);
        public const string Category = JdCategory.Usage;
    }
}
```

`nameof(JD0007)` inside `JD0007` resolves to the containing type's name and is a
valid constant expression. Using it makes `DCAT0005` and `DCAT0013` structurally
impossible to violate, and is the form `DCAT0012` asks for.

### 7.4 Naming the container

Every use site pays for the container name twice:

```csharp
[SuppressMessage(JustDummiesRules.JD0007.Category, JustDummiesRules.JD0007.Id)]
[SuppressMessage(Dummies.JD0007.Category, Dummies.JD0007.Id)]
```

The catalogue form is inherently more verbose than the literal it replaces.
Keep container names **short** — `Dummies.JD0007.Id`, not
`JustDummiesRules.JD0007.Id`. This matters most for large generated catalogues.

Name the container for what it holds: **`{Vendor}Rule`** for the rules and
**`{Vendor}Category`** for the categories (§7.7). `SonarRule.S1144.Id` then reads
as "Sonar rule S1144's id", and the singular carries better at the use site than a
plural would.

**One constraint bounds the shortening: never name the container after the first
segment of its own namespace.** A container `JustDummies` declared in
`namespace JustDummies.Analyzers.Suppressions` is unusable. A consumer writing
`using JustDummies.Analyzers.Suppressions;` resolves the simple name
`JustDummies` to the namespace — a member of the global namespace, which is
found before any type imported by a using-directive — so every reference fails
with `CS0234`. Only the catalogue author can fix it, by renaming; the consumer
cannot work around it.

### 7.5 Optional metadata

A rule may expose further metadata:

```csharp
[DiagnosticRule]
public static class JD0007
{
    public const string Id = nameof(JD0007);
    public const string Category = JdCategory.Usage;

    public const string Title =
        "Dummy factories should follow the expected convention";

    public const string MessageFormat =
        "Type '{0}' does not follow the expected dummy factory convention";

    public const string Description =
        "Explains the condition detected by the analyzer.";

    public const string HelpLinkUri =
        "https://justdummies.io/analyzers/JD0007";

    public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;
}
```

These members are not required to use the rule in a suppression attribute, and
the first version does not validate them. They are not decoration, however:
they are the arguments of `DiagnosticDescriptor` (§15.2).

**Dependency caveat.** Every member above is a plain string except `Severity`.
`DiagnosticSeverity` is an enum and therefore constant-capable, but it lives in
`Microsoft.CodeAnalysis.Common` — a rule exposing it forces a Roslyn dependency
on every consumer of the catalogue. Declare `Severity` only in a project that
already references Microsoft.CodeAnalysis, typically the analyzer itself
(§15.2). A standalone catalogue package must stay on plain strings.

**Known limit.** Localised text (`LocalizableString`, resx-backed descriptors)
falls outside the `const` model. The catalogue covers the id/category axis;
resource files remain the right tool for translated text.

For third-party catalogues, the upstream title is carried as an XML documentation
comment rather than as a `Title` constant, and no `Description` constant is
emitted — see §14.1 and ADR-0014.

### 7.6 Catalogue provenance

A catalogue that mirrors somebody else's analyzer is a snapshot, and nothing in
the compiled assembly would otherwise say which release it reflects or how stale
it is. The foundation therefore exposes a second, assembly-level attribute:

```csharp
namespace DiagnosticCatalog;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class CatalogSourceAttribute : Attribute
{
    public CatalogSourceAttribute(string source, string sourceVersion, string generatedOn);

    public string Source { get; }
    public string SourceVersion { get; }
    public string GeneratedOn { get; }
}
```

Applied by the generated catalogue:

```csharp
[assembly: CatalogSource(
    source:        "SonarAnalyzer.CSharp",
    sourceVersion: "10.31.0.145097",
    generatedOn:   "2026-07-30")]
```

The date is a **string**, not a `DateTime`: attribute arguments must be
compile-time constants and no date type can be one (§3.1). The value is an ISO
8601 calendar date, `yyyy-MM-dd`, the same convention `AssemblyMetadataAttribute`
is used for.

Like `DiagnosticRuleAttribute`, this attribute must never be made
`[Conditional]` (§3.4): it is read from metadata, which is the whole point.
A later analyzer can use it to report a catalogue whose snapshot has aged past a
configured threshold, or whose `SourceVersion` no longer matches the analyzer
package the project actually references. Neither check ships in 1.0 (§24).

The attribute is meant for *generated* catalogues. A first-party catalogue
maintained by hand next to its own analyzer needs no provenance record: the two
ship from one repository at one version (§15).

### 7.7 Categories declared once

A catalogue repeats very few distinct categories across very many rules: the Sonar
catalogue spends 456 rule declarations on 13 distinct values, StyleCop 193 on 8.
Repeating the literal in every rule is 456 places for one value to drift. The
foundation therefore exposes a third attribute, marking the class that declares
each category once:

```csharp
namespace DiagnosticCatalog;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DiagnosticCategoryAttribute : Attribute
{
}
```

```csharp
[DiagnosticCategory]
internal static class SonarCategory
{
    public const string MajorCodeSmell = "Major Code Smell";
    public const string MinorCodeSmell = "Minor Code Smell";
}

public static class SonarRule
{
    [DiagnosticRule]
    public static class S1144
    {
        public const string Id = nameof(S1144);
        public const string Category = SonarCategory.MajorCodeSmell;
    }
}
```

**The indirection is free.** A `const` initialised from another `const` is still a
compile-time constant, so `SonarRule.S1144.Category` remains valid as an attribute
argument and still folds to the literal `"Major Code Smell"` in metadata
(Appendix A10). Nothing in §10 changes either: the argument still resolves to the
field `Category` declared on the rule type, so `DCAT0001` compares the same two
symbols it always did. The initialiser is not part of the resolution.

**What the marker buys.** The categories would fold identically as plain constants
without it, and that is why the marker is needed rather than why it is optional:
without it an analyzer cannot tell a category constant from any other string constant
in the assembly. With it, the generator marks the container it emits, and a future
check can validate that the class holds nothing but non-empty `const string` members.
It is also what a catalogue's own analysis reads. Applying it is **required** of any
class a rule reaches its category through — §8.5, reported as `DCAT0011`
([ADR-0028](adr/0028-require-every-rule-to-reach-its-category-through-a-declared-constant.en.md)).

A generated container is `internal` ([ADR-0026](adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)),
so no fix may offer `SonarCategory.MajorCodeSmell` to a consumer: naming a
category apart from its rule survives the vendor recategorising that rule, and
the suppression is then left asserting a category the rule no longer carries —
which, by §3.2, nothing will ever report.

Like the other two attributes, it must never be made `[Conditional]` (§3.4).

**Naming the constants.** A generated catalogue derives each constant name
mechanically from the category value — strip non-identifier characters, capitalise
each word. `"Major Code Smell"` becomes `MajorCodeSmell`;
`"StyleCop.CSharp.SpacingRules"` becomes `StyleCopCSharpSpacingRules`. The result
sometimes stutters, and stripping a common prefix would read better. It is
deliberately not done: the common prefix changes the moment upstream adds a
category outside it, which would rename every existing constant and break every
consumer that referenced one (§23.1). Stability outranks elegance here.

---

## 8. Structural contract of a rule

### 8.1 The rule type

The type must be:

* a class;
* static;
* non-generic;
* accessible from consuming code when the catalogue is public.

Invalid — not static:

```csharp
[DiagnosticRule]
public sealed class JD0007
{
}
```

Invalid — generic:

```csharp
[DiagnosticRule]
public static class JD0007<T>
{
}
```

### 8.2 The `Id` member

The rule must expose exactly one public member named `Id`:

```csharp
public const string Id = "...";
```

The value must be non-null, non-empty, not whitespace-only, and must match the
canonical identifier of the diagnostic. The recommended form is
`nameof(JD0007)`.

When the diagnostic identifier is not a valid C# identifier, the type name and
the id necessarily differ:

```csharp
[DiagnosticRule]
public static class RULE_001
{
    public const string Id = "RULE-001";
    public const string Category = JdCategory.Usage;
}
```

### 8.3 The `Category` member

The rule must expose exactly one public member named `Category`:

```csharp
public const string Category = "...";
```

The value must be non-empty and must match the category declared by the
originating analyzer's `DiagnosticDescriptor`. Because nothing verifies this at
runtime (§3.2), accuracy here is a matter of catalogue credibility.

Where that value comes from is a separate requirement, §8.5.

### 8.4 No inheritance

A rule must not inherit from a base class representing a diagnostic. A static
class cannot participate in classic inheritance, and abstract properties could
never be used as constant attribute arguments. `DiagnosticCatalog` therefore
defines an analyzer-verified structural contract, not an inheritance-imposed
object-oriented one.

### 8.5 The `Category` member reaches a declared category

The initialiser of `Category` must resolve to a `const string` declared in a class
marked `[DiagnosticCategory]` (§7.7):

```csharp
[DiagnosticCategory]
internal static class ContosoCategory
{
    public const string Usage = "Usage";
}

[DiagnosticRule]
public static class CT0001
{
    public const string Id = nameof(CT0001);
    public const string Category = ContosoCategory.Usage;
}
```

Resolution is semantic, so every spelling binding to the same field satisfies this: a
qualified name, an aliased container, a `using static`, a container declared in another
assembly. What does not satisfy it is an initialiser that is constant without being one
field reference — a literal, a `nameof`, a concatenation — because none of those leaves
the value with a single declaration.

This requirement is about the catalogue rather than about the rule. A rule failing it
compiles, folds to the same literal in metadata (Appendix A10) and suppresses exactly what
it should; §10 is unaffected, since the argument still resolves to the `Category` field on
the rule type and the initialiser plays no part in that resolution. What it costs is one
spelling per category value, across a catalogue that repeats very few of them over very
many rules.

Violation is reported as `DCAT0011`, at `Warning`: the audience is whoever authors a
catalogue, which is ADR-0027's split, and there is no error to report.

Because the initialiser is syntax, this is the one requirement of §8 that cannot be
evaluated over a metadata symbol. It is therefore source-only in the strict sense —
`DCAT0010` (§11.10) does not replay it across an assembly boundary.

---

## 9. Supported suppression attributes

The two suppression attributes are **not interchangeable**. They are decoded by
different components, with different accepted identifiers:

| Attribute | Decoded by | Accepted ids | Category used? |
| --- | --- | --- | --- |
| `SuppressMessageAttribute` | Roslyn (`SuppressMessageAttributeState`) | any | no |
| `UnconditionalSuppressMessageAttribute` | ILLink / ILCompiler | **`IL####` only** | no |

### 9.1 `UnconditionalSuppressMessageAttribute` is trim/AOT only

The name means "not `[Conditional]`". The BCL documents the distinction on the
type itself:

> *UnconditionalSuppressMessageAttribute is different than
> SuppressMessageAttribute in that it doesn't have a ConditionalAttribute. So it
> is always preserved in the compiled assembly.*

That preservation is a requirement rather than a detail: ILLink and ILCompiler
read suppressions from the **compiled assembly**, long after the compiler has
run, so a `[Conditional]` attribute would be invisible to them (§3.4).

Its decoder rejects anything that is not an IL warning id:

```csharp
if (!(attribute.ConstructorArguments[1].Value is string warningId)
    || warningId.Length < 6
    || !warningId.StartsWith("IL")
    || !int.TryParse(warningId.AsSpan(2, 4), out info.Id))
```

`[UnconditionalSuppressMessage(Rules.JD0007.Category, Rules.JD0007.Id)]` is
therefore ignored outright — the attribute is not processed by Roslyn's
suppression state either.

Supporting this attribute consequently means supporting a catalogue of **trim
and AOT warnings** (`IL2026`, `IL3050`, …), not "the same model with a different
attribute". It also yields a cheap, useful diagnostic: warn when a rule whose
`Id` does not match `IL####` is used in `UnconditionalSuppressMessage`
(`DCAT0009`) — a silent no-op that nothing else reports today.

**Two decoders read this attribute, and the one quoted above is only the
linker's.** The compile-time trim analyzer — the source of the `IL2xxx` warnings
seen during an ordinary build — implements its own rule: truncate at the first
colon, then match the id exactly. Measured rather than assumed, and the
divergence is one shape wide (A13). §11.9 records which of the two `DCAT0009`
mirrors and what that leaves uncovered.

### 9.2 Placement

Suppressions may be placed on a type, a method, a property, a field, an
assembly, or inside a `GlobalSuppressions.cs` file.

### 9.3 Alias resolution

Analysis must never depend on the short name written in source; it must resolve
the attribute's real symbol. Aliases are therefore supported:

```csharp
using Suppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

[Suppress(
    SomeRules.RULE001.Category,
    SomeRules.RULE001.Id,
    Justification = "...")]
```

---

## 10. Analyzing use sites

### 10.1 Mandatory implementation path

**`AttributeData` cannot be used.** By the time constructor arguments are
exposed as `TypedConstant`, constants have been folded: you get the value
`"Usage"` and the `IFieldSymbol` is gone. The whole of this section is
unimplementable through `AttributeData`.

The required path is:

```csharp
context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
// then, per argument expression:
var symbolInfo = context.SemanticModel.GetSymbolInfo(argument.Expression);
```

`IAttributeOperation` does preserve the underlying `IFieldReferenceOperation`,
but it requires Microsoft.CodeAnalysis 4.6 or later. The syntax-based path works
on every supported Roslyn version and is the one specified here.

### 10.2 Principle

For each of the first two arguments the analyzer resolves:

* the referenced constant field;
* its declaring type;
* whether that type carries `[DiagnosticRule]`.

The use site is coherent when both fields belong to the same rule type.

### 10.3 Valid case

```csharp
[SuppressMessage(
    SomeRules.RULE001.Category,
    SomeRules.RULE001.Id,
    Justification = "...")]
```

```text
Category → SomeRules.RULE001
Id       → SomeRules.RULE001
```

No diagnostic.

### 10.4 Invalid case

```csharp
[SuppressMessage(
    SomeRules.RULE001.Category,
    SomeRules.RULE002.Id,
    Justification = "...")]
```

```text
Category → SomeRules.RULE001
Id       → SomeRules.RULE002
```

`DCAT0001` is reported.

### 10.5 Accepted syntactic forms

Analysis relies on Roslyn symbols, not on source text. The canonical form is
qualified member access:

```csharp
SomeRules.RULE001.Category
```

A type alias is fully equivalent and recommended when the container name is
long:

```csharp
using Rule = SomeRules.RULE001;

[SuppressMessage(Rule.Category, Rule.Id, Justification = "...")]
```

`using static` is **recognised but not recommended**:

```csharp
using static SomeRules.RULE001;

[SuppressMessage(Category, Id, Justification = "...")]
```

Two `using static` directives for two rules in the same file make `Category` and
`Id` ambiguous, which is a compile error. The form therefore only works for one
rule per file and breaks as soon as a second suppression is needed. The analyzer
must resolve it; the documentation must not promote it.

### 10.6 Intermediate constants

```csharp
private const string RuleId = SomeRules.RULE001.Id;

[SuppressMessage(SomeRules.RULE001.Category, RuleId, Justification = "...")]
```

This form is **checkable**, contrary to what a first reading suggests. When an
argument resolves to a constant field whose declaring type is not a rule type,
the analyzer compares its constant *value* exactly as it does for a literal
(§11.6). It is not the canonical form and no code fix is offered, but it is not
a blind spot.

---

## 11. Foundation diagnostics

The diagnostic prefix is `DCAT`, and it is settled: it was checked against the
known community prefixes before publication, because a released id is a contract
nobody renames (§23) and there is no registry to consult afterwards
(Appendix B2).

**Shipped** says whether 1.0 implements the diagnostic. The two that do not were
specified and deliberately left out (§24); they keep their ids so that
implementing one later cannot reuse a number this document has already spent.

| Id | Target | Title | Default severity | Shipped |
| --- | --- | --- | --- | --- |
| `DCAT0001` | use site | Category and Id must reference the same diagnostic rule | Error | yes |
| `DCAT0002` | definition | A diagnostic rule must be declared as a static non-generic class | Warning | yes |
| `DCAT0003` | definition | A diagnostic rule must expose a public constant string named Id | Warning | yes |
| `DCAT0004` | definition | A diagnostic rule must expose a public constant string named Category | Warning | yes |
| `DCAT0005` | definition | The diagnostic rule type name should match its Id | Info | yes |
| `DCAT0006` | use site | Use a diagnostic catalog reference instead of string literals | Error | **yes — core** |
| `DCAT0007` | use site | Suppression mixes a catalog reference with a string literal | Error | yes |
| `DCAT0008` | use site | Suppression identifier does not resolve to a known diagnostic rule | None (opt-in) | no |
| `DCAT0009` | use site | UnconditionalSuppressMessage only accepts IL#### identifiers | Warning | yes |
| `DCAT0010` | use site | Referenced diagnostic rule type is malformed | Warning | no |
| `DCAT0011` | definition | A diagnostic rule's category must reference a declared category constant | Warning | yes |
| `DCAT0012` | definition | A rule identifier should be written as nameof | Warning | yes |
| `DCAT0013` | definition | The diagnostic rule type name does not say its Id | Warning | yes |
| `DCAT0014` | use site | A suppression must carry a justification | Warning | yes |

Definition diagnostics (`DCAT0002`–`DCAT0005`, `DCAT0011`–`DCAT0013`) only fire on
source the compiler can see. A malformed rule inside a *referenced assembly*
produces nothing — which is what `DCAT0010` exists to cover, for every requirement
it can evaluate over a metadata symbol.

Two of them are narrower still, and permanently so, because they decide a question
about the SOURCE rather than about the symbol and metadata carries no answer to it:
§8.5 reads an initialiser (`DCAT0011`), and `DCAT0012` reads whether an identifier
was written as `nameof` (§11.12).

### 11.1 `DCAT0001` — members from different rules

Reported when the two arguments do not name one rule's `Category` and that same
rule's `Id`. Two faults fall under it.

The arguments resolve to fields declared on two different `[DiagnosticRule]`
types:

```csharp
[SuppressMessage(
    SomeRules.RULE001.Category,
    SomeRules.RULE002.Id,
    Justification = "...")]
```

Or a member fills a slot that is not its own. A rule type carries more than the
pair — a generated catalogue emits `HelpLinkUri` beside it — so completion offers
all of them in one list, and the declaring types match in the first case below:

```csharp
[SuppressMessage(SomeRules.RULE001.Id, SomeRules.RULE001.Category)]
[SuppressMessage(SomeRules.RULE001.Category, SomeRules.RULE001.HelpLinkUri)]
```

Both compile, both resolve, and neither suppresses anything: Roslyn matches a
suppression on the identifier alone (§3.3), and the identifier slot names no
diagnostic in either.

A misplaced member is reported **without a fix**. The two alignments of §12.1
rewrite one slot to the other's rule, which would leave the wrong member sitting
in the other; and whether the wrong member or the wrong rule was written is not
something a tool can know (ADR-0018).

### 11.2 `DCAT0002` — invalid rule type

Reported when a `[DiagnosticRule]` type is not a static non-generic class.

### 11.3 `DCAT0003` — missing or invalid identifier

Reported when the `Id` member is absent, not public, not a field, not constant,
not of type `string`, or empty.

### 11.4 `DCAT0004` — missing or invalid category

Same validations as `Id`.

### 11.5 `DCAT0005` — type name differs from identifier

```csharp
[DiagnosticRule]
public static class RULE_0001
{
    public const string Id = "RULE-0001";
    public const string Category = JdCategory.Usage;
}
```

Reported when the type name is not the identifier **and no name could have
been**, which is the case §8.2 blesses: the identifier carries a character C#
forbids in an identifier, and the type is named a legalisation of it.

**Precise trigger condition.** With `Id` differing from the type name:

1. `SyntaxFacts.IsValidIdentifier(Id)` is `false` — asked of the WHOLE
   identifier, before any truncation; and
2. the type name, reduced to its letters and digits, **starts with** the
   identifier reduced the same way, after truncation at the first colon (§11.6).

Otherwise the divergence was chosen rather than imposed, and `DCAT0013` (§11.13)
reports it.

**This replaces the trigger condition earlier revisions specified**, which used
`IsValidIdentifier` as a *silencer* — report only when it returns `true` — and so
said nothing at all about the declaration that most needs saying something about:

```csharp
[DiagnosticRule]
public static class RULE42
{
    public const string Id = "RULE-0001";  // silent under the old condition
    public const string Category = JdCategory.Usage;
}
```

The predicate answers *"was the exact name available?"*, not *"is the name
excusable?"*. Used as a silencer it granted the excuse to every identifier that
could not be a type name, whether or not the type made any attempt to render it.

**Severity, and why it is reported at all.** Nothing is repairable here:
`RULE_0001` and `RULE0001` both render `"RULE-0001"` and this specification
elects neither. `Info` and no code fix follow from that. It is reported rather
than passed over because `DCAT0013` fails the same comparison one step later — so
this diagnostic is the boundary between the two made visible, and the only handle
a consumer has for raising it in `.editorconfig`.

Two `Id` values that are not valid C# identifiers, and land on opposite sides:

```csharp
public static class MW0002          { public const string Id = "MW-0002"; }        // DCAT0005
public static class IL2026Annotated { public const string Id = "IL2026:Members…"; } // DCAT0005
public static class RULE42          { public const string Id = "RULE-0001"; }      // DCAT0013
```

The second holds because the identifier is truncated at the first colon before
comparison, exactly as §11.6 truncates a suppression's — a form ILLink honours
must not be reported as a naming fault.

### 11.6 `DCAT0006` — replaceable string literals

```csharp
[SuppressMessage("Usage", "JD0007", Justification = "...")]
```

**Identifier normalisation is mandatory.** Before comparison, truncate the
literal at the first colon, matching Roslyn's own behaviour (§3.3):

```text
"JD0007:Dummy factories should follow the convention"  →  "JD0007"
```

Skipping this step makes the analyzer miss the form Visual Studio generates,
i.e. the bulk of the code worth migrating.

Matching rules:

* no known rule matches the normalised `(Category, Id)` pair → no diagnostic;
* exactly one rule matches → diagnostic plus a deterministic code fix;
* several rules match → diagnostic without a single automatic fix.

The code fix drops the friendly-name suffix. This is an accepted, documented
trade-off: the rule's `Title` constant or its XML documentation replaces it.

**Default severity is `Error`, not `Info`** (ADR-0027). Referencing a catalogue
package is itself the statement of intent: a project that has taken the
dependency has decided its suppressions are catalogue references, and a
suggestion no build output shows does not carry that decision. The cost is
deliberate and belongs in the release notes — adopting a catalogue fails the
build on every existing literal suppression at once. A project migrating gradually lowers it in
`.editorconfig` (§17), which is also how the diagnostic was always meant to be
tuned.

### 11.7 `DCAT0007` — mixed reference and literal

```csharp
[SuppressMessage(SomeRules.RULE001.Category, "RULE001", Justification = "...")]
```

The most common partially migrated state, and **the only case where the fix is
fully deterministic**: the intended rule is known from the already-migrated
argument, so there is no ambiguity to resolve. Higher practical value than
`DCAT0001`.

### 11.8 `DCAT0008` — unresolvable identifier (strict mode)

Disabled by default. When a project opts in, every `checkId` must resolve to a
known catalogue rule; any literal or unknown identifier is reported.

This is the endgame of the library: it turns "my suppressions are catalogue
references" from a convention into an enforced invariant. It is opt-in because
a project that references analyzers without a matching catalogue would otherwise
be flooded.

### 11.9 `DCAT0009` — non-IL identifier in `UnconditionalSuppressMessage`

Reported when a rule whose `Id` does not match `IL####` is used in
`UnconditionalSuppressMessageAttribute` (§9.1). The suppression is a silent
no-op that no other tool reports.

**The attribute is read by two different decoders, and they are not the same
one** (A13). The linker reads it from the compiled assembly, accepting anything
whose characters 3–6 parse as a number; the compile-time trim analyzer truncates
at the first colon and then matches the id exactly. They agree on everything a
generated catalogue can produce — an `Id` of `nameof(IL2026)` is `IL2026` — and
diverge on one shape: `IL####` followed by anything that is not a colon.
`IL20265` is honoured by the linker as `IL2026` and ignored at compile time.

The check mirrors the **linker**, so it stays quiet on that shape. Reporting it
would say "this is not an IL identifier", which is false: it is one, malformed.
The consequence is bounded and worth stating — a suppression of that shape works
when publishing and not when building, and `DCAT0009` does not name it. It is
unreachable from a generated catalogue, and a hand-written rule declaring
`Id = "IL20265"` has a larger problem than this diagnostic.

### 11.10 `DCAT0010` — malformed referenced rule

Reported at the use site when a referenced `[DiagnosticRule]` type does not
satisfy the structural contract, and is therefore unusable. Covers the blind
spot left by `DCAT0002`–`DCAT0004` across assembly boundaries.

Deliberately the structural contract alone. A naming fault does not make a rule
unusable — the reference resolves and suppresses what it says it does — and the
consumer of a catalogue cannot repair a name they do not own.

### 11.11 `DCAT0011` — category not reached through a declared constant

Specified in §8.5, which states the requirement and the forms that satisfy it.

### 11.12 `DCAT0012` — identifier not written as `nameof`

Reported when the value of `Id` **is** the type's name and the initialiser is not
a `nameof` invocation.

```csharp
[DiagnosticRule]
public static class JD0007
{
    public const string Id = "JD0007";  // reported
    public const string Category = JdCategory.Usage;
}
```

Nothing is incorrect. The literal agrees with the type name at the moment it is
written and nothing holds it there: renaming the type leaves it behind, still
compiling, now naming a rule the type is not. §7.3 recommends `nameof` for
exactly this reason, and this is the diagnostic that says so.

**The only diagnostic in this specification decided on syntax.** `nameof(JD0007)`
and `"JD0007"` are the same constant once folded, so `IFieldSymbol.ConstantValue`
cannot separate them and a rule read from metadata carries no trace of which was
written. It is therefore not reported against a referenced assembly, and that is
not a gap: across that boundary there is no longer a form to recommend.

**Any `nameof` satisfies it**, qualified or not — `nameof(Vendor.JD0007)` is held
together by the same operator. An implementation MAY match the invocation on the
token's text: a constant initialiser reading `nameof(...)` cannot be an ordinary
call, since a call is not a constant expression.

**Location.** The initialiser expression, not the type's identifier token where
every other definition diagnostic reports. The fault is the expression, and the
fix rewrites the expression.

**Code fix — *Use `nameof`*.** Offered when `Id` is a field of its own; declined
when one field declaration carries several declarators, per §12's rule against
repairing a member the diagnostic did not name.

### 11.13 `DCAT0013` — type name does not say the identifier

Reported when the type name is not the identifier and **nothing forced that** —
the complement of §11.5, and the branch that specification's earlier trigger
condition left silent.

```csharp
[DiagnosticRule]
public static class RuleSeven
{
    public const string Id = "JD0007";  // JD0007 was available as a type name
    public const string Category = JdCategory.Usage;
}
```

**Precise trigger condition.** With `Id` differing from the type name, either:

1. `SyntaxFacts.IsValidIdentifier(Id)` is `true` — the exact name was available
   and was not taken; or
2. it is `false`, and the type name reduced to its letters and digits does not
   start with the identifier reduced the same way (§11.5).

The first clause catches a case comparing letters and digits alone would forgive:
`RULE001` declaring `"RULE_001"`, where an underscore is legal in an identifier
and the type could have been spelled exactly.

**Rationale.** The reference compiles, resolves and suppresses correctly, and
reads as something it is not: `Vendor.RuleSeven.Id` names `JD0007` at every use
site. That is a worse failure than an unusable rule, which announces itself.

**No code fix.** Renaming the type changes a published name; rewriting the
identifier changes which diagnostic is suppressed. Which of the two is the
mistake is not knowable from the code (ADR-0018).

**Severity.** `Warning`, alongside the other definition diagnostics rather than
above them. The rule is new and has one known false-positive shape behind it — the
friendly-name form of §11.5 — so it earns a release before being allowed to stop a
build. A catalogue author wanting it stricter raises it in `.editorconfig`, which
is what reporting it at all provides.

### 11.14 `DCAT0014` — suppression without a justification

Reported at the use site when a suppression carries no `Justification`, or carries
one that is blank.

```csharp
[SuppressMessage(SomeRules.RULE001.Category, SomeRules.RULE001.Id)]   // reported
[SuppressMessage("Usage", "RULE001")]                                 // reported
```

Every other use-site diagnostic in this document checks WHICH diagnostic a line
silences. This one checks that the line says WHY, which is the half no tool can
reconstruct afterwards: the warning is gone, and the reason it was acceptable
exists only in the head of whoever wrote the attribute.

**Presence, never quality.** The value is read for its length. §5 rules out
assessing what a justification says and §24 rules out validating one
intelligently; both remain in force, and this diagnostic is deliberately the
weakest check that still closes the gap. A one-word reason satisfies it.

**One non-blank value is refused**: `"<Pending>"`, the placeholder the IDE writes
when it generates a suppression. It is matched exactly and case-sensitively —
recognising a tool's own token for "not written yet" is reading a marker, whereas
ruling on `"n/a"` or `"obvious"` would be reading prose. `Justification = null`
is blank, as is any string of whitespace.

**Trigger condition.** Every suppression the analyzer reads, whether its pair
references a catalogue rule or is written entirely in values
([ADR-0037](adr/0037-require-a-justification-on-every-suppression.en.md)). This is
the one diagnostic here that resolves no rule to have something to say, so the
restriction §11.9 places on itself does not apply: `DCAT0009` needs the identifier
to be a rule before it can judge it, and this needs only the attribute. It is also
the only check reaching a literal that names a rule no referenced catalogue
describes — `DCAT0006` matches such a pair against nothing and stays silent, so
before this the line was reported by nothing at all. Applies to both attributes of
§9.1; the trimmer's carries the same property.

**The one exception** is an identifier that resolves to no value — `null`, which
compiles. Roslyn matches a suppression on the identifier, so such a line silences
nothing, has nothing to justify, and gives the message nothing to name.

**Location.** The whole attribute, as every use-site diagnostic reports.

**Reported alongside other faults.** The question is independent of the pair's
state, so an incoherent, half-migrated or replaceable suppression that also says
nothing reports both. A literal pair matching a known rule therefore carries
`DCAT0006` and this one at once, and applying the migration fix leaves this one
standing — converting a suppression does not answer the question it never
answered.

**No code fix**, and none is possible: what belongs there is the one part of the
attribute that cannot be read off the code, which is ADR-0018's exact
prohibition.

**Severity.** `Warning`, and not `Error` like the three use-site rules ADR-0027
promoted. It reports lines that are otherwise entirely correct, and it reports
them from the first build after the package is referenced rather than after a
migration — shipping it as an error would fail that build on every undocumented
suppression a codebase already had. One `.editorconfig` line raises it, and the
adoption guide names the line that lowers it while a backlog is worked through.

---

## 12. Code fixes

All fixes must set an explicit `EquivalenceKey` so that *Fix all occurrences*
applies one consistent choice across a document, project or solution.

### 12.1 Fixing an incoherent pair (`DCAT0001`)

For:

```csharp
[SuppressMessage(
    SomeRules.RULE001.Category,
    SomeRules.RULE002.Id,
    Justification = "...")]
```

two fixes must be offered, with distinct equivalence keys:

```text
Use RULE001.Id        (EquivalenceKey = "AlignOnCategory")
Use RULE002.Category  (EquivalenceKey = "AlignOnId")
```

Category-based correction:

```csharp
[SuppressMessage(
    SomeRules.RULE001.Category,
    SomeRules.RULE001.Id,
    Justification = "...")]
```

Identifier-based correction:

```csharp
[SuppressMessage(
    SomeRules.RULE002.Category,
    SomeRules.RULE002.Id,
    Justification = "...")]
```

The code fix must never guess which rule was intended. When the two rules live
in different containers or namespaces, the fix must also add the required
`using`.

### 12.2 Replacing string literals (`DCAT0006`)

For:

```csharp
[SuppressMessage("Usage", "JD0007", Justification = "...")]
```

when exactly one rule matches:

```csharp
[SuppressMessage(
    JustDummiesRules.JD0007.Category,
    JustDummiesRules.JD0007.Id,
    Justification = "...")]
```

The fix adds the necessary `using` directive.

### 12.3 Completing a mixed suppression (`DCAT0007`)

A single, deterministic fix: replace the remaining literal with the reference
from the rule already identified by the other argument.

### 12.4 Fixing a definition

When it can be done unambiguously:

* make a class static;
* make `Id` public;
* make `Category` public;
* replace `static readonly string` with `const string` when the expression is
  constant;
* add a missing member with a placeholder;
* rewrite an `Id` that spells its own type name as `nameof` (§11.12).

```csharp
public const string Category = "TODO";
```

The code fix must never invent a real category.

**Each of these is conditional, and the condition is the same one every time:
the repair must be written in the code already.** `static` is offered only to a
class that could carry it — no type parameters, no base list, no instance member
or constructor — and never to a `partial` type, whose other parts decide the
question and are not visible to the fix. The modifier repairs are offered only
when the member is a single-variable field holding a non-blank constant string,
which is what leaves the value untouched; a wrong type, a blank value or a
non-constant initialiser is refused, because the code says nothing about what
was meant. `Id` is added as `nameof(TheRule)` rather than as a placeholder,
being §8.2's recommended form and derived from the declaration rather than
invented; `Category` has no such source and takes the literal above.

A placeholder category **stops the diagnostic being reported**, since `"TODO"`
is not blank. That is the cost of a placeholder, and it is the reason that fix
is named for the constant it declares rather than for the rule it completes.

The `nameof` rewrite is the one item on the list that repairs nothing broken, and
so decides nothing: the value it writes is the value already there. It is refused
for a field declaring several constants at once, on the shared rule above, and for
a generic type, where `nameof` would have to name the constructed type.

**No fix exists for `DCAT0005` or `DCAT0013`**, and none may be added. Neither
has a repair the code determines: §11.5 has two renderings this specification
declines to choose between, and §11.13 has two repairs that change different
things — a published type name, or which diagnostic is suppressed.

### 12.5 Fixing every occurrence

Every fix above is offered at the **document**, **project** and **solution**
scopes. A codebase adopts a catalogue by meeting hundreds of literal
suppressions at once (§3.5), and converting them one invocation at a time is not
a migration anybody performs.

Two of the fixes edit a place the document SHARES between occurrences — a
suppression fix appends to the compilation unit's `using` list, the member fix
inserts at the top of a rule's body — so the batch fixer Roslyn provides cannot
serve them: it computes each occurrence's change against the pristine document
and drops any that conflicts with one already merged. What it drops is not the
insertion alone but that occurrence's whole document change, and the operation
then reports success having done part of the work. So the fixes that share an
offset rewrite each document **once, for all of its occurrences together**,
which leaves nothing to reconcile.

A wider scope reaches every C# document of the project, or of every project, and
must leave the rest of the solution exactly as it was: a project with no
occurrence comes back byte for byte, and no occurrence anywhere the scope
covered is left behind.

---

## 13. Catalogue discovery

The analyzer discovers rules in the current compilation and in referenced
assemblies. A rule is recognised when its type carries
`DiagnosticCatalog.DiagnosticRuleAttribute` (matched by metadata name, §7.2) and
exposes valid `Id` and `Category` members.

The analyzer builds an internal representation:

```csharp
internal sealed record DiagnosticRuleSymbol(
    INamedTypeSymbol RuleType,
    IFieldSymbol IdField,
    IFieldSymbol CategoryField,
    string Id,
    string Category);
```

This representation belongs to the analyzer implementation only and is not part
of the public API.

* The **functional** key of a rule is `Category + Id`, with the `Id` truncated
  at the first colon exactly as a written `checkId` is (§3.3). Both ends of the
  comparison are normalised or neither is: normalising only the query makes a
  rule that declares a suffixed identifier unmatchable by any suppression.
* The **structural** key of a reference is the Roslyn symbol of the
  `[DiagnosticRule]` type.

### 13.1 Indexing cost

Walking every type of every referenced assembly is an expensive metadata sweep,
and "index once per compilation" understates it. Two mandatory mitigations:

1. **Pre-filter assemblies.** Only visit assemblies whose
   `IAssemblySymbol.Modules.First().ReferencedAssemblies` include the
   `DiagnosticCatalog` assembly, or that declare the attribute themselves. Everything
   else cannot contain a rule.
2. **Build the index lazily** inside `RegisterCompilationStartAction`, behind a
   `Lazy<T>`, so the cost is paid only when a use site actually needs a
   value-based lookup — that is, only for `DCAT0006` / `DCAT0008`.

`DCAT0001`, `DCAT0007`, `DCAT0009` and `DCAT0014` need no index at all: each resolves its
rule from the attribute itself. `DCAT0007` does compare a value, but against the
rule its already-migrated argument names (§11.7), so it never looks one up —
comparing a value and looking one up are not the same need.

---

## 14. Consumption by a third-party catalogue

A specialised package may reference `DiagnosticCatalog` and declare rules for an
analyzer it does not own. Thirteen are implemented, all generated by the same tool
from the descriptors of their upstream package.

**Live rules** are the rules the mirrored release still declares. **Published
constants** are the rule types the catalogue actually ships, which is the live
rules *plus* every rule retired upstream and carried forward as `[Obsolete]`
(§14.1 rule 9, §23.1) — a constant is never deleted, so the second number only
ever grows. **Help links** counts the published constants that carry one; a
vendor populating `HelpLinkUri` on none of its descriptors yields a catalogue
with none, because nothing is synthesised (§14.1 rule 5). The two counts are
equal on every row today: no rule mirrored here has yet been retired upstream.

<!-- catalogue-facts:begin -->

| Catalogue | Mirrors | Live rules | Published constants | Categories | Help links |
| --- | --- | ---: | ---: | ---: | ---: |
| `DiagnosticCatalog.Sonar` | `SonarAnalyzer.CSharp 10.31.0.145097` | 456 | 456 | 13 | 0 |
| `DiagnosticCatalog.NetAnalyzers` | `Microsoft.CodeAnalysis.NetAnalyzers 10.0.302` | 318 | 318 | 10 | 318 |
| `DiagnosticCatalog.StyleCop` | `StyleCop.Analyzers.Unstable 1.2.0.556` | 197 | 197 | 8 | 197 |
| `DiagnosticCatalog.CodeStyle` | `Microsoft.CodeAnalysis.CSharp.CodeStyle 5.6.0` | 120 | 120 | 3 | 117 |
| `DiagnosticCatalog.NUnit` | `NUnit.Analyzers 4.14.0` | 99 | 99 | 3 | 99 |
| `DiagnosticCatalog.Xunit` | `xunit.analyzers 1.27.0` | 90 | 90 | 3 | 90 |
| `DiagnosticCatalog.Trimming` | `Microsoft.NET.ILLink.Tasks 10.0.10` | 77 | 77 | 3 | 0 |
| `DiagnosticCatalog.MSTest` | `MSTest.Analyzers 4.3.3` | 62 | 62 | 3 | 62 |
| `DiagnosticCatalog.Roslyn` | `Microsoft.CodeAnalysis.Analyzers 5.6.0` | 52 | 52 | 9 | 13 |
| `DiagnosticCatalog.AspNetCore` | `Microsoft.AspNetCore.App.Ref 10.0.10` | 35 | 35 | 3 | 26 |
| `DiagnosticCatalog.PublicApi` | `Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0` | 23 | 23 | 1 | 23 |
| `DiagnosticCatalog.Syslib` | `Microsoft.NETCore.App.Ref 10.0.10` | 13 | 13 | 4 | 13 |
| `DiagnosticCatalog.BannedApi` | `Microsoft.CodeAnalysis.BannedApiAnalyzers 5.6.0` | 3 | 3 | 1 | 2 |

<!-- catalogue-facts:end -->

`DiagnosticCatalog.Self` is generated the same way and is not in this table: it
mirrors this repository's own analyzers rather than a third party's (§15).

Every figure above is recounted from the generated sources and the compiled
assemblies by `CatalogueFactsTests`, so a regeneration that moves one fails the
build rather than leaving this table quietly wrong.

```csharp
using DiagnosticCatalog;

namespace DiagnosticCatalog.Sonar;

[DiagnosticCategory]
internal static class SonarCategory
{
    public const string MajorCodeSmell = "Major Code Smell";
}

public static class SonarRule
{
    [DiagnosticRule]
    public static class S1144
    {
        public const string Id = nameof(S1144);
        public const string Category = SonarCategory.MajorCodeSmell;
    }
}
```

Consumption:

```csharp
using DiagnosticCatalog.Sonar;

[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Instantiated through reflection by the DI container.")]
```

> **Accuracy requirement.** A third-party catalogue must derive every category
> from the target analyzer's actual `DiagnosticDescriptor`, never from
> documentation or from memory. Because the platform ignores the category
> (§3.2), a wrong value will never be reported by anything — and a catalogue
> whose whole purpose is to be the authoritative answer cannot afford silent
> inaccuracy. Sonar makes the point concrete: its categories are
> `{Severity} {Type}` pairs, so `S1144` is `"Major Code Smell"` and `S1481` is
> `"Minor Code Smell"`. No amount of reading the documentation yields those
> strings, and getting them wrong costs nothing and is never reported.

### 14.1 How a generated catalogue is produced

The generator (`eng/CatalogGen`) reads the upstream analyzer assembly's metadata
for the types it marks with `[DiagnosticAnalyzer]`, loads and constructs those,
and reads the `DiagnosticDescriptor` instances they declare. That is how the
compiler finds analyzers, and reading them the same way is what keeps a
catalogue to the rules a consumer's build can actually report
([ADR-0031](adr/0031-find-analyzers-the-way-the-compiler-finds-them.en.md)).
Descriptors are the only source that cannot have drifted.

```text
dcat generate \
    --package SonarAnalyzer.CSharp --package-version latest \
    --namespace DiagnosticCatalog.Sonar --container SonarRule \
    --output src/DiagnosticCatalog.Sonar/SonarRules.g.cs
```

The engine is `eng/CatalogGen` and the command line is `dcat`, the .NET tool it
ships inside ([ADR-0017](adr/0017-publish-the-generator-as-a-cli-on-its-own-release-train.en.md)).
The upstream release is `--package-version`, never `--version`: on a .NET tool
`--version` is universally read as "which version of the tool am I running", and
that is the meaning it keeps.

Every catalogue in the table above is declared in `eng/catalogs.json` instead, so
one `dcat generate --manifest eng/catalogs.json` produces all of them and the list
lives as data rather than as thirteen command lines.

Rules for the generator, each of which is load-bearing:

1. **Read descriptors, not documentation.** Rule-metadata JSON and published
   rule pages drift from what the analyzer declares, and per §3.2 the divergence
   is silent.
2. **Emit deterministically ordered output** so a regeneration diff shows only
   genuine upstream change.
3. **Report every exclusion.** A descriptor is skipped only when its category is
   empty — an entry that is not a suppressable diagnostic — or when its id is not
   a valid C# identifier. Both cases are printed with the id and the reason;
   nothing is dropped silently. For `SonarAnalyzer.CSharp 10.31.0.145097` that is
   nine `S9999-*` entries, which are internal metrics and telemetry channels.
4. **Ship ids, categories and titles.** The first two are facts about a third
   party's software; the title is that vendor's own sentence naming what the rule
   reports on, carried as the rule's documentation comment because an identifier
   restated cannot say what a rule is about. Rule descriptions and message formats
   are that vendor's documentation and must not be redistributed in the package
   (ADR-0014). A message format is also not one value per rule: 203 of Sonar's 456
   carry placeholders filled at analysis time and 37 carry nothing else, so
   publishing one would mean inventing a sentence no descriptor declares, which
   item 5 forbids.
5. **Do not synthesise values that were not read.** `SonarAnalyzer.CSharp`
   populates `HelpLinkUri` on 0 of its 465 descriptors, so the generated
   catalogue carries no help links rather than links assembled from a guessed
   URL pattern.
6. **Declare each category once** in a `[DiagnosticCategory]` class and have the
   rules refer to it (§7.7). This is §8.5 rather than a preference: a generator
   emitting the literal per rule produces a catalogue that reports `DCAT0011` on
   every rule it wrote.
7. **Take the requested language and the language-neutral assemblies, exclude the
   other languages.** Layouts differ and the difference is invisible if you get it
   wrong: Sonar ships one assembly directly under `analyzers/`, StyleCop uses
   `analyzers/dotnet/cs/`, and `Microsoft.CodeAnalysis.NetAnalyzers` uses both —
   the bulk of the CA rules sit in a language-neutral assembly at
   `analyzers/dotnet/`, with only the language-specific ones under `cs/` and
   `vb/`. Keeping only `.../cs/` silently drops most of the CA rules; keeping
   everything silently absorbs Visual Basic rules into a C# catalogue. Neither
   failure shows up in the output.
8. **Resolve `latest` to the latest *stable* version.** A catalogue mirrors a
   release people consume; `Microsoft.CodeAnalysis.NetAnalyzers` and
   `StyleCop.Analyzers` both publish prereleases ahead of stable, and pinning a
   catalogue to a preview by accident is silent too.
9. **Never delete a rule.** A rule the upstream package has stopped declaring is
   carried forward from the previous output and marked `[Obsolete]` with the
   version that dropped it. Deleting the constant would break its consumers'
   recompilation, because they inlined its value (§23.1); an obsolete constant
   gives them `CS0618` instead — a warning naming the rule and telling them to
   remove the suppression. If upstream ever restores the rule, the mark is
   dropped again automatically.
10. **Leave the file untouched when nothing moved, and rewrite it when anything
   did.** The generator renders the file it would write, reduces it and its own
   previous output to a canonical form — line endings normalised, the
   `generatedOn` stamp elided — and compares the two. Equal means the file is
   left exactly as it stands, `generatedOn` included; without that the scheduled
   job of §14.3 would open a pull request every night whose only content was a
   new date. Different means the file is rewritten, and stamped with today.

   Comparing the whole rendered file rather than a list of fields is what makes
   the check exhaustive. A catalogue publishes its rules *and* the namespace it
   declares, the class its rules sit in, the source it records and the language
   those analyzers were read for — the last four from the manifest rather than
   from upstream. A comparison made of rules and a version reported "current"
   for every one of them.

   Line endings are normalised because they are the one difference that is not
   published content: a checkout under `core.autocrlf` rewrites every line in the
   file and moves nothing a consumer can see.

   `dcat validate` is this same comparison stopped before the write (§14.3), which
   is why a pipeline can ask whether a catalogue is still true without touching
   the tree.
11. **Record provenance** with `[assembly: CatalogSource]` (§7.6).

### 14.2 Versioning a generated catalogue

Because §7.6 records the exact upstream version in metadata, the package version
does not have to encode it — and it does not: a catalogue's version runs on its
own Semantic Versioning line, incremented from what changed in the catalogue
(ADR-0015). A regeneration fix therefore needs no upstream release to hang off,
and an upstream release that changes no published rule moves no version. Either
way a constant is never deleted (§23.1): a rule retired upstream becomes
`[Obsolete]`.

Synchronisation with upstream is automated (§14.3).

### 14.3 Scheduled synchronisation

`.github/workflows/nightly-catalogs.yml` runs nightly and on demand. It
regenerates every catalogue listed in `eng/catalogs.json` — the list lives in the
repository as data, so it is not duplicated in CI configuration — and then:

1. stops silently when nothing changed, which is the normal outcome;
2. builds the whole solution, because a regeneration that no longer compiles means
   upstream changed shape and is a signal rather than something to paper over;
3. runs the generator a **second** time and fails if the output moved, which
   catches any loss of determinism before it can churn every future diff;
4. opens or updates a single pull request on a fixed branch, carrying in its body
   a report of every change to what the catalogues publish. The rule-level ones
   are named one by one — added, recategorised, retitled, relinked, retired,
   declared again upstream — and a change that moves the file without moving a
   rule is reported as such, so a reviewer meeting one knows to read the diff
   rather than the list.

**It never publishes a package.** The pull request exists because a category or an
id that moved upstream changes a published contract, and because the platform
never reads a suppression's category (§3.2) a wrong value merged here would
produce no symptom anywhere. A human has to look.

The job needs `contents: write` and `pull-requests: write`, and nothing else. It
uses the `gh` CLI already present on the runner rather than a third-party action,
so the only trust boundary is GitHub's own token.

---

## 15. Consumption by a first-party project

A project that owns its analyzer need not publish a separate catalogue package.

### 15.1 Direct exposure

```csharp
namespace JustDummies.Analyzers.Suppressions;

public static class Dummies
{
    [DiagnosticRule]
    public static class JD0007
    {
        public const string Id = nameof(JD0007);
        public const string Category = JdCategory.Usage;
    }
}
```

The architectural rule: **when a vendor owns the analyzer, the catalogue ships
with that analyzer. When it does not, an independent catalogue package is
published.**

### 15.2 Closing the loop with `DiagnosticDescriptor`

This is the strongest reason for a first-party project to adopt the convention,
and it is the point of §7.5. The analyzer should build its descriptor **from the
catalogue**:

```csharp
private static readonly DiagnosticDescriptor Descriptor = new(
    id:                 Dummies.JD0007.Id,
    title:              Dummies.JD0007.Title,
    messageFormat:      Dummies.JD0007.MessageFormat,
    category:           Dummies.JD0007.Category,
    defaultSeverity:    Dummies.JD0007.Severity,
    isEnabledByDefault: true,
    description:        Dummies.JD0007.Description,
    helpLinkUri:        Dummies.JD0007.HelpLinkUri);
```

One source of truth for the analyzer *and* for every suppression of it. The
category published by the catalogue is then exact by construction rather than by
diligence — which is precisely what a third-party catalogue cannot guarantee
(§14).

---

## 16. NuGet packaging

Roslyn analyzers are distributed in the `analyzers` folder of a NuGet package.
Analysis assemblies must never become runtime dependencies of the consuming
application.

### 16.1 Two packages, two audiences

A single package cannot serve both audiences, because they need opposite things:

| Audience | Needs | Reference style |
| --- | --- | --- |
| **Consumer** — writes suppressions | analyzers only | `PrivateAssets="all"`, no runtime dependency |
| **Catalogue author** — declares rules | `DiagnosticRuleAttribute` resolvable *by their own consumers* | ordinary `DiagnosticCatalog` reference, dependency declared — or the source-embedded attribute (§7.2) |

Recommending `PrivateAssets="all"` universally produces the failure mode
described in §7.2. Hence:

```text
DiagnosticCatalog.nupkg
├── lib/netstandard2.0/DiagnosticCatalog.dll
├── lib/netstandard2.0/DiagnosticCatalog.xml
└── README.md

DiagnosticCatalog.Analyzers.nupkg          (DevelopmentDependency = true)
├── analyzers/dotnet/cs/DiagnosticCatalog.Analyzers.dll
├── analyzers/dotnet/cs/DiagnosticCatalog.CodeFixes.dll
├── AnalyzerReleases.Shipped.md
├── README.md
└── icon.png
```

A convenience metapackage may depend on both.

### 16.2 Consumer reference

```xml
<ItemGroup>
  <PackageReference Include="DiagnosticCatalog.Analyzers"
                    Version="1.0.0"
                    PrivateAssets="all" />
</ItemGroup>
```

### 16.3 Transitivity must be tested, not assumed

The NuGet documentation states that the default `PrivateAssets` value for a
`PackageReference` is `contentfiles;analyzers;build`, implying analyzers do not
flow transitively. In practice
[NuGet/Home#13813](https://github.com/NuGet/Home/issues/13813) reports that
transitive analyzers *do* flow. **Depend on neither direction.**

* `tools/packaging/verify-consumption.sh` performs a real restore of the produced
  packages and asserts whether the analyzer activates for a consumer of a
  catalogue package. It runs on every pull request, from the release rehearsal,
  where real `.nupkg` files exist.

**Measured, and it is not what the documentation implies.** Three catalogue
packages differing only in `PrivateAssets` were built and consumed:

| A catalogue referencing the analyzer with | The analyzer runs for its consumers |
| --- | --- |
| no `PrivateAssets` at all | **yes** |
| `PrivateAssets="none"` | yes |
| `PrivateAssets="all"` | no |

So the analyzer **does** flow transitively by default, despite the package
setting `DevelopmentDependency` and despite NuGet documenting analyzers as
non-transitive — the behaviour reported in
[NuGet/Home#13813](https://github.com/NuGet/Home/issues/13813).

Two consequences, both the reverse of the earlier assumption:

* A catalogue that wants to bring the checking along needs **no lever at all**;
  `PrivateAssets="none"` is confirmed to work and changes nothing.
* A catalogue that does *not* want to impose analysis on its consumers must say
  so explicitly with `PrivateAssets="all"`. Silence propagates.

A consumer may still reference `DiagnosticCatalog.Analyzers` directly, and should
when no catalogue package supplies it.

---

## 17. Configuration

Every rule must be configurable through the standard Roslyn analyzer
mechanisms:

```ini
dotnet_diagnostic.DCAT0001.severity = error
dotnet_diagnostic.DCAT0002.severity = error
dotnet_diagnostic.DCAT0003.severity = error
dotnet_diagnostic.DCAT0004.severity = error
dotnet_diagnostic.DCAT0005.severity = suggestion
dotnet_diagnostic.DCAT0006.severity = warning
dotnet_diagnostic.DCAT0007.severity = error
dotnet_diagnostic.DCAT0008.severity = warning   # opt-in strict mode
dotnet_diagnostic.DCAT0009.severity = error
dotnet_diagnostic.DCAT0011.severity = error
dotnet_diagnostic.DCAT0012.severity = error
dotnet_diagnostic.DCAT0013.severity = error
dotnet_diagnostic.DCAT0014.severity = error
```

The sample overrides every rule to show that every rule is reachable; it is not a
statement of the defaults, which §16 gives. `DCAT0001`, `DCAT0006` and `DCAT0007`
already ship at `Error`, so the line that matters in practice is the one going the
other way — `DCAT0006` down to `suggestion` while an existing codebase migrates
([ADR-0027](adr/0027-ship-the-use-site-diagnostics-as-errors.en.md)). No
proprietary configuration format is required for the first version.

---

## 18. Behaviour on generated code

Use-site diagnostics must not be reported inside automatically generated code.
Definition diagnostics must be, because a generated catalogue is itself
generated code.

**`ConfigureGeneratedCodeAnalysis` is per-analyzer, not per-diagnostic.** These
two requirements therefore cannot coexist in one `DiagnosticAnalyzer` class. The
implementation must split into two:

| Analyzer class | Diagnostics | Generated-code flags |
| --- | --- | --- |
| `DiagnosticRuleDefinitionAnalyzer` | `DCAT0002`–`DCAT0005`, `DCAT0011`–`DCAT0013` | `Analyze \| ReportDiagnostics` |
| `SuppressionUsageAnalyzer` | `DCAT0001`, `DCAT0006`–`DCAT0010`, `DCAT0014` | `None` |

Rule definitions produced by an external tool must additionally be validated
by generator tests, compilation tests, and source manifest validation.

---

## 19. Performance

The analyzers must:

* enable concurrent execution;
* avoid repeated whole-compilation syntactic sweeps;
* index rules at most once per compilation, lazily (§13.1);
* pre-filter referenced assemblies before any metadata walk (§13.1);
* compare symbols with `SymbolEqualityComparer.Default`;
* analyze only the relevant attributes;
* cache the symbols of the recognised suppression attributes;
* perform no network access;
* read no external file on each attribute analysis.

---

## 20. Compatibility

The foundation must support:

* SDK-style projects;
* local suppressions;
* assembly-level suppressions;
* `GlobalSuppressions.cs` files;
* catalogues defined in the same project;
* catalogues supplied by a referenced assembly;
* type aliases;
* `using static` (resolved, not recommended — §10.5);
* `SuppressMessageAttribute`;
* `UnconditionalSuppressMessageAttribute`, **for `IL####` rules only** (§9.1).

The first version may be limited to C#. Visual Basic support may be considered
later.

`DiagnosticCatalog` is **not strong-named**, and stays that way (Appendix B6). A
strong-named assembly referencing it reports `CS8002`, which a project applying
warnings-as-errors must suppress with `<NoWarn>$(NoWarn);CS8002</NoWarn>`. That
is the whole cost, and it falls only on an assembly that is itself strong-named
*and* declares a catalogue of its own: a consumer of a catalogue reads `const`
values the compiler inlines, so no reference to the catalogue assembly is
emitted at all.

The decision had to be taken before the first release rather than after it:
adding *or* removing a strong name changes the assembly identity of every
reference, a binary breaking change in either direction.

---

## 21. Required tests

### 21.1 Rule definition tests

* a valid rule;
* a non-static class;
* a generic class;
* a missing `Id`;
* a missing `Category`;
* a non-constant `Id`;
* a non-constant `Category`;
* an empty value;
* an identifier differing from the class name, where the exact name was
  available (must report `DCAT0013`);
* an identifier that cannot be a C# identifier, rendered by the class name
  (must report `DCAT0005`, and **not** `DCAT0013`);
* an identifier that cannot be a C# identifier, ignored by the class name
  (must report `DCAT0013`);
* an identifier carrying a friendly-name suffix, whose class name renders its
  head (must report `DCAT0005`);
* an identifier equal to the class name, written as a literal (must report
  `DCAT0012`) and written as `nameof` (must report nothing);
* the same literal reached through a referenced assembly (must report nothing:
  metadata carries no form);
* a rule declared with a source-embedded attribute (§7.2).

### 21.2 Suppression tests

* a valid pair;
* a category and an identifier from different rules;
* rules coming from a referenced assembly;
* a `using static` form;
* a type alias;
* an assembly-level suppression;
* a `GlobalSuppressions.cs` file;
* an intermediate constant (§10.6);
* `SuppressMessageAttribute`;
* `UnconditionalSuppressMessageAttribute` with an `IL####` rule;
* `UnconditionalSuppressMessageAttribute` with a non-IL rule → `DCAT0009`;
* a pair naming a rule and carrying no `Justification` → `DCAT0014`;
* a `Justification` that is empty, whitespace, `null` or the IDE placeholder →
  `DCAT0014`;
* a `Justification` of one word, and one reached through a constant (must report
  nothing: §11.14 requires presence and reads no further);
* a pair written entirely in values, carrying no `Justification` → `DCAT0014`,
  including one naming a rule no referenced catalogue describes, which nothing
  else reports;
* an identifier resolving to no value, carrying no `Justification` (must report
  nothing: it silences nothing).

### 21.3 String literal tests

* no matching rule;
* exactly one matching rule;
* several matching rules;
* a correct category with an unknown identifier;
* a correct identifier with an incorrect category;
* **an identifier carrying a `:FriendlyName` suffix** (§3.3) — the form Visual
  Studio generates;
* one reference and one literal → `DCAT0007`.

### 21.4 Code fix tests

* the category-based correction;
* the identifier-based correction;
* the deterministic `DCAT0007` correction;
* string literal replacement;
* `using` insertion;
* justification preservation;
* `Scope`, `Target` and `MessageId` preservation;
* no modification of other attributes;
* *Fix all occurrences* honouring `EquivalenceKey` consistently;
* the definition repairs of §12.4, and — one assertion per case — the definition
  faults for which no fix is offered. The second half is the load-bearing one: a
  refusal is a claim about the code, and the test for it must show the diagnostic
  was still reported, so that a fix which quietly starts repairing a case it had
  declined cannot pass as one that never had a repair.

### 21.5 Real compilation tests

A test project must genuinely compile:

```csharp
[SuppressMessage(
    TestRules.TEST0001.Category,
    TestRules.TEST0001.Id,
    Justification = "Compilation test.")]
public sealed class Subject
{
}
```

This protects the essential constraint: `Id` and `Category` must remain usable
as constant attribute arguments.

A reflection assertion must confirm that constant folding produced the expected
metadata. Because `SuppressMessageAttribute` is `[Conditional("CODE_ANALYSIS")]`
(§3.4), the test project **must define that symbol**, or the attribute is never
emitted and the assertion silently reads `null`:

```xml
<DefineConstants>$(DefineConstants);CODE_ANALYSIS</DefineConstants>
```

```csharp
var attribute = typeof(Subject).GetCustomAttribute<SuppressMessageAttribute>();

Assert.NotNull(attribute);   // fails outright when CODE_ANALYSIS is undefined
Assert.Equal("TEST0001", attribute!.CheckId);
Assert.Equal("Usage", attribute.Category);
```

A companion project that does **not** define the symbol must assert the
opposite — the attribute is absent from metadata — which is what makes the
zero-footprint guarantee of §4 a tested property rather than a claim.

### 21.6 End-to-end suppression test

The premise of the whole library must be proven, not assumed: a real analyzer
emits a diagnostic, a catalogue-based `[SuppressMessage]` is applied, and the
diagnostic **is actually absent** from the compilation result. Without this
test, §27 asserts only that the code compiles.

### 21.7 Packaging tests

* a real restore of the produced packages;
* the analyzer activates for a direct consumer;
* the transitivity behaviour of §16.3 is asserted, whatever it turns out to be;
* the analyzer assemblies do not appear in the consumer's output folder.

---

## 22. Required documentation

* a README stating the problem;
* a minimal example;
* documentation for catalogue authors;
* documentation for consumers;
* the list of `DCATxxxx` diagnostics;
* the `.editorconfig` configuration procedure;
* the `PrivateAssets` matrix of §16.1;
* an explicit statement of the limits in §5.1;
* a versioning policy;
* a contribution guide;
* an example specialised package;
* an example integration into a first-party analyzer, including §15.2.

---

## 23. Versioning

The packages follow Semantic Versioning.

**Patch** — analyzer fixes, code fix fixes, performance improvements,
documentation fixes, any change that leaves the public contract intact.

**Minor** — a new diagnostic, a new code fix, a new optional metadata member,
support for a new compatible attribute, a new feature disabled by default.

**Major** — renaming `DiagnosticRuleAttribute`, `DiagnosticCategoryAttribute` or
`CatalogSourceAttribute`,
changing the mandatory `Id` or
`Category` names, changing the structural definition of a rule, removing a
public diagnostic, changing a rule's behaviour incompatibly, changing public
namespaces, and changing the assembly identity (assembly name, package id, or
strong-name key).

### 23.1 Note on catalogue packages

Constants are **inlined at the consumer's compile time**. Removing a `const`
from a published catalogue therefore breaks recompilation. A rule retired
upstream must be marked `[Obsolete]`, never deleted.

---

## 24. What 1.0 contains

**Shipped**

* `DiagnosticRuleAttribute`, with metadata-name matching (§7.2);
* static class validation (`DCAT0002`);
* `Id` validation (`DCAT0003`);
* `Category` validation (`DCAT0004`);
* the same-rule coherence check (`DCAT0001`);
* **literal detection and its code fix (`DCAT0006`) — core, not optional**
  (§3.5), including `:FriendlyName` normalisation;
* mixed reference/literal detection and its deterministic fix (`DCAT0007`);
* the `IL####` guard for `UnconditionalSuppressMessage` (`DCAT0009`);
* the justification requirement (`DCAT0014`) — presence only, per §11.14;
* the declared-category requirement (`DCAT0011`);
* `SuppressMessageAttribute` support;
* `UnconditionalSuppressMessageAttribute` support, scoped per §9.1;
* the two code fixes for incoherent pairs;
* the definition fixes of §12.4, each offered only where the repair is written
  in the code already;
* the naming diagnostics `DCAT0005`, `DCAT0012` and `DCAT0013`, and the
  `nameof` fix of the second;
* *Fix all occurrences* over a document, a project and a whole solution (§12.5);
* the foundation and analyzer packages (§16.1) with analyzer release tracking;
* the generator, published as the `dcat` .NET tool
  ([ADR-0017](adr/0017-publish-the-generator-as-a-cli-on-its-own-release-train.en.md)),
  with its four verbs — `generate`, `validate`, `list`, `explain` (§14.1);
* the thirteen generated vendor catalogues of §14, and
  `DiagnosticCatalog.Self` (§15);
* nightly synchronisation with every mirrored vendor, which opens a pull request
  and never publishes (§14.3);
* documentation;
* analyzer, compilation, end-to-end and packaging tests.

**Deliberately absent**

Each of these was considered and left out, rather than missed:

* `DCAT0008` and `DCAT0010`, which were specified and not implemented;
* a catalogue source generator — generation happens once, in the repository that
  publishes the catalogue, not in every consumer's build (§25);
* automatic import of an external catalogue into a consumer's project;
* documentation generation;
* `Scope` / `Target` validation;
* intelligent justification validation;
* Visual Basic support
  ([ADR-0020](adr/0020-a-catalogue-is-generated-for-c-sharp-only.en.md));
* a runtime diagnostic model;
* a web catalogue portal.

---

## 25. Possible evolutions

The following extensions may later be developed as separate packages:

```text
DiagnosticCatalog.Generator
DiagnosticCatalog.Tool
DiagnosticCatalog.Documentation
DiagnosticCatalog.<Vendor>
```

### 25.1 `Scope` / `Target` validation

In `GlobalSuppressions.cs`, `Target = "~M:Ns.Type.Method(System.Int32)"` is a
hard-coded documentation comment id that rots silently on every rename. Nothing
in the platform reports it.
`DocumentationCommentId.GetFirstSymbolForDeclarationId` can verify that it still
resolves. This is the natural next feature and fits the structural-coherence
thesis exactly — arguably a larger day-to-day pain than the category/id literals
themselves.

### 25.2 Strict mode

`DCAT0008` (§11.8) promoted from opt-in to a documented, recommended
configuration once the catalogue ecosystem is broad enough not to flood a
project with false positives.

### 25.3 Generator

A generator could turn a manifest into constant classes:

```json
{
  "rules": [
    { "id": "JD0007", "category": "Usage", "title": "Example rule" }
  ]
}
```

### 25.4 More generated catalogues

Generated catalogues are no longer a future evolution: the method, the generator,
thirteen catalogues and their scheduled synchronisation are specified in §14.1–§14.3
and implemented. What remains is further vendors.

**A Visual Basic variant is not a manifest entry.** An earlier reading of this
section held that it was, on the grounds that the generator already takes
`--language`. Measured against `Microsoft.CodeAnalysis.NetAnalyzers`,
`--language vb` reads 311 descriptors and then refuses: three types will not
load, because a Visual Basic analyzer derives from
`Microsoft.CodeAnalysis.VisualBasic`, which the descriptor worker does not carry.
The refusal is correct — it is §14.3 declining to emit a catalogue short of the
rules those types declare — but it means the option promised what the tool could
not do, so `cs` is now the only value it accepts. Supporting Visual Basic means
giving the worker that Roslyn, and carrying a second construction path for as
long as the tool exists. `ADR-0020` decides against it: Visual Basic is closed to
new language features, so its analyzer population is small and will not grow, and
every install would pay for it. A settled position, not a deferred task.

### 25.5 Justification validation

A separate analyzer or tool could verify that `Justification` is present, is not
a generic value, actually explains the exception, and is not merely `TODO`,
`N/A` or `False positive`. This must stay separate from the fundamental rule
identity check.

### 25.6 CLI

```text
diagnostic-catalog validate
diagnostic-catalog generate
diagnostic-catalog list
diagnostic-catalog explain JD0007
```

---

## 26. Complete example

Declaration:

```csharp
using DiagnosticCatalog;

namespace ExampleAnalyzer.Suppressions;

[DiagnosticCategory]
internal static class ExampleCategory
{
    public const string Design = "Design";
    public const string Usage = "Usage";
}

public static class Example
{
    [DiagnosticRule]
    public static class EXAMPLE0001
    {
        public const string Id = nameof(EXAMPLE0001);
        public const string Category = ExampleCategory.Design;
        public const string Title = "Avoid example design";
        public const string HelpLinkUri = "https://example.org/rules/EXAMPLE0001";
    }

    [DiagnosticRule]
    public static class EXAMPLE0002
    {
        public const string Id = nameof(EXAMPLE0002);
        public const string Category = ExampleCategory.Usage;
        public const string Title = "Avoid example usage";
        public const string HelpLinkUri = "https://example.org/rules/EXAMPLE0002";
    }
}
```

Valid use:

```csharp
using System.Diagnostics.CodeAnalysis;
using ExampleAnalyzer.Suppressions;

[SuppressMessage(
    Example.EXAMPLE0001.Category,
    Example.EXAMPLE0001.Id,
    Justification = "Required by the external framework contract.")]
public sealed class FrameworkAdapter
{
}
```

Invalid use:

```csharp
[SuppressMessage(
    Example.EXAMPLE0001.Category,
    Example.EXAMPLE0002.Id,
    Justification = "Required by the external framework contract.")]
```

```text
DCAT0001: Category and Id must reference the same diagnostic rule.

  Use EXAMPLE0001.Id
  Use EXAMPLE0002.Category
```

Partially migrated use:

```csharp
[SuppressMessage(
    Example.EXAMPLE0001.Category,
    "EXAMPLE0001",
    Justification = "Required by the external framework contract.")]
```

```text
DCAT0007: Suppression mixes a catalog reference with a string literal.

  Use Example.EXAMPLE0001.Id
```

---

## 27. Version 1.0 acceptance criteria

Version `1.0` is complete when every item below holds, and each is asserted by a
test rather than by inspection:

1. a third-party library can declare a rule with `[DiagnosticRule]`, with or
   without referencing the `DiagnosticCatalog` assembly (§7.2);
2. that rule can be used in a real `SuppressMessageAttribute`;
3. **a diagnostic actually emitted by a real analyzer is actually suppressed**
   by a catalogue-based suppression, proven by the test in §21.6;
4. in a compilation that defines `CODE_ANALYSIS`, reflection confirms the
   expected `CheckId` and `Category`; in an otherwise identical compilation that
   does not, the attribute is absent from metadata altogether — both asserted by
   §21.5 (§3.4);
5. the analyzer accepts a category and an identifier from the same rule;
6. the analyzer detects a category and an identifier from different rules;
7. two explicit corrections are offered when the intent is ambiguous;
8. invalid rule definitions are detected;
9. a literal suppression is replaced when a unique match exists, **including the
   `Id:FriendlyName` form** (§3.3);
10. a partially migrated suppression is detected and deterministically fixed;
11. *Fix all occurrences* converts every occurrence of a document, of a project,
    and of a whole solution, leaving the projects it has nothing to do in
    untouched (§12.5);
12. global suppressions are supported;
13. `UnconditionalSuppressMessageAttribute` is supported for `IL####` rules, and
    misuse is reported (§9.1);
14. the analyzers introduce no runtime dependency, asserted by §21.7;
15. the packages can be installed from NuGet, and their transitivity behaviour
    is documented from a real restore test (§16.3);
16. the thirteen vendor catalogues of §14 are generated from their upstream
    descriptors, each recording the release it mirrors (§7.6), and regenerating
    any of them twice produces the same bytes;
17. `dcat validate` answers `2` on a catalogue that no longer matches its
    source, `0` on one that does, and `1` on a run that could not finish
    (§14.1);
18. `dcat explain` prints a suppression that compiles where it is pasted, with
    no `using` directive of the reader's;
19. the `DCAT` diagnostics are themselves catalogued, by this repository's own
    generator, and the catalogue is checked against the analyzers beside it
    (§15);
20. every documented case has an automated test;
21. a sample catalogue is provided;
22. the documentation is sufficient to build a catalogue without reading the
    foundation's internals.

---

## 28. Architectural summary

```text
A rule
    =
a static class marked [DiagnosticRule]
    +
a const string Id          → the value that actually matters
    +
a const string Category    → the value nobody else publishes
```

```text
The problem
    =
checkId is a magic string that fails silently and permanently
```

```text
The foundation
    =
the public contract
    +
the analyzers
    +
the code fixes
```

Specialised catalogues supply the data specific to each analyzer;
`DiagnosticCatalog` supplies the shared convention and guarantees its correct
use.

---

## Appendix A — Verified platform behaviour

Every behavioural claim in this document was checked against source, not
recalled. Re-verify before any major revision.

| # | Claim | Source |
| --- | --- | --- |
| A1 | `SuppressMessageAttribute` has a single constructor `(string category, string checkId)`; both parameters required and non-nullable. | [`SuppressMessageAttribute.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/SuppressMessageAttribute.cs) |
| A2 | Roslyn ignores the category when matching a suppression: *"Ignore the category parameter because it does not identify the diagnostic…"* | [`SuppressMessageAttributeState.cs`](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/DiagnosticAnalyzer/SuppressMessageAttributeState.cs) |
| A3 | Roslyn truncates `checkId` at the first `:`, allowing an optional friendly name. | same as A2 |
| A4 | ILLink also ignores the category, **and accepts only `IL####` identifiers** (`Length >= 6`, `StartsWith("IL")`, 4 digits parsed at offset 2). | [`UnconditionalSuppressMessageAttributeState.cs`](https://github.com/dotnet/runtime/blob/main/src/tools/illink/src/linker/Linker/UnconditionalSuppressMessageAttributeState.cs) |
| A5 | A category-less constructor has been proposed upstream and is still open, undecided. | [dotnet/runtime#68153](https://github.com/dotnet/runtime/issues/68153) |
| A6 | NuGet documents analyzers as non-transitive by default, but transitive flow is reported in practice — behaviour must be tested. | [NuGet/Home#13813](https://github.com/NuGet/Home/issues/13813), [PackageReference docs](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files) |
| A7 | `SuppressMessageAttribute` is `[Conditional("CODE_ANALYSIS")]` and is therefore not emitted into metadata unless that symbol is defined. Confirmed empirically: reflection returns `null` by default, and the expected values once `CODE_ANALYSIS` is defined. | [`SuppressMessageAttribute.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/SuppressMessageAttribute.cs) |
| A8 | `UnconditionalSuppressMessageAttribute` carries no `[Conditional]`, which is its stated reason to exist: *"…it doesn't have a ConditionalAttribute. So it is always preserved in the compiled assembly."* | [`UnconditionalSuppressMessageAttribute.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/UnconditionalSuppressMessageAttribute.cs) |
| A9 | `SonarAnalyzer.CSharp 10.31.0.145097` declares 465 descriptors across 448 analyzer types. Categories are `{Severity} {Type}` pairs — 13 distinct values, e.g. `S1144` = `"Major Code Smell"`. Nine `S9999-*` entries carry an empty category, and `HelpLinkUri` is populated on **0** of the 465. | Read from the package's own `DiagnosticAnalyzer.SupportedDiagnostics` by `eng/CatalogGen` (§14.1) |
| A10 | A `const string` initialised from another `const string` remains a compile-time constant: it is accepted as an attribute argument and folds to the literal in metadata. Verified by reflecting over a `[SuppressMessage]` whose category came through `SonarCategory.MajorCodeSmell` — `Category` read back as `"Major Code Smell"`. | Compilation + reflection test (§7.7) |
| A12 | Rules do get retired upstream: `CA2109` and `CA2229` are declared by `Microsoft.CodeAnalysis.NetAnalyzers 6.0.0` and no longer by `10.0.302`. Carried forward as `[Obsolete]`, a consumer still referencing one gets `CS0618` naming the rule, rather than a hard `CS0117` from a deleted member. | Regeneration across the two versions, plus a compile of the consuming form (§14.1) |
| A11 | `Microsoft.CodeAnalysis.NetAnalyzers 10.0.302` declares 318 descriptors over 10 categories, all with help links, and splits its analyzers between a language-neutral assembly at `analyzers/dotnet/` and per-language ones under `cs/` and `vb/`. `StyleCop.Analyzers 1.1.118` declares 193 over 8 categories of the `StyleCop.CSharp.*Rules` shape, all with help links. `StyleCop.Analyzers 1.2.0-beta.556` is a metapackage carrying no analyzer assembly; the rules live in `StyleCop.Analyzers.Unstable`. | Same method as A9 |
| A13 | The **compile-time** trim analyzer does **not** use the linker decoder of A4. It truncates `checkId` at the first colon and then requires an exact, case-sensitive match against the diagnostic id. So `IL2026:FriendlyName`, `IL2026:` and `IL2026:a:b` are honoured, while `IL20265` — which A4's decoder accepts as `IL2026` — is ignored, as are `il2026` and `" IL2026"`. Both paths ignore the category, agreeing with A2 and A4. | Compilation with `EnableTrimAnalyzer`, suppressing a real `IL2026` one identifier shape at a time and observing which warnings survive (§9.1) |

## Appendix B — Decisions taken

These are the design questions that were open while the library was being built.
Each is recorded with the answer it was given and the reasoning behind it, so a
reader meeting one of them in the body knows it was decided rather than
overlooked. None of them is open at 1.0.

| # | Question | Decision |
| --- | --- | --- |
| B1 | ~~Final product name. `Catalog` names a library that deliberately contains no catalogue (§2.5).~~ | **Settled** — the name stands. It says what the library is for, not what it holds; the alternatives each lose something, and four packages are published. |
| B2 | ~~Is the `DCAT` prefix free? Community analyzer prefixes are not centrally registered.~~ | **Settled — it is.** Checked by @reefact against the known prefixes before 1.0. It had to close before publication rather than after: a released id is a contract nobody renames (§23), and there is no registry to consult later. |
| B3 | ~~Should the purely structural fallback of §7.2 (no attribute) be enabled by default?~~ | **Settled — no.** Matching is attribute-only, and the structural fallback stays documented and off. Turning it on would make every static class of two constants named `Id` and `Category` a rule, in somebody else's assembly, with no way for them to opt out. |
| B4 | ~~Are aliases and `using static` worth the analyzer complexity given §10.5?~~ | **Settled — yes, both are resolved.** An alias or a `using static` at a use site binds to the same symbol, so refusing to follow it would report `DCAT0006` on a suppression that is already a checked reference. Neither is promoted in the documentation: they resolve, and §10.5 says why they are not the shape to reach for. |
| B5 | ~~Does the ILLink Roslyn analyzer (`IL2xxx` at compile time) share the decoder verified in A4?~~ | **Settled — it does not** (A13). The two agree on everything a generated catalogue can produce, and diverge on one shape: `IL####` followed by anything that is not a colon. `DCAT0009` mirrors the linker, so it stays quiet there; see §11.9. |
| B6 | ~~Strong-name the `DiagnosticCatalog` assembly?~~ | **Settled** — unsigned, and it stays that way past 1.0. A catalogue's consumer is unaffected either way: they read `const` values, which the compiler inlines, so no reference to the catalogue assembly is emitted at all and the application runs without it. Only an assembly that is *itself* strong-named **and** uses the marker attributes to declare a catalogue of its own is affected, and only by `CS8002` — a warning, on any target framework, not a .NET Framework matter. That is not this library's primary audience. |
| B7 | ~~Should `DiagnosticCatalog.Sonar`'s package version track `SonarAnalyzer.CSharp`, or run on its own line?~~ | **Settled** — its own line, for every catalogue: [ADR-0015](adr/0015-a-catalogues-version-runs-on-its-own-line.en.md). |
