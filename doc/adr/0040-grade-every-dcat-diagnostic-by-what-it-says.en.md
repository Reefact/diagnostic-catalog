# ADR-0040 | Grade every DCAT diagnostic by what it says, not by whom it addresses

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0040-grade-every-dcat-diagnostic-by-what-it-says.fr.md)

**Status:** Accepted
**Proposed:** 2026-08-06
**Accepted:** 2026-08-06
**Decision Makers:** Reefact

## Context

Thirteen `DCAT` ids ship, and their default severities were settled one at a time.

[ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md) promoted `DCAT0001`, `DCAT0006` and
`DCAT0007` to `Error` and held everything else at `Warning`, on an **audience** argument: those three
are what a consumer references a catalogue for, while `DCAT0002`–`DCAT0004`, `DCAT0011`–`DCAT0013`
address whoever *authors* one, which it described as "a different audience with a different build".
It held `DCAT0009` at `Warning` on a second argument — the check misses an identifier reached through
a constant, so promoting a rule that under-detects "fails builds unevenly".

[ADR-0039](0039-require-a-justification-on-every-suppression.en.md) then added `DCAT0014` and shipped
it at `Warning`, explicitly departing from ADR-0027: the rule was new, it reports on suppressions that
are otherwise entirely correct, and an error would have met a whole codebase at once. It recorded
"revisit the severity one release from now" as a follow-up.

`DCAT0015` arrived last and its severity is recorded nowhere but a comment in `Descriptors.cs`, which
reaches for ADR-0027's audience argument by analogy and adds that it is the one diagnostic reading a
fact from outside the compilation.

What each id actually reports, as facts:

* `DCAT0001` — the two arguments name two different rules; the suppression resolves and silences
  something other than what it claims.
* `DCAT0002`–`DCAT0004` — a type marked `[DiagnosticRule]` misses §8's structural contract: it is not
  a static non-generic class, or it exposes no public `const string Id`, or no public `const string
  Category`. A rule in that state publishes nothing a suppression can name.
* `DCAT0005` — the identifier carries a character C# forbids in a type name, so the type name is the
  identifier legalised and no closer spelling exists. There is nothing to repair.
* `DCAT0006` — literals that a catalogue in the compilation could replace with checked references.
* `DCAT0007` — a suppression half migrated: one argument is a reference, the other a value.
* `DCAT0009` — an `UnconditionalSuppressMessage` whose identifier is not an IL warning. ILLink's
  decoder discards it and Roslyn never reads the attribute, so the line has no effect anywhere.
* `DCAT0011` — a rule reaches its category without going through a constant declared in a
  `[DiagnosticCategory]` class. It folds to the right literal today.
* `DCAT0012` — a rule identifier written as a literal where `nameof` would not drift. It agrees with
  the type name today.
* `DCAT0013` — the identifier is a valid C# identifier, the type could have been named it, and was
  not. Every use site reads a name that does not say which diagnostic it suppresses; the reference
  compiles, resolves and works.
* `DCAT0014` — nothing records why the diagnostic is silenced. Presence is checked; content is never
  judged.
* `DCAT0015` — a catalogue package publishes rules and packs no opt-in, so referencing it checks
  nobody. The package does not do the one thing it is for, and the silence is indistinguishable from
  a codebase with nothing to report.

Three further facts bear on the timing. The analyzers have **never been published**: the `lib` train's
last release is `0.1.0`, which shipped attributes only, so no consumer has a build that a severity
change here can break. Every severity is overridable per id and per path through ordinary
`.editorconfig`. And a catalogue this repository publishes is *generated*, so `DCAT0002`–`DCAT0004`
and `DCAT0011`–`DCAT0013` cannot fire on one — the audience ADR-0027 reasoned about is a third-party
author writing a catalogue by hand, or anyone declaring rules for an internal ruleset.

## Decision

Every `DCAT` diagnostic's default severity is decided by **what the diagnostic says about the code** —
`Error` when this library's mandatory contract is unmet, when the suppression is incorrect or without
effect, or when the package does not deliver the behaviour it promises; `Warning` when the code works
today and is liable to drift, badly anchored, or misleading; `Info` for a legitimate exception nobody
can repair that is nevertheless worth making visible — and never by which audience the diagnostic
addresses.

## Rationale

**Audience is not a property of the defect.** ADR-0027 split on who reads the message, and the split
does not survive contact with what the messages say. A rule declaration missing its `Id` publishes a
catalogue member no suppression can ever name; a suppression naming that member wrongly resolves to
nothing. Neither works, and the previous model put one a tier below the other because the first is
read by a package author and the second by a package consumer. It also has no stable referent: a
catalogue's author *is* a consumer of the foundation, and the same project is frequently both.

**"Different build" was the load-bearing claim, and it is not true of the failure.** The author's
build being separate would matter if the defect stopped there. It does not: the catalogue publishes,
the constants ship, and the failure is delivered to everyone downstream in a form none of them can
see. `DCAT0015` is the sharpest case — a package whose entire purpose is to have its consumers
checked, shipping a version that checks nobody — and it was the one held quietest.

**Under-detection is not uncertainty.** ADR-0027's second argument, kept for `DCAT0009`, confuses a
false negative with a false positive. A form the analyzer does not recognise is a form it says nothing
about; it does not make the forms it does recognise less certain. `DCAT0009` reports a line that
*every* tool in the chain discards — the author believes a warning is silenced and it is not — and the
existence of a second shape nobody has taught the analyzer to see is no reason to soften that. Held
the other way round, the argument forbids any diagnostic from being an error until its coverage is
total, which no diagnostic's ever is.

**"It is new" is a reason to watch a rule, not a reason to grade it.** ADR-0039's departure was about
*timing* rather than about what `DCAT0014` says, and it said so — the follow-up asks for a revisit one
release later. That release has not happened, and the reason it can be answered now is the third fact
in Context: nothing is published, so the cost the record was protecting against — an existing consumer
meeting a wall of errors on the day they upgrade — does not exist. The encounter that remains is the
first build after *adopting* a catalogue, which `DCAT0006` already produces and which the adoption
guide already stages with one `.editorconfig` line. Adding `DCAT0014` to that same first build costs
one more id on the same line.

**A justification is part of the contract, not an ornament.** ADR-0039 established that a suppression
without a reason destroys information no tool can recover afterwards. A requirement whose default is
`Warning` is a requirement held by attention, which is precisely the argument ADR-0027 made for the
three ids it promoted. The two records reached opposite conclusions from the same premise, one release
apart.

**The warning tier keeps a real meaning, which is what makes the error tier mean anything.** What is
left at `Warning` is exactly what works and stays fragile: a category free to drift from its siblings
(`DCAT0011`), an identifier anchored to nothing (`DCAT0012`), and a name that misleads every reader of
the use site (`DCAT0013`). None of the three reports a line that fails to do its job, and `DCAT0013`
has no repair a tool can even point at — renaming the type and rewriting the identifier are both
changes only the author can choose between. A model where nearly everything is an error would be the
old default with the sign flipped, and would carry as little information.

**`Info` stays a single, stated exception.** `DCAT0005` reports a divergence its author could not have
avoided, and reports it only so the boundary `DCAT0013` enforces one step later is visible rather than
silent. That is a distinct thing from "less certain" or "less urgent", which is why it is one id and
not a tier things drift into.

## Alternatives Considered

### Keep the audience split and promote nothing

The status quo, and it is defensible on cost: a catalogue author writing rules by hand meets six ids
at once the day they upgrade.

Rejected because the cost is small and one-sided. Every catalogue this repository publishes is
generated and cannot trigger those ids; the population that meets them is people declaring rules by
hand, for whom the diagnostics are a checklist for a contract they have opted into and which is
otherwise undocumented at build time. And the split misgrades `DCAT0015`, whose whole subject is a
package that fails its consumers — the audience argument says "author", the defect says "everyone
downstream".

### Promote the structural rules and leave `DCAT0014` and `DCAT0015` a release behind

Would honour ADR-0039's "revisit one release from now" literally and gather adoption reports first.

Rejected because the release it defers to is `1.0.0` itself. Waiting means shipping the first published
version of these analyzers with a severity the record already describes as provisional, then changing
it in `1.1.0` — which *is* a change to a stranger's build, and the only version of this decision that
ever could be. The cheap moment to set a default is before anybody depends on it, and that moment is
now.

### Ship every diagnostic as an error

Simple, and consistent with "the guarantee is a property of the whole".

Rejected because it erases the distinction the severity is there to carry. `DCAT0013` reports a
declaration that works, that misleads, and that no tool can repair without making a choice belonging
to its author; failing a build over it would make the level meaningless and would push teams to
silence the whole category — the one outcome that costs more than any single severity.

### Express the model as a proprietary configuration surface

A "strict"/"lenient" profile a project could select, instead of per-id defaults.

Rejected on the grounds ADR-0027 already gave for `.editorconfig`: Roslyn's own severity keys are
per-id and per-path, they are what every team already knows, and a second format would have to
reimplement path scoping to be useful.

## Consequences

### Positive

* A new id is graded by asking one question about what it reports, rather than by looking at whichever
  neighbour it was declared beside. The model is stated once, in `Descriptors.cs` and in the guide.
* `DCAT0015` reaches the severity its subject deserves: a catalogue that would silently check nobody
  fails the build that would have published it.
* The one-line `.editorconfig` ramp already documented for `DCAT0006` now covers the whole first-build
  encounter, because `DCAT0014` lands in the same place instead of on a quieter level nobody reads.

### Negative

* Adopting a catalogue on an existing codebase now meets two error-severity ids on the first build
  rather than one. The adoption guide's downgrade line names both.
* A hand-written catalogue that has been building with six warnings stops building. The repair is
  mechanical for `DCAT0002`–`DCAT0004`, and the diagnostics guide states each one's fix.
* A catalogue that deliberately arranges the analyzer opt-in some other way must now say so — through
  `DiagnosticCatalogAnalyzerOptIn`, or an `.editorconfig` line — rather than living with a warning.
* **Promoting `DCAT0015` forced its trigger to be narrowed first.** MSBuild marks every project
  packable by default, so the classification behind it read a console application or an internal
  library declaring rules of its own as a catalogue publishing without its opt-in. As a warning that
  was noise; as an error it is a build failing over a package nobody would publish. The verdict is now
  computed while a package is actually being produced, which is both where the defect exists and where
  its message can be acted on — and it is a real reduction in when the diagnostic is seen, paid to
  make the severity honest.

### Risks

* **The tiers become a label rather than a test.** "Mandatory contract, incorrect, or without effect"
  is a sentence somebody can read into anything if they are not trying. `DefaultSeverityTests` pins the
  map so a change is deliberate, and the guide states the tier next to every id, but neither can force
  the question to be asked honestly.
* **Category-wide silencing.** A first build that fails on two ids invites
  `dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = none`, which turns everything off
  including the checks the team wanted. The configuration guide distinguishes that key from the
  per-id ramp and from `EnableDiagnosticCatalogAnalyzers`, which are three different behaviours and
  were previously described as if two of them answered the same need.

## Follow-up Actions

* If adoption reports show `DCAT0014` failing builds over lines whose reasons genuinely cannot be
  written, revisit — with the reports, not with this argument.
* When an id is added, state its tier in its descriptor comment and in `DefaultSeverityTests`; a new
  id with no stated tier is the failure this record exists to prevent.

## References

* [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md) — the audience split this record
  replaces, and the source of the `DCAT0002`–`DCAT0004`, `DCAT0011`–`DCAT0013` and `DCAT0009`
  severities.
* [ADR-0039](0039-require-a-justification-on-every-suppression.en.md) — the record that shipped
  `DCAT0014` as a warning and asked for exactly this revisit.
* [ADR-0038](0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.en.md) — the opt-in
  `DCAT0015` reports the absence of.
* [The `DCAT` diagnostics](../guide/diagnostics.en.md) — each id, its tier and its `.editorconfig` key.
* [Configuration](../guide/configuration.en.md) — the ramp, the category switch and the MSBuild
  property, and why they are three answers to three different questions.
