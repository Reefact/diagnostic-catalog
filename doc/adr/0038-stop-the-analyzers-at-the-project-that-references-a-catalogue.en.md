# ADR-0038 | Stop the analyzers at the project that references a catalogue

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.fr.md)

**Status:** Proposed
**Proposed:** 2026-08-06
**Decision Makers:** Reefact

## Context

[ADR-0037](0037-ship-the-analyzers-inside-the-foundation-package.en.md) folded the `DCAT` analyzers
into `DiagnosticCatalog`, so that referencing any catalogue delivers the checks with no second
package to discover. It ships them in `analyzers/dotnet/cs/`, which NuGet resolves as an asset, and
recorded as a **risk** that "the analyzer travels the second hop as readily as the first".

That risk was then measured against real packages, outside CI. An application referencing an
ordinary library, which itself referenced a catalogue for its own suppressions, failed to build:

```
error DCAT0006: Reference 'DiagnosticCatalog.Sonar.SonarRule.S1144'
                instead of the string literals "Major Code Smell" and "S1144"
```

Its project file named no catalogue, no analyzer and no foundation. It had one line, a
`PackageReference` to the library. `DCAT0006` ships as an error
([ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md)), so the build stopped.

ADR-0037 named the mitigation and put it on the library: `PrivateAssets="all"` on its own catalogue
reference, "free for it because it owes nobody the attribute". Measured to work. But it is a lever
held by somebody with no reason to reach for it — a library author is not thinking about whether
their consumers want analysis — and its failure mode lands on a third party who cannot anticipate
it, cannot see the cause in their own project file, and did not choose any part of the chain.

**A NuGet asset has no notion of distance.** The foundation is transitive for a catalogue's
consumer and transitive again for that consumer's consumer; nothing in the package can tell the two
apart, so no producer-side setting on an `analyzers/` folder can serve the first and refuse the
second. Both halves of the flow the fold depends on — analyzers reaching a transitive consumer at
all — are the behaviour of [NuGet/Home#13813](https://github.com/NuGet/Home/issues/13813), which
contradicts NuGet's own documentation and could be closed by a release.

**MSBuild draws the line NuGet does not.** A package's `build/` folder is imported for a **direct**
`PackageReference` and for nothing further out; `buildTransitive/` is imported for every consumer.
That asymmetry is documented, deliberate, and is the reason `buildTransitive/` was added.

Three further behaviours were measured while drafting this record, and each one changed the design:

* A package carrying `buildTransitive/` has its `build/` folder **ignored entirely**, for direct
  consumers too. The generated `.nuget.g.targets` imports only the `buildTransitive/` file. So a
  package cannot hold direct-only assets and transitive assets at once, and the foundation cannot
  use `build/` to recognise its own direct consumers.
* MSBuild evaluates **every property before any item**, so a property condition may not read
  `@(PackageReference)`. The recognition has to be one condition on the item group itself.
* Adding the analyzers from a target rather than at evaluation time would keep them out of a
  design-time build, so the IDE would not load them.

## Decision

The analyzer assemblies ship in `dcat-analyzers/`, a folder NuGet resolves nothing from, and reach
a compiler only through `buildTransitive/DiagnosticCatalog.targets`, which adds them when
`EnableDiagnosticCatalogAnalyzers` is `true` or when the foundation is among the building project's
own `PackageReference` items.

Every catalogue packs `build/<its own package id>.props`, which sets that property. NuGet imports
it for a direct reference and for nothing further out.

A project may set `EnableDiagnosticCatalogAnalyzers` itself, in either direction, and neither
clause overwrites it.

## Rationale

The property the fold bought was *using a catalogue means being checked*. What it delivered was
*being anywhere downstream of a catalogue means being checked*, and the second is a different
statement about a different person — one who made no choice and cannot see why their build fails.
Bounding the flow at one hop is what makes the sentence true as written.

The bound is placed where the knowledge is. A catalogue knows it is a catalogue; NuGet's asset
resolution does not, and a library author does not know what their consumers want. So the opt-in is
packed by the producer, into every catalogue, derived from the project file rather than declared in
a list — the same argument `ReleaseTrain` settles, and the reason a fourteenth catalogue carries it
without anyone remembering.

Moving off the `analyzers/` folder also moves the arrangement off
[NuGet/Home#13813](https://github.com/NuGet/Home/issues/13813). ADR-0037 depended on undocumented
behaviour and mitigated it by re-measuring on every pull request; this depends on the documented
behaviour of `build/` and `buildTransitive/` instead. The measurement stays, because it is how the
undocumented behaviour was found in the first place.

The consumer-side property is not a convenience. ADR-0037's fifth negative consequence was that a
consumer who wants the attributes and not the checks "can no longer express that by declining a
package reference, and has to silence the diagnostics in `.editorconfig` instead" — one package,
one lever, and `PrivateAssets="all"` withholds `[DiagnosticRule]` along with the analysis. Reading
a property restores the lever without splitting the package, and it points both ways: the
application two hops out that *does* want the checks can ask for them.

## Alternatives Considered

### Leave it, and document `PrivateAssets="all"` for library authors

Nothing changes in the packaging, the guide gains a section, and the lever is already measured to
work.

Rejected because it is a guarantee that rests on every library author, everywhere, knowing a rule
about a package they took for their own reasons. Nothing enumerates them and no check can hold the
set true. It is the same shape as the "reference the analyzer package from every catalogue"
alternative ADR-0037 rejected, moved one hop out and onto people who do not read this repository's
documentation at all.

### Lower `DCAT0006` to a warning

The leak stops failing builds, and a deliberate adopter raises it to `error` in `.editorconfig`.

Rejected because it treats the symptom. The application two hops out would still be analysed by a
catalogue it never chose, still see diagnostics it cannot explain, and still have no lever; it would
merely see them in yellow. It also gives up what ADR-0027 bought for the consumer who *did* choose,
which is the larger population. The severity question can be reopened on its own merits once the
audience is the right one.

### Fold a copy of the analyzers into each catalogue

A catalogue becomes self-sufficient, and with the same `build/` gate the flow would stop at one hop.

Rejected for the reason ADR-0037 rejected it, made worse by the gate. The catalogues ride different
trains at different paces, so two of them carry analyzer assemblies of the same file name at
different versions. NuGet unified those by package identity; a gate that adds them **by path** gives
MSBuild nothing to unify, so the compiler is handed two, and the duplication ADR-0037 measured its
way out of comes back. Keeping the assemblies in the one foundation is what makes "exactly one
analyzer instance" hold for a consumer of several catalogues, and that check is in
`tools/packaging/verify-consumption.sh`.

### Detect the direct catalogue reference from the foundation alone

No per-catalogue file, so third-party catalogues need to ship nothing.

Rejected because the foundation cannot know which package ids are catalogues. It would have to
inspect the resolved graph for direct references that depend on it, which is neither available at
evaluation time nor stable across NuGet versions — trading a documented mechanism for a clever one,
to save a catalogue author three lines.

## Consequences

### Positive

* An application is analysed by a catalogue it references, and by nothing further away. The
  arrangement that failed a stranger's build no longer does.
* The delivery rests on documented NuGet behaviour rather than on
  [NuGet/Home#13813](https://github.com/NuGet/Home/issues/13813), so a NuGet release restoring the
  documented behaviour would leave it working.
* A consumer can decline the analysis and keep the attribute, which one package could not offer
  before, and can also ask for it from further out.
* A consumer of several catalogues is still checked by exactly one analyzer, at one version,
  because the assemblies stay in the one package NuGet unifies.

### Negative

* A catalogue must now ship a file to have its consumers checked. Ours do, derived from the project
  file, but a third-party catalogue that ships nothing leaves its consumers **silently** unchecked —
  the build succeeds and nothing reports. That is a real regression for a reader who publishes a
  catalogue without reading [`doc/guide/packaging-a-catalogue`](../guide/packaging-a-catalogue.en.md).
* The analyzers are handed to the compiler by our own MSBuild rather than by NuGet's asset
  resolution, so a mistake in one `.targets` file disables all analysis everywhere, and does so
  silently. The consumption checks are what stands between that and a release.
* `dcat-analyzers/` is not a convention any tool knows. Anything that reads a package looking for
  analyzers — an SBOM consumer, a security scanner, a mirror — will not find these where it expects
  them.
* `NU5100` is suppressed on the four packed assemblies, so the warning that would catch a genuinely
  misplaced assembly in this project is off.

### Risks

* The gate is one MSBuild condition, and MSBuild has no type system. A typo in the property name
  fails open in the quiet direction: no analyzer, no diagnostic, no error. Only
  `tools/packaging/verify-consumption.sh` would notice, which is why it now asserts activation as
  well as its absence.
* `@(PackageReference)` is read at evaluation time to recognise a direct reference to the
  foundation. A project that adds its references from a target — rare, but legal — would not be
  recognised.
* The opt-in props is packed under `build/$(PackageId).props`. A catalogue that sets `PackageId`
  after `Directory.Build.targets` is imported, or not at all, would pack it under the wrong name and
  be silently silent.

## Follow-up Actions

* Reopen the severity question of [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md) on
  its own merits, now that the population seeing `DCAT0006` is the one that referenced a catalogue.
* Decide whether `dcat` should emit `build/<id>.props` for a catalogue it generates, so a
  third-party author gets the opt-in without reading the guide.
* Consider a `DCAT` diagnostic, or a pack-time check, for a catalogue package that depends on the
  foundation and ships no opt-in — the one failure mode this decision makes silent.

## References

* [ADR-0037](0037-ship-the-analyzers-inside-the-foundation-package.en.md) — the fold this record
  keeps, the risk it recorded, and the negative consequence this reverses.
* [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md) — the severity that made the leak
  a failed build rather than a puzzle.
* [ADR-0007](0007-depend-across-trains-through-published-packages.en.md) — why the catalogues take
  a `PackageReference` to the foundation and `DiagnosticCatalog.Self` takes a `ProjectReference`,
  which is why the opt-in rule matches both.
* [`doc/specification.en.md`](../specification.en.md) — §16, the packaging and the transitivity it
  measures.
* [`doc/guide/packaging-a-catalogue`](../guide/packaging-a-catalogue.en.md) — what a catalogue
  author must now ship.
* `tools/packaging/verify-consumption.sh` — the eighteen checks that hold all of this, including
  the one hop that must work and the one that must not.
