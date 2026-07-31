# ADR-0015 | A catalogue's package version runs on its own line, never the upstream's

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

Each catalogue mirrors one upstream analyzer package and publishes on its own
release train ([ADR-0002](0002-partition-releases-into-trains-by-commit-scope.md)).
Two version numbers therefore exist for every catalogue: the one this repository
publishes, and the one it mirrors.

The upstream release is already recorded, per catalogue, in assembly metadata by
`[assembly: CatalogSource]` (§7.6). §14.2 observes that the package version does
not have to encode it and that the two can move independently, but stops short of
choosing — *"whichever scheme is chosen"*. Appendix B7 recorded the choice as an
open question to settle before the first public release. The three catalogues'
own changelogs, meanwhile, already state the answer as fact in their preamble:
the upstream release is read from the assembly rather than inferred from the
package number.

The three mirrored vendors number their releases differently and none of them
uses Semantic Versioning as NuGet understands it:
`SonarAnalyzer.CSharp 10.31.0.145097` carries four segments,
`Microsoft.CodeAnalysis.NetAnalyzers 10.0.302` three,
`StyleCop.Analyzers 1.1.118` three.

The two numbers demonstrably move at different times, in both directions:

* `0.2.0` of all three catalogues shipped a change to every rule's documentation
  while mirroring exactly the releases `0.1.0` already mirrored. Nothing upstream
  had moved.
* The scheduled synchronisation (§14.3) regularly finds an upstream release that
  changes no rule this repository publishes, and leaves the catalogue untouched
  by design — an upstream version moves, the catalogue does not.

The release workflow rejects SemVer build metadata (`+…`) outright, because NuGet
drops it from a package's identity.

## Decision

A catalogue's package version is its own Semantic Versioning line, incremented
from what changed in the catalogue; the upstream release it mirrors is carried in
assembly metadata and is never encoded in the package version.

## Rationale

The two numbers answer different questions, and a single number cannot answer
both. A package version tells a consumer whether upgrading is safe — whether a
constant they reference could have moved, which §23 and §23.1 define precisely.
The upstream version tells them which vendor release the catalogue reflects.
Collapsing the second into the first would leave the first unable to say
anything, which is the failure ADR-0002 partitions the trains to avoid: a train's
number must say something about that train.

The demonstration is stronger than the argument. `0.2.0` had to ship while
upstream stood still, and a tracking scheme had no number available for it: the
mirrored release had not changed, so any number derived from it was already
taken. The converse happens more often still — the nightly job finds an upstream
release carrying nothing this catalogue publishes and correctly writes nothing.
A scheme that binds the two must invent a number in the first case and suppress
one in the second, and both inventions are the catalogue lying about its own
provenance.

The vendors' own schemes settle what remains. A four-segment version is not a
SemVer value, so `10.31.0.145097` cannot be a package version at all; and the
three vendors do not agree on a shape, so no single mapping serves the three
catalogues. Encoding the upstream release beside a SemVer core is closed off too:
build metadata is rejected by the release workflow because NuGet drops it from
the package identity, and a prerelease tag would mark every release a prerelease.

Metadata is the right home for the mirrored release because it cannot be
truncated to fit. `[assembly: CatalogSource]` carries the vendor's version string
exactly as the vendor wrote it, four segments and all, next to the date of
generation — which no package version could hold whatever scheme were chosen.

## Alternatives Considered

### Track the upstream version

Considered because it is instantly legible: a consumer reading
`DiagnosticCatalog.Sonar 10.31.0` would know what it mirrors without opening
anything, and the question "is this catalogue current?" would answer itself.

Rejected because it leaves the catalogue unable to ship its own changes. `0.2.0`
is the case in hand: nothing upstream moved, and every number derived from
upstream was already published. It also cannot be done faithfully — Sonar's four
segments are not a SemVer value — and it would make a catalogue's `MAJOR` follow
a vendor's numbering rather than the contract break §23 reserves it for.

### Carry the upstream version beside a SemVer core, as build metadata or a prerelease tag

Considered because it would keep an independent line while still showing the
mirrored release in the number a consumer reads first.

Rejected because the release workflow already refuses build metadata: NuGet drops
`+…` from a package's identity, so two different upstream releases would produce
the same package. A prerelease tag would carry it, but at the cost of marking
every release of every catalogue a prerelease.

### Bind only the major: the catalogue's `MAJOR` follows the vendor's

Considered as a middle path — legible at a glance, while leaving `MINOR` and
`PATCH` free for the catalogue's own movement.

Rejected because `MAJOR` is the one segment with a defined meaning here: §23 and
§23.1 make it the signal that a referenced constant may have moved. Spending it
on a vendor's unrelated numbering would make the only breaking-change signal a
consumer has fire on releases that break nothing, and stay silent on ones that
do.

## Consequences

### Positive

* A catalogue can ship a change of its own — a generator fix, a documentation
  change — without waiting for the vendor to release.
* A package version keeps the meaning §23 gives it, so a consumer reading one
  learns whether the upgrade can break their compilation.
* The three catalogues follow one rule despite mirroring three incompatible
  vendor schemes.

### Negative

* A consumer cannot tell from the package version which upstream release a
  catalogue mirrors; they read `[assembly: CatalogSource]`, or the changelog
  entry, both of which state it.
* Two numbers have to be held in mind for each catalogue rather than one.

### Risks

* A consumer assumes the package version tracks upstream and concludes the
  catalogue is stale. Mitigation: each catalogue's changelog preamble states the
  rule and points at the metadata, and each release entry names the mirrored
  version.

## Follow-up Actions

* Close Appendix B7 in the specification against this record, in both languages.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.md) — why the
  trains version independently at all.
* [doc/specification.en.md](../specification.en.md) — §7.6, §14.2, §14.3, §23 and
  §23.1, and Appendix B7.
* `src/DiagnosticCatalog.Sonar/CHANGELOG.md` and its counterparts — where the
  rule is stated to consumers.
