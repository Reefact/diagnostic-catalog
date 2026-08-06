# The rule contract

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./rule-contract.fr.md)

For anyone who needs the exact shape rather than the shortest one — writing a generator, reviewing a
hand-written catalogue, or working out why a declaration is not recognised. The normative source is
[the specification](../specification.en.md), §7 to §10; this is it distilled.

## The whole contract, in five requirements

A rule is a type that satisfies all five:

| # | Requirement | Reported by |
| --- | --- | --- |
| 1 | Marked `[DiagnosticRule]` | — (an unmarked type is simply not a rule) |
| 2 | A **static, non-generic class** | `DCAT0002` |
| 3 | A public `const string` named `Id`, non-blank | `DCAT0003` |
| 4 | A public `const string` named `Category`, non-blank | `DCAT0004` |
| 5 | That `Category` reaches a constant declared in a `[DiagnosticCategory]` class | `DCAT0011` |

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

Nothing else is required. No base class, no interface, nothing to register.

Requirements 4 and 5 are two questions about the same member, and they are worth keeping apart.
The fourth asks whether the category can be an attribute argument at all; the fifth asks whether the
value has one declaration or many. The first is about a rule that does not work. The second is about a
catalogue that works and drifts.

## Why structural rather than inherited

A rule cannot inherit its contract, and this is a language fact rather than a preference.

A `const` cannot be declared by an interface or overridden from a base class, and an abstract property
could never be an attribute argument — which is the entire purpose. A static class cannot participate
in classic inheritance at all.

So the contract is **verified by an analyzer** rather than imposed by a type system that has no way to
impose it ([ADR-0008](../adr/0008-express-a-rule-as-a-marked-static-class-of-constants.en.md)).

## The marker is matched by name, never by symbol

`[DiagnosticRule]` is recognised by its **fully qualified metadata name**:

```text
DiagnosticCatalog.DiagnosticRuleAttribute
```

This is a correctness requirement, not an optimisation, and two behaviours depend on it:

* **A catalogue may declare the marker itself** rather than take a package dependency — the pattern
  PolySharp uses for `IsExternalInit`. Its attribute is a different symbol and a symbol comparison
  would never match it.
* **An unresolvable attribute still matches.** When a consumer's compilation cannot resolve
  `DiagnosticCatalog.dll`, `[DiagnosticRule]` degrades to an error type — which still carries its
  name. A symbol comparison would find nothing, report nothing, and produce output indistinguishable
  from a codebase with no problems: the exact failure this library exists to eliminate, reproduced
  inside the tool meant to detect it.

The converse holds too: an attribute of the same **short** name in another namespace is somebody
else's and is deliberately not matched.

## `Id` — and when it differs from the type name

The recommended form is `nameof`, which cannot drift from the type it names:

```csharp
public const string Id = nameof(JD0007);
```

But the id is the diagnostic's canonical identifier, and **not every identifier is a valid C#
identifier**. When they differ, the type name yields:

```csharp
[DiagnosticRule]
public static class RULE_001
{
    public const string Id = "RULE-001";
    public const string Category = ContosoCategory.Usage;
}
```

A value that is null, empty or whitespace-only counts as **absent** — `DCAT0003`, not a rule with a
blank id.

## `Category` — the member nothing can verify

Same shape, same rules, plus requirement 5 on where the value comes from. What no requirement reaches
is the value *itself*, and the distinction is worth being exact about: requirement 5 checks that the
category has a single declaration, never that the string in it is right. What it should be is the
category the originating analyzer's `DiagnosticDescriptor` declares, and nothing in the platform
compares the two.

That is not a gap in this library — it is the property the library exists because of. Accuracy here
is a matter of the catalogue's credibility, which is why the catalogues in this repository are
generated from descriptors rather than transcribed
([ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).

## Categories declared once — requirement 5

A `const` initialised from another `const` is **still a compile-time constant**:

```csharp
[DiagnosticCategory]
internal static class SonarCategory
{
    public const string MajorCodeSmell = "Major Code Smell";
}

[DiagnosticRule]
public static class S1144
{
    public const string Id = nameof(S1144);
    public const string Category = SonarCategory.MajorCodeSmell;   // still valid as an argument
}
```

`[DiagnosticCategory]` is **required**, and requirement 5 is what requires it. The constants would fold
identically without it; what the marker buys is that tooling can tell a category constant from any
other string constant in the assembly, which is what lets a fix offer the named constant in place of a
literal. In a catalogue this repository generates the container is
`internal`, so a suppression names a category only through the rule that carries it — see
[ADR-0026](../adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md). A hand-written
catalogue may still publish one; the contract does not forbid it, and the container may live in another
assembly.

The decision to require it, and what it deliberately does not buy, is
[ADR-0028](../adr/0028-require-every-rule-to-reach-its-category-through-a-declared-constant.en.md).

## Which attributes are analysed

| Attribute | Analysed | Note |
| --- | --- | --- |
| `SuppressMessageAttribute` | yes | The ordinary case. Not emitted into your assembly — it is `[Conditional("CODE_ANALYSIS")]`. |
| `UnconditionalSuppressMessageAttribute` | yes | Trim/AOT only. **Is** emitted, and its decoder accepts only `IL####` identifiers — hence `DCAT0009`. |

**Aliases on the attribute itself are resolved.** Analysis never depends on the short name written in
source:

```csharp
using Suppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

[Suppress(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "...")]
```

## Accepted syntactic forms at a use site

Analysis works on **Roslyn symbols**, not on source text, so every form that resolves to the same
member is equivalent.

**Qualified member access** — the canonical form:

```csharp
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]
```

**A type alias** — fully equivalent, and recommended when the container name is long:

```csharp
using Unused = DiagnosticCatalog.Sonar.SonarRule.S1144;

[SuppressMessage(Unused.Category, Unused.Id)]
```

**`using static`** — recognised, **not recommended**:

```csharp
using static DiagnosticCatalog.Sonar.SonarRule.S1144;

[SuppressMessage(Category, Id)]
```

Two `using static` directives for two rules in one file make `Category` and `Id` ambiguous, which is a
compile error. The form works for one rule per file and breaks as soon as a second suppression is
needed. The analyzer resolves it; the documentation does not promote it.

**An intermediate constant** — checkable, contrary to first reading:

```csharp
private const string RuleId = SonarRule.S1144.Id;

[SuppressMessage(SonarRule.S1144.Category, RuleId)]
```

When an argument resolves to a constant field whose declaring type is *not* a rule type, the analyzer
compares its constant **value**, exactly as it does for a literal. It is not the canonical form and no
fix is offered — but it is not a blind spot either.

## How an identifier is matched

Roslyn truncates a suppression's identifier at the **first colon** before matching, and this library
does the same. That is what makes the form Visual Studio's *Suppress → In Source* generates
recognisable:

```csharp
[SuppressMessage("Major Code Smell", "S1144:Unused private members should be removed")]
```

The suffix is a friendly name and carries no meaning to the platform. The `DCAT0006` fix drops it: it
duplicated the rule's own title, which the catalogue carries as XML documentation.

`UnconditionalSuppressMessage` honours the same form — `IL2026:FriendlyName` — which is why
`DCAT0009` mirrors the trimmer's decoder rather than applying a stricter pattern. Reporting an
identifier the trimmer *does* honour would be telling you to change something that works.

## What is out of the model

| | Why |
| --- | --- |
| `#pragma warning disable S1144` | Takes a bare identifier token, not an expression. There is no position a constant could occupy. |
| `dotnet_diagnostic.S1144.severity` | An `.editorconfig` key is plain text, read outside the C# compilation model entirely. |
| `Severity` as a rule member | An enum can be `const`, but `DiagnosticSeverity` lives in `Microsoft.CodeAnalysis.Common` — declaring it forces Roslyn on every consumer of the catalogue. |
| Localised title or message | `LocalizableString` cannot be a `const`. The catalogue covers the id and category axis; resource files remain the right tool. |

## Where to go next

* [**The `DCAT` diagnostics**](diagnostics.en.md) — what is reported when a declaration or a use site
  misses the contract.
* [**Troubleshooting**](troubleshooting.en.md) — when the contract looks satisfied and nothing is
  reported anyway.
* [**The specification**](../specification.en.md) — §7 to §10, normative, with the platform behaviour
  each requirement rests on.

---

<div align="center">
<a href="./README.en.md">↑ Table of contents</a> · <a href="./troubleshooting.en.md">Troubleshooting →</a>
</div>
