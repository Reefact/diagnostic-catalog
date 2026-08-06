# Getting started

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./getting-started.fr.md)

<!-- dcat-doc:missing SonarRule.S1145 the deliberate mistake of step 3; the whole step is the CS0117 it produces -->

For anyone with a project that already suppresses analyzer warnings. Ten minutes, one package
reference, and one deliberate mistake — the mistake is the point.

You will:

1. reference a catalogue;
2. rewrite one suppression against constants;
3. break it on purpose and watch the compiler catch it;
4. decide what the rest of the codebase does.

## What you need

A C# project that already runs an analyzer and suppresses at least one of its warnings — Sonar, the
.NET `CAxxxx` rules, or StyleCop. If you have no suppressions at all, there is nothing here for you
yet; [when not to use this](when-not-to-use.en.md) says so plainly.

Nothing else. No SDK version to bump, no tool to install, no generator to run.

## 1. Reference a catalogue

Pick the one matching the analyzer whose warnings you suppress:

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="0.1.0" />
```

There is one for [SonarAnalyzer](https://www.nuget.org/packages/DiagnosticCatalog.Sonar) (`Sxxxx`),
one for [the .NET analyzers](https://www.nuget.org/packages/DiagnosticCatalog.NetAnalyzers)
(`CAxxxx`), one for [StyleCop](https://www.nuget.org/packages/DiagnosticCatalog.StyleCop)
(`SAxxxx`), one for [the Roslyn IDE rules](https://www.nuget.org/packages/DiagnosticCatalog.CodeStyle)
(`IDExxxx`), one for [xUnit's](https://www.nuget.org/packages/DiagnosticCatalog.Xunit)
(`xUnitxxxx`), one for [NUnit's](https://www.nuget.org/packages/DiagnosticCatalog.NUnit)
(`NUnitxxxx`), one for [MSTest's](https://www.nuget.org/packages/DiagnosticCatalog.MSTest)
(`MSTESTxxxx`) and one for [the trimming and AOT warnings](https://www.nuget.org/packages/DiagnosticCatalog.Trimming)
(`ILxxxx`). Reference more than one if you run more than one.

That one reference also turns the checking on: every catalogue depends on `DiagnosticCatalog`, which
carries the `DCAT` analyzers and their code fixes beside the marker attributes and may not be hidden
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.en.md)). There is no second
package to add — and if you want the checks with no catalogue at all, `DiagnosticCatalog` on its own
is the answer.

One consequence lands on the very first build. `DCAT0006` reports each literal suppression that
matches a rule you now have, as an **error**, so a codebase already holding a few hundred of them
meets them all at once. Turn it down first and the tour stays a tour:

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.DCAT0006.severity = suggestion
```

[Adopting a catalogue](adopting-a-catalogue.en.md) is what that line is the first step of; step 4
comes back to it.

## 2. Rewrite one suppression

Find a suppression you already have. It looks like this:

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "Called by the serializer.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Replace the two strings with the two constants:

```csharp
using DiagnosticCatalog.Sonar;

[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Called by the serializer.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Build. It compiles, the warning is still suppressed, and the assembly you get is byte-for-byte the
one you had before — see [step 5](#5-check-what-it-cost-you).

Type `SonarRule.` and IntelliSense lists every rule the catalogue carries; type `S1144` and it
narrows to it. Hover the constant and you get the rule's own title, which is where the prose you
used to paste into the suppression now lives.

## 3. Break it on purpose

This is the step worth doing rather than reading. Change one digit:

```csharp
[SuppressMessage(SonarRule.S1145.Category, SonarRule.S1145.Id)]
```

Build again:

```text
error CS0117: 'SonarRule' does not contain a definition for 'S1145'
```

Now do the same to the version you started with:

```csharp
[SuppressMessage("Major Code Smell", "S1145")]
```

Build again. It compiles. Nothing is reported, by anything, ever — and the warning the suppression
was hiding is quietly back, or quietly not, depending on whether the code that raised it is still
there.

That difference is the whole library. [Why magic strings fail](the-problem.en.md) takes the second
build apart and explains why nothing in the platform is in a position to report it.

Put the digit back.

## 4. Decide what the rest of the codebase does

You now have one checked suppression and, most likely, a few hundred that are still strings. Three
honest options:

* **Leave them.** A catalogue is useful one suppression at a time. Nothing degrades because the rest
  of the file is still literals — but the severity line from step 1 stays, because `DCAT0006`
  reports every one of them.
* **Convert as you touch them.** Rewrite a suppression when you are already editing its file. Costs
  nothing extra and reaches the code that changes.
* **Convert in bulk.** This is what the `DCAT` analyzers are for — they report every literal
  suppression that matches a rule you have, with a fix that rewrites it and adds the `using`, and
  *Fix all occurrences* applies it across a project or a solution in one step.

  They are already in your compilation: the catalogue brought them in with it, and there is nothing
  else to reference.

Which to pick is the subject of the adoption section of
[Writing suppressions that the compiler checks](writing-suppressions.en.md).

## 5. Check what it cost you

Nothing, and this is measurable rather than claimed.

`SuppressMessageAttribute` is `[Conditional("CODE_ANALYSIS")]`. Unless you define that symbol — and
almost nobody does — the compiler does not write the attribute into your assembly at all. The
constants are folded to their values before that point, so what survives of the whole suppression
is: nothing. No attribute, no strings, no reference to the catalogue, no assembly to load at start-up.

The catalogue is a compile-time convenience, and the repository asserts it with a test rather than
promising it — `tests/DiagnosticCatalog.ZeroFootprint.UnitTests`.

The one deliberate exception is `UnconditionalSuppressMessage`, which carries no `[Conditional]`
precisely so the trimmer can read it from the compiled assembly. There the values are folded in as
plain strings, which is what the trimmer wanted anyway.

## What you did not have to do

Worth naming, because most tooling asks for all of it:

* no source generator to run, and nothing in `obj/` that has to stay in step;
* no configuration file, unless you want to change a diagnostic's severity;
* no runtime dependency, and nothing to initialise at start-up;
* no change to how you build, test or publish.

## Where to go next

* [**Why magic strings fail**](the-problem.en.md) — what step 3 actually demonstrated, and why the
  category argument is the worse half.
* [**Core concepts**](concepts.en.md) — rule, catalogue, container, category: the four words the
  rest of the documentation uses.
* [**Writing suppressions that the compiler checks**](writing-suppressions.en.md) — aliases,
  adoption on a large codebase, and the two things this cannot reach.

---

<div align="center">
<a href="./the-problem.en.md">← Why magic strings fail</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./concepts.en.md">Core concepts →</a>
</div>
