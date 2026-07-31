# When not to use this

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./when-not-to-use.fr.md)

For anyone deciding whether to adopt. Written to talk you out of it where it should — a library that
only argues for itself is one you have to evaluate twice.

## The short answer

Reach for this when **suppressions are load-bearing** — when there are enough of them, over a long
enough life, that one of them being quietly wrong is a real cost. Below that line, the ceremony is
not repaid.

## Cases where it is not worth it

### A handful of suppressions in one project

Ten suppressions, one repository, one team, no analyzer upgrade in sight. You can read them all in a
minute, and a wrong category costs nothing you would ever notice.

The library is not harmful here — it costs a `PackageReference` and no run-time anything — but it
solves a problem you do not have. Adopt it when the number grows, which is a decision you can take
later at no penalty: nothing about the literal form has to be undone first.

### You suppress with `#pragma`, not with attributes

```csharp
#pragma warning disable S1144
```

This is **out of reach, permanently**. The directive takes a bare identifier token, not an
expression, so there is no position where a constant could be substituted. No version of this
library will change that; it is the C# grammar.

If your codebase suppresses mostly this way, the attribute-based coverage will feel like it misses
the point — because for you, it does.

### You configure severity in `.editorconfig` rather than suppressing at a site

```ini
dotnet_diagnostic.S1144.severity = none
```

Also out of reach, for a related reason: `.editorconfig` keys are plain text read outside the C#
compilation model entirely. A project that turns rules off globally and never suppresses at a site
has nothing here to check.

### Your rules are localised

If your analyzer's titles and messages are `LocalizableString`, resx-backed, then that text cannot be
a `const` and falls outside this model. The identifier and the category still can be — those are the
axis this library covers — but if the value you wanted to strongly reference is the translated
message, resource files remain the right tool and this is not an alternative to them.

### You want the suppression *justified*, not just spelled correctly

This checks that a suppression is **structurally coherent**: that it names one real rule, coherently.
It has no opinion on whether suppressing that rule at that site was a good idea, and it will never
have one. That judgement is what `Justification` is for, and what code review is for.

A team whose actual problem is "people suppress things they should fix" is not helped by any of this.

## Cases where it is worth it

Stated as the mirror, so the line is visible from both sides:

* **A codebase that suppresses routinely.** Hundreds of sites, several analyzers, several
  repositories. One wrong category is invisible; a hundred of them is a record nobody can trust.
* **An analyzer upgrade path that must surface renames and retirements.** A vendor bump that voids a
  suppression silently is the exact failure the `[Obsolete]` carry-forward converts into a build
  warning that names the rule.
* **An analyzer author who wants their rules referenced symbolically.** Feeding your own
  `DiagnosticDescriptor` from your own catalogue makes the category your users write exact by
  construction — something a third-party mirror can never offer.
* **A team standardising a ruleset across repositories.** The catalogue is the shared vocabulary, and
  it is checked by the compiler in each repository rather than agreed in a wiki.

## The costs, stated plainly

Not "trade-offs". Costs.

| Cost | Size |
| --- | --- |
| A `PackageReference` per project that writes suppressions. | One line. |
| Longer suppression lines — `SonarRule.S1144.Category` against `"Major Code Smell"`. | Real, and the reason aliases exist. |
| Your catalogue is a snapshot of a vendor release. | It goes stale, and only `dcat validate` or a regeneration will say so. |
| Adopting on an existing codebase reports every literal suppression at once, if you take the analyzers. | Manageable with a severity ramp; unmanageable if you take the package under `TreatWarningsAsErrors` and change nothing else. |

What is **not** a cost, and is often assumed to be: run time. There is none. The attribute is
`[Conditional("CODE_ANALYSIS")]` and is not emitted; the constants fold before that. No dependency
reaches your application, and a test asserts it.

## What to do instead, if the answer is no

* **Keep the literals and write the category from the descriptor.** If you own the analyzer, at least
  make sure the value you paste comes from `DiagnosticDescriptor` rather than from documentation
  about it. Most of the drift this library prevents starts there.
* **Grep before an upgrade.** A text search for the ids you suppress, run against the vendor's
  release notes, catches retirements. It is manual and it does not catch categories, but it is
  something.
* **Read [the alternatives](alternatives.en.md).** The next page compares this against the other
  ways people solve the same problem, including doing nothing.

---

<div align="center">
<a href="./concepts.en.md">← Core concepts</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./alternatives.en.md">The alternatives →</a>
</div>
