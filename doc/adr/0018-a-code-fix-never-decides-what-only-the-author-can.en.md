# ADR-0018 | A code fix never decides what only the author can decide

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0018-a-code-fix-never-decides-what-only-the-author-can.fr.md)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

The analyzers report four diagnostics at a suppression site, three of which ship
a code fix. Each of the three has met a case where the repair is not uniquely
determined by the code:

* `DCAT0006` reports string literals that a catalogue rule would replace. When
  two catalogues describe the same vendor rule, both match the same
  `(Category, Id)` pair and either could be the one meant.
* `DCAT0007` reports a half-migrated suppression — one reference, one literal.
  When the literal names something the referenced rule does not declare, the
  suppression silences one diagnostic today and would silence another once
  completed.
* `DCAT0001` reports a category and an identifier taken from two different
  rules. Either argument could be the typo.

The specification decides each of these separately: §11.6 gives the ambiguous
match a diagnostic without a single automatic fix, §11.7 describes the
deterministic completion, and §12.1 requires two fixes for an incoherent pair and
states that the fix must never guess which rule was intended. It states no
general rule covering the three, and later diagnostics — `DCAT0008`, `DCAT0010`,
and the definition fixes of §12.4 — are specified without one.

Two facts about the platform bear on how such a fix behaves in practice. Roslyn
matches a suppression on its identifier alone and never consults the category, so
correcting the category of an incoherent pair leaves what is suppressed unchanged
while correcting the identifier changes it. And every fix here is offered through
*Fix all occurrences*, which applies one choice across a document, project or
solution without the author seeing each site.

A first attempt to state the shared principle — that a fix never changes what a
suppression suppresses — was written and found false while implementing §12.1:
one of the two corrections that section mandates changes exactly that.

The reasoning behind each individual refusal currently lives in the comment next
to it. Nothing states that the three are one position, and the pressure to add a
default runs the other way: a fix that offers nothing, or offers two options and
recommends neither, reads as an unfinished feature rather than a decision.

## Decision

A code fix in this repository performs only repairs that are uniquely determined
by the code it reads, and offers no automatic fix — or offers every candidate
without ranking them — wherever more than one repair is defensible.

## Rationale

The three cases in the Context are the same situation wearing different clothes:
the code admits more than one repair, and the information that would settle it is
the author's intent, which is not in the code. A fix that picks one is not being
helpful; it is guessing, and it is guessing silently, because an applied fix
leaves no trace of the alternative it discarded.

*Fix all occurrences* is what makes that guess expensive rather than merely
wrong. A single mistaken suggestion is caught when the author reads the result;
the same suggestion applied across a solution rewrites hundreds of sites that
nobody reads, and the sites it damages are indistinguishable afterwards from the
ones it repaired. The mechanism the library offers for migrating a codebase in
one gesture is precisely the mechanism that makes an unfounded choice
unreviewable.

Silence has a cost and it is the smaller one. A diagnostic reported without a fix
still names the problem and its location, and the author repairs it with the
knowledge the tool lacked. The reverse — a confident fix built on a guess — costs
the thing the library exists to provide, since a suppression that silences the
wrong diagnostic is exactly the invisible failure the whole design is aimed at.

The `DCAT0001` case shows why the rule must be about *deciding* rather than about
consequences, and why the narrower formulation was the wrong one. Because Roslyn
ignores the category, one of the two mandated corrections is harmless and the
other is not, and it is tempting to offer the harmless one alone. That would
still be a choice made on the author's behalf, and it would be wrong every time
the identifier was the argument written correctly. Ranking is a weaker form of
the same error: an option presented first is the one accepted without reading.

Recording the position rather than leaving it in three comments is what makes it
survive the pressure described in the Context. Each refusal, read alone, looks
like a gap someone could helpfully close; read together, they are a policy, and a
future diagnostic inherits it instead of re-deciding it case by case. The
specification cannot serve that purpose here because it decides the three
instances without stating what they share.

## Alternatives Considered

### Leave the three decisions where the specification put them

Considered because the specification already decides all three cases, and an ADR
restating decisions taken elsewhere adds a second place for them to drift from.
The check that CLAUDE.md mandates is the habit, not the artefact, and most
changes rightly produce none.

Rejected because the specification decides the instances and never states the
rule, so it gives no guidance for the diagnostics still unwritten. The evidence
that the shared rule is not self-evident is that stating it correctly took two
attempts, the first found false only when the implementation contradicted it.

### Offer a preferred fix and mark the others as alternatives

Considered because it is what most analyzer packages do, and because an author
facing two options with no recommendation may reasonably ask which one the tool
thinks is right.

Rejected because a preference is a choice, and the information that would justify
it is absent from the code by definition of these cases. Under *Fix all
occurrences* the preferred option is the one applied everywhere, so the ranking
does not soften the guess — it scales it.

### Decide each future case on its own merits

Considered because the three cases differ in detail, and a general rule risks
forbidding a repair that is genuinely safe in some case not yet met.

Rejected because deciding case by case is what produced the three unconnected
comments, and because the rule as stated does not forbid a safe repair: a
uniquely determined one remains fully automatic, which is what the deterministic
`DCAT0007` completion and the ordinary `DCAT0006` replacement already are.

## Consequences

### Positive

* A migration applied across a solution changes only what the code determines,
  so the result is reviewable as a mechanical transformation rather than as a
  set of suggestions.
* A diagnostic without a fix still reports, so no case is hidden by the absence
  of a repair.
* The diagnostics still to be written inherit a stated position instead of
  re-deriving one, and a reviewer has something to hold a new fix against.

### Negative

* Some reported cases have no automatic repair at all, which reads as an
  incomplete feature to anyone who does not know why.
* An author facing two unranked corrections must understand the difference
  between them before choosing, which the diagnostic message and the package
  documentation have to carry.

### Risks

* The rule can be honoured in letter and broken in spirit by declaring a case
  "uniquely determined" on a thin argument. The mitigation is that each such
  claim is a testable assertion about the code, and the test for a refused fix
  asserts that the diagnostic was still reported — so a fix that quietly starts
  offering a repair cannot pass as a fix that never had one.
* A future Roslyn that matched suppressions on the category as well as the
  identifier would change which repairs are consequential, though not which are
  determined. The dependency is pinned by a test rather than left as an
  assumption.

## Follow-up Actions

* Apply the position to the `DCAT0008` and `DCAT0010` fixes when those
  diagnostics are written, and to the §12.4 definition fixes if they are built.
* Keep the package documentation stating, for each case that offers no fix or
  offers an unranked choice, what the author needs in order to decide.

## References

* Specification §11.6, §11.7, §12.1 — the three decisions this generalises.
* ADR-0010 — a related refusal to let tooling silently remove something a
  consumer relies on.
* Pull requests #35, #38, #40 and #42 — where the three cases were implemented.
