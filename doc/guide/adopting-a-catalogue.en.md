# Adopting a catalogue on an existing codebase

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./adopting-a-catalogue.fr.md)

For anyone with more suppressions than they want to convert by hand. How to go from a few hundred
literals to checked references without a week of red builds.

> **Where this stands today.** The bulk conversion described below is
> `DiagnosticCatalog.Analyzers`, and that package **has no version on nuget.org yet**. Everything on
> this page about severities, scoping and ordering applies the day it ships; until then the manual
> path at the end is what is available.
> [Project status](https://github.com/Reefact/diagnostic-catalog#-project-status) is the current
> answer.

## The day-one problem

You reference a catalogue, you reference the analyzers, you build — and `DCAT0006` fires on **every
literal suppression that matches a rule you now have**. Not a sample. All of them, at once.

That is not a defect. It is the diagnostic doing exactly what it is for: it reports a suppression
that a catalogue reference could replace, and after you add the catalogue, every one of them
qualifies. A version that trickled would be a version that never finished.

And `DCAT0006` ships as an **error**
([ADR-0027](../adr/0027-ship-the-use-site-diagnostics-as-errors.en.md)), so this does not wait for a
`<TreatWarningsAsErrors>` to bite: the build that added the package is the build that failed, with
hundreds of errors, in code nobody touched. Teams reasonably conclude the library is not ready.

Which is why the first line of the ramp below is not optional on an existing codebase. It is the one
deliberate downgrade the default expects you to make.

## The ramp

Three settings over three moments, and the whole adoption fits inside them.

```mermaid
flowchart LR
    A["<b>Day 1</b><br/>suggestion<br/><i>visible in the IDE,<br/>silent in the build</i>"]
    B["<b>Migrating</b><br/>Fix all occurrences,<br/>project by project"]
    C["<b>Done</b><br/>back to the default error<br/><i>a new literal cannot land</i>"]
    A --> B --> C
```

**Day one — make it visible without making it fatal.**

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.DCAT0006.severity = suggestion
```

A suggestion appears in the IDE as a lightbulb and in `dotnet build` as nothing. The build that adds
the package is green, and the migration starts when you decide rather than when the package arrives.

**While migrating — leave the other three alone.**

`DCAT0001` and `DCAT0007` are errors already, and they should stay that way. They mean a suppression
is *not doing what it looks like*: a pair naming two different rules, or a half-converted one. Both
are defects you want reported the moment they appear, and neither fires in bulk — they only exist
where somebody has already started using references. `DCAT0009` is the same in kind but still ships
as a warning, because it misses an identifier reached through a constant; raise it if a trimmed build
matters to you.

```ini
dotnet_diagnostic.DCAT0009.severity = error
```

**When you finish — delete the line.**

```ini
# gone: dotnet_diagnostic.DCAT0006.severity = suggestion
```

Removing the downgrade restores the default: from then on a new literal suppression cannot merge,
which is what keeps the codebase converted after the person who converted it moves on.

## Converting

Build once with the analyzers referenced and every convertible suppression carries a fix. In Visual
Studio and Rider, *Fix all occurrences* applies it across a **document**, a **project** or the
**solution** in one step.

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "kept for reflection")]
// becomes
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "kept for reflection")]
```

Three behaviours are worth knowing before you run it across a solution.

**Everything else in the attribute is left exactly as written.** `Justification`, `Scope`, `Target`
and `MessageId` are yours and are not touched. The fix rewrites two arguments and adds the `using`
the reference needs.

**The friendly-name suffix is dropped.** Visual Studio's *Suppress → In Source* writes
`"S1144:Unused private members should be removed"`; the fix recognises that form and replaces the
whole thing. The prose lived in the suppression only because there was nowhere else to put it — the
catalogue carries the rule's own title as XML documentation, so hovering the constant gives it back.

**Two cases get the diagnostic and no fix**, on purpose:

| Situation | Why no fix |
| --- | --- |
| Two catalogues describe the same rule | Choosing between them is a decision about which package your file depends on. |
| `DCAT0007` where the literal names a *different* rule from the reference beside it | Completing it would silence a different rule than the one silenced today, and let the original warning back in. That is a change of behaviour, not a migration ([ADR-0018](../adr/0018-a-code-fix-never-decides-what-only-the-author-can.en.md)). |

Both are places where a lightbulb would have to guess, and the fix declines rather than guessing
quietly.

## Scoping while you work

`.editorconfig` sections are ordinary paths, so a folder can run ahead of or behind the rest:

```ini
[*.cs]
dotnet_diagnostic.DCAT0006.severity = suggestion

# Converted, and staying converted.
[src/Billing/**.cs]
dotnet_diagnostic.DCAT0006.severity = error

# Legacy, scheduled for deletion rather than conversion.
[src/Legacy.Interop/**.cs]
dotnet_diagnostic.DCAT0006.severity = none
```

This is what makes "convert project by project" a real strategy rather than an intention: each
converted area is locked at `error` as it lands, so the boundary only ever moves forward.

## Generated code is already out of scope

You do not have to exclude it, and this is the one thing about the adoption that is free.

`ConfigureGeneratedCodeAnalysis` is per-**analyzer**, not per-diagnostic, and this package ships two
classes precisely so the two groups can differ:

| Analyzer | Diagnostics | Runs on generated code |
| --- | --- | --- |
| `SuppressionUsageAnalyzer` | `DCAT0001`, `DCAT0006`, `DCAT0007`, `DCAT0009` | **no** |
| `DiagnosticRuleDefinitionAnalyzer` | `DCAT0002`, `DCAT0003`, `DCAT0004` | **yes** |

Use-site diagnostics stay out of generated files because a suppression in one is not the author's to
fix, and reporting them would flood every one. Definition diagnostics run *into* them on purpose: a
generated catalogue is generated code, and checking it is the main thing that analyzer is for.

Roslyn decides what counts as generated: a file named `*.g.cs` or `*.generated.cs`, a type marked
`[GeneratedCode]`, or a file you declare yourself:

```ini
[src/Legacy/Interop.cs]
generated_code = true
```

## What order to convert in

There is no mechanism behind this, only experience of what leaves a codebase in a coherent state.

1. **One small project first, by hand.** Not to save time — to see what the diff looks like in review
   before opening one with four hundred files in it.
2. **Then the projects with the most suppressions**, with *Fix all occurrences* per project. The
   review is mechanical and the reviewer needs to be told that: a diff of four hundred identical
   two-argument rewrites is read by spot-checking, not by reading.
3. **Raise `DCAT0006` to `error` for each project as it lands**, in a scoped `.editorconfig` section.
4. **Last, the file that suppresses `DCAT` diagnostics themselves**, if you have one. That is what
   [`DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self/README.md) is for.

Keep the conversion in its own pull requests, separate from behaviour changes. A rewrite of every
suppression in a project is exactly the diff a real change should not be hiding in.

## What will not convert, and is not meant to

Two forms are permanently out of reach, and neither is reported:

```csharp
#pragma warning disable S1144        // takes a bare identifier; no constant fits
```

```ini
dotnet_diagnostic.S1144.severity = none   # plain text, outside the C# compilation model
```

If a large share of your suppressions are `#pragma`, the conversion will feel thin — see
[when not to use this](when-not-to-use.en.md).

## Without the analyzers package

Until it is published, the mechanised path is not available. What still works:

* **Convert as you touch.** Rewrite a suppression when you are already editing its file. This reaches
  the code that actually changes, and costs nothing extra.
* **A careful search-and-replace.** `"Major Code Smell", "S1144"` → `SonarRule.S1144.Category,
  SonarRule.S1144.Id`, one rule at a time, adding the `using` per file. The compiler is the check
  here: anything you get wrong is a build error rather than a silent no-op, which is the whole
  premise.
* **Do not automate it against a regex over the whole codebase.** The Visual Studio suffix form, the
  `Scope`/`Target` variants and multi-line attributes will each need a different pattern, and a
  rewrite that half-matches is how a `DCAT0007` gets created rather than fixed.

## Where to go next

* [**Configuration**](configuration.en.md) — every severity key, the category-wide switch, and what
  is deliberately not configurable.
* [**The zero-footprint guarantee**](zero-footprint.en.md) — what the conversion costs your shipped
  assembly, and how that is asserted rather than promised.
* [**The `DCAT` diagnostics**](diagnostics.en.md) — the full reference for each id.

---

<div align="center">
<a href="./writing-suppressions.en.md">← Writing suppressions that the compiler checks</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./configuration.en.md">Configuration →</a>
</div>
