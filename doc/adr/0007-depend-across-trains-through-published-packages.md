# ADR-0007 | Depend across trains through published packages, never project references

**Status:** Proposed
**Proposed:** 2026-07-30
**Decision Makers:** Reefact

## Context

The repository publishes several release trains that version and tag
independently (ADR-0002): the foundation, and one catalog per diagnostic-rule
vendor. All the trains live in a single solution.

Each catalog is built on the foundation. A catalog project therefore needs the
foundation's types at compile time.

`dotnet pack` converts a `<ProjectReference>` into a package dependency stamped
at **the version being packed**. Packing the Sonar catalog at `4.0.0` with a
project reference to the foundation would therefore declare a dependency on
foundation version `4.0.0` — a version of a different train, which nothing ever
published and which the `lib` train may never reach.

A consumer restoring such a package gets `NU1102`: the dependency cannot be
resolved from any feed. Packages on nuget.org are immutable, so the broken
package stays published; only a new version fixes it.

Within a single train the situation is different: all its projects are packed in
the same invocation at the same version, so a project reference between them is
stamped at a version that is being published right now.

A project reference is also the ordinary way to bundle something into a package
without depending on it — an analyzer shipped inside the library it supports,
say — and those references target projects that publish nothing of their own.

At the time of this decision no catalog project exists, so no such reference can
yet have been written.

## Decision

A project on one release train depends on another train only through a
`PackageReference` to a published version, never through a `ProjectReference`.

## Rationale

The failure this prevents is both silent and permanent. Nothing about a
cross-train project reference is visible at build time: the solution compiles,
the tests pass, the pack succeeds, and the defect exists only inside the produced
`.nuspec`. It surfaces at the first consumer's restore, on an artifact that
cannot be withdrawn. Against that asymmetry, forbidding the construct outright is
proportionate — there is no version of it that works.

Depending through a published package is also the honest expression of what
independent trains mean. A catalog does not ship *with* a particular working copy
of the foundation; it ships against a foundation version that exists on
nuget.org, which is precisely what its package must declare. Making the build
resolve the same artifact the package declares removes the gap between what was
compiled and what a consumer will restore.

The cost is that a catalog does not automatically pick up an unreleased
foundation change: a change spanning both must release the foundation first, then
bump the catalog's reference. That is not friction the decision adds, it is the
release order independent trains already imply, made explicit rather than
discovered.

The rule is checked on the project files rather than on the produced package,
because that is where the answer is exact. A `.nuspec` cannot distinguish a
dependency that came from a project reference from a legitimate package reference
that happens to carry the same version; the project file states the construct
directly. Checking it on every pack — including the pull-request rehearsal — is
what makes the rule arrive when the reference is written rather than when a
release is attempted.

A reference to a project that declares no train is deliberately left alone. Those
projects publish nothing, so no dependency is stamped for them; flagging them
would break the ordinary bundling pattern and produce failures with no defect
behind them.

## Alternatives Considered

### Allow the project reference and override the emitted dependency version

Considered because MSBuild can override what a project reference contributes to
the package, so the correct published version could be stamped while the build
still compiles against local source.

Rejected because it makes the package declare a dependency on an artifact the
build never used. The compiled code and the declared dependency would be free to
diverge, which converts a restore failure — loud, immediate — into a runtime
mismatch that appears only in the consumer's application.

### Put every catalog on the foundation's train

Considered because it removes the cross-train case entirely: everything is
co-published at one version, and project references are always valid.

Rejected because it is ADR-0002 reversed. It reinstates exactly the coupling that
decision exists to remove — a vendor's rule update would move the foundation's
version, and the foundation's stability promise would be at the mercy of four
release cadences.

### Rely on review to catch the construct

Considered because the rule is simple to state and a reviewer who knows it will
see the reference in a diff.

Rejected because the construct is written once, when a catalog project is
created, and is invisible from then on. The failure surfaces months later, at a
release, with the author's context long gone. A check that runs on every pull
request costs nothing and never forgets.

### Split each catalog into its own repository

Considered because separate repositories make a cross-train project reference
impossible to write.

Rejected for the reasons already recorded in ADR-0002: the catalogs share the
foundation and its test helpers, and the split would multiply the CI/CD surface
before a single catalog exists. A repository split remains available later and
would make this rule redundant rather than wrong.

## Consequences

### Positive

* A published package can never declare a dependency on a version that was never
  published.
* What a catalog compiles against and what its package declares are the same
  artifact.
* The release order implied by independent trains is stated, not discovered.

### Negative

* A change spanning the foundation and a catalog needs two releases, in order.
* A catalog cannot be developed against unreleased foundation source without a
  local feed or a pre-release version.
* Contributors meet a rule that the solution's structure would otherwise let them
  break naturally.

### Risks

* The friction pushes someone to add the forbidden reference "temporarily".
  Mitigation: the check fails the pack on every pull request, so a temporary
  version of it does not survive review.
* A pre-release foundation version is referenced and then never published,
  leaving a catalog pinned to a version that does not exist. Mitigation: the
  reference resolves at restore, so the build fails immediately rather than the
  consumer's.

## Follow-up Actions

* Establish how a catalog is developed against an unreleased foundation — a
  pre-release version on nuget.org, or a local feed — when the first catalog is
  created.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.md) — why the
  trains are independent.
* [ADR-0006](0006-publish-through-trusted-publishing-with-provenance-and-an-sbom.md).
* `tools/packaging/pack.sh` — the check.
* [CONTRIBUTING.md](../../CONTRIBUTING.md) — "Cross-train dependencies".
