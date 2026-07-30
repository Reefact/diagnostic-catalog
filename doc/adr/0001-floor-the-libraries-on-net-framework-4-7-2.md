# ADR-0001 | Floor the libraries' .NET Framework support at 4.7.2

**Status:** Proposed
**Proposed:** 2026-07-30
**Decision Makers:** Reefact

## Context

The shipped libraries target `netstandard2.0` and `net10.0`. `netstandard2.0` is
what makes the libraries consumable from .NET Framework at all; the formal .NET
Framework minimum for `netstandard2.0` is 4.6.1.

On .NET Framework versions before 4.7.2, `netstandard2.0` support relies on
retrofitted facades, additional package assets, and consumer-side binding
redirects. .NET Framework 4.7.2 is the first version that provides the relevant
facades in-box.

`netstandard2.0` is a **compile** contract. It constrains the API surface the
compiler accepts; it does not describe the runtime that loads the assembly. .NET
Framework and modern .NET differ in globalization (NLS versus ICU), in the
`netstandard.dll` facade that redirects type references, and in parts of the
reflection stack. A build that targets `netstandard2.0` therefore proves nothing
about behaviour on .NET Framework.

A rule identifier in a catalog is a contract: consumers reference it
symbolically, and a resolution that silently differs between runtimes is a defect
the consumer cannot see coming.

.NET Framework assemblies execute on Windows only; the CI Windows runner image
carries a .NET Framework runtime, and the Linux runners carry no Mono. The
`net472` targeting pack needed for compilation can be supplied by a NuGet
reference-assemblies package, so no runner-side installation is required.

A compatibility promise that is never executed cannot provide a trustworthy
support boundary.

## Decision

The shipped `netstandard2.0` libraries support .NET Framework 4.7.2 and later,
and that support is proven by executing their test suites on the .NET Framework
4.7.2 runtime rather than inferred from the compile target.

## Rationale

4.7.2 is the lowest version on which the libraries can be consumed without the
fragile compatibility plumbing earlier framework versions require. Below it, a
consumer's experience depends on binding redirects that this repository neither
writes nor can verify.

It is also the lowest version the repository can actually exercise. Aligning the
documented floor with a continuously executed runtime turns an aspirational
statement into an enforceable contract — which is the point, given that the
differences that matter here (globalization, the facade, reflection) are exactly
the ones a compile-only target hides.

The decision deliberately chooses the practical, testable boundary over the
theoretical `netstandard2.0` minimum. Supporting 4.6.1 would mean promising
behaviour on a runtime the repository has no way to run, for consumers on a
platform that has received no new major version in years.

Restricting the floor's execution to Windows is not a limitation the decision
imposes but a property of the platform; the ordinary build and the local inner
loop stay unaffected because the floor's inner build is gated and off by default.

The floor also constrains what a shipped library may use: a compiler marker the
.NET Framework base class library does not ship cannot be relied upon in shipped
code, since a consumer compiling against .NET Framework would have to supply it
themselves. Test code may polyfill it; product code may not.

## Alternatives Considered

### Keep the formal `netstandard2.0` minimum of 4.6.1

Considered because it is the widest claim the compile target formally allows, and
it costs nothing to write down.

Rejected because the claim would be unverified: the repository cannot execute on
4.6.1, and support there depends on consumer-side plumbing outside its control. A
support boundary nobody tests is a boundary that fails at the consumer's site.

### Floor at 4.6.2

Considered because it is serviced longer than 4.6.1 and is still meaningfully
wider than 4.7.2.

Rejected because it carries the same facade and binding-redirect constraints as
4.6.1 and is equally unexecutable with the supported test stack. It would buy a
wider claim of the same untested kind.

### Target only modern .NET and drop `netstandard2.0`

Considered because it removes the floor, the Windows job and the polyfill
question entirely, and simplifies every shipped project.

Rejected because it removes .NET Framework consumers from the addressable set.
Diagnostic rule catalogs describe analyzers that run against long-lived
codebases, which is precisely where .NET Framework still lives; excluding them
would narrow the library's reach for a maintenance saving that the gated inner
build already keeps small.

### Trust the compile target and skip the execution job

Considered because `netstandard2.0` already fails the build on an API the
platform does not have, which catches a real class of mistake at no cost.

Rejected because the failures this decision is about are not API-surface
failures. Culture-sensitive comparison, facade type resolution and reflection
behave differently at run time on a build the compiler accepted.

## Consequences

### Positive

* The .NET Framework support statement is executed on every pull request rather
  than asserted.
* Consumers on .NET Framework avoid the binding-redirect fragility of pre-4.7.2
  runtimes.
* The boundary is stable: .NET Framework is no longer receiving new major
  versions, so the floor is unlikely to move.

### Negative

* Consumers on .NET Framework 4.6.1 through 4.7.1 are outside the supported
  range.
* A Windows CI leg must be maintained in addition to the ordinary matrix.
* Shipped code cannot use language features whose compiler markers the .NET
  Framework base class library lacks.

### Risks

* A test project that exercises a shipped library forgets to join the floor, so
  the library is compiled for `netstandard2.0` but never executed on it.
  Mitigation: membership is declared by the project's own import and the CI job
  discovers importers rather than reading a list, so joining is a one-line edit
  in the project that needs it and cannot be forgotten in a second place.
* The floor job is configured but not enforced, so a red floor does not block a
  merge. Mitigation: ADR-0005 — the job must be a required status check.

## Follow-up Actions

* Keep the user-facing support statement at .NET Framework 4.7.2 or later.
* Make the framework-floor job a required status check when branch protection is
  configured.

## References

* [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.md) —
  why a configured check is not yet an enforced one.
* `build/Net472TestFloor.props` and `.github/workflows/ci.yml` — the mechanism.
* [CONTRIBUTING.md](../../CONTRIBUTING.md) — "The .NET Framework floor".
