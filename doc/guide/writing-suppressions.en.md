# Writing suppressions that the compiler checks

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./writing-suppressions.fr.md)

For anyone who writes `[SuppressMessage(...)]`. You do not need to know anything about how
DiagnosticCatalog works to read this.

## The problem, in one example

You have a warning you have decided to accept, so you silence it:

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "Called by the serializer.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Two strings. The compiler does not check either of them, because as far as it is concerned they are
just text. So all of the following compile, ship, and do nothing at all:

```csharp
[SuppressMessage("Major Code Smell", "S1145")]   // typo — one digit
[SuppressMessage("Major Code Smell", "S 1144")]  // stray space
[SuppressMessage("Major Code Smell", "S1144")]   // correct today; the rule is retired next year
```

Nothing warns you. The suppression simply stops matching, and the warning it was hiding comes back —
or, worse, never comes back because the code was deleted and the dead suppression stays forever.
This is not a rare mistake. It is the normal outcome of writing an identifier by hand with no
feedback.

## What this library does about it

It replaces the two strings with two **constants**, which the compiler does check:

```csharp
[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Called by the serializer.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Now:

* misspell the rule and you get a **compile error**, not a silent no-op;
* rename it in the IDE and every use site follows;
* ask "where is this rule suppressed?" and *Find All References* answers, because it is a reference;
* when the vendor retires the rule, the constant is marked obsolete and you are told at build time.

The compiled output is **byte-for-byte identical** to the version with literals. The constants are
folded away at compile time, so this costs your application nothing — not a dependency, not a
startup check, not a byte. See [the zero-footprint note](#does-this-end-up-in-my-application) below.

## Getting started

### 1. Reference a catalogue

A catalogue is a package of constants for one analyzer's rules. Pick the one matching the analyzer
whose warnings you suppress:

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="0.1.0" />
```

There is one for [SonarAnalyzer](https://www.nuget.org/packages/DiagnosticCatalog.Sonar), one for
[the .NET analyzers](https://www.nuget.org/packages/DiagnosticCatalog.NetAnalyzers) (`CAxxxx`) and
one for [StyleCop](https://www.nuget.org/packages/DiagnosticCatalog.StyleCop) (`SAxxxx`) and one
for [the Roslyn IDE rules](https://www.nuget.org/packages/DiagnosticCatalog.CodeStyle) (`IDExxxx`)
and one for [xUnit's](https://www.nuget.org/packages/DiagnosticCatalog.Xunit) (`xUnitxxxx`) and one
for [NUnit's](https://www.nuget.org/packages/DiagnosticCatalog.NUnit) (`NUnitxxxx`) and one
for [MSTest's](https://www.nuget.org/packages/DiagnosticCatalog.MSTest) (`MSTESTxxxx`) and one
for [the trimming and AOT warnings](https://www.nuget.org/packages/DiagnosticCatalog.Trimming) (`ILxxxx`).

That is the only line you need for the guarantee itself. A misspelled rule is now a compile error,
because `SonarRule.S1144.Id` is a member the compiler resolves — no analyzer is involved in that.

The `DCAT` diagnostics below are a separate package, `DiagnosticCatalog.Analyzers`, and they are what
finds the suppressions you have *not* converted yet. **It has no version on nuget.org today**, so a
catalogue does not bring it along — nothing can reference a package that has never been published
([ADR-0007](../adr/0007-depend-across-trains-through-published-packages.en.md)). It rides the `lib`
train, so the next tag there ships it;
[project status](https://github.com/Reefact/diagnostic-catalog#-project-status) is the current
answer.

### 2. Write the suppression

```csharp
using System.Diagnostics.CodeAnalysis;
using DiagnosticCatalog.Sonar;

[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Called by the serializer through reflection.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Each catalogue names its container after its vendor: `SonarRule`, `NetAnalyzersRule`,
`StyleCopRule`, `DcatRule`. Singular, because the use site reads `SonarRule.S1144` — one rule, named.

### 3. Migrate what you already have

You do not have to do this by hand. Build once, and every literal suppression that matches a rule in
your catalogue is reported as `DCAT0006` with a fix attached. Accept it once, or use **Fix all
occurrences** to convert a document, a project or the whole solution in one step.

It handles the form Visual Studio generates, suffix and all:

```csharp
[SuppressMessage("Major Code Smell", "S1144:Unused private members should be removed")]
// becomes
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]
```

The suffix is dropped. It was prose repeating the rule's own title, which the catalogue carries in
its XML documentation — hover the constant and you get it back.

## The shorthands, and one to avoid

Long container names get repetitive. An **alias** is the recommended way out:

```csharp
using Unused = DiagnosticCatalog.Sonar.SonarRule.S1144;

[SuppressMessage(Unused.Category, Unused.Id, Justification = "...")]
```

Checked exactly like the long form: the analysis works on symbols, never on the text you typed.

`using static` also works but is **not** recommended:

```csharp
using static DiagnosticCatalog.Sonar.SonarRule.S1144;

[SuppressMessage(Category, Id, Justification = "...")]   // fine — but only for one rule per file
```

A second `using static` in the same file makes `Category` and `Id` ambiguous, and the fix is to undo
it. The alias scales; this does not.

## What you will be told about, and why

Four diagnostics can appear at a suppression. Full reference in
[the diagnostics guide](diagnostics.en.md); here is what each one means in practice.

**`DCAT0001` — the two arguments come from different rules.**

```csharp
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S2094.Id)]
```

Copy-paste, almost always. It is reported *even when both rules share a category*, because then the
line works today and breaks the day the vendor recategorises either one — the kind of defect that
surfaces years later with no clue attached.

Two fixes are offered and neither is marked as the default, because only you know which half was the
typo. Worth knowing while you choose: Roslyn matches a suppression on the **identifier alone** and
never looks at the category, so correcting the category changes nothing about what is suppressed,
while correcting the identifier changes it.

**`DCAT0006` — these literals match a rule you have.** The migration above.

**`DCAT0007` — you migrated half of it.**

```csharp
[SuppressMessage(SonarRule.S1144.Category, "S1144")]
```

Completed from the rule the other argument already names. If the literal names something *else* —
`"S9999"` beside `SonarRule.S1144.Category` — you get the diagnostic and **no** fix, because
completing it would silence a different rule than the one silenced today. That is your call, not a
lightbulb's.

**`DCAT0009` — `UnconditionalSuppressMessage` with a non-`IL` rule.** That attribute is read by the
trimmer, which accepts only `IL####` identifiers and discards everything else. So the suppression
you wrote does nothing, and nothing else in the toolchain would ever have told you.

## Turning them into build errors

The three that look at a use site are errors by default; the rest are warnings. All are configurable
like any Roslyn diagnostic:

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.DCAT0009.severity = error        # raising one that ships as a warning
dotnet_diagnostic.DCAT0006.severity = suggestion   # migrating gradually
```

`DCAT0001` and `DCAT0007` are errors already, and so is `DCAT0006`: all three mean a suppression is
not doing what it appears to do, and a guarantee held only where somebody remembered is not one
([ADR-0027](../adr/0027-ship-the-use-site-diagnostics-as-errors.en.md)).

That has a cost worth knowing before you reference the package. On an existing codebase `DCAT0006`
fires on **every** literal suppression at once, and being an error it fails the build that day —
`TreatWarningsAsErrors` no longer has anything to do with it. Lower it to `suggestion`, migrate at
your own pace, then delete the line.

## Does this end up in my application?

No. `SuppressMessageAttribute` is `[Conditional("CODE_ANALYSIS")]`, which means the compiler does not
write it into your assembly at all unless you ask for it. The constants are folded before that, so
what remains of the whole suppression is nothing: no attribute, no strings, no reference to the
catalogue.

The catalogue package is a compile-time convenience. It is not a runtime dependency, and this is
asserted by a test rather than promised — see `tests/DiagnosticCatalog.ZeroFootprint.UnitTests`, and
[the zero-footprint guarantee](zero-footprint.en.md) for exactly what that test establishes and what
it does not.

The one exception is deliberate: `UnconditionalSuppressMessage` carries no `[Conditional]`, precisely
so the trimmer can read it from the compiled assembly long after the compiler has finished. It is
preserved, with the catalogue's values folded in as plain strings.

## Two things this cannot help with

Stated plainly rather than left for you to discover:

| What you write | Why it is out of reach |
| --- | --- |
| `#pragma warning disable S1144` | Takes a bare identifier, not an expression. No constant can be substituted, ever. |
| `dotnet_diagnostic.S1144.severity` in `.editorconfig` | Configuration keys are plain text, outside the C# compilation model entirely. |

And one boundary worth being clear about: this checks that a suppression is **structurally
coherent** — that it names a real rule, coherently. It has no opinion on whether suppressing that
rule *there* was a good idea. That judgement stays yours, which is what `Justification` is for.

## Where to look next

* [`DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self/README.md) — the `DCAT` rules
  themselves as a catalogue, for when you suppress one of *these* diagnostics.
* [The diagnostics reference](diagnostics.en.md) — every `DCAT` id, what triggers it, how to
  configure it.
* [Publishing a catalogue](authoring-a-catalogue.en.md) — if your team owns an analyzer.

---

<div align="center">
<a href="./alternatives.en.md">← The alternatives</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./adopting-a-catalogue.en.md">Adopting a catalogue on an existing codebase →</a>
</div>
