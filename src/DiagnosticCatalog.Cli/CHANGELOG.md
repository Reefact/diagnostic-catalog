# Changelog

All notable, user-facing changes to the **`cli` release train** — the
`DiagnosticCatalog.Cli` package, which installs the `dcat` .NET tool — are
documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)
and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This train versions independently of the foundation. The tool follows Roslyn and
the package layouts its vendors publish, both of which move for reasons that have
nothing to do with the contract a catalogue rests on
([ADR-0002](../../doc/adr/0002-partition-releases-into-trains-by-commit-scope.en.md),
[ADR-0017](../../doc/adr/0017-publish-the-generator-as-a-cli-on-its-own-release-train.en.md)). Two
commit scopes ride it: `cli` for the shell — the command tree, the arguments, the
exit codes — and `cataloggen` for the engine that acquires analyzer assemblies and
reads their descriptors.

## [Unreleased]

Nothing published yet. `dcat` has never been released, so everything below is what
its first version will carry rather than a change against a previous one.

### Added

* **`dcat generate`** — writes a catalogue from the `DiagnosticDescriptor`
  instances an analyzer actually declares, never from its documentation
  ([ADR-0009](../../doc/adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)).
  Five kinds of source: a package on nuget.org or any feed your `NuGet.config`
  already configures, a `.nupkg` on disk, one or more of your own projects, a
  solution, and one or more assemblies you have already built. `--manifest`
  generates any number of catalogues in one run, and `--summary` reports what
  moved — rules added, recategorised, retitled, retired — so a scheduled
  regeneration opens a pull request a human can read instead of one they have to
  merge blind.
* **`dcat validate`** — everything `generate` does, stopping one step short of
  writing. Exit `2` means the catalogue on disk no longer matches its source and
  exit `1` means the source would not resolve, kept distinct on purpose so a feed
  outage is never reported as a drifted contract.
* **`dcat list` and `dcat explain`** — read a *compiled* catalogue, reflection-only,
  from the package assembly you already reference. `explain` prints the
  `[SuppressMessage]` line to copy, fully qualified. Both state which upstream
  release the catalogue mirrors and when it was generated before answering, because
  a catalogue is a snapshot and its age decides what its answer is worth.
* **`--solution`** — reads the projects in a solution that declare
  `<ProducesDiagnosticRules>true</ProducesDiagnosticRules>`, and nothing else.
  Which projects produce analyzers is never inferred: referencing Roslyn matches
  build tools and test projects, and declaring a `DiagnosticAnalyzer` matches
  fixtures. A project joins by saying so, in its own file. A solution where nobody
  declares it is refused rather than read as empty, because generating nothing and
  exiting zero reads to a scheduled job exactly like a catalogue that was current.
* **A JSON schema for the manifest**, published beside it. Point `$schema` at it
  and your editor reports a mistyped key where you type it, rather than after a
  package has been downloaded. It carries the constraints and not only the shape:
  one source per entry, `version` and `source` for feeds only, `sourceName` and
  `sourceVersion` for sources on disk only.
* **Refusal over guessing.** An analyzer that cannot be constructed, an assembly
  that will not load, a solution whose projects declare none: the tool emits
  nothing and exits non-zero. A catalogue silently short of a rule is
  indistinguishable from one whose vendor retired it, and would publish that rule
  as `[Obsolete]` — telling your users something false about somebody else's
  product.
* **Reproducible output.** The same upstream release yields the same bytes: ordinal
  ordering, culture-invariant titles, and an untouched file when neither the source
  nor any rule moved.
* **A budget on every spawned process.** Constructing an analyzer is third-party
  code and the one step here that can hang rather than fail, so the descriptor
  worker and MSBuild are each stopped if they outrun theirs — ten minutes and two,
  against measured times of seconds. `DCAT_TIMEOUT_SECONDS` raises both.
* **C# analyzers, and a refusal for the rest.** Reading descriptors means
  constructing each analyzer, and the descriptor worker carries only C# Roslyn, so
  `--language` accepts `cs` and refuses anything else at the command line rather
  than after a package has been downloaded. A settled position rather than a gap
  awaiting work
  ([ADR-0020](../../doc/adr/0020-a-catalogue-is-generated-for-c-sharp-only.en.md)).
* **It reads; it does not build.** A project, a solution or an assembly must
  already be built, and what the tool cannot find it names — along with the build
  command that would produce it. That is what keeps `validate` safe to run against
  a working copy: it restores nothing and writes no `obj/`.
