# ADR-0014 | Ship the vendor's rule title as a catalogue's documentation

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

Supersedes [ADR-0011](0011-redistribute-rule-facts-only-never-the-vendors-prose.md).

## Context

[ADR-0011](0011-redistribute-rule-facts-only-never-the-vendors-prose.md) decided
that a generated catalogue ships identifiers, categories and help links, and none
of the vendor's rule titles, message formats or descriptions. It considered
shipping titles alone and rejected that on the ground that brevity is not the
distinction that matters: a title is a sentence the vendor wrote, and several
hundred of them is still their catalog.

Under that decision a rule documents itself by restating its own identifier —
`Rule S1144, category Major Code Smell` — on the constant a consumer is hovering
precisely because they already have the identifier in front of them. Nothing in
that sentence tells them what the rule is about.

A `DiagnosticDescriptor` carries a title, a message format and a description, and
the three are not the same kind of text. Measured across the three mirrored
packages as they currently ship:

* A **title** is one complete sentence naming the rule's subject: 54 characters on
  average for `SonarAnalyzer.CSharp`, 153 at most. None of the 967 titles across
  the three packages contains a line break, and none is truncated.
* A **description** is a paragraph of rationale: 215 characters on average, 677 at
  most, and for 35 of the 456 Sonar rules it is visibly an excerpt, ending on a
  colon that introduces a list the package does not carry. The whole prose corpus
  is 100 KB against 25 KB for the titles.
* A **message format** is a template, not a sentence: 203 of the 456 Sonar rules
  carry placeholders filled at analysis time, and 37 carry nothing but `{0}`,
  their text being assembled inside the analyzer, sometimes as several distinct
  sentences for one rule.

`SonarAnalyzer.CSharp` compiles the same titles and descriptions into an internal
rule catalogue inside its own assembly, byte for byte identical to what its
descriptors declare on all 456 rules. The assembly carries no other rule text: no
embedded resource, no literal over 700 characters, no markup. A consumer who
suppresses a Sonar rule already has that assembly on disk, because it is the
analyzer raising the diagnostic they are suppressing.

`SonarAnalyzer.CSharp` populates `HelpLinkUri` on none of its 456 published rules,
so its catalogue has no link to send a reader to. The .NET analyzer and StyleCop
catalogues populate it on every rule.

Roslyn's quick info renders `<summary>` and, by a default-on option,
`<remarks>`; a documentation comment is also what a completion list shows while a
consumer types a rule's name.

## Decision

A generated third-party catalogue ships the rule title its upstream descriptor
declares, as that rule's documentation comment, and still never ships the vendor's
rule descriptions or message formats.

## Rationale

The line ADR-0011 drew — a fact about the software on one side, text authored
about it on the other — is the right line, and it is kept. What moves is where the
title sits relative to it. A title names what the analyzer reports on; it is the
label by which the vendor's own tooling, the IDE's error list and every consumer
already identify the rule. A description is different in kind, not merely in
length: it argues why the rule exists, it is the substance of the vendor's
documentation, and it is the part that carries their reasoning rather than their
identification. Shipping the first and not the second is a line that can be
applied mechanically, because the descriptor separates the two fields already.

ADR-0011 was right that length alone could not carry the distinction; the
measurements above show that length is not what is being relied on. A title is
complete, singular and never truncated. A description is an excerpt of something
larger, sometimes visibly cut mid-sentence — which makes it, unlike a title, a
thing this repository could only ever ship a damaged copy of. A message format is
not even a sentence: it is a template whose text does not exist until an analysis
run produces it, and for the rules that carry only a placeholder there is no
single value to ship at all. The three fields fail the redistribution question
differently, and only one of them passes it.

The honesty argument that ran alongside the licensing one in ADR-0011 also
resolves differently for a title. That argument was that an unaffiliated mirror
carrying the vendor's explanatory text would read as the vendor's documentation
and would age against their pages with nothing to say so. A title is not
explanatory text: it identifies rather than explains, so it cannot be mistaken for
the rule's documentation, and it ages exactly as the identifier does — when
upstream renames a rule, regeneration carries the new name in the same diff that
carries everything else. Sending a reader to the vendor's page remains the
answer for the explanation, and a catalogue's documentation is a poor place to
reproduce one.

The cost of the previous decision fell hardest on the catalogue with no help
links. A Sonar rule constant could say nothing whatsoever about itself: no title,
and no page to point at. The consequence ADR-0011 accepted as "tooltips say less
than they could" was, in that catalogue, tooltips saying nothing at all.

The scale argument survives too, in the sense that matters. Twenty-five kilobytes
of titles are not a restatement of SonarSource's rule catalog: their catalog is the
rules, their rationale and their examples, and what remains without the prose is a
list of names for things the analyzer reports — the same list this repository is
already entitled to publish as identifiers, given a sentence each rather than a
number.

## Alternatives Considered

### Keep ADR-0011 as it stands

Considered because it is the decision on record, it needs no licensing question
reopened, and it keeps the catalogues to the smallest defensible content.

Rejected because it leaves a rule's documentation restating the identifier the
reader already has, and in the Sonar catalogue it leaves them with nothing else
either — no title, no link. The distinction ADR-0011 wanted to avoid drawing on
brevity can be drawn on kind instead, which is what this decision does.

### Ship the message format instead of the title

Considered because it is the sentence a consumer actually reads in the error list,
it is imperative where a title is declarative — `Make this field 'private' and
encapsulate it in a 'public' property.` against `Fields should not have public
accessibility` — and it tells them what to do rather than what is wrong.

Rejected because it is not one value per rule. Of the 456 Sonar rules, 203 carry
placeholders that only an analysis run fills, and 37 carry nothing but a
placeholder, their sentences built inside the analyzer and several per rule in
some cases. Publishing one of those would mean choosing a sentence no descriptor
declares, which is exactly the invention [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.md)
forbids. A rule whose documentation reads `Remove the unused {0} {1} '{2}'.` is
worse than one that reads nothing.

### Ship the title and the description

Considered because the description is the part that answers "why", it is already
in hand, and a consumer who wants it currently has to leave their editor for it.

Rejected because a description is the vendor's documentation in the sense
ADR-0011 identified, at four times the volume, and because 35 of the Sonar ones
are truncated in the package itself — this repository cannot ship a complete copy
even if it decided to ship one, only a damaged one that reads as the vendor's.

### Generate the documentation on the consumer's machine instead of shipping it

Considered because the consumer already has the vendor's assembly on disk, so a
build step could produce the documentation locally from it, redistributing
nothing and matching the exact version they compile against.

Rejected for this decision because it answers a different question at a much
higher cost: it puts assembly loading into every consumer's build, depends on IDE
documentation caching behaviour this repository does not control, and delivers
nothing to a consumer reading the catalogue on a machine where the vendor's
package is not installed. It remains the reasonable route if descriptions are ever
wanted, and it is not foreclosed by this decision.

## Consequences

### Positive

* Hovering a rule constant says what the rule is about, in every catalogue —
  including the Sonar one, whose descriptors supply no help link and which
  therefore had nothing else to offer.
* The rule of what a catalogue ships stays mechanical and per-field, so it can be
  applied by the generator rather than judged rule by rule.
* A rule renamed upstream now shows the rename in the regeneration diff as a
  changed sentence, not only as a changed identifier.

### Negative

* The packages carry roughly 25 KB of the vendors' authored titles, which
  ADR-0011 declined to carry at all. The licensing question is answered by the
  fact/authored-text line rather than closed by carrying nothing.
* A title reworded upstream now moves the generated file, so a release that
  changes no rule can still produce a diff to review.
* The distinction between a title and a description has to be stated per catalogue
  and cannot be checked by the build, exactly as ADR-0011's own line could not.

### Risks

* A maintainer extends the same reasoning to descriptions, one field at a time,
  on the ground that the boundary already moved once. Mitigation: the boundary
  moved on a stated difference in kind, recorded here with the measurements that
  support it; a further move needs its own ADR arguing its own difference.
* A vendor objects to their titles being carried. Mitigation: each catalogue
  states that it is unofficial and unaffiliated, names the upstream release it
  mirrors, and points at the vendor's own documentation; and the position is
  revisitable per vendor without changing the generator.

## Follow-up Actions

* Restate in each catalogue's consumer documentation what the package now contains
  and where the vendor's own rule descriptions live.
* Keep the per-field restriction recorded with the generator, where whoever
  changes generation next will read it.
* Revisit if a vendor publishes explicit terms for redistributing its rule
  metadata.

## References

* [ADR-0011](0011-redistribute-rule-facts-only-never-the-vendors-prose.md) — the
  decision this one supersedes.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.md) — why a
  value that was never read must not be invented.
* [doc/specification.en.md](../specification.en.md) — §7.5 and §14.1.
* `eng/CatalogGen` — the generator.
