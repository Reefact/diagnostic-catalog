# ADR-0017 | Publish the generator as a CLI, on its own release train

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

This repository publishes catalogues that mirror analyzers it does not own, and it
generates them with a tool it keeps to itself. `eng/CatalogGen` is marked
unpackable and lives outside `src/`: it produces the catalogues and ships nothing.

That has a recorded consequence. Commits are partitioned into release trains by
scope (ADR-0002), and `cataloggen` is the one scope belonging to no train —
`CONTRIBUTING.md` states the reason plainly, that the generator ships nothing so
nothing it does can move a published version. It is the single place in the
convention where a `feat` or a `fix` reaching no release note is correct rather
than an accident.

Three facts have moved since that was written.

**The generator is no longer specific to the catalogues here.** Until recently it
could reach analyzers exactly one way: name a package, and it is downloaded from
nuget.org. It now also reads analyzer assemblies already on disk, so what it does
for `SonarAnalyzer.CSharp` it does for an analyzer a developer built five seconds
ago. The capability that made it internal — knowing how to fetch the three
packages this repository mirrors — is no longer what it is.

**A command-line tool is already foreseen, and already has a scope.** The
specification lists `DiagnosticCatalog.Tool` among the possible evolutions and
sketches its verbs, of which `generate` is one. The `cli` scope exists in the
closed scope list and routes today to the `lib` train, though no CLI project
exists to use it.

**The `lib` train is deliberately very stable**, because a catalogue contract
rests on it. `CONTRIBUTING.md` gives that as the reason `cataloggen` is not scoped
`core`: riding the `lib` train would bump the foundation's version for work its
consumers never see.

Two further facts bear on where a published generator would sit. Its cadence is
set by things outside this repository — the Roslyn versions upstream analyzers are
compiled against, and the folder layouts their packages use — and not by the
foundation's contract, which changes for unrelated reasons and much more rarely.
And it holds no reference to the foundation: it emits `using DiagnosticCatalog;`
as text, so nothing links the two assemblies and ADR-0007's cross-train rule has
nothing to bind.

Finally, the same maintainer's `first-class-errors` repository already publishes a
command-line tool this way: a `cli` train, carrying both the CLI's scope and the
scope of the documentation generator behind it, versioning apart from that
repository's `lib` train.

## Decision

The catalog generator is published as a .NET tool on a `cli` release train of its
own, which both the `cli` and `cataloggen` scopes route to and which versions
independently of `lib`.

## Rationale

The decision follows from what a version number is supposed to say. The `lib`
train's version speaks for the foundation on which every catalogue contract rests,
and generator work reaches no consumer of that foundation: a correction to how an
analyzer package is unpacked changes nothing a project referencing
`DiagnosticCatalog` can observe. Publishing the generator on `lib` would put
movement into a number whose stability is the point, and would do it on a cadence
the foundation does not control — every Roslyn release and every upstream
repackaging becoming a foundation release. That is the argument `CONTRIBUTING.md`
already makes for keeping the generator off `core`, and publishing does not
weaken it; it makes it sharper, because now the movement would be visible to
consumers as a version bump rather than merely recorded.

The reason to publish at all is that the generator's usefulness stopped being
specific to this repository. The value it carries is the method recorded in
ADR-0009 — read the descriptors, never the documentation, because the platform
never validates a suppression's category and a transcription that drifts produces
no symptom anywhere. That reasoning is not about SonarSource, Microsoft or
StyleCop; it holds for anyone who ships analyzers and wants a catalogue their
consumers can reference symbolically. Keeping the only implementation of it
private means every such person either transcribes their own rules by hand — the
failure mode ADR-0009 exists to refuse — or rebuilds the tool. Now that reaching
their analyzers no longer requires them to be a public package, nothing but the
packaging stands between the method and the people it serves.

A train of its own, rather than a fourth position of some other kind, is what the
existing architecture already provides for this: a train is precisely a package
that versions and publishes at its own pace, and the generator has a pace of its
own. Naming that train `cli` rather than inventing another name follows from the
specification having already decided there is one command-line tool with several
verbs, of which generation is one. A separate `cataloggen` train would version the
same executable twice.

Routing `cataloggen` to it, rather than retiring the scope in favour of `cli`,
keeps a distinction the repository already finds worth making. The scope list
separates the tool's shell from the engine behind it elsewhere too, and the
release record reads better for it: a change to how descriptors are read and a
change to how the command parses its arguments are different facts about the same
package. What changes is only that they now reach a release note, which is the
whole content of this decision as it applies to `cataloggen`.

The absence of any reference from the generator to the foundation is what makes
this cheap rather than delicate. ADR-0007 forbids a project on one train from
carrying a project reference to a project on another, because `dotnet pack` would
stamp a dependency on a version that was never published. There is no such
reference to remove: the generator produces text naming the foundation's
attributes and never binds to them. The version skew this normally creates —
the tool built against one version of a library, the consumer's project holding
another — cannot arise, for the same reason the sibling repository's tool holds no
reference to its own library. That property is worth stating because it is easy to
lose: adding a reference later, for the convenience of compiler-checking the
emitter's use of the API, would trade a structural guarantee for a checked one.

## Alternatives Considered

### Keep the generator private, as it is today

Considered seriously, because it has a real advantage: nothing about the tool is a
public contract. Its command line, its manifest format, its behaviour when an
analyzer cannot be constructed and the range of Roslyn versions it tolerates are
all free to change with a single commit, because the only caller is this
repository's nightly job.

Rejected because the cost of that freedom is now paid by other people. The
argument in ADR-0009 — that a catalogue derived from documentation is confidently
wrong and that nothing in a consumer's build disagrees with it — applies to every
analyzer author, and the only tool that acts on it is the one kept here. The
freedom is worth something; it is not worth being the reason the method stays
unavailable.

### Publish it on the `lib` train, using the `cli` scope where it already routes

Considered because it requires no new train: the scope exists and points at `lib`
already, so this is the smallest possible change.

Rejected because it inverts the reasoning `CONTRIBUTING.md` uses to keep the
generator off `core`. The foundation's version would move for work its consumers
cannot see, on a cadence driven by upstream analyzer releases, and a project
depending on the foundation would see churn that says nothing about the contract
it depends on.

### Give the generator a train of its own, distinct from the CLI's

Considered because the generator and a command-line shell around it are genuinely
different components, and because it would let the engine move without republishing
the tool.

Rejected because they are one published artifact. The specification foresees a
single command with several verbs, and generation is one of them; two trains
versioning the same executable would produce two version numbers for one package
and no way to say which one a user installed.

### Publish the generator as a library rather than a tool

Considered because it would let a build integrate generation directly, and because
a library is the form this repository already knows how to publish.

Rejected because it puts the wrong thing in the consumer's process. Reading
descriptors means loading third-party analyzer assemblies and constructing them —
executing code the consumer did not write — and a library form makes that happen
inside their build rather than inside a tool they invoked. The need is a build
step, not an API.

## Consequences

### Positive

* The foundation's version keeps saying something about the foundation, on the
  train whose stability the catalogue contracts rest on.
* The method recorded in ADR-0009 becomes available to anyone shipping analyzers,
  rather than only to the catalogues this repository happens to mirror.
* Generator work reaches a release record for the first time: `cataloggen`
  commits stop being correct-but-invisible and start describing a published
  artifact.
* The tool's cadence becomes honest — it moves when Roslyn and upstream package
  layouts move, which is what actually drives it.

### Negative

* The command line, the manifest format and the generated file's shape become
  public contracts, changeable only as a version bump rather than a commit.
* The generator currently targets a single recent runtime, which a published tool
  cannot assume of the machines that install it.
* Emission behaviour written for this repository — provenance recorded in the
  catalogue, and the banners refreshed in the files beside it — reaches
  repositories that did not ask for it and may not want it.

### Risks

* **Loading happens in the tool's own process.** The reader resolves every
  Roslyn request onto the version it already holds, which works because this
  repository controls the three packages it reads. Published, the tolerated range
  becomes something users discover by failure.
* **A package identity is costly to rename after adoption.** The identity this
  train publishes under should be settled and claimed before the first release
  rather than after.
* **Provenance may not fit a first-party catalogue.** The foundation's own
  documentation states that a catalogue maintained alongside its own analyzer
  needs no provenance record, which is exactly the case a locally-read source
  creates.

## Follow-up Actions

* Settle the published identity and the command name, and claim the identity
  before the first release.
* Decide the runtime floor the tool targets, and whether a build on that floor is
  allowed to run on newer majors.
* Decide whether provenance and the banner rewriting are always-on or opt-in, now
  that a source can be first-party.
* Move the `cli` scope off the `lib` train and route `cataloggen` to the new one,
  in the single source of truth the release tooling reads.
* Consider reading descriptors out of process, so the runtime the reader binds to
  can follow the assemblies it is given rather than the tool's own.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.md) — the train
  partition this decision adds to.
* [ADR-0007](0007-depend-across-trains-through-published-packages.md) — the
  cross-train dependency rule, which this train has nothing to bind.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.md) — the
  method whose availability is the reason to publish.
* [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — the scope and train tables, and the
  reasoning for keeping the generator off `core`.
* [`doc/specification.en.md`](../specification.en.md) §25, §25.6 — the foreseen
  tool and its verbs.
* [`Reefact/first-class-errors`](https://github.com/Reefact/first-class-errors) —
  a sibling repository publishing a command-line tool on a `cli` train of its own.
