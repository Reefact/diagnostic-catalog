# ADR-0026 | Reach a category only through the rule that carries it

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)

**Status:** Proposed
**Proposed:** 2026-08-02
**Decision Makers:** Reefact

## Context

A generated catalogue publishes two things a suppression can name. The rule
member:

```csharp
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]
```

And, until this decision, the category constant on its own:

```csharp
[SuppressMessage(SonarCategory.MajorCodeSmell, SonarRule.S1144.Id)]
```

Both compile. Both fold to the same two strings. Today they are
indistinguishable in the emitted metadata, and the second reads well enough that
an IDE completion list invites it.

They stop agreeing the moment the vendor moves the rule. When SonarSource
recategorises `S1144`, the catalogue is regenerated and
`SonarRule.S1144.Category` follows it. `SonarCategory.MajorCodeSmell` does not:
it still names the category the rule used to be in. The suppression keeps
compiling, keeps reading as deliberate, and stops matching the diagnostic it was
written for. That is the silent, permanent failure this library exists to
prevent, reintroduced through a member the library itself published.

The analyzers report the decoupled form, which is how it was found: the usage
suite wrote it as legitimate consumer code and the build failed. But reporting
it is a poor remedy for three reasons. The message calls the category
`the literal "Major Code Smell"`, and there is no literal in that source. The
diagnostic that fires, `DCAT0007`, is defined as *mixing a catalogue reference
with a string literal*, which is not what happened. And a warning is advice: it
can be suppressed, ignored, or never surfaced by a build that does not treat
warnings as errors.

## Decision

**A category constant is not part of a catalogue's public surface.** The
generator emits the `[DiagnosticCategory]` container as `internal`.

A rule's own `Category` member stays public and is unchanged:

```csharp
public const string Category = SonarCategory.MajorCodeSmell;   // still public, still folded
```

A `const` initialised from another `const` is a compile-time constant, so the
public member carries the literal value and no consumer loses anything they can
legitimately use. What they lose is the ability to name a category *without*
naming the rule it belongs to — which is the whole intent.

This makes the decoupling **unwritable** rather than discouraged. A consumer
reaching for the wrong spelling gets `CS0122` from the compiler at the point of
use, not a warning they may or may not see, and not a lint they can disable.

## Consequences

**This supersedes part of [ADR-0012](0012-a-catalogue-never-renames-a-member-it-published.en.md).**
That record's context states that a catalogue publishes "a category constant,
referenced as `SonarCategory.MajorCodeSmell`" in "the consumer's own source,
inside `SuppressMessageAttribute` arguments". After this decision that sentence
describes something that no longer compiles. ADR-0012's actual rule — that a
catalogue never renames a member it published — is untouched and still binds
every public member. Only its premise about which members are public narrows.
ADR-0012 is not edited here; if this is accepted, its successor status is the
maintainer's to record.

**It is a breaking change on three published packages.** `DiagnosticCatalog.Sonar`,
`DiagnosticCatalog.NetAnalyzers` and `DiagnosticCatalog.StyleCop` are on
nuget.org at `0.2.1`, `0.2.1` and `0.3.0`. Any consumer who wrote
`SonarCategory.MajorCodeSmell` stops compiling on upgrade, with a clear
compiler error and a one-line repair: name the rule instead. The change lands in
`1.0.0-preview.1`, which is the cheapest moment it will ever have — a preview
exists so a decision like this can still be taken.

**The specification's account of what the marker buys narrows.** §7.7 says the
`[DiagnosticCategory]` marker lets the `DCAT0006` code fixer offer
`SonarCategory.MajorCodeSmell` instead of a bare literal. No fixer implements
that today, and after this decision none should: offering an internal member to
a consumer would not compile. The marker keeps its other stated purpose, which
is to let tooling recognise a category constant, and gains a plainer one — it is
what the generator marks so a future check can validate the container's
contents.

**It does not fix the intermediate constant.** A consumer who writes
`const string RuleId = SonarRule.S1144.Id;` and pairs it with that rule's
category is still reported by `DCAT0007`, and the guide still lists that form
under *accepted*. That is a separate defect in `SuppressionAttribute.Resolve`,
which classifies by declaring type and never follows an initialiser, and it is
not addressed here.

**Nothing enforces this beyond the generator.** A hand-written catalogue can
still publish a public category container; the contract does not forbid it, and
`DCAT0002`–`DCAT0004` say nothing about categories. This decision binds what
*this repository generates*.

## Follow-up Actions

* Report the false positive on the intermediate constant separately, and settle
  whether `Resolve` should follow one hop — the finding this decision does not
  cover.
* Correct `DCAT0007`'s message, which names a literal that need not exist in the
  source.
* Reword specification §7.7 so the marker's stated benefit no longer names a code
  fix that cannot be offered.
