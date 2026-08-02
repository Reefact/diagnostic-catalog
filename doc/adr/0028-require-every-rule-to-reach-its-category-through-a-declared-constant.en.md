# ADR-0028 | Require every rule to reach its category through a declared constant

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0028-require-every-rule-to-reach-its-category-through-a-declared-constant.fr.md)

**Status:** Accepted
**Proposed:** 2026-08-02
**Accepted:** 2026-08-02
**Decision Makers:** Reefact

## Context

A rule satisfied four requirements: the marker, a static non-generic class, a public
`const string Id`, and a public `const string Category`. Nothing said where the
category's *value* came from. `Category = "Usage"` and
`Category = ContosoCategory.Usage` both satisfied the contract, and the documentation
said so explicitly in five places — the attribute's own summary, the rule-contract
guide, the authoring guide, the glossary and the specification all called
`[DiagnosticCategory]` optional.

The two forms are indistinguishable downstream. A `const` initialised from another
`const` is a compile-time constant, so both fold to the same literal in metadata, both
are valid attribute arguments, and both suppress exactly the same diagnostic. Nothing
in the platform, in the emitted assembly, or in a reflecting consumer can tell them
apart. The only component that can is an analyzer reading the initialiser.

Three facts bear on whether that difference is worth reporting.

A catalogue repeats very few distinct categories across very many rules: the Sonar
catalogue spends 456 rule declarations on 13 values, StyleCop 193 on 8. Every
transcription is a place for one of them to drift, and a drifted category has no
symptom — Roslyn matches a suppression on its identifier alone (§3.2), so a
misspelled category changes nothing a build, a test or a tool can observe.

The four catalogues this repository generates already declare their categories once,
in a marked container. So does every example in the authoring guide from its second
section onward. What the contract permitted and what this repository practised had
already diverged; the minimal example in the root README was the visible edge of that
gap, and it is where the question was raised.

The marker is what makes the container legible to tooling. Without it an analyzer
cannot tell a category constant from any other string constant in an assembly, so a
fix replacing a literal category has nothing to offer in its place. That capability
exists per-catalogue today: it works for a catalogue that opted in and silently does
nothing for one that did not.

`DiagnosticCatalog.Analyzers` has no version on nuget.org. Its
`AnalyzerReleases.Shipped.md` is empty, so no consumer's build currently sees any
`DCAT` diagnostic at all.

## Decision

**A rule's `Category` must resolve to a `const string` declared in a class marked
`[DiagnosticCategory]`**, which becomes the fifth requirement of the structural
contract and is reported as `DCAT0011` at `Warning`.

## Rationale

The requirement does not make a wrong category right, and it should not be defended as
if it did. A category constant is declared by the same hand, in the same assembly, at
the same moment as the rule that names it; there is no independent referent for the
reference to disagree with, so the indirection relocates the single point of truth
rather than creating a second one to check it against. Being consistent about a wrong
value is not the same as being right.

What it buys is uniformity, and uniformity is the thing being bought deliberately.
Every catalogue then has one shape: a reader moving between them sees the same form,
and tooling can rely on the container existing rather than hoping for it. That last
point is the concrete gain — the fix that offers a named constant in place of a
literal category stops being a capability that some catalogues happen to support and
becomes one that always works. A capability available only where an author opted in is
one no consumer can count on.

It also closes the gap between what the contract permitted and what this repository
already did. Generated code that does not look like the code the documentation tells
you to write is a standing invitation to wonder which one is right, and the answer had
been "both", which is the least useful answer available.

The cost is one class per catalogue, in a file that already holds hundreds of lines of
rules. The catalogue small enough for that class to be a real burden — a single rule,
a single category — is not a catalogue anyone publishes. Weighed against a requirement
that applies for the catalogue's whole life, a one-time class is not a serious price.

`Warning` rather than `Error` follows [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md)
rather than departing from it: the audience is whoever authors a catalogue, not whoever
consumes one, and every definition diagnostic addresses that audience. There is also
nothing here that fails. The rule compiles, folds correctly and suppresses what it
should; what is wrong is a property of the catalogue, not a defect in the declaration.
Reporting that as an error would claim a severity the facts do not support.

Doing it now rather than later is what makes it cheap. The analyzer package has never
shipped, so the requirement reaches its first consumer as part of the contract rather
than as a change to it. The same requirement added after publication would turn every
existing catalogue's build noisy at once, for a property none of them had been asked
to have.

## Alternatives Considered

### Leave the marker optional and recommend it in the guides

The status quo, and the cheapest option: the guides already recommend the container
from their second section onward, and the generator already emits it.

Rejected because a recommendation is exactly what fails to deliver uniformity. It
leaves the shape of a catalogue to whether its author read the right page, and it
leaves the literal-replacement fix unable to rely on a container being there. The gap
between the recommendation and the contract is also what produced the question this
ADR answers.

### Report the divergence instead of the shape

Rather than requiring a form, report two rules in one assembly whose categories differ
only by case, spacing or a near miss — the actual defect that declaring each category
once prevents.

Rejected as the decision, though not as an idea: it catches drift wherever it comes
from, needs no contract change, and fires only where factorisation would have helped.
But it delivers nothing for uniformity, which is what is being bought here, and it is
silent on a catalogue whose literals all happen to agree today. It remains worth having
later, alongside this requirement rather than instead of it.

### Ship it as an error

Considered and initially chosen, on the grounds that a guarantee left to attention is
not a guarantee.

Rejected because nothing in a rule that fails this is broken: it compiles, folds to the
right literal and suppresses the right diagnostic. An error asserts a severity the
facts do not carry, and it would contradict ADR-0027's split between the consumer's
build and the author's without a reason that split does not already cover. Severity
stays configurable per project in `.editorconfig`, so a catalogue author who wants the
requirement enforced hard can raise it in one line.

### Offer a code fix that extracts the literal

Deferred rather than rejected. The repair is a class that may not exist yet, holding a
constant nobody has named; a fix inventing both would be guessing at the catalogue's
vocabulary — the naming rules a generated container follows are mechanical, but a
hand-written one's are not. Worth revisiting once the shape is established.

## Consequences

### Positive

* Every catalogue has the same shape, whoever wrote it and whether or not it was
  generated.
* Tooling may assume a marked container exists, so a fix replacing a literal category
  always has a constant to offer.
* The documentation loses an optional axis: one fewer decision for a catalogue author,
  and five pages that no longer have to explain a choice.
* Generated catalogues and the shape the guides teach are now the same shape.

### Negative

* Every hand-written catalogue must declare a container, including the smallest.
* A new definition diagnostic to document, translate and keep in step.
* `DCAT0011` arrives with no code fix, where the three definition diagnostics beside it
  all carry one.

### Risks

* The requirement buys uniformity, not correctness, and the two are easy to confuse. A
  reader who takes `DCAT0011` as protection against a wrong category has been misled by
  it; the pages that describe it say plainly that the value itself is checked by
  nothing. If that framing erodes, the requirement starts being cited for a guarantee
  it does not provide.
* It is the first requirement of §8 that cannot be evaluated over a metadata symbol,
  because it reads an initialiser. `DCAT0010` will therefore cover four of the five
  requirements across an assembly boundary rather than all of them, and the asymmetry
  has to be remembered when that diagnostic is written.

## Follow-up Actions

* Consider the intra-catalogue divergence check described above, which catches drift
  this requirement does not.
* Revisit a code fix for `DCAT0011` once the container is a shape authors expect.

## References

* [ADR-0008](0008-express-a-rule-as-a-marked-static-class-of-constants.en.md) — the
  structural contract this adds a requirement to.
* [ADR-0026](0026-reach-a-category-only-through-the-rule-that-carries-it.en.md) — why a
  generated container is `internal`, and why a consumer never names a category alone.
* [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md) — the severity split
  this follows.
* [The rule contract](../guide/rule-contract.en.md) and
  [the specification](../specification.en.md), §7.7 and §8.5.
