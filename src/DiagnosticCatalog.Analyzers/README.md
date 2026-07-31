# DiagnosticCatalog.Analyzers

Roslyn analyzers for [DiagnosticCatalog](https://github.com/Reefact/diagnostic-catalog).

They check two things: that a rule **declaration** satisfies the structural contract, and that a
**suppression** referencing one is coherent — a category and an id taken from two different rules, a
half-migrated suppression mixing a reference with a literal, a literal that a catalogue reference
would replace.

## Migrating an existing codebase

The package also carries the code fix for the last of those, which is how a codebase adopts a
catalogue in practice:

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "kept for reflection")]
// becomes
[SuppressMessage(SonarRules.S1144.Category, SonarRules.S1144.Id, Justification = "kept for reflection")]
```

*Fix all occurrences* applies it across a document, project or solution in one step, and the `using`
the reference needs is added for you. Everything else in the attribute is left exactly as written —
`Justification`, `Scope`, `Target` and `MessageId` are yours.

Two behaviours worth knowing before you run it:

* **The friendly-name suffix is dropped.** Visual Studio writes
  `"S1144:Unused private members should be removed"`; the fix recognises that form and replaces the
  whole thing with the reference. The prose lived in the suppression only because there was nothing
  else to hold it — the rule's own documentation has it now.
* **When two catalogues describe the same rule, no fix is offered.** The diagnostic still appears, so
  nothing is hidden, but choosing between them is yours to make.

A suppression left half migrated — one reference, one literal — is reported too, and completed from
the rule the migrated argument already names:

```csharp
[SuppressMessage(SonarRules.S1144.Category, "S1144", Justification = "kept for reflection")]
// becomes
[SuppressMessage(SonarRules.S1144.Category, SonarRules.S1144.Id, Justification = "kept for reflection")]
```

Only the literal is rewritten; whatever spelling you chose for the other side, an alias included, is
left alone. And if the literal names something the referenced rule does not — `"S9999"` beside
`SonarRules.S1144.Category` — you get the diagnostic and no fix. Completing that one would silence a
different rule than the one silenced today, which is a decision for you and not for a lightbulb.

## When the two arguments name different rules

That one gets **two** fixes and no recommendation:

```text
Use SonarRules.S1144.Id        — keep the category, correct the identifier
Use SonarRules.S2094.Category  — keep the identifier, correct the category
```

Only you know which half was the typo, so neither is offered as the default. Worth knowing while you
choose: Roslyn matches a suppression on the **identifier alone** and never looks at the category, so
correcting the category leaves what is suppressed exactly as it is, while correcting the identifier
changes it.

## Referencing it

Analysis assemblies must never become runtime dependencies, so reference it privately:

```xml
<PackageReference Include="DiagnosticCatalog.Analyzers" Version="0.1.0" PrivateAssets="all" />
```

A catalogue package may bring these analyzers to its own consumers instead, so that referencing the
catalogue is enough. That is a decision the catalogue makes, not a default of this package.

## What it does not do

It does not validate an arbitrary string. `[SuppressMessage("Usage", "S1144")]` with the wrong
category matches no known rule, and nothing is reported — the mechanism that makes a wrong category
impossible is the constant itself, which the compiler checks. These analyzers get you to the
constants and keep you there.

## Licence

Apache-2.0. Unofficial; not affiliated with any analyzer vendor.
