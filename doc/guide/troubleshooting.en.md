# Troubleshooting

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./troubleshooting.fr.md)

For anyone whose build is telling them something unexpected — or, more often, telling them nothing.
Symptoms first, cause second.

## Nothing is reported at all

The commonest report, and it has four causes with the same appearance.

```mermaid
flowchart TB
    S["no DCAT diagnostic anywhere"]
    S --> Q1{"is DiagnosticCatalog.Analyzers<br/>referenced?"}
    Q1 -- "no" --> A1["that package carries the diagnostics.<br/>A catalogue alone gives constants."]
    Q1 -- "yes" --> Q2{"is it PrivateAssets=all<br/>on a package you CONSUME?"}
    Q2 -- "yes" --> A2["analyzers do not flow from<br/>a dependency that hides them"]
    Q2 -- "no" --> Q3{"do you reference a catalogue<br/>describing the rules you suppress?"}
    Q3 -- "no" --> A3["DCAT0006 reports only rules it can see.<br/>No catalogue, no match, silence by design."]
    Q3 -- "yes" --> Q4{"is the file generated?"}
    Q4 -- "yes" --> A4["use-site diagnostics do not run<br/>on generated code, deliberately"]
    Q4 -- "no" --> A5["check .editorconfig severity"]
```

**The analyzers are a separate package.** Referencing `DiagnosticCatalog.Sonar` gives you constants,
and a misspelled rule is a compile error — that is the whole guarantee and it needs no analyzer. What
finds the suppressions you have *not* converted is `DiagnosticCatalog.Analyzers`, which today **has no
version on nuget.org**. [Project status](https://github.com/Reefact/diagnostic-catalog#-project-status)
is the current answer.

**`DCAT0006` is silent by design when it knows nothing.** It reports a literal pair only when a rule
the compilation can see matches it. A codebase with no catalogue stays completely quiet — which is
correct, and looks identical to a broken installation.

**Generated code is excluded on purpose**, for use-site diagnostics only. A suppression inside a
generated file is not the author's to fix. `DCAT0002`–`DCAT0004` do run there, because a generated
catalogue is exactly what they exist to check. [Configuration](configuration.en.md#generated-code)
has the table.

## My rule declaration is not recognised

You wrote `[DiagnosticRule]` and no `DCAT0002`/`0003`/`0004` appears, and neither does your rule in
anyone's `DCAT0006`.

| Check | Why |
| --- | --- |
| Is the attribute's full name `DiagnosticCatalog.DiagnosticRuleAttribute`? | Matching is by fully qualified metadata name. An attribute of the same short name in another namespace is not this one. |
| If you declared the marker yourself, is it in the `DiagnosticCatalog` namespace? | The dependency-free copy is supported — but only at that exact name. |
| Is the class `static` and non-generic? | A generic type has no constant members to offer. |
| Are `Id` and `Category` `const`, not `static readonly`? | This is the mistake people make first. |

The last one is worth expanding, because the code looks right:

```csharp
public static readonly string Id = "JD0007";   // has a value at run time…
                                               // …and cannot be an attribute argument
```

`static readonly` is initialised at run time. An attribute argument must be known at **compile** time.
That is a C# rule, and it is the reason the whole model is built on `const`.

## `CS0117: 'SonarRule' does not contain a definition for 'S1145'`

Working as intended. That is the library doing the one thing it exists to do — the reference does not
resolve, so the build stops, where a literal would have compiled and silently suppressed nothing.

Check the id against the vendor's, or run `dcat explain <catalogue.dll> S1145` to see whether the
catalogue knows it under a different spelling.

If the rule **used to exist**, see the next entry.

## `CS0618: 'SonarRule.S1144' is obsolete`

The vendor retired the rule, and the catalogue carried it forward rather than deleting it
([ADR-0010](../adr/0010-carry-a-retired-rule-forward-as-obsolete.en.md)).

The message names the release that dropped it. What to do:

* **the suppressed warning no longer exists** — delete the suppression;
* **it was replaced** — the obsolete message says by what, when the vendor said so;
* **you are not ready** — suppress `CS0618` at that site, and leave a note. You are choosing to keep a
  suppression that no longer matches anything, which is worth writing down.

The alternative — deleting the constant — would have given you `CS0117` naming nothing useful, on an
upgrade, with no clue that a rule had been retired at all.

## `CS0246: The type or namespace name 'DiagnosticRule' could not be found`

You are declaring rules of your own, and the foundation is not resolvable in your compilation.

Usually because a catalogue you reference hid it:

```xml
<!-- inside a catalogue package you consume -->
<PackageReference Include="DiagnosticCatalog" PrivateAssets="all" />
```

Add `DiagnosticCatalog` yourself, and — if it is your catalogue — stop hiding it. See
[packaging a catalogue](packaging-a-catalogue.en.md#reference-the-foundation-the-ordinary-way).

## `DCAT0006` fires on hundreds of files at once

Expected, on the day you add the analyzers to an existing codebase. It reports every literal
suppression a catalogue reference could replace, and after you add the catalogue, all of them qualify.

Under `TreatWarningsAsErrors` that fails the build immediately. Lower it to `suggestion`, migrate,
then raise it — [adopting a catalogue](adopting-a-catalogue.en.md) is the whole procedure.

## `DCAT0006` appears but offers no fix

Two catalogues describe the same rule. Choosing between them is a decision about which package that
file depends on, and a lightbulb has no basis for it.

Reference one catalogue, or write the reference by hand.

## `DCAT0007` appears and offers no fix

The literal names a **different** rule from the reference beside it:

```csharp
[SuppressMessage(SonarRule.S1144.Category, "S9999")]
```

Completing it from `S1144` would silence a different rule than the one silenced today, and let the
original warning back in. That is a change of behaviour, not a migration
([ADR-0018](../adr/0018-a-code-fix-never-decides-what-only-the-author-can.en.md)).

Decide which rule you meant, and write it.

## `Category` and `Id` became ambiguous

```csharp
using static DiagnosticCatalog.Sonar.SonarRule.S1144;
using static DiagnosticCatalog.Sonar.SonarRule.S2094;   // now Category and Id are ambiguous
```

`using static` works for exactly one rule per file. Use an alias instead — it scales and is checked
identically:

```csharp
using Unused = DiagnosticCatalog.Sonar.SonarRule.S1144;
using Dead   = DiagnosticCatalog.Sonar.SonarRule.S2094;
```

## `dcat` says the project must already be built

It reads; it does not build. Run `dotnet build -c Release` first — the message names the path it
looked at and the command that would produce it.

This is what keeps `dcat validate --project` safe against a working copy: it restores nothing, writes
no `obj/`, and touches no output.

## `dcat` says a `.deps.json` names no Roslyn

```text
MyLib.deps.json names no Roslyn — reading through this tool's
```

Not an error. Handing a dependency graph to the descriptor worker **replaces** its own rather than
extending it, so a graph that names no Roslyn would leave the worker with none at all. A
`netstandard2.0` library's `.deps.json` is exactly that, and `dcat` says so rather than reading it.

Your analyzer is read through the tool's Roslyn instead. That is only a problem if it was compiled
against a newer one and uses APIs the tool's does not have.

## `dcat validate` exits `1` and I expected `2`

Different failures, deliberately distinct:

* **`2`** — the catalogue no longer matches its source. A drifted contract.
* **`1`** — it could not be checked: the source would not resolve. A feed outage, an expired
  credential, a rate limit.

A pipeline that treats them alike either retries a real drift or opens a pull request for a network
blip. [Keeping a catalogue current](ci-integration.en.md#reading-the-exit-codes) has the shape.

## A suppression compiles but the warning is still there

Check whether `Scope` and `Target` are involved. This library checks that a suppression names one real
rule, **coherently** — it has no opinion on whether the attribute is placed or scoped correctly, and
never will.

Also check the id itself: Roslyn matches on the **identifier alone** and never on the category, so a
suppression whose category is wrong still suppresses, and one whose id is wrong never does.

## Where to go next

* [**The `DCAT` diagnostics**](diagnostics.en.md) — every id, and what triggers it.
* [**The rule contract**](rule-contract.en.md) — the exact shape a declaration must have.
* [**FAQ**](faq.en.md) — the questions that are not symptoms.

---

<div align="center">
<a href="./rule-contract.en.md">← The rule contract</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./faq.en.md">FAQ →</a>
</div>
