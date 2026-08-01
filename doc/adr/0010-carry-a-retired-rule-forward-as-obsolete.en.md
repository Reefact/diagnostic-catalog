# ADR-0010 | Carry a retired rule forward as obsolete, never delete its constant

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0010-carry-a-retired-rule-forward-as-obsolete.fr.md)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

A generated catalog is produced from the descriptors an upstream analyzer
declares (ADR-0009) and is regenerated as that analyzer releases. A regeneration
that simply wrote what upstream currently declares would drop any rule the vendor
has stopped declaring.

Vendors do retire rules. `Microsoft.CodeAnalysis.NetAnalyzers` declares `CA2109`
and `CA2229` at version 6.0.0 and no longer at 10.0.302.

What a catalog publishes is `const string`. C# inlines a constant's value into
the referencing assembly at the *consumer's* compile time, so a consumer does not
depend on the catalog's constant at run time — their already-built assemblies
carry the folded literal and are unaffected by anything the catalog does later.
They depend on it at every recompilation.

Removing a public constant is therefore source-breaking, and the break arrives at
the consumer's next build, which may be long after they upgraded the package. The
compiler's message names a member that does not exist.

When upstream retires a rule, the consumer's suppression for it has become inert:
the diagnostic is no longer reported, so nothing is being suppressed. The
suppression should be removed. The open question is how its author learns that.

Catalogs version and publish on their own release trains, independently of the
foundation (ADR-0002), so a catalog can take a major version without moving
anything else in the repository.

## Decision

A rule the upstream analyzer has stopped declaring stays in the catalog and is
marked obsolete, naming the upstream version that dropped it, and deleting a rule
constant is a major version of that catalog.

## Rationale

Both options interrupt the consumer; they differ in what the interruption says. A
deleted constant produces a compile error about a member that does not exist,
which contains no trace of what actually happened — not that the rule was retired
upstream, not in which version, not that the correct response is to delete the
suppression rather than to find a replacement. The consumer's only lead is the
name they themselves wrote. An obsolete constant carries all of it, on the exact
line that has to change, and points at the response they would have had to work
out anyway. For the same cost in attention, one of the two explains itself.

That the obsolete form is a warning rather than an error is the right severity,
not a softening. A retirement upstream is housekeeping: the consumer's
suppression is inert, not harmful, and nothing about their build is broken. A
hard failure would be disproportionate to what happened, and worse, it would make
catalog upgrades something to postpone — which is the opposite of what a mirror
of somebody else's rules needs from its consumers.

The rule also follows from what the library claims a rule identifier is. The
whole proposition is that a reference is a contract rather than a string; a
contract that is withdrawn whenever a third party tidies up is not a contract,
and a consumer who chose symbolic references to gain stability would have bought
the reverse. A catalog's version number is the only thing that tells them about
compatibility, and a regeneration that silently removed public members would move
that number on the strength of somebody else's release note.

The cost accepted is that a catalog accumulates. Over enough upstream releases,
some share of its surface describes rules nobody can trigger, and those entries
appear in the completion lists that are part of what the catalog sells. That is
real, and it is the honest price of the promise: the alternative is a tidier
artifact that periodically breaks the people who depend on it. The cost is also
bounded — an obsolete entry is a compile-time constant and a line of
documentation, and it costs nothing at all to a consumer who does not reference
it.

Reserving deletion for a major version, rather than forbidding it outright, keeps
a route to prune without weakening the promise. The train structure already lets
a catalog take a major version without dragging the foundation's, so the option
exists and is properly priced: a consumer who reads the version number is warned,
which is exactly what was missing from silent removal.

## Alternatives Considered

### Delete the rule and let the regeneration diff speak

Considered because it keeps the catalog an exact mirror of what upstream
currently declares — a defensible definition of what a mirror is — and it is the
only option that keeps the artifact from growing without bound.

Rejected because "exact mirror" describes the wrong artifact. Consumers reference
the catalog's members by name, which makes it an API and not only a reflection of
someone else's package. The regeneration diff is read by this repository's
maintainer; the consumer sees none of it, and receives instead a compile error
with nothing in it.

### Keep the constant but leave it unmarked

Considered because it is the least intrusive option available: nothing breaks,
nothing warns, and every existing suppression keeps compiling exactly as before.

Rejected because it is silent in the direction that matters. The consumer keeps a
suppression that suppresses nothing and is never told, and the catalog goes on
asserting the existence of a rule its vendor retired — a catalog claiming to be
the authoritative answer, quietly holding a stale one. It trades a wrong-and-loud
outcome for a wrong-and-quiet one, which is the failure mode this repository
rules out elsewhere (ADR-0009).

### Move retired rules into a separate legacy package

Considered because it keeps the live catalog clean while preserving the constants
for anyone still referencing them, and it makes the accumulation someone's
explicit choice rather than an inevitability.

Rejected because from the consumer's position it *is* a deletion: the member
disappears from the package they reference, and the recovery is to discover a
second package and add it. It solves the repository's tidiness problem by moving
the cost onto the people the promise was made to.

### Escalate the obsolescence to an error after some period

Considered because it makes cleanup eventually mandatory instead of perpetually
optional, and it would keep obsolete entries from lingering forever in completion
lists.

Rejected as an unpriced break. It would fail builds on a schedule this repository
invented, for a change a third party made, with no version boundary a consumer
could pin against or reason about — the same silent-removal problem, merely
deferred and given a timer.

## Consequences

### Positive

* Upgrading a catalog never breaks a consumer's recompilation.
* A retirement upstream reaches the consumer as a message naming the rule and the
  version that dropped it, on the line that has to change.
* A catalog's version number keeps meaning what Semantic Versioning says it
  means, because removals are versioned rather than incidental.

### Negative

* Catalogs grow monotonically; in a mature one, part of the surface describes
  rules that can no longer be triggered.
* Those entries dilute completion, which is one of the things a catalog is
  referenced for.
* Generation is no longer a pure function of the upstream package: producing a
  catalog requires knowing what that catalog previously published.

### Risks

* A rule is retired upstream and later restored, leaving a permanent and wrong
  obsolescence mark. Mitigation: the mark is derived at each regeneration from
  what upstream declares at that moment, so a restoration removes it without
  anyone intervening.
* A rule is renamed rather than retired, and the catalog carries an obsolete old
  entry and an unrelated-looking new one with nothing connecting them.
  Mitigation: none automatic — the regeneration pull request lists additions and
  retirements together, which is where a human can recognise a pair.
* A consumer suppresses obsolescence warnings globally and never cleans up.
  Mitigation: none available; a catalog cannot outrank the consumer's own warning
  configuration, and the promise deliberately stops short of forcing them.

## Follow-up Actions

* State the never-delete rule in each catalog's own consumer documentation: it is
  a promise made to consumers, not an internal convention.
* Keep additions and retirements both visible in the regeneration pull request,
  so a rename can be recognised as one.
* Decide, before a catalog first takes a major version, whether that major
  actually prunes retired entries or whether they are kept indefinitely.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.en.md) — why a
  catalog can take a major version on its own.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.en.md) — where
  a catalog's content comes from, and why silence is the failure to avoid.
* [doc/specification.en.md](../specification.en.md) — §14.1, §23.1, and
  Appendix A12.
* `eng/CatalogGen` — the generator.
