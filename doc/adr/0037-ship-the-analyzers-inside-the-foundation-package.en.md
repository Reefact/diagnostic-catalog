# ADR-0037 | Ship the analyzers inside the foundation package

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0037-ship-the-analyzers-inside-the-foundation-package.fr.md)

**Status:** Proposed
**Proposed:** 2026-08-06
**Decision Makers:** Reefact

## Context

The `lib` release train carries three projects: `DiagnosticCatalog`, which holds the marker
attributes; `DiagnosticCatalog.Analyzers`, which holds the `DCAT` diagnostics and, packed inside
it, the code fixes; and `DiagnosticCatalog.Self`, the `DCAT` rules expressed as a catalogue. A
train is tagged once and packs every project declaring it
([ADR-0002](0002-partition-releases-into-trains-by-commit-scope.en.md)), so the three always carry
the same version and can never be released apart.

That train has been tagged once, as `lib-v0.1.0`. At that commit `src/` held four projects, and
`DiagnosticCatalog.Analyzers` was not among them — it was written later. `DiagnosticCatalog` 0.1.0
is therefore on nuget.org and the analyzer package has never been published at all.

§16.1 of the [specification](../specification.en.md) splits the two packages by audience: a
consumer who writes suppressions wants the analyzers and no runtime dependency, while a catalogue
author needs the attribute to reach their own consumers. It notes that a convenience metapackage
may depend on both.

Every catalogue package depends on `DiagnosticCatalog`, and must not hide it: the attribute has to
reach whoever consumes the catalogue, both for reflection over the rule types and so they can
declare rules of their own. That dependency is mandatory, already declared, and already open.

No catalogue references `DiagnosticCatalog.Analyzers`. A project referencing a catalogue receives
the rule constants and the attribute assembly, and no `DCAT` diagnostic of any kind — so nothing
reports the suppressions still written as string literals, which is the migration `DCAT0006` exists
to drive. The project README states this and names the missing publication as the reason.

§16.3 measured transitive analyzer flow against real packages, and
`tools/packaging/verify-consumption.sh` re-measures it on every pull request: a catalogue that
references the analyzer without hiding it does deliver it to its own consumers, which is the
opposite of what NuGet documents. The measurement covers one hop — a project referencing a
catalogue. It does not cover a project referencing a library that references a catalogue.

A packed catalogue's dependency on `DiagnosticCatalog` carries NuGet's default private-asset list,
which names analyzers among the excluded assets. The flow §16.3 measured therefore holds despite
that list rather than because of it.

`DCAT0006` ships as an error by default
([ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md)), on the stated ground that
referencing a catalogue package is itself the statement of intent.

The definition diagnostics report only on types marked with the rule attribute; the analyzer
returns immediately on anything else. A project that consumes a catalogue and declares no rules of
its own is therefore reported on by the use-site diagnostics alone, whatever package delivered
them.

`DiagnosticCatalog.CodeFixes` declares no release train and is packed inside the analyzer package —
the one project shape [ADR-0007](0007-depend-across-trains-through-published-packages.en.md)
blesses for code that is not a package of its own.

## Decision

The analyzers and their code fixes ship inside the `DiagnosticCatalog` package, which carries both
the attribute assembly and the analyzer assemblies, and `DiagnosticCatalog.Analyzers` ceases to be
a package identity of its own.

## Rationale

A package is split from another in order to version independently of it. These two are on the same
train, so there is no independence to buy: every tag that ships one ships the other, at the same
number, forever. What the split actually delivers is a second name to discover and a second
reference to write, and the record of that cost is the state the repository is in today — thirteen
published or buildable catalogues, none of which is checked by anything.

Folding the analyzers into the foundation makes *using a catalogue means being checked* a property
of the dependency graph that already exists, rather than of thirteen references somebody has to
add and keep in step. That distinction is the whole argument. A guarantee that rests on every
catalogue author remembering a line is a guarantee nothing enumerates and no test can assert;
`ReleaseTrain` membership taught the same lesson, which is why it is declared in the project rather
than in a list somewhere else.

§16.1's two audiences do not survive contact with the packaging. A catalogue consumer already
receives the attribute assembly whether they want it or not, because the catalogue is forbidden
from hiding it — so the audience that wanted analyzers with no runtime dependency does not exist
among catalogue consumers, and among the others it is asking to avoid an assembly of marker
attributes that the zero-footprint tests already hold to leaving no trace in a consumer's build.

ADR-0027 justifies shipping `DCAT0006` as an error on the ground that referencing a catalogue is a
statement of intent. That ground is currently false: referencing a catalogue delivers nothing that
can report, and whoever eats the error is whoever separately went looking for the analyzer package,
which is a stronger intent than the one the record argues from. This decision does not weaken
ADR-0027 — it makes the package it names the package that carries the consequence.

The split some readers will want instead — use-site checks for consumers, definition checks for
catalogue authors — is already achieved, by behaviour rather than by packaging. Nothing in the
definition set fires on a project that declares no rules, so the analyzer assembly is already
use-site-only for exactly the audience that would have asked for the split, and a consumer who does
declare rules is a catalogue author who wants the rest.

The timing is the part that does not wait. `DiagnosticCatalog.Analyzers` has never been published,
so no `.csproj` anywhere names it and folding it costs nobody anything. The next `lib` tag makes it
a public package identity, and withdrawing a published identity is a breaking change for whoever
adopted it first — the readers most likely to have been paying attention.

## Alternatives Considered

### Publish the analyzer package and reference it from every catalogue

The smallest change: §16.1 stands, the packaging stands, and each catalogue gains one reference —
version-less under central package management, so the version itself is a single edit.

Rejected because it buys the property by repetition. Thirteen references today and one more with
every catalogue added, each of which must be written, must decline to hide the analyzer, and must
be remembered by whoever adds the fourteenth. Nothing enumerates package references, so no check
can hold the set true, and the failure mode is silence — a catalogue whose consumers are not
checked looks exactly like one whose consumers are.

### Fold the analyzers into each catalogue package

The most direct reading of the goal: a catalogue becomes self-sufficient, and no consumer needs to
know that a foundation exists at all.

Rejected because the catalogues ride different trains at different paces. A project referencing two
of them would load two copies of the analyzer assembly at two versions, and Roslyn reports from
every analyzer it loads — so the same suppression would be diagnosed twice, by two versions that
may disagree.

### Ship a convenience metapackage depending on both, as §16.1 suggests

Nothing existing changes, no identity is withdrawn, and the reader who wants everything gets one
name to reference.

Rejected because it answers a discovery problem with a third name to discover. The reader who never
found out that the checks live in a second package is precisely the reader who will not find out
that they also live in a third.

### Leave the packaging alone and document it harder

It costs nothing, and the documentation is already in place: the troubleshooting page opens with a
flowchart whose first question is whether the analyzer package is referenced.

Rejected because it accepts silence as the default state, which is the failure this repository
exists to remove. A suppression whose category is wrong compiles, runs and reports nothing; a
codebase whose suppressions are unchecked builds, ships and reports nothing. Answering the second
with a page the reader has to already suspect they need is the same bet that the first one loses.

## Consequences

### Positive

* Referencing any catalogue delivers the checks, with no per-project declaration to write, to
  review, or to remember when the fourteenth catalogue is added.
* The analyzers can never be a release behind the attribute they read, because there is one package
  and one version where there were two of each on one train.
* ADR-0027's justification becomes true of the package it names: the reference that states the
  intent is the reference that delivers the diagnostic.
* No catalogue gains a cross-train dependency it does not already carry, so
  [ADR-0007](0007-depend-across-trains-through-published-packages.en.md) is not even engaged.

### Negative

* §16.1 stops describing the packaging and has to be rewritten, along with the project README's
  status table and the troubleshooting flowchart, which name the analyzer package as the thing to
  reference.
* The foundation stops being publishable as a pure library: it carries analyzer assemblies, and
  their Roslyn dependency becomes a constraint on a package every consumer of every catalogue
  receives.
* A consumer who wants the attributes and not the checks can no longer express that by declining a
  package reference, and has to silence the diagnostics in `.editorconfig` instead.
* The first catalogue release after the change fails builds that were green, everywhere a literal
  suppression matches a catalogued rule, because `DCAT0006` is an error.

### Risks

* The transitive flow this decision depends on contradicts NuGet's own documentation, and the
  default private-asset list names analyzers among the excluded assets. A NuGet release restoring
  the documented behaviour would close the path silently. The mitigation is that the flow is
  re-measured against real packages on every pull request rather than assumed.
* The two-hop path is unmeasured. A library that references a catalogue for its own suppressions
  may impose error-severity diagnostics on its consumers, who chose neither the catalogue nor the
  analyzer, and this decision makes that path live before anything measures it.
* Folding is free only while the identity is unpublished. If a `lib` tag ships first, the same
  decision costs a deprecation and a migration note instead of a rename nobody can observe.

## Follow-up Actions

* Rewrite §16.1 and §16.3 of the [specification](../specification.en.md), which describe the two
  packages and the transitivity levers a single package no longer needs.
* Extend `tools/packaging/verify-consumption.sh` with the two-hop case, and do it before the next
  `lib` tag rather than after.
* Update the project README's status table and
  [`doc/guide/troubleshooting`](../guide/troubleshooting.en.md), which both send a reader to a
  package that would no longer exist.
* State in the release notes of the first catalogue release carrying the change that adopting it
  fails the build on every literal suppression matching a catalogued rule.
* Decide whether `DiagnosticCatalog.Self`, also on the `lib` train and also unpublished, keeps its
  own package identity — the same question, to which the same answer is not obviously right.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.en.md) — why the train, not the
  package, is what versions.
* [ADR-0007](0007-depend-across-trains-through-published-packages.en.md) — the cross-train rule
  this decision avoids engaging, and the project shape it blesses for code that is not its own
  package.
* [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md) — the severity, and the statement
  of intent this record makes true.
* [`doc/specification.en.md`](../specification.en.md) — §16, the packaging it describes and the
  transitivity it measured.
* `tools/packaging/verify-consumption.sh` — what re-measures the flow on every pull request, and
  what has to learn the second hop.
