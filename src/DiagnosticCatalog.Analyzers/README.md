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
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "kept for reflection")]
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
[SuppressMessage(SonarRule.S1144.Category, "S1144", Justification = "kept for reflection")]
// becomes
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "kept for reflection")]
```

Only the literal is rewritten; whatever spelling you chose for the other side, an alias included, is
left alone. And if the literal names something the referenced rule does not — `"S9999"` beside
`SonarRule.S1144.Category` — you get the diagnostic and no fix. Completing that one would silence a
different rule than the one silenced today, which is a decision for you and not for a lightbulb.

## When the two arguments name different rules

That one gets **two** fixes and no recommendation:

```text
Use SonarRule.S1144.Id        — keep the category, correct the identifier
Use SonarRule.S2094.Category  — keep the identifier, correct the category
```

Only you know which half was the typo, so neither is offered as the default. Worth knowing while you
choose: Roslyn matches a suppression on the **identifier alone** and never looks at the category, so
correcting the category leaves what is suppressed exactly as it is, while correcting the identifier
changes it.

## Writing a rule by hand

A catalogue is normally generated, and generated code satisfies the contract by construction. When you
write one yourself, three fixes are there for the mechanical part:

```csharp
[DiagnosticRule]
public sealed class JD0007                      // → Make 'JD0007' static
{
    private static readonly string Id = "JD0007";   // → Make 'Id' a public constant
                                                    // → Declare 'public const string Category'
}
```

Each is offered **only where the repair is already written in the code**. `static` is not offered to a
generic type, to a `struct`, or to a class holding an instance member — the keyword would not compile
there, and removing what blocks it is a change to your design rather than a repair of it. A `partial`
class is refused too: the parts the fix cannot see are the ones that decide.

The member repairs correct modifiers and never the value. A `const int Id`, a blank string, an
initialiser that is not constant — those are reported with no fix, because the code says nothing about
what you meant.

> **The one to think about before pressing it.** *Declare 'public const string Category'* writes
> `"TODO"`. That is a real string, so `DCAT0004` stops being reported — you have swapped a warning for
> a marker. A category nobody fills in is wrong forever and invisible in every build, because Roslyn
> matches a suppression on its identifier alone. `Id` is different: it is written `nameof(JD0007)`,
> read off the declaration rather than invented.

## Referencing it

Analysis assemblies must never become runtime dependencies, so reference it privately:

```xml
<PackageReference Include="DiagnosticCatalog.Analyzers" Version="0.1.0" PrivateAssets="all" />
```

A catalogue package that references this one brings the analyzers to **its** consumers too, so
referencing the catalogue alone is enough once one does. That was measured against a real restore
rather than read from NuGet's documentation, which says the opposite:

| A catalogue referencing this package with | The analyzers run for its consumers |
| --- | --- |
| no `PrivateAssets` | **yes** |
| `PrivateAssets="none"` | yes |
| `PrivateAssets="all"` | no |

If you publish a catalogue and would rather not impose analysis on everyone downstream, say so
explicitly with `PrivateAssets="all"` — silence propagates.

## What it does not do

It does not validate an arbitrary string. `[SuppressMessage("Usage", "S1144")]` with the wrong
category matches no known rule, and nothing is reported — the mechanism that makes a wrong category
impossible is the constant itself, which the compiler checks. These analyzers get you to the
constants and keep you there.

## Licence

Apache-2.0. Unofficial; not affiliated with any analyzer vendor.
