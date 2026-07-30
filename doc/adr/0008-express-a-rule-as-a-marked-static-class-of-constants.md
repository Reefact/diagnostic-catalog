# ADR-0008 | Express a rule as a marked static class of constants, never an interface

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

The library exists to replace the magic strings passed to
`SuppressMessageAttribute` with references a compiler can check. That attribute
exposes a single constructor taking a category and a `checkId`, both positional
and required, so a rule reference is only ever useful in one position: as an
attribute argument.

C# requires every attribute argument to be determinable at compile time. A
`const` qualifies. A property, a `static readonly` field, a `record` and a static
instance do not, and a constant cannot be virtual or overridden. No interface and
no abstract base class can therefore supply the values a use site needs: an
implementation could satisfy such a contract and still be unusable at the only
place its values are read. A static class, which is what a pure container of
constants wants to be, cannot participate in inheritance at all.

A static class exposing two string constants is an ordinary shape that ordinary
code arrives at for unrelated reasons. Nothing in the shape alone states that its
author meant to declare a diagnostic rule.

Catalogs are shipped as NuGet packages, and tooling discovers the rules a
referenced package declares by reading them out of that assembly's compiled
metadata, not from source. An attribute marked `[Conditional]` is not emitted
into metadata unless the symbol is defined at the *declaring* assembly's compile
time; `SuppressMessageAttribute` is itself `[Conditional("CODE_ANALYSIS")]`,
which is why the BCL had to add a second, non-conditional attribute for consumers
that read suppressions from metadata.

One attribute's arguments cannot be referenced from another attribute.

At the time of this decision the marker attribute ships and the analyzer that
validates declarations against it does not.

## Decision

A diagnostic rule is expressed as a static, non-generic class exposing its
identifier and its category as public string constants, marked by a dedicated
attribute, and that structural contract is verified by an analyzer rather than by
the type system.

## Rationale

The object-oriented expression of this contract is not rejected on taste; the
language forecloses it. Whatever an interface or a base class imposed would be
imposed on members that cannot appear where the values must appear, so the
contract would be satisfied by types that do not work. A contract that can be
honoured everywhere except at the point of use is worse than no contract: it
would be checked by the compiler and still let the failure through.

Once the type system is out of the running, the question is not whether to accept
an unchecked contract but which checker to use. An analyzer is the natural
answer, and not merely an available one: it can assert exactly the properties
that matter — that the type is static and non-generic, that it carries one `Id`
and one `Category`, that neither is empty — and it reports them where the
declaration is written, which is what a type system would have done. It is also
the same class of tool the library is built to serve, so nothing new is asked of
a consumer's build.

The marker attribute earns its place because it is the only thing that
distinguishes a rule from any other static class of constants. Without a declared
signal, tooling would have to infer intent from shape and would report on types
whose author never opted in; the point of the catalog is that a rule reference is
a deliberate contract, and a contract nobody opted into is a guess. Declaring the
intent costs one line where the rule is written and nothing at all where it is
used, since the reference folds to a literal and the marker plays no part in the
consumer's assembly.

The marker deliberately carries no arguments. Putting the identifier and the
category on it would not remove the constants, because an attribute's arguments
cannot be referenced from another attribute and the use site is an attribute;
they would simply be stated twice, in two places nothing keeps in step. A marker
that says only *this is a rule* has no second copy to drift from.

The trade-off accepted is that a malformed rule still compiles. That is the
honest position: there is no version of this contract the compiler could enforce,
so the analyzer is not a convenience layered on top of a checked contract — it is
the whole check, and treating it as optional would leave the contract stated only
in prose.

## Alternatives Considered

### An interface imposing `Id` and `Category` properties

Considered because it is the ordinary way to say "these members must exist": it
is checked by the compiler, discoverable in an IDE, and gives tooling a type to
match on.

Rejected because a property cannot be an attribute argument. A type could
implement the interface, satisfy every compiler check, and still be unusable in
the one position a rule reference is written. Formalising a contract that cannot
be honoured where it matters would mislead precisely the authors it was meant to
guide.

### An abstract base class, or one instance — a `record` or a singleton — per rule

Considered because a base type could carry shared behaviour and give tooling
something to reflect over, and because an instance model reads more naturally
than a class used as a namespace.

Rejected for the same reason, compounded: abstract properties are no more
constant than ordinary ones, an instance is not a compile-time value either, and
a static class cannot inherit at all. The model would have to abandon static
classes to gain a base type, and would gain nothing usable at a use site in
exchange.

### Put the identifier and the category on the marker attribute

Considered because it would make the declaration self-describing and give tooling
a single place to read a rule's data, without any structural expectation about
the type's members.

Rejected because it removes nothing. The constants must still exist for the use
site, since one attribute's arguments cannot be referenced from another, so the
attribute would be a second statement of the same two values with no mechanism
keeping them equal — the duplication this library exists to eliminate,
reintroduced in the declaration.

### Recognise rules purely by shape, with no marker

Considered because the constants really are the contract, and shape-only matching
would let a catalog declare rules with no attribute and no package dependency at
all.

Rejected as the default because the shape is not distinctive: static classes with
a string constant named `Id` occur for unrelated reasons, and matching on shape
alone would make tooling report on code whose author never opted in. It remains
useful as a documented fallback for authors who want it, but the explicit marker
is what turns a shape into a declaration.

## Consequences

### Positive

* A rule reference works in the only position it has to work in, and folds to the
  literal the platform expects.
* Tooling can tell a rule from any other static class, at the declaration and
  across an assembly boundary.
* The declaration states one fact once: the values live in the constants, and the
  attribute says what they are.

### Negative

* The contract is invisible to the compiler, so the analyzer is not optional —
  a catalog authored without it is unverified, and today no such analyzer ships.
* The expected shape must be documented, because no type signature carries it and
  no IDE can offer to generate it.
* A malformed rule is a diagnostic rather than a build error unless the consuming
  build promotes it.

### Risks

* The marker is made `[Conditional]` — a plausible-looking economy, since it
  serves no run-time purpose. Every catalog shipped as a package would then go
  silently invisible: cross-assembly discovery reads the marker out of referenced
  metadata, a conditional attribute is not there, and the result is no rules
  found, no diagnostics reported and no error anywhere. Mitigation: the
  prohibition is recorded in the specification and as a maintainer constraint
  beside the attribute's declaration, where whoever would make the change reads
  it.
* An author declares rules without referencing the analyzer and ships a catalog
  nothing ever checked. Mitigation: the packaging documentation states which
  package performs the checking; the risk cannot be closed from this repository.

## Follow-up Actions

* Ship the analyzer that validates the structural contract; until it exists the
  contract rests on documentation alone.
* Document the expected shape in the consumer-facing documentation of the
  foundation package, since no signature expresses it.
* Keep the never-`[Conditional]` constraint stated next to the marker's
  declaration and in the specification.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.md) — what
  fills a catalog built on this contract.
* [doc/specification.en.md](../specification.en.md) — §3.1, §3.4, §7.1, §8.
* `src/DiagnosticCatalog/DiagnosticRuleAttribute.cs` — the marker and its
  maintainer constraint.
