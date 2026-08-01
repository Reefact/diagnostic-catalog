# ADR-0020 | A catalogue is generated for C# only

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0020-a-catalogue-is-generated-for-c-sharp-only.fr.md)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

A catalogue's content is derived by *constructing* every analyzer an assembly declares and
reading the `DiagnosticDescriptor` instances it reports with (ADR-0009). Construction is
not incidental to the method — it is the method. Nothing else in the pipeline can answer
what a rule's category is, because nothing else is what the analyzer reports with.

Constructing an analyzer requires the Roslyn it derives from. The descriptor worker carries
`Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.CSharp.Workspaces`. It does not
carry `Microsoft.CodeAnalysis.VisualBasic`, and there is no Roslyn for F# at all — F# does
not use the platform.

Until this decision, `--language` accepted `cs`, `vb` and `fs`, and the manifest schema's
`enum` listed the same three. The specification (§25.4) stated that a Visual Basic
catalogue was therefore "a manifest entry rather than new code". Measured against
`Microsoft.CodeAnalysis.NetAnalyzers`, `--language vb` resolved the package, downloaded it,
selected two assemblies, constructed 265 analyzers and read 311 descriptors — and then
refused, because three types in `Microsoft.CodeAnalysis.VisualBasic.NetAnalyzers.dll` would
not load.

That refusal is §14.3 working correctly: a catalogue short of a rule is indistinguishable
from one whose vendor retired it, and the emitter would publish those three rules as
`[Obsolete]`, telling that vendor's users something false about their product. The defect
was not the refusal. It was that the tool advertised the run at all, and kept the promise
right up to the point of breaking it, at the cost of a download.

Visual Basic is the only language this would plausibly be extended to, and its trajectory is
settled. Microsoft has stated it does not plan to evolve Visual Basic as a language: no new
language features, a consumption-only approach where the runtime introduces something needing
syntax, and no extension to new workloads. The language remains supported and serviced — it is
not abandoned — but it is closed. Its analyzer population is correspondingly small and not
growing.

Separately, and not affected by this decision: reading a *package* has to recognise which
folders belong to which language, because layouts differ. `Microsoft.CodeAnalysis.NetAnalyzers`
puts most of its rules in a language-neutral assembly and only the language-specific ones
under `cs/` and `vb/`, so a C# read works by excluding the other languages rather than by
keeping its own folder.

## Decision

`dcat` generates catalogues for **C# only**. `--language` and the manifest's `language` key
accept `cs` and nothing else, and a request for another language is refused before any
package is resolved.

This rests on two legs, and either alone would hold it up. The tool **cannot** read another
language, because the worker carries only C# Roslyn. And this project **will not** carry
Visual Basic's, because the language is closed to new features and its analyzer population is
small and not growing.

## Rationale

The first leg is a consequence of ADR-0009 rather than a policy. Because content comes from
constructed analyzers, the set of languages a catalogue can be generated for is exactly the
set whose Roslyn the worker can load — no more, and not by choice. That much would be true
of any position on Visual Basic, including an enthusiastic one.

The second leg is the position, and it is a judgement rather than a consequence. Microsoft
has closed Visual Basic to new language features; the population of Visual Basic analyzers is
small and will not grow. Carrying a second Roslyn in every install of a published tool, and
maintaining a second construction path, is an ongoing cost against a shrinking return. This
project declines it.

Recording both matters, because they fail differently. If the second leg were the only one, a
reader might expect the option to work with a flag or a plugin; if the first were, they might
expect the restriction to lift the day somebody measures the package. Neither is true: the
mechanism explains why the tool does not do it today, and the judgement explains why it is not
scheduled to.

A future maintainer asking "why only C#?" should therefore find both — "the worker carries
only C# Roslyn" and "and we are not adding another, for these reasons" — rather than an
argument about which languages deserve tooling, which is not what this is.

Refusing up front rather than at the end follows from the same place. The tool's refusals
exist to be actionable; one delivered after a package has been downloaded and hundreds of
descriptors read is a promise the tool spent effort keeping before breaking. A caller who
asked for Visual Basic is not helped by discovering at the end that they could not have it,
and a pipeline distinguishes "this invocation is wrong, no retry fixes it" from "the run
could not finish" by exit code — which only works if the wrong invocation is recognised as
wrong.

Refusing at both entry points is not belt-and-braces. A manifest entry reaches the run
without passing through any option parsing, so validating the flag alone would leave the
same request true of the command line and false of the file that does the same thing —
which is exactly the shape of silent disagreement this repository exists to eliminate.

The languages a *package layout* is known to use are deliberately left alone, and still
include Visual Basic and F#. Knowing about a language and being able to read it are
different facts. A C# read has to recognise a `vb/` folder in order to exclude it; deriving
the exclusion set from the readable set would keep everything, and Visual Basic rules would
be absorbed into a C# catalogue — a failure with no symptom in the output, which is the
category of failure this repository exists to prevent.

## Alternatives Considered

### Ship Visual Basic Roslyn in the worker

Considered because it would make `--language vb` mean what it said, and because the
mechanism otherwise already works: the package layout is handled, the language filter is
correct, and the descriptors read up to the point of construction.

Rejected, and not merely deferred. `dcat` is a published tool whose size a consumer pays for
at install time; ADR-0019 accepted a growth from 6.4 MB to 7.7 MB, but for a capability every
user needs. This one would be paid by every user to serve a population that is small and, the
language being closed to new features, not going to grow. A second construction path would
also have to be kept working for as long as the tool exists, against upstream that is
explicitly not moving.

The trade is not close enough to leave open. What would reopen it is the premise changing —
Visual Basic resuming development, or a concrete demand this project is willing to serve — not
a measurement of the package, which would only price a cost already judged not worth paying.

### Leave the option accepted and let the run refuse

Considered because the refusal already happens, is correct, and explains itself — nothing is
silently wrong today.

Rejected because "correct at the end" is not the same as honest at the start. The option's
help text, and the schema's `enum`, are read by people deciding what to attempt; both stated
a capability the tool did not have. An editor completing `"language": "vb"` from a schema
that lists it is the drift this repository refuses, one step removed: the artifact that
exists to be checked was itself wrong.

### Remove `--language` and the manifest key entirely

Considered because a single-valued option is a knob that turns nowhere, and `dcat` has no
published version, so removing it now would cost nothing.

Rejected because the key is the place this decision is expressed to a user, and because the
filtering it names is load-bearing whether or not it is selectable. Removing it would hide
the fact that a language was chosen at all, and would have to be reintroduced — as a
breaking change, by then — the day the worker carries a second Roslyn.

## Consequences

### Positive

* A language the tool cannot read is refused as a usage error, before a package is
  resolved, with the reason and the remedy.
* The command line and the manifest answer the same request the same way.
* The schema reports it in an editor, before the tool is run at all.
* The specification no longer claims a Visual Basic catalogue is a manifest entry.

### Negative

* Visual Basic analyzers cannot be catalogued by this tool, and this is not a gap awaiting
  work. For a house-rules VB analyzer, the method recorded in ADR-0009 is unavailable here,
  and no workaround reaches it — the honest answer to such a user is that this tool is not
  for them.
* `--language` accepts one value, which reads as a knob that turns nowhere. It is kept
  because the key is where this decision is expressed to a user, and because the filtering it
  names is load-bearing whether or not it is selectable.

### Risks

* Stated as a judgement, the position can read as a slight against Visual Basic. It is not
  one: the language is supported and serviced, and nothing here says otherwise. What it says
  is that a closed language's small analyzer population does not justify a second Roslyn in
  every install of this tool.
* The two legs can be mistaken for one, leaving a reader to think the restriction lifts as
  soon as somebody packages the dependency. `CatalogLanguages` states the mechanism in place
  and points here for the judgement, so both are reachable from the code.

## Follow-up Actions

* None. This is a settled position, not a deferred task. Reopening it needs the premise to
  change — Visual Basic resuming development, or a demand this project decides to serve —
  rather than a measurement.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.en.md) — content comes
  from constructed analyzers, which is why the worker's Roslyn decides the language set.
* [ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.en.md) — why a rule missing from a
  read is published as retired, and therefore why an incomplete read must refuse.
* [ADR-0017](0017-publish-the-generator-as-a-cli-on-its-own-release-train.en.md) — the tool
  whose package size the first alternative weighs.
* [ADR-0019](0019-resolve-packages-through-the-users-own-nuget-configuration.en.md) — the
  precedent for accepting package growth when the capability serves every user.
* `doc/specification.en.md` §14.3 and §25.4 — the refusal this relies on, and the claim it
  corrects.
