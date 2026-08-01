# ADR-0019 | Resolve packages through the user's own NuGet configuration

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0019-resolve-packages-through-the-users-own-nuget-configuration.fr.md)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

This repository publishes `dcat` (ADR-0017), and the reason it publishes it is stated
there: the method recorded in ADR-0009 — derive a catalogue from the descriptors the
analyzers declare, never from documentation — is worth having for anyone who ships
analyzers, not only for the three vendors mirrored here.

Until this decision, the tool reached exactly one feed. `api.nuget.org` was written into
the source as a constant, and the flat-container protocol was called by hand over HTTP.

Analyzers are frequently *not* public. A company's house rules ship as a package on a
feed only that company can reach, and reaching such a feed is what the tool could not do:
no configured source was consulted, and no credential was ever sent.

NuGet's configuration is not a single file but a hierarchy — machine-level, user-level,
and every folder from the working directory upwards — and `dotnet restore` on the same
machine already resolves against it. Credentials declared in it come in several kinds.
Two of them, values encrypted at rest and those supplied by a credential provider, are
not readable from the configuration files at all: obtaining them means asking NuGet's own
client.

The repository pins every dependency centrally and asks that a new one carry a clear
reason (`CLAUDE.md`, "Change guidelines"). `dcat` is a published artifact whose size a
consumer pays for once at install time; before this decision its package was 6.4 MB.

## Decision

`dcat` resolves and downloads packages through the user's own NuGet configuration — the
sources and credentials their machine declares — never through a feed this repository
chose for them.

## Rationale

The decision follows from what publishing the tool was *for*. ADR-0017 argues that the
method should be available to anyone shipping analyzers; a tool that reaches one public
feed is available only to people whose analyzers are already public, which is close to
the opposite of the population that most needs a catalogue of house rules. The gap was
not a missing convenience, it was the argument for publishing being quietly hollowed out.

Choosing the feed on the user's behalf is also a decision the tool has no standing to
make. Their machine already answers "where do packages come from" for every other tool in
the .NET toolchain, and it answers it in a place they control and their organisation
audits. A tool that disagrees with `dotnet restore` on the same machine is surprising in
the specific way that costs the most time: it fails where everything else succeeds, and
the reason is invisible because the configuration it ignored is the one the user was
looking at.

Credentials are what make this a decision about a dependency rather than only about
behaviour. Honouring a configuration whose secrets cannot be read outside NuGet's client
is not something a hand-rolled implementation can do partially well — it would be correct
for the plaintext case and quietly wrong for the encrypted and provider-supplied ones. The
failure that produces is the worst available shape: the package appears not to exist, on a
feed the user knows holds it. Deciding to honour the configuration therefore decides
against hand-rolling it; the two are not independent choices.

The cost accepted is a large dependency inside a published artifact, and a tool whose
behaviour now depends on the machine it runs on. The second is not a defect to be
mitigated but the decision itself: "resolve as this machine is configured to resolve" is
precisely a statement that two differently configured machines will resolve differently.
What it obliges in exchange is that a failure to find a package must say which sources
were consulted, because otherwise the user cannot see the configuration the tool used.

## Alternatives Considered

### Keep the hardcoded feed and add a source URL option

Considered because it needs no new dependency and covers the simplest private-feed case:
an internal server with anonymous read access.

Rejected because a private feed is usually private, which is to say credentialed, and this
covers exactly the feeds that are not. It also leaves the tool ignoring the sources the
machine declares, so it would keep disagreeing with `dotnet restore` on the same machine —
the surprise this decision exists to remove — while appearing to have addressed it.

### Read the configuration by hand

Considered because it avoids the dependency entirely and would handle the configuration
hierarchy and plaintext credentials, which is most of the mechanism.

Rejected because the part it cannot handle is the part that fails silently. Encrypted
credentials and credential providers are not readable outside NuGet's client, so this
implementation would work for some users and, for others, report that a package does not
exist on a feed that holds it. A tool that is correct for a subset of its users and
misleading for the rest is worse than one that is honestly limited, because nothing
distinguishes the two cases from the outside.

### Require the user to fetch the package themselves

Considered because the tool already accepts a package file on disk, so this costs nothing
to build.

Rejected because it moves the work to the user on every regeneration, and a scheduled job
— the case the catalogues here exist to serve — cannot do it at all. It answers "how do I
read this package once" and not "how does my catalogue stay current".

## Consequences

### Positive

* A package on a private feed is read with no extra flag: the tool uses what the machine
  already declares.
* The tool agrees with `dotnet restore` on the same machine, including where a repository's
  own configuration overrides the user's.
* Version selection becomes SemVer ordering rather than a position in a feed's answer,
  which the previous implementation only got right because one feed happened to sort it.

### Negative

* A large dependency travels inside a published artifact: the tool package grows from
  6.4 MB to 7.7 MB.
* Behaviour depends on machine configuration, so reproducing a resolution failure requires
  the configuration and not only the command line.

### Risks

* The client is a broad surface with its own advisory history — the version this decision
  was taken at was chosen over an earlier one carrying `GHSA-g4vj-cjjj-v7hg`. NuGet audit
  warnings are deliberately warnings rather than errors in this repository, so a future
  advisory will not fail a build; noticing it is a review habit rather than a gate.
* A configuration that resolves on a maintainer's machine and not on a runner will look
  like a tool failure. Reporting the sources consulted is what keeps that diagnosable, and
  is therefore load-bearing rather than cosmetic.

## Follow-up Actions

* None binding. Watch the client's advisories when its version is bumped, since the audit
  warnings that would otherwise announce them are not promoted to errors here.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.en.md) — the method whose
  availability is the reason the tool is published at all.
* [ADR-0017](0017-publish-the-generator-as-a-cli-on-its-own-release-train.en.md) — the
  decision to publish, whose argument this one carries out.
* [`CLAUDE.md`](../../CLAUDE.md), "Change guidelines" — the requirement that a new
  dependency carry a clear reason.
