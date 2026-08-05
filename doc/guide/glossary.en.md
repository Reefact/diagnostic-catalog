# Glossary

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./glossary.fr.md)

For anyone who met a word here and wants the exact sense it carries. Alphabetical; each entry says
what it is and, where it matters, what it is *not*.

## Catalogue

An assembly full of [rules](#rule), describing one analyzer. `DiagnosticCatalog.Sonar` is one.

A catalogue is a **snapshot** of the analyzer it mirrors, not a live view: it describes the release it
was generated from, and its [provenance](#provenance) records which. That is why age is the first
thing `dcat list` prints.

Not a package of behaviour. A catalogue contains constants and their XML documentation, and nothing
that runs.

## Category

The first argument of `[SuppressMessage(...)]`, and the one nothing in the platform reads.

Roslyn matches a suppression on the [identifier](#identifier) alone; the category is carried into
metadata — when it is carried at all — and consulted by no compiler, analyzer, test or tool. A wrong
category therefore produces no symptom anywhere, which is the failure this library exists to remove.

Its authoritative value is what the originating analyzer's `DiagnosticDescriptor` declares — not what
the vendor's documentation says about it.

## Category class

A class of `const string` category values, so the same category is written once rather than in every
rule. `SonarCategory` is one; the Sonar catalogue spends 456 rule declarations on 13 of its members.

Marking it `[DiagnosticCategory]` is **required**: a rule must reach its category through a constant
declared in a marked class, which `DCAT0011` reports. What the marker buys is that tooling can tell a
category constant from any other string constant. In a catalogue this repository generates the
container is `internal`, so a suppression names a category only through the rule that carries it —
`SonarRule.S1144.Category`, never the category on its own
([ADR-0026](../adr/0026-reach-a-category-only-through-the-rule-that-carries-it.en.md)).

## Check id

Roslyn's name for the second argument of `[SuppressMessage(...)]` — what this documentation calls the
[identifier](#identifier). It may carry a `:FriendlyName` suffix, which the platform truncates at the
first colon and otherwise ignores.

## Container

The class the rules are nested in, and therefore the first word of every use site: `SonarRule.S1144`.

**Singular**, always — one rule, named. The plural also breaks the derived name: a container ending in
`Rule` names the [category class](#category-class) too, so `SonarRule` gives `SonarCategory` where
`SonarRules` would give `SonarRulesCategory`.

Your users pay for this name twice per suppression and cannot shorten it. They can [alias](#alias) it.

## Alias

A `using` that gives a rule a local name:

```csharp
using Unused = DiagnosticCatalog.Sonar.SonarRule.S1144;
```

Checked exactly like the long form — analysis works on symbols, never on the text you typed. The
recommended shorthand, and the one that scales, unlike `using static`.

## Descriptor

A `DiagnosticDescriptor`: the object an analyzer declares to describe one of its rules — id, title,
message format, category, severity, help link.

The **source of truth** for everything a catalogue publishes. `dcat` constructs every analyzer it
finds and reads the descriptors they actually declare, rather than the vendor's documentation about
them ([ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).

## Foundation

The `DiagnosticCatalog` package. Three attributes and nothing else: `[DiagnosticRule]`,
`[DiagnosticCategory]`, `[assembly: CatalogSource]`.

Referenced by every catalogue, and by anyone declaring rules of their own. A catalogue that hides it
behind `PrivateAssets="all"` leaves its consumers unable to declare rules themselves.

## Identifier

The value of a rule's `Id` — `S1144`, `CA1822`, `SA1000`, `DCAT0006`. The second argument of a
suppression, and the **only** one Roslyn matches on.

Usually the rule type's own name, written `nameof(S1144)` so the two cannot drift. They differ when
the diagnostic's canonical identifier is not a valid C# identifier: `RULE_001` holding `"RULE-001"`.

## Marker

`[DiagnosticRule]`, and what makes a type a rule.

Matched by **fully qualified metadata name** — `DiagnosticCatalog.DiagnosticRuleAttribute` — never by
symbol identity. That is what lets a catalogue declare its own copy rather than take a dependency, and
what keeps an unresolvable attribute recognisable instead of silently invisible.

## Mirror

A catalogue describing somebody else's analyzer. The twelve vendor catalogues here are mirrors.

A mirror can only copy what its source declares **today**. It cannot make a category exact by
construction the way a [first-party](first-party-analyzers.en.md) catalogue can — which is the one
thing owning both buys you.

## Provenance

The assembly-level record of which upstream release a catalogue mirrors, and when it was generated:

```csharp
[assembly: CatalogSource(source: "…", sourceVersion: "…", generatedOn: "yyyy-MM-dd")]
```

The date is a `string` because an attribute argument must be a compile-time constant and no date type
can be one.

A first-party catalogue needs none: it mirrors nothing.

## Release train

A group of scopes that versions, tags and publishes together. `lib`, `cli`, `sonar`, `netanalyzers`,
`stylecop`.

A project joins one by declaring `<ReleaseTrain>` in its own `.csproj`, and that declaration is the
whole membership. Trains exist so that following SonarSource's pace never drags the foundation's
version along ([ADR-0002](../adr/0002-partition-releases-into-trains-by-commit-scope.en.md),
[ADR-0015](../adr/0015-a-catalogues-version-runs-on-its-own-line.en.md)).

## Rule

One analyzer diagnostic, expressed as a static class holding `const string Id` and
`const string Category`, marked `[DiagnosticRule]`.

A **type**, not a row in a table or a key in a file. That shape is forced: an attribute argument must
be a compile-time constant, a `const` lives on a type, and giving each rule its own type is what makes
`S1144.Id` read as one thing. The full requirements are [the rule contract](rule-contract.en.md).

## Suppression

An application of `[SuppressMessage(...)]` or `[UnconditionalSuppressMessage(...)]` — silencing a
warning, never deleting code.

The ordinary one is `[Conditional("CODE_ANALYSIS")]` and is not emitted into your assembly. The
unconditional one is, precisely so the [trimmer](#trimmer) can read it.

## Trimmer

ILLink, the .NET tool that removes unreachable code from a published application.

It reads `UnconditionalSuppressMessage` out of your **compiled assembly**, long after the compiler has
finished, and its decoder accepts only identifiers shaped like `IL####` — discarding everything else
outright. That is why `DCAT0009` exists: a trim suppression naming a Sonar or StyleCop rule is a
no-op no other tool in the toolchain reports.

## Use site

A place where a rule is referenced — in practice, a suppression. The counterpart of a *definition*,
which is where the rule is declared.

The distinction runs through the diagnostics: `DCAT0001`, `DCAT0006`, `DCAT0007` and `DCAT0009` look
at use sites; `DCAT0002`, `DCAT0003` and `DCAT0004` look at definitions. They also differ on generated
code, which is why they ship as two analyzer classes.

---

<div align="center">
<a href="./faq.en.md">← FAQ</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./architecture.en.md">Repository architecture →</a>
</div>
