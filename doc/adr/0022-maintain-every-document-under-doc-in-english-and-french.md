# ADR-0022 | Maintain every document under `doc/` in English and French

**Status:** Proposed
**Proposed:** 2026-07-31
**Decision Makers:** Reefact

## Context

The repository's stated language is English — source, comments, commit messages,
branch names, pull request titles, issues — with one documented exception:
[`doc/specification.en.md`](../specification.en.md) is accompanied by
[`doc/specification.fr.md`](../specification.fr.md), and the English version is
declared canonical where the two disagree. That pair has been maintained by hand
since it was written; it is 1940 and 2003 lines respectively.

The reader-facing documentation today is the root `README.md` (310 lines) and
three guides under [`doc/guide/`](../guide/), all English. The decision records
under [`doc/adr/`](.) are nineteen documents, all English. Seven package READMEs
under `src/` are shipped inside the `.nupkg` as `<PackageReadmeFile>` and
rendered by nuget.org, which offers no language switch, resolves no relative
link, and renders whatever single file the package declares.

Reefact is a French-speaking organisation, and its sibling project
[`first-class-errors`](https://github.com/Reefact/first-class-errors) maintains
a complete bilingual reader-facing set — roughly thirty documents in `.en.md` /
`.fr.md` pairs, each carrying a language banner under its title and a
previous/next navigation footer.

Nothing in this repository's continuous integration reads Markdown. The lint
workflow covers shell and workflow YAML; the .NET build covers C#. A broken
relative link, a page whose translation was never written, and a page nothing
links to are all invisible to every check that runs today.

The documentation makes claims about compiled artifacts: which `DCAT` ids the
analyzers report, which options `dcat` accepts, which release a catalogue
mirrors. Three of those claims are already checked — `DocumentedMirrorTests` and
`DocumentedSiblingsTests` read the package READMEs against the generated
`CatalogSourceAttribute` and against the release-train declarations — and the
rest are not.

The subject matter is a category of defect that produces no symptom: a
suppression whose category is wrong compiles, runs, and reports nothing, forever.
That is the failure this repository was written to remove, and it is the standard
the repository applies to itself — a fix ships with a test seen failing, a
catalogue is regenerated and compared on every pull request, a release is
rehearsed before it is cut.

## Decision

Every document under `doc/` is maintained in English and French, page for page,
with the English version canonical; the package READMEs under `src/` stay
English-only.

## Rationale

The exception already exists and has held. The specification has been maintained
as a pair, by hand, at four thousand lines, with English declared canonical — so
the question is not whether this repository can sustain a bilingual document, but
whether the rule that permits exactly one of them still describes what the
repository does. Extending the policy to the rest of `doc/` records the practice
instead of leaving each new document to argue for itself.

The audience argues for it independently of the maintainer's own language. This
library is adopted by teams, not by individuals: the guides that matter most —
adopting a catalogue across an existing codebase, what a `DCAT` diagnostic means,
why deleting a constant breaks a consumer's build — are read by whoever is asked
to migrate the code, not by whoever chose the library. A reader who half-follows
the argument for the category axis is a reader who writes the category by hand,
which is the outcome the whole design exists to prevent. Translation here buys
comprehension of a subtle point, not convenience.

Naming English canonical is what keeps the pair from becoming two libraries'
worth of documentation. A French page is a translation: it may be behind, and
when it is, the reader is told which document wins. The alternative — two
independent documents — has no such fallback, and its failure mode is two pages
that describe different products with nothing saying which is true.

The package READMEs are excluded because their renderer decides. nuget.org shows
one file per package, with no switch between languages and no working relative
link; a bilingual page there would either duplicate every section inside one
document or link to a translation the reader cannot reach. Their audience is also
different in kind — someone evaluating a package from a search result, not
someone learning the model — and they are already constrained by two tests, which
is where their obligations are recorded.

Enforcing the pair with a test is the part that makes the decision survivable.
Every argument above fails the moment the French half lags, and lagging is the
normal outcome of a policy that rests on remembering: the page that is hardest to
translate is the page that most needed translating, and it is the one that gets
deferred. Nothing else in this repository is left to attention — the coding rules
are checked twice, the catalogue is regenerated and compared, the release is
rehearsed — and a documentation set is the artifact where an omission is least
visible, because no reader who cannot read the page is in a position to report
that it is missing. A check that refuses a page without its sibling converts that
silent gap into a red build, which is the same move as everything else here.

The same reasoning extends past parity to the claims the documentation makes
about the code. A `DCAT` id documented after it was removed, and an id shipped
with no page describing it, are both mistakes that no reader can distinguish from
a correct document; checking a page against the descriptors it describes is
[ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.md) applied to
prose — compare against what the analyzer actually declares, never against
another document.

The decision records are included rather than exempted, and that is the
uncomfortable half. An ADR is a historical log: it is never edited in place, so
its translation is written once and then left alone, which is the cheapest
possible ongoing cost and the highest possible one-off cost — nineteen documents,
several of them long. What settles it is that the ADRs are where the reasoning
lives. A reader who is told that a category is a published contract, and who
wants to know why a catalogue never renames a member, is sent to
[ADR-0012](0012-a-catalogue-never-renames-a-member-it-published.md); sending them
to a document they cannot read makes the guide that links it a dead end. A
`doc/` folder where the guides are bilingual and the reasoning behind them is not
teaches the reader that the reasoning is not for them.

## Alternatives Considered

### Keep the documentation English-only, as the current policy states

English is the working language of .NET tooling: the analyzer vendors this
library mirrors publish in English, the platform documentation is English, and a
reader already running SonarAnalyzer or StyleCop reads their rule pages in
English every day. Machine translation is now good enough for technical prose, so
a French reader is not shut out. The policy would need no change and no document
would need writing.

Rejected because it makes the wrong reader carry the cost. Machine translation
does well on description and badly on the exact points this documentation exists
to make — that one argument is read and the other is not, that a constant is
inlined into the consumer's own assembly at the consumer's compile time, that
correcting the category changes nothing about what is suppressed. Those are the
sentences a reader has to follow precisely, and they are the ones a translation
engine flattens. It also leaves the reasoning where it already was: the
specification is bilingual because someone judged that its argument had to land,
and the guides make the same argument to more people.

### Maintain French pages as independent documents rather than translations

A French document written for its own reader can pick its own examples, its own
order, and its own length. It never lags a translation because it is not one,
and it can be shorter — the objection to translation is usually that it
reproduces English structure into a language that would have organised the
material differently.

Rejected because it doubles the surface that has to stay true about the same
product. Every behavioural claim would then exist twice with no stated authority
between them, so a corrected claim in one language leaves the other one wrong and
nothing says which to believe. The existing specification pair already answers
this: it declares English canonical precisely so that a divergence has a
resolution.

### Generate the French from the English in the build

A translation step in continuous integration would keep the pair in sync by
construction, remove the parity question, and make an untranslated page
impossible.

Rejected because it puts a network service on the path of a document that makes
precise claims, with nobody reading its output before it ships. It also
contradicts the reason the nightly catalogue workflow opens a pull request rather
than merging one: automation finds the change, a human accepts it. A generated
translation of a paragraph explaining why a wrong category has no symptom would
be published without any French reader having seen it, which is the same
unreviewed-contract move that decision refused.

### Extend the bilingual policy to the guides but exempt the decision records

The ADRs are internal: their audience is maintainers and contributors, who work
in the repository's language by policy, and they are the longest documents per
unit of reader. Exempting them would cut the one-off cost roughly in half and
touch nothing that a consumer of the packages reads.

Rejected because the guides link into them. The reasoning is deliberately not
duplicated in the guides — the ADRs exist so it is recorded once — so a bilingual
guide whose "why" is English-only relocates the gap rather than removing it. The
cost is also the shape that argues against exempting: an accepted ADR is never
edited in place, so its translation is written once and then costs nothing, which
makes it the *cheapest* part of this decision to sustain and only the most
expensive to start.

## Consequences

### Positive

* A French-speaking team adopting the library reads the argument, not only the
  instructions — including the parts that are hardest to accept on faith.
* The language policy stops being a rule with one hand-maintained exception, and
  becomes a rule with a stated boundary that a test enforces.
* Both repositories in the organisation present one navigation convention, so a
  reader moving between them meets no second layout.
* The checks the pair requires bring more than parity: link resolution, a single
  navigation order with no orphan page, and — the two that reach into the code —
  every shipped `DCAT` documented and every documented `dcat` option real.

### Negative

* Every documentation change is now two edits, and a page cannot merge without
  its translation. A small correction in English is a small correction in French
  as well, and the test declines to let one land alone.
* The one-off cost is large: nineteen decision records and a full guide set, in
  a language whose technical vocabulary for this domain — descriptor, suppression,
  catalogue, train — has to be settled once and then applied consistently.
* A contributor who does not write French cannot complete a documentation change
  alone. That is a real barrier to outside contribution on documentation, and it
  has no mitigation beyond a maintainer finishing the pair.

### Risks

* The French set drifts in meaning while staying in structure. The parity test
  compares headings and code fences, which catches a page half written and misses
  a page mistranslated. Nothing but review catches that, and the reviewer pool
  for it is one person.
* The precedent widens. A repository that translates `doc/` invites the argument
  that it should translate the package READMEs, the error messages the analyzers
  report, and eventually the diagnostic titles — the last of which cannot be
  translated at all, because a `const` cannot be localised. The boundary in the
  Decision is the mitigation and is stated for that reason.
* Two of the checks assert against compiled artifacts, so they carry the usual
  hazard of a check that reads a build output: if the copy that puts those
  artifacts beside the tests ever stops working, the assertions pass by having
  nothing to compare. `DocumentedSiblingsTests` already guards its own family
  against exactly that, and the same guard belongs here.

## Follow-up Actions

* Restate the language rule in [`CLAUDE.md`](../../CLAUDE.md) and
  [`CONTRIBUTING.md`](../../CONTRIBUTING.md): English by default everywhere, with
  `doc/` bilingual and the package READMEs English-only.
* Record the layout, the banner, the navigation footer and the diagram rules in
  [`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md), where the check can be described
  next to the rule it enforces.
* Add the documentation test project, including the guard that fails when it
  finds nothing to assert against.
* Translate the nineteen existing decision records, and rename them to the
  `.en.md` / `.fr.md` pair the rest of `doc/` uses.

## References

* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.md) — a
  rule is recorded where the tooling that enforces it can read it, so none rests
  on attention alone.
* [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.md) —
  the same standard applied to what automation is allowed to merge.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.md) — check
  a claim against what the analyzer declares, never against another document.
* [`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md) — the layout and the checks this
  decision requires.
* [`first-class-errors`](https://github.com/Reefact/first-class-errors) — the
  sibling project whose bilingual layout and navigation this follows.
