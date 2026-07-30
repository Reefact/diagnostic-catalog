# ADR-0006 | Publish through trusted publishing, with signed provenance and an embedded SBOM

**Status:** Proposed
**Proposed:** 2026-07-30
**Decision Makers:** Reefact

## Context

The repository publishes NuGet packages to nuget.org from a GitHub Actions
workflow. Publishing requires an API key.

A long-lived API key stored as a repository secret is valid until it is revoked,
is usable from anywhere by anyone who obtains it, and grants push rights to every
package it covers. nuget.org also supports **trusted publishing**: a workflow
presents a GitHub OIDC token, and nuget.org exchanges it for a short-lived,
single-use key, provided a policy naming the repository and the workflow exists.

A published package is **immutable**. nuget.org does not allow a version to be
replaced; a mistake is corrected only by publishing another version, and the bad
one stays listed or, at best, unlisted.

nuget.org **repository-signs** every upload, adding a signature file inside the
`.nupkg`. The bytes on nuget.org are therefore not the bytes the build produced,
and their checksums differ.

Consumers of a diagnostic rule catalog have no practical way to tell, from the
package alone, which commit and which build produced it.

Software supply-chain expectations now routinely include a machine-readable
inventory of a package's components, and OpenSSF Scorecard — which this
repository already runs — scores signed releases and pinned, reproducible builds.

The release workflow is the one workflow that no ordinary CI run exercises: its
version resolution, packaging, credential exchange and permissions execute for
the first time on a real tag, in production, once.

## Decision

Packages are published to nuget.org through OIDC trusted publishing, and every
published artifact carries a signed build-provenance attestation and an embedded
SPDX SBOM.

## Rationale

Trusted publishing removes the standing credential rather than protecting it.
There is nothing in the repository's secrets that grants push rights, so a leaked
secret, a compromised fork workflow or an over-broad token cannot publish; the
key that does exist is minted per run, expires, and is single-use. Given that a
published version can never be withdrawn, removing the credential that could
publish a wrong one is worth more than any rotation policy on a stored one.

The provenance attestation answers the question the package itself cannot: which
repository, workflow, commit and runner produced these bytes. It is signed
through the job's own OIDC identity, so it cannot be forged by anyone who did not
run this workflow on this repository. It is deliberately produced **before**
either publication, so nothing is ever released or pushed that has not been
attested.

The attestation covers the artifacts as built, which are published verbatim as
GitHub Release assets. It cannot cover the nuget.org copy, because nuget.org
re-signs it — that is a property of the registry, not a gap in the decision, and
it is why the Release assets exist alongside the nuget.org listing: they are the
copy a consumer can verify against the attestation, while the nuget.org copy is
verified through the registry's own signature.

The SBOM is embedded in the package rather than published beside it, so it
travels with the artifact and cannot be separated from it by a consumer's mirror,
proxy or offline feed. Its presence is asserted on the produced package on every
pack, not assumed from a build flag: a regression in the SBOM tooling would
otherwise leave a green pack producing inventory-less packages.

Because none of this is exercised by ordinary CI, the pipeline is made
rehearsable in two ways. The side-effect-free part — build, pack, SBOM, the
packaging guards — runs on every pull request, so packaging regressions surface in
normal review. The rest is a dispatchable dry run that deliberately keeps the
OIDC login and the attestation in: a misconfigured trusted-publishing policy or a
missing permission is exactly what a rehearsal must catch, and both fail loudly
without publishing anything. Only the two steps with irreversible effects are
skipped.

## Alternatives Considered

### Store a long-lived NuGet API key as a repository secret

Considered because it is the default, it is simple, and it needs no policy
configured on nuget.org.

Rejected because it creates a standing credential whose blast radius is every
package it covers and whose lifetime is until someone remembers to rotate it.
Against an immutable registry, the cost of that credential being used once is
permanent.

### Trusted publishing, but no attestation and no SBOM

Considered because trusted publishing alone already removes the credential risk,
which is the largest one, and the rest adds moving parts to the release path.

Rejected because it leaves the consumer with no way to connect a package to the
build that made it, and no inventory of what is inside it. The marginal cost is a
step and a package reference; the marginal value is the only evidence a consumer
can check independently.

### Publish the SBOM as a separate release asset

Considered because it keeps the package smaller and the SBOM easier to read
without unzipping.

Rejected because an SBOM that travels separately is an SBOM that stops travelling:
a consumer resolving through a mirror, a proxy feed or an offline restore sees the
package and never the asset. Embedding ties the inventory to the artifact it
describes.

### Sign the packages with a code-signing certificate instead

Considered because author signing is the established NuGet mechanism and is
verifiable with `dotnet nuget verify`.

Rejected as the *primary* mechanism because it re-introduces exactly what trusted
publishing removes: a long-lived secret held by the workflow. It also attests
identity, not provenance — it says who signed, not which commit and which build
produced the bytes. It remains compatible with this decision if author signing is
ever wanted in addition.

## Consequences

### Positive

* No credential in the repository can publish a package.
* Every released artifact can be traced to a commit, a workflow and a runner, by
  anyone, without trusting this project.
* Every package carries its own component inventory.
* The release path is rehearsable, including its credential exchange, without
  publishing anything.

### Negative

* Publishing depends on a policy configured outside the repository, on nuget.org,
  for each package — including each new catalog package.
* A consumer verifying provenance must verify against the GitHub Release asset,
  not the nuget.org copy, which is a distinction that has to be documented.
* The release job needs three write scopes it would not otherwise need.

### Risks

* The trusted-publishing policy is missing or misconfigured for a new package, so
  the first real release of a catalog fails at the credential exchange.
  Mitigation: the OIDC login runs on dry runs too, so the policy can be validated
  before a tag is ever pushed.
* The SBOM tooling regresses and packages ship without an inventory. Mitigation:
  the manifest's presence is asserted on the produced package, and the assertion
  runs on every pull request through the rehearsal.
* The attestation is assumed to cover the nuget.org bytes and a verification
  against them fails, reading as tampering. Mitigation: stated here and in the
  workflow, next to the step that produces it.

## Follow-up Actions

* Create a trusted-publishing policy on nuget.org for each published package, and
  set the `NUGET_USER` secret.
* Dispatch a dry run before the first real release of every new package.
* Document, for consumers, that provenance is verified against the GitHub Release
  asset.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.md) — what a
  release publishes.
* [ADR-0007](0007-depend-across-trains-through-published-packages.md).
* `.github/workflows/release.yml`, `.github/workflows/release-dryrun.yml`,
  `tools/packaging/pack.sh`.
