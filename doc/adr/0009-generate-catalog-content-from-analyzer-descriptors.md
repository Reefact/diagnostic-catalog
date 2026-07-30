# ADR-0009 | Generate catalog content from analyzer descriptors, never from documentation

**Status:** Proposed
**Proposed:** 2026-07-30
**Decision Makers:** Reefact

## Context

The repository ships catalogs that mirror analyzers it does not own — SonarSource's,
Microsoft's, the StyleCop.Analyzers project's. A catalog states, for every rule
that analyzer reports, its identifier and its category.

Three sources could supply that content: the vendor's published rule
documentation, the rule-metadata files shipped inside the vendor's package, and
the `DiagnosticDescriptor` instances the analyzer assemblies themselves declare.
Only the last is what the analyzer actually reports with; the other two are
parallel artifacts maintained beside it.

Roslyn never reads a suppression's category. It matches on `checkId` alone, and
says so in its own source. A category that is wrong therefore produces no error,
no warning, no failed suppression and no failing test — not at build time, not at
test time, not at run time, not ever. There is no later moment at which the
mistake surfaces.

The categories are not guessable. `SonarAnalyzer.CSharp` composes its categories
as `{Severity} {Type}` pairs, so `S1144` is declared as `"Major Code Smell"` —
a string no reading of the rule's documentation yields, since the page discusses
severity and rule type separately and never in that combined form.

A wrong identifier fails differently: the suppression is simply dead, the
diagnostic keeps being reported, and nothing names the cause.

The whole proposition of a catalog is that a consumer does not have to look these
values up.

## Decision

A generated catalog derives its content from the `DiagnosticDescriptor` instances
the upstream analyzer assemblies declare, never from that vendor's published
documentation or from rule-metadata files shipped alongside.

## Rationale

The choice of source is settled by the failure mode rather than by convenience.
Every source other than the descriptors is a transcription, and a transcription
can drift from the code it describes — but here the drift is undetectable. Since
the platform never reads the category, a value that was correct when it was
copied and wrong a release later produces nothing observable at any point in any
consumer's lifecycle. When a mistake has no symptom, the only defensible
requirement is a source that cannot be mistaken, and the descriptors are that
source because they *are* what the analyzer reports with.

Correctness on this axis cannot be recovered downstream by testing. A test
asserts a value against a reference, and the only reference worth asserting
against is the descriptor itself; a test written against documentation is a
second copy of the same transcription, carrying the same drift and lending it
the appearance of verification. Testing can establish that generation is
deterministic and that nothing was dropped without saying so, but no test can
make a transcribed corpus true.

Sonar shows that this is not a marginal risk. A documentation-derived catalog
would not be slightly wrong in a tail of unusual rules; it would be wrong for
that vendor's entire rule set, because the value the analyzer declares is a
composition that the documentation never states in that form. A catalog that is
uniformly wrong on one of the two values it publishes is worse than no catalog:
it is confidently wrong, and nothing in the consumer's build disagrees with it.

Credibility follows the same line. A catalog is worth referencing only if it is
authoritative; if it is a transcription, the consumer's honest position is that
they must check it against the vendor before trusting it — which is exactly the
work the catalog was created to remove. There is no partial version of this: the
value is entirely in the source.

The cost accepted is that generation is harder than reading a file. It must
obtain the vendor's assemblies, construct the analyzers they contain and know
which of them belong to the language being mirrored, and an upstream release that
changes shape breaks generation instead of being absorbed quietly. That cost is
paid once, inside a tool this repository controls, at generation time and never
by a consumer — and a generation that stops rather than guesses is the behaviour
this decision is buying.

## Alternatives Considered

### Transcribe from the vendor's published rule documentation

Considered because it is the source a human consults, it is complete and current,
it explains each rule, and it requires no tooling at all beyond reading.

Rejected because documentation describes a rule; it is not the value the analyzer
declares, and where the two disagree nothing reports it. Sonar's composed
categories are the demonstration: the documentation is not stale there, it simply
never states the string the descriptor carries. A source that can be
simultaneously accurate as prose and wrong as data is not usable as data.

### Read the rule-metadata files the vendor ships in its own package

Considered because they are machine-readable, versioned with the package, and
maintained by the vendor, which makes them far likelier to agree with the
descriptors than a web page — and reading them needs no assembly loading.

Rejected because "far likelier to agree" is exactly the property that does not
help when disagreement is silent. Those files remain a parallel artifact
generated for the vendor's own purposes; their format differs per vendor and can
change under them. Trading certainty for a plausible proxy is only rational when
something downstream would catch the difference, and nothing does.

### Maintain the catalogs by hand, tracking each upstream release

Considered because a maintainer reading a release note understands the change,
can annotate it, and can exercise judgement a generator cannot — and it requires
no generator to build or keep working.

Rejected because it makes accuracy a function of sustained attention across
hundreds of rules per vendor, at a cadence this repository does not control and
does not see in advance, with an error at the end of it that nobody will ever
report. Hand maintenance is a reasonable strategy when mistakes are noticed; here
they are not.

### Publish identifiers only and let consumers supply the category

Considered because it removes the problem entirely — no category shipped, no
category to be wrong — while still delivering the compile-checked identifier that
turns a renamed or retired rule into a build error.

Rejected because the category is the argument for which no other help exists. It
is required by the attribute, it is never validated by anything, and outside one
IDE's built-in suppression fix nothing suggests the right value. Dropping it
would concede the half of the contract that has no other answer, and leave the
consumer writing a magic string next to a symbolic reference.

## Consequences

### Positive

* A catalog's content is what the analyzer reports with, by construction, and
  cannot have drifted from it.
* Regenerating against a new upstream version is a diff of facts, reviewable as
  such, rather than a re-reading exercise.
* A wrong value is a defect in a tool this repository owns — reproducible and
  fixable once — rather than a transcription slip that recurs.

### Negative

* Generation depends on loading and constructing a third party's analyzers, so an
  upstream release that changes shape stops the generator instead of being
  absorbed.
* A catalog can carry only what the descriptors declare; there is no friendlier
  source to enrich it from.
* Knowing which assemblies in a vendor's package belong to the language being
  mirrored is itself a place to be wrong, and getting it wrong yields output that
  looks complete.

### Risks

* The generator reads the wrong subset of a package and produces a catalog that
  is plausibly sized and quietly incomplete. Mitigation: generation reports every
  descriptor it excludes, with the identifier and the reason, so an unexplained
  absence is visible rather than inferred from a count.
* A category or an identifier moves upstream and reaches consumers unreviewed,
  where no downstream check can ever contradict it. Mitigation: regeneration
  opens a pull request carrying the diff and publishes nothing on its own; the
  review has to happen at the only point where the change is visible.
* A future maintainer adds a documentation-derived fallback for a value the
  descriptors do not supply. Mitigation: generation is required to fail rather
  than substitute another source, which is the same reasoning that excludes
  synthesised values in ADR-0011.

## Follow-up Actions

* Keep exhaustive reporting of exclusions a hard requirement of the generator,
  since it is what makes an incomplete catalog visible.
* Keep the human review step on the regeneration pull request; no downstream
  check can replace it.
* Record in each catalog's own metadata the exact upstream version it mirrors, so
  a stale snapshot is at least readable from the artifact.

## References

* [ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.md) — what happens
  when regeneration finds a rule gone.
* [ADR-0011](0011-redistribute-rule-facts-only-never-the-vendors-prose.md) —
  what of a descriptor a catalog may ship.
* [doc/specification.en.md](../specification.en.md) — §3.2, §14, §14.1, and
  Appendix A2 and A9.
* `eng/CatalogGen` — the generator.
