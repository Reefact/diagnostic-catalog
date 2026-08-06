# ADR-0037 | Require a justification on every suppression

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0037-require-a-justification-on-every-suppression.fr.md)

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

**`DCAT0006` does not cover the literals, and cannot be made to.** It reports a literal pair only
when a rule the compilation can see matches it, deliberately: firing on unmatched literals would
report every hand-written suppression in a codebase that has adopted no catalogue, which is also why
`DCAT0008` was specified as opt-in. The consequence, measured on a project referencing the analyzers
and one vendor catalogue, is a shape reported by nothing at all:

| Suppression | Reported by |
| --- | --- |
| a catalogue reference, no justification | this decision, and nothing before it |
| a literal naming a rule the catalogue knows | `DCAT0006` — the migration, not the reason |
| a literal naming a rule no catalogue knows | **nothing** |

The third row is the one that decides this record. A restriction to catalogue references would leave
the requirement absent exactly where a codebase has adopted the least, which is where suppressions
are least likely to have been thought about.

**The requirement exists in the ecosystem, once.** StyleCop's `SA1404`, *Code analysis suppression
should have justification*, has covered it for years, on every suppression. Reaching it means taking
`StyleCop.Analyzers` and its several hundred style rules, which is a decision about a codebase's
whole style, not about its suppressions.

**Two measurements bound the cost.** The usage suite — 219 suppression attributes written to look
like code a consumer would write, of which some eighteen are written with a leading literal, and
whose build IS the assertion that the analyzers stay quiet on it — produced exactly **two** reports,
both on fixtures that existed to pin the previous behaviour, and **the same two whether the rule
covers catalogue references alone or every suppression**. Broadening it cost nothing measurable on
that corpus. And the check itself is cheap: it reads one named argument off an attribute the analyzer
has already bound.

**Two existing decisions constrain the shape.** [ADR-0018](0018-a-code-fix-never-decides-what-only-the-author-can.en.md)
forbids a code fix from deciding what only the author can. [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md)
ships the use-site diagnostics as errors, on the argument that referencing a catalogue package is
itself the statement of intent — an argument made about suppressions that are *wrong*, not about
suppressions that are correct and terse.

## Decision

Every suppression this package analyses must carry a non-blank `Justification`, whether its pair
references a catalogue rule or is written entirely in literals, checked by `DCAT0014` for presence
alone and never for content.

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

**Every suppression, because this is the one question that does not depend on the catalogue.** Every
other diagnostic here needs to resolve a rule to have anything to say; this one needs only the
attribute. A literal suppression silences a warning exactly as a reference does and says exactly as
little about why, so a rule that asked only the migrated ones would be asking on the basis of
something irrelevant to what it checks. The flooding argument that keeps `DCAT0009` and `DCAT0008`
off literals does not transfer: those two need an index of known rules to say anything true, and go
wrong — or say nothing — where the catalogue is absent. This one is exactly as true, and exactly as
actionable, on a literal.

**The cost of covering the literals was measured rather than argued.** The corpus written to look
like consumer code reports the same two sites under both readings of the rule. That is not proof that
no codebase will meet more, and the guides say plainly that adoption reports every unjustified
suppression at once — but the shape that was feared, a wave of reports arriving from code the package
had nothing to do with, did not appear where it would have been visible.

**It overlaps `SA1404`, and that is the honest description.** The two now ask the same question, and
a codebase running both will see both. What differs is the price of admission: `SA1404` arrives with
several hundred style rules attached, and this arrives with the package a team already took for its
suppressions. Nobody is asked to install anything to get it, and a project that wants only one of the
two silences the other in one `.editorconfig` line. A duplicated question is a smaller cost than a
question nobody is in a position to ask.

**A warning rather than an error, deliberately departing from ADR-0027's default.** That record's
argument is that a project referencing a catalogue has decided its suppressions are references, so a
suppression that is *not one* should fail the build. This rule reports something else: a suppression
that resolves correctly and is terse. It also now reports on adoption rather than after migration,
which makes the error default costlier still — a codebase referencing the analyzers for the first
time would meet a build failure on every unjustified suppression it has. Warning keeps that
encounter readable, and the severity is one `.editorconfig` line away for anyone who wants it now.
This is the same reasoning that kept `DCAT0013` a warning.

**No code fix, by ADR-0018 exactly.** The justification is the one part of the attribute that cannot
be read off the code. A fix could only insert a placeholder, and the rule already refuses the
platform's own placeholder as an answer.

## Alternatives Considered

### Restrict it to suppressions that reference a catalogue rule

This was the first shape of the rule, and the argument for it is real: it keeps the diagnostic
addressed to projects that opted into the catalogue, it mirrors the line `DCAT0009` already draws,
and it makes `DCAT0006` and this one hand over cleanly rather than reporting the same line twice.

Rejected because of the third row of the table in Context. A literal naming a rule no referenced
catalogue knows is reported by nothing, and the restriction makes that permanent — the requirement
would be absent precisely where a codebase has adopted the least. The hand-over it buys is
cosmetic: `DCAT0006` and `DCAT0014` report different faults on the same line and both survive the
other's fix, so keeping them apart tidies the output of one build and leaves a hole in every project
that never migrates. The measurement removed the remaining objection — the broader rule cost nothing
on the corpus where the cost would have shown.

### Point at `SA1404` and ship nothing

The requirement already exists, implemented and maintained by StyleCop, and adding a rule that asks
the same question to every consumer's build is a real cost.

Rejected because reaching it costs `StyleCop.Analyzers` in full. A team that has adopted a catalogue
to make its suppressions checkable has said nothing about wanting several hundred style rules, and
telling them the missing half of the contract is available in another package is telling them the
library stops one argument short of its own thesis.

### Ship it as an error, with the other use-site rules

ADR-0027's argument is general, and a justification that is optional in practice is a justification
half the codebase will not write.

Rejected because that record's argument is about suppressions that are wrong. Every existing use-site
error reports a line that does not do what it looks like; this reports a line that does exactly what
it looks like and says nothing about why. Now that the rule covers every suppression, an error
default would also make the first build after referencing the package fail on code the package has
never had an opinion about, which is the worst possible introduction to it. The door stays open: a
release from now, with the rule's false-positive shapes known, promoting it is a two-line change and
its own decision.

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

* the second half of a suppression's contract is checked, on every suppression, by the package a team
  already has;
* the one shape nothing reported — a literal naming a rule no catalogue knows — is covered, and it is
  the shape a codebase that has adopted least has most of;
* the check is the weakest one that closes the gap, so the non-goals about content stay intact and
  stay honest;
* the rule needs nothing new from a consumer — no package, no configuration, no attribute — and its
  answer does not depend on which catalogues are referenced.

### Negative

* referencing the analyzers now reports every unjustified suppression in a codebase at once, and not
  only the migrated ones;
* a line being migrated reports twice, `DCAT0006` for the pair and `DCAT0014` for the reason, until
  both are answered;
* it asks the same question as `SA1404` for a codebase running both;
* two usage-suite fixtures that documented the previous behaviour had to change, and the suppression
  guide's sentence about the analyzers having no opinion had to be qualified.

### Risks

* **Adoption noise.** The corpus says the cost is small; a codebase with a thousand undocumented
  suppressions will say otherwise. The severity is a warning and the adoption guide names the line
  that lowers it, which is the mitigation rather than a hope.
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
* [Adopting a catalogue](../guide/adopting-a-catalogue.en.md) — where the cost of the broad trigger
  is met, and the line that lowers it for the length of a migration.
* [Specification §11.14](../specification.en.md) — the trigger condition, and §5, where the
  non-goals about content now say what this decision does and does not touch.
