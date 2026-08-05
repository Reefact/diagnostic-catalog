# ADR-0031 | Find analyzers the way the compiler finds them

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0031-find-analyzers-the-way-the-compiler-finds-them.fr.md)

**Status:** Accepted
**Proposed:** 2026-08-05
**Accepted:** 2026-08-05
**Decision Makers:** Reefact

## Context

The generator reads an upstream analyzer package by loading its assemblies and
constructing the analyzers they declare, because a `DiagnosticDescriptor` exists
only at run time (ADR-0009). Until now it selected those analyzers by asking each
assembly for every type it declares and keeping the non-abstract subclasses of
`DiagnosticAnalyzer`.

Materialising a type resolves its base type and its interfaces. An analyzer
package is mostly not analyzers: it carries code fixes, internal helpers and
service types, and those reach assemblies the generator has no reason to hold.
When one cannot be resolved, the whole enumeration answers with a single
`ReflectionTypeLoadException` carrying the types that survived and no name for
the ones that did not.

A read that lost a rule must be refused, because an absent rule is
indistinguishable from a retired one and would be published as `[Obsolete]`,
telling a vendor's users something false about that vendor's product
(ADR-0024, ADR-0010). With no name for what was lost, the generator could not
tell a code fix from an analyzer, so it refused the run.

Measured across twenty analyzer packages, four were refused that way:
`Roslynator.Analyzers`, `Roslynator.Formatting.Analyzers`,
`Microsoft.CodeAnalysis.Analyzers` and
`Microsoft.CodeAnalysis.BannedApiAnalyzers`. In each case the descriptors had
already been read in full; what failed to load declared no rule. Two distinct
causes were involved — an internal service that could not be constructed, and
internal types implementing a Roslyn interface that has since gained a member —
and neither is answerable by anything the generator can carry.

The compiler does not enumerate types. Roslyn discovers analyzers by reading an
assembly's metadata for the types marked with `[DiagnosticAnalyzer]` and loading
those alone. An analyzer the attribute does not name is loaded by no host and
reports no diagnostic in any build.

Two descriptors are reachable only through types the attribute does not name.
`SecurityCodeScan.VS2019` declares one whose identifier is `Debug` and whose
title does not resolve, and `Microsoft.CodeAnalysis.CSharp.CodeStyle` declares
`IDE0079`, whose analyzer is driven by the IDE through a separate interface
rather than by analyzer discovery; with `IDE0079` configured as a warning and
code-style enforcement on, a build reports it on an unnecessary suppression not
at all, while the same harness reports `IDE0005`.

## Decision

The generator selects the analyzers an assembly marks with
`[DiagnosticAnalyzer]`, read from metadata before anything is loaded, and no
longer enumerates the types an assembly declares.

## Rationale

The refusal was right on the evidence it had and wrong about the packages it
turned away, and the two are the same fact: a type that failed to load has no
name left to ask about. Nothing could be added to the generator to make that
judgement sound, because the information required to make it is destroyed by the
failure it is judging. The only way to answer "did an analyzer go missing" is to
know which analyzers exist *before* loading anything, which is what reading the
attribute from metadata provides. The refusal is therefore preserved and made
precise rather than relaxed: an attributed analyzer that fails to load still
stops the run.

Following the compiler's own discovery also settles what a catalogue is *for*.
A catalogue exists so that a suppression's arguments are compile-checked, and a
suppression is only meaningful for a diagnostic a consumer's build can report.
Selecting by base type published rules from types no host loads — a descriptor
whose title does not resolve, and one belonging to an analyzer the compiler
never runs. Those entries invited a consumer to reference a rule that will never
be raised where a reference is checked.

The alternative of reading every type and refusing only when an attributed one
is lost was rejected on determinism, which matters more here than coverage. A
catalogue is a generated file committed to a repository and regenerated on a
schedule; its content must depend on the upstream assembly and nothing else.
Under that hybrid, whether a rule appears would depend on whether an unrelated
helper happened to resolve on the machine doing the generating — so the same
upstream release could produce different catalogues on a maintainer's laptop and
on the nightly runner, and the difference would read as a vendor adding or
retiring rules. Metadata cannot fail that way: it is the same bytes everywhere.

The cost is accepted rather than dismissed. `IDE0079` is a real, documented rule
that a consumer may want to suppress in an editor, and it leaves the reachable
set. It is not currently published by any catalogue this repository ships, so
nothing in circulation changes; what changes is what a future catalogue of the
IDE rules would contain. The judgement is that a catalogue which describes only
what a build can report is worth more than one which is complete about rules
whose references it cannot make meaningful — and that a rule whose analyzer no
host loads is better absent than present and unenforceable.

## Alternatives Considered

### Keep enumerating types, and refuse only when an attributed analyzer is lost

Considered because it is strictly more inclusive: it fixes the spurious refusals
while keeping every descriptor the previous behaviour produced, including
`IDE0079`, and it requires reading the attribute anyway.

Rejected because it makes a catalogue's content depend on the environment that
generated it. A helper type resolves or does not depending on which assemblies
happen to be reachable, and the rules that ride on it would appear and disappear
with it — reported downstream as the vendor having changed its rule set. A
generated artifact that is not reproducible from its input is a worse defect
than a missing rule, because nothing anywhere would flag it.

### Supply whatever the failing types need, case by case

Considered because it worked once already: deploying `Microsoft.Bcl.AsyncInterfaces`
beside the reader unblocked three packages, and the same move might unblock more.

Rejected because it does not terminate and does not generalise. The two causes
measured here are not missing dependencies at all — an internal service that
throws on construction, and types compiled against an older Roslyn whose
interface has since gained a member — and no dependency the reader carries can
answer either. It also concedes the premise, which is that the reader should be
loading those types in the first place.

### Filter the attribute by the language it declares

Considered because `[DiagnosticAnalyzer]` names the languages an analyzer serves,
and catalogues are generated for C# only (ADR-0020).

Rejected because the language is already decided upstream of this, by which
assemblies the acquisition selects out of a package, and a second filter could
only subtract. Packages that ship one assembly for both languages are common, and
an analyzer whose declared languages did not match the expected spelling would
lose its rules silently — the failure this whole area exists to prevent.

## Consequences

### Positive

* The four packages refused for types that declare no rule are read completely:
  `Roslynator.Analyzers` (242 rules), `Microsoft.CodeAnalysis.Analyzers` (52),
  `Roslynator.Formatting.Analyzers` (55) and
  `Microsoft.CodeAnalysis.BannedApiAnalyzers` (3).
* The four catalogues this repository ships regenerate byte for byte unchanged.
* A shortfall is now a named analyzer rather than an anonymous count, so a
  refusal says which rule is at risk.
* Assemblies declaring no analyzer — most of what an analyzer package contains —
  are no longer loaded at all.

### Negative

* `IDE0079` and `SecurityCodeScan.VS2019`'s `Debug` entry leave the reachable
  set. Neither is published by a catalogue this repository ships today.
* An analyzer that declares no attribute is no longer catalogued even where it
  loads cleanly, which is a behaviour change for any such package not measured
  here.

### Risks

* The attribute is matched on its simple name, so an unrelated attribute of the
  same name would select a type. The base type is checked after loading, so the
  cost is a type passed over rather than a rule invented.
* A vendor could in principle rely on a host other than the compiler to run an
  unattributed analyzer, as Roslyn itself does for `IDE0079`. Rules reached that
  way are outside what a catalogue can promise.

## Follow-up Actions

* Decide whether a catalogue of the IDE rules is worth publishing, knowing that
  `IDE0079` would be absent from it.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.en.md) — content comes from descriptors
* [ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.en.md) — why an absent rule is dangerous
* [ADR-0020](0020-a-catalogue-is-generated-for-c-sharp-only.en.md) — language selection
* [ADR-0024](0024-fail-on-any-diagnostic-the-ratchet-cannot-see.en.md) — refusing what cannot be seen
