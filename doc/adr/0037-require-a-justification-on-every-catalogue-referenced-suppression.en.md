# ADR-0037 | Require a justification on every catalogue-referenced suppression

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0037-require-a-justification-on-every-catalogue-referenced-suppression.fr.md)

**Status:** Proposed
**Proposed:** 2026-08-06
**Decision Makers:** Reefact

## Context

This library exists because a suppression is a contract written in strings nothing checks. Eleven
diagnostics now cover one half of that contract — four at the use site, seven on the declaration —
and all of them answer the same question: **which** diagnostic a line silences. Once they are
satisfied the compiler carries it from then on, and a renamed rule breaks the build instead of
silently suppressing nothing.

**The other half is `Justification`, and nothing anywhere requires it.**
`SuppressMessageAttribute` and `UnconditionalSuppressMessageAttribute` both declare the property;
both leave it optional. A suppression compiles, resolves and silences its warning with the property
absent, and no diagnostic in the platform says a word. What is lost when it is absent cannot be
recovered afterwards by anybody: the warning is gone, so there is nothing left to re-examine, and the
reason it was acceptable existed only in the head of whoever wrote the attribute. A reader six months
later cannot distinguish a considered suppression from a pasted one.

**The specification rules out two neighbouring things, and neither is this.** §5 lists among the
non-goals "assess the semantic quality of a justification" and "generate a justification
automatically"; §24 lists "intelligent justification validation" among what 1.0 deliberately omits.
All three are about the CONTENT of a justification. None of them is about its presence.

**The documentation went further than the specification did.** The suppression guide told readers
that this library "has no opinion on whether suppressing that rule *there* was a good idea. That
judgement stays yours, which is what `Justification` is for", and the usage suite carried a fixture —
`DocumentedForms.NoJustification` — whose comment stated that "nothing requires the property to be
present, and its absence is not a defect these analyzers know about". That was an accurate reading of
the analyzers as they stood, written where a reader would take it for a decision.

**The requirement exists in the ecosystem, once.** StyleCop's `SA1404`, *Code analysis suppression
should have justification*, has covered it for years, on every suppression including those written
entirely in literals. Reaching it means taking `StyleCop.Analyzers` and its several hundred style
rules, which is a decision about a codebase's whole style, not about its suppressions.

**Two measurements bound the cost.** The usage suite — 219 suppression attributes written to look
like code a consumer would write, whose build IS the assertion that the analyzers stay quiet on it —
produced exactly **two** reports under the new rule, both on fixtures that existed to pin the old
behaviour. And the check itself is cheap: it reads one named argument off an attribute the analyzer
has already bound.

**Two existing decisions constrain the shape.** [ADR-0018](0018-a-code-fix-never-decides-what-only-the-author-can.en.md)
forbids a code fix from deciding what only the author can. [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md)
ships the use-site diagnostics as errors, on the argument that referencing a catalogue package is
itself the statement of intent — an argument made about suppressions that are *wrong*, not about
suppressions that are correct and terse.

## Decision

A suppression whose category or identifier references a catalogue rule must carry a non-blank
`Justification`, checked by `DCAT0014` for presence alone and never for content.

## Rationale

**The gap is the same gap the library was built to close, one argument to the right.** Every existing
use-site diagnostic makes the compiler responsible for something a reader used to have to take on
trust. `Justification` is the last argument of the attribute that nothing checks, and it is the only
one whose loss is unrecoverable: a wrong identifier can be found by rereading the code, an absent
reason cannot be found at all. Leaving it out while checking everything around it is a boundary that
holds only because the specification never asked the question.

**Presence and quality are different questions, and only one of them is out of reach.** Judging what
a justification says means judging a suppression's legitimacy, which §5 rules out for good reasons: a
tool scoring prose is wrong in both directions, blessing fluent nonsense and rejecting a good reason
tersely put. Whether the property is there at all is a structural fact about the attribute — the same
kind of fact as whether the identifier resolves — and it is decided by reading a string's length. The
non-goals survive this decision intact, and the specification now says so where it lists them.

**The audience restriction keeps the rule addressed to the people who opted in.** Reporting on a
suppression written entirely in literals would fire on every hand-written suppression in a project
that has referenced the analyzers without adopting a catalogue — the flooding argument `DCAT0009`
already makes for staying off literals, and `DCAT0008` was left opt-in for. Restricting it to
suppressions that reference a rule also makes the hand-over clean rather than overlapping: `DCAT0006`
asks for the migration, and this takes the line once the migration is done.

**It is complementary to `SA1404`, not a duplicate of it.** The two differ in what they cost and in
what they cover. `SA1404` covers every suppression and costs a whole style-rule package; this covers
the suppressions a project has already declared to be catalogue references and costs nothing beyond
the package it already has. A codebase running both sees `SA1404` first on the literals and this one
after the migration, which is two rules agreeing rather than two rules arguing.

**A warning rather than an error, deliberately departing from ADR-0027's default.** That record's
argument is that a project referencing a catalogue has decided its suppressions are references, so a
suppression that is *not one* should fail the build. This rule reports something else: a suppression
that is a reference, resolves correctly, and is terse. Failing every such build the day the package
updates would punish projects that adopted a catalogue before the rule existed, over lines nothing
had ever asked about. The severity is one `.editorconfig` line away for anyone who wants it now,
which is the escalation reporting it at all provides — the same reasoning that kept `DCAT0013` a
warning.

**No code fix, by ADR-0018 exactly.** The justification is the one part of the attribute that cannot
be read off the code. A fix could only insert a placeholder, and the rule already refuses the
platform's own placeholder as an answer.

## Alternatives Considered

### Point at `SA1404` and ship nothing

The requirement already exists, implemented and maintained by StyleCop, and adding a rule to every
consumer's build to duplicate an existing one is a real cost.

Rejected because reaching it costs `StyleCop.Analyzers` in full. A team that has adopted a catalogue
to make its suppressions checkable has said nothing about wanting several hundred style rules, and
telling them the missing half of the contract is available in another package is telling them the
library stops one argument short of its own thesis. The two coexist for anyone who wants both, which
is what makes shipping this one cheap rather than redundant.

### Report on every suppression, literals included

It is what a reader asking "does something require the justification?" expects, and it is what
`SA1404` does.

Rejected on the flooding argument this repository has already made twice — for `DCAT0009`, which
stays off literals, and for `DCAT0008`, which was left opt-in because a project referencing analyzers
without a matching catalogue would otherwise be swamped. Referencing the analyzers must not turn
every pre-existing hand-written suppression into a warning about a property nobody had been asked
for. The coverage lost is smaller than it looks: `DCAT0006` reports the literals first, and this
takes over as they are converted.

### Ship it as an error, with the other use-site rules

ADR-0027's argument is general, and a justification that is optional in practice is a justification
half the codebase will not write.

Rejected because that record's argument is about suppressions that are wrong. Every existing use-site
error reports a line that does not do what it looks like; this reports a line that does exactly what
it looks like and says nothing about why. Both readings of "adopting a catalogue is a statement of
intent" do not survive being applied to correct code — the first build after a package update is not
the moment to discover several hundred of them. The door stays open: a release from now, with the
rule's false-positive shapes known, promoting it is a two-line change and its own decision.

### Judge the justification, not merely require it

A required-but-empty justification invites `"x"`, and a rule that accepts `"x"` can be read as
theatre.

Rejected, and firmly. §5 rules it out, §24 rules out the intelligent version of it, and both are
right: minimum lengths and forbidden-word lists reject terse good reasons and accept fluent bad ones,
and every project would end up configuring around the check rather than using it. What survives is a
narrow exception that is not a judgement of content — the IDE's own `<Pending>` placeholder, matched
exactly, because it is one tool's literal token for "not written yet" rather than an opinion about
prose. Whether `"x"` is a real reason is a question for a code review, which is where it belongs.

## Consequences

### Positive

* the second half of a suppression's contract is checked, by the same package that checks the first;
* the check is the weakest one that closes the gap, so the non-goals about content stay intact and
  stay honest;
* a codebase migrating to a catalogue is prompted for the reason at the moment the code, and the
  person who suppressed it, are still in front of whoever is converting;
* the rule needs nothing new from a consumer — no package, no configuration, no attribute.

### Negative

* a project that adopted a catalogue before this rule sees new warnings on correct code, in numbers
  proportional to how little it wrote justifications;
* two usage-suite fixtures that documented the previous behaviour had to change, and the suppression
  guide's sentence about the analyzers having no opinion had to be qualified;
* one more diagnostic on a page a reader already has to hold in their head.

### Risks

* **`"x"` as an answer.** Nothing stops a codebase from discharging the rule with a word. Accepted:
  the alternative is judging prose, and a review catches what a length cannot.
* **The placeholder exception growing.** `<Pending>` is matched exactly today; a future contributor
  reading it as licence to add `"TBD"`, `"n/a"` and their friends would turn a marker check into
  prose judgement one string at a time. The boundary is stated in the descriptor, the guide and the
  tests, in those words, for that reason.
* **Promotion pressure.** A warning that is easy to satisfy invites being raised to an error before
  its false-positive shapes are known. The severity table and this record say what would have to be
  true first.

## Follow-up Actions

* revisit the severity one release from now, against real adoption reports rather than against this
  argument;
* if `Scope`/`Target` validation (§25.1) ships, review whether the two use-site checks on the
  attribute's *properties* should be described together in the guide rather than separately.

## References

* [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md) — the severity default this record
  deliberately departs from, and the argument it turns on.
* [ADR-0018](0018-a-code-fix-never-decides-what-only-the-author-can.en.md) — why no fix is offered.
* [The `DCAT` diagnostics](../guide/diagnostics.en.md) — `DCAT0014` as a consumer meets it.
* [Specification §11.14](../specification.en.md) — the trigger condition, and §5, where the
  non-goals about content now say what this decision does and does not touch.
