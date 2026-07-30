# ADR-0002 | Partition releases into trains by commit scope

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

The repository ships two kinds of artifact. One is the **foundation**: the
library that defines, generates and validates catalogs, together with its
analyzers, its command-line tool and its test-support package. The others are
**catalogs**, one per diagnostic-rule vendor — SonarQube, the Microsoft .NET
analyzers, StyleCop.

The two kinds change for unrelated reasons. A catalog changes when its vendor
ships, renames, deprecates or removes rules; the repository does not control that
cadence and does not observe it in advance. The foundation changes when its own
contract changes, which is expected to be rare — a catalog is a contract, and the
foundation is what the contract rests on.

A rule identifier is referenced symbolically by consumers. Removing one from a
catalog is a breaking change of that catalog. It says nothing about the
foundation.

Semantic Versioning describes the compatibility of **one** artifact. A version
number shared by several artifacts describes none of them: it moves for reasons
that belong to another.

The repository already requires a Conventional Commits scope drawn from a closed
list, checked at commit time (ADR-0003). The scope is a statement the author
makes about which component the change belongs to.

At the time of this decision the repository contains no code: the shape chosen
here constrains every project, package and changelog that follows.

## Decision

Each release train — the foundation, and one per vendor catalog — versions, tags
and publishes independently, and a commit is routed to its train by its
Conventional Commits scope.

## Rationale

A single version across the repository would force a release of the foundation
every time a vendor published rules, and would make the foundation's version
number uninformative: a consumer pinning it could not distinguish a change to the
foundation's contract from an addition to somebody else's rule list. The
foundation's stability is the property the library sells; coupling its version to
a third party's cadence would be selling the opposite.

In the other direction, a vendor removing rules must be able to publish a major
version of that catalog without dragging the foundation's major with it. Under a
shared version, one vendor's cleanup would announce a breaking change to every
consumer of every package.

Routing by scope, rather than by any separate metadata, keeps the mapping
derivable from the history alone. The scope is already required, already drawn
from a closed list, and already checked at write time, so the routing needs no
artifact that could fall out of date, and a commit's destination is decided by
the person who best knows it — its author — at the moment they write it.

The trade-off accepted is a larger release surface: more tags, one changelog per
train, and a scope list that grows with each catalog. That cost is proportional
to the number of catalogs, paid once per catalog, and is the direct price of the
independence the decision buys.

Because routing depends on the scope being present, the two version-driving
commit types cannot be left unscoped — an unscoped one would match no train and
disappear from the release record silently. Making that a hard rejection at write
time is what keeps this decision from degrading in practice.

## Alternatives Considered

### One version for the whole repository

Considered because it is the simplest release process: one tag, one changelog,
one number to reason about, and no routing at all.

Rejected because it makes the foundation's version meaningless to the consumers
who care most about it, and because it forces every package to move whenever any
vendor does. The simplicity is real, but it is bought by destroying the
information the version number exists to carry.

### One repository per catalog

Considered because it gives each catalog its own version, changelog, issue
tracker and CI by construction, with no routing mechanism at all.

Rejected because the catalogs are consumers of the foundation and of its
test-support package, so splitting now would duplicate the whole CI/CD surface
across four repositories before a single catalog exists, and would turn every
foundation change into a cross-repository migration. A split remains available
later, per catalog, once one has a life of its own.

### Route by path — which project a commit touched

Considered because a path is an observable fact that needs no author discipline
and cannot be misdeclared.

Rejected because a commit that touches shared foundation code alongside one
catalog would match several trains with no way to say which release it belongs
to, and because a path records where code happens to live rather than what the
change is about. The scope is a statement of intent, which is what a release
record needs; a file path is an implementation artifact that moves under
refactoring.

### Release everything continuously from `main`, with no trains

Considered because it removes versioning judgement entirely.

Rejected because it does not address the problem: the packages would still share
a number, and consumers of a foundation that promises stability need to be able
to pin it.

## Consequences

### Positive

* The foundation's version describes the foundation, and a catalog's version
  describes that vendor's rules.
* A vendor's breaking change is announced to that vendor's consumers only.
* The release record is derived from the commit history with no separate mapping
  to maintain.
* Adding a catalog is additive: a scope, a train, a changelog.

### Negative

* More tags, more changelogs, and a release process that must be run per train.
* The scope list must be extended whenever a catalog is added, in the linter and
  in the contribution guide together.
* A single change that spans the foundation and a catalog needs two commits, or
  one commit carrying both scopes and landing in both trains' notes.

### Risks

* A contributor picks a plausible but wrong scope and the change is announced in
  the wrong train's notes. Mitigation: the scope list is closed and checked, and
  the contribution guide names each scope by the vendor or component it covers,
  including the deliberate `analyzers` versus `netanalyzers` distinction.
* The release tooling and the linter's scope list drift apart, so a valid commit
  routes nowhere. Mitigation: the contribution guide states that the linter's
  list is the checkable copy and the two are changed together; a scope the linter
  does not know is rejected at commit time, which fails closed.

## Follow-up Actions

* Implement the release workflow per train, with train-prefixed tags.
* Give each catalog project its own changelog when the project is created.
* Extend the scope list, the guide's scope table and the train table together
  whenever a catalog is added.

## References

* [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.md) — the
  convention this routing depends on.
* [CONTRIBUTING.md](../../CONTRIBUTING.md) — "Scope", and the train table.
* `tools/commit-lint/lint-commit-message.sh` — the checked scope list.
