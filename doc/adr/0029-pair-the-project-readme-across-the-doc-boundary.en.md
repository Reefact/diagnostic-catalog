# ADR-0029 | Pair the project README across the `doc/` boundary

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0029-pair-the-project-readme-across-the-doc-boundary.fr.md)

**Status:** Accepted
**Proposed:** 2026-08-02
**Accepted:** 2026-08-02
**Decision Makers:** Reefact

## Context

[ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.en.md) made
every document under [`doc/`](..) bilingual, with the English version canonical,
and left the package READMEs under `src/` English-only. It argued that second
exclusion from the renderer: nuget.org shows one file per package, offers no
language switch, and resolves no relative link, so a bilingual page there would
either duplicate every section inside one document or link to a translation the
reader cannot reach.

It made no argument about the project README. The Context of that record counted
it among the reader-facing documentation of the day, beside the guides; the
Decision then named `doc/` and `src/` and did not mention it again. The rule that
kept it English was a table row in [`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md),
which stated the boundary — outside `doc/` — rather than a reason.

The renderer argument does not transfer. GitHub renders the project README on the
repository's landing page and resolves relative links from it, which is why that
page already linked into the bilingual set twice, offering the French half of the
documentation map and the French half of the documentation front door. A French
reader was therefore sent into a translated set through the one page on the way in
that had no translation.

What GitHub does impose is the name and the place. It renders a repository's
landing page from a file called `README.md` at the root, and from nothing else: a
`README.en.md` is not picked up, and neither is a README that lives under `doc/`.
The same file is what the repository's page shows to anyone arriving from a search
result, a package page, or a link.

The parity checks in `tests/DiagnosticCatalog.Documentation.UnitTests` key on the
language suffix: a document named `<name>.en.md` or `<name>.fr.md` is in the set
and is checked against its sibling, and a document without a suffix is simply not
in it. Nothing is exempted by a list, which is deliberate — a check with an
exception list drifts into a check with only exceptions. The project README
carries no suffix, so it had never been in the set; those tests read it only for
its links.

Two pages were doing one job. `doc/README.en.md` was the documentation front door
— a signpost naming the four kinds of document that live under `doc/` and which
question each answers — and the project README carried a `Documentation` section
listing the same four. The front door had exactly one inbound link, from that
section; the guide's own navigation footer already pointed past it, back to the
project README.

The sibling project [`first-class-errors`](https://github.com/Reefact/first-class-errors),
whose layout ADR-0022 followed, pairs its root `README.md` with a French half held
inside its documentation folder, the English half carrying the language banner and
the French half pointing back at the root.

## Decision

The documentation front door is merged into the project README, whose English half
stays at the repository root because GitHub renders it there and whose French half
is `doc/README.fr.md`, and the documentation checks treat the two as siblings
across the folder boundary.

## Rationale

The exclusion was a side effect of drawing the boundary by folder, not a judgement
about the document. Every other exclusion in ADR-0022 was argued from something
about the page — the package READMEs from the renderer that shows them — and this
one was argued from where the file happens to sit. Once the root README is
recognised as a documentation page that GitHub keeps outside `doc/`, the boundary
stops describing a decision and starts describing a filesystem.

The audience argument of ADR-0022 lands harder here than anywhere it was already
accepted. That record justified translating the guides because they are read by
whoever is asked to migrate the code rather than by whoever chose the library. The
README is the page that reader meets first, and the only one many of them read at
all: it carries the argument about why a wrong category produces no symptom, which
is the single point the whole design exists to make. Translating the guides and
leaving that page in English inverts the priority the record set.

The constraint GitHub imposes is real but narrower than nuget.org's, and the
difference is what settles the shape. nuget.org cannot show a translation at all,
so the decision there was between one language and a document folded in half.
GitHub can show one; it only insists that the English half be called `README.md`
and sit at the root. So the constraint removes the *suffix* and fixes the
*location* of one half — it does not remove the *pair*.

Merging the front door is what makes the pair cost nothing extra, and it is worth
doing on its own terms. A signpost to four documents and a README section listing
the same four are one page written twice, and the duplicate was already showing:
the front door was reachable from a single link, and the guide's navigation walked
around it. Folding it into the README removes a hop from every reader's path and
leaves one page to keep true instead of two — and had it been kept, the repository
would have had to hold a French front door and a French README side by side, each
saying most of what the other says.

The French half therefore keeps the name `README.fr.md`, and that name still means
what it means in `doc/guide/` and `doc/adr/`: the index of the folder it sits in.
The project README *is* now that index — it is where the guide, the specification,
the records and the conventions are named — so `doc/` gains its French index and
its English one lives at the root, displaced by the renderer rather than by a
choice.

Declaring the pair to the checks, rather than exempting the page from them, is the
same move ADR-0022 made and for the same reason. Every argument above fails the
moment the French half lags, and lagging is the normal outcome of a policy that
rests on remembering. A page that is bilingual by convention and unchecked by
construction is the exact failure that record was written to remove, and the
project README is the worst page in the repository to leave in that state, because
it is the one changed most often for reasons that have nothing to do with
translation.

## Alternatives Considered

### Keep the project README English-only, as the previous convention stated

The README is a shop window rather than a document to study, its audience is
evaluating rather than learning, and a reader who wants more is one click from a
fully bilingual set. Leaving it alone would need no ADR, no translation, and no
change to the checks.

Rejected because it makes the front door the only monolingual step on the way in.
The set behind it is bilingual, the README linked into that set in French twice
already, and the argument the page carries is the one ADR-0022 said had to land
precisely. A reader who cannot follow the README does not reach the guides whose
translation was justified by their needing to.

### Keep the documentation front door and name the French README something else

The front door could have stayed as `doc/README.en.md` and `doc/README.fr.md`, and
the French half of the project README could have taken a distinct name such as
`doc/project-readme.fr.md`. Nothing would have been merged, no inbound link would
have moved, and the change would have been additive.

Rejected because it keeps two pages doing one job and adds a third. The
duplication was already there in English; a bilingual policy would have doubled it,
leaving a French front door and a French README beside each other saying most of
the same things, with a second spelling of "readme" invented to tell them apart.

### Put the French half at the repository root, as `README.fr.md`

It keeps the pair in one folder, makes the sibling relationship obvious to anyone
listing the root, and needs no change to how siblings are computed.

Rejected because the root is GitHub's constraint on one file, not a home for the
documentation set. A second top-level Markdown page competes with the one the
repository page renders, and it puts a documentation page outside the folder whose
conventions govern it — a page that would then be bilingual by policy while
sitting where the policy says nothing applies.

### Reduce the root README to a stub and hold the pair entirely under `doc/`

The pair would then need no cross-folder sibling and would follow the existing
convention without any exception at all, the root carrying only a title and a link
into the set.

Rejected because the root README is what nuget.org, search engines and the
repository page actually show. Degrading the page most readers see, in order to
protect a naming rule, trades the audience for the convention — and keeping it
whole alongside a copy under `doc/` would instead create two English pages making
the same claims, with nothing saying which is canonical.

## Consequences

### Positive

* The page most readers see first exists in both languages, and the argument it
  carries — that a wrong category produces no symptom — reaches the reader ADR-0022
  was written for.
* The language policy stops resting on a boundary that describes a filesystem, and
  states instead which renderer forces which exception.
* One page replaces two. The signpost to the guide, the specification, the records
  and the conventions is a section of the README, so a reader arriving at the
  repository is one page from everything rather than two.
* The pair is checked by the same theories as every other page: a missing half, a
  heading dropped, a table row added on one side only, a banner that points
  nowhere.

### Negative

* The project README can no longer be edited alone. A badge row, a package added to
  a table, a corrected sentence: each is now two edits, and the parity theory
  declines to let one land without the other.
* The sibling relationship is no longer derivable from the filename. One pair in
  the repository is declared rather than computed, and a reader of the checks has
  to meet that declaration to understand why.
* `doc/` is now the one folder whose index is split: `README.fr.md` sits in it and
  its English counterpart does not, which reads as an omission until the reason is
  known.
* A contributor who does not write French cannot complete a README change alone —
  the barrier ADR-0022 accepted for `doc/`, now extended to the page most likely to
  attract an outside contribution.

### Risks

* The French half drifts in meaning while holding its shape. The parity theories
  count headings, samples, list items and table rows; they do not read French, and
  the README is the page where a stale sentence is most visible to the most people.
* The README grows. Absorbing the front door put a section into a page that is
  already long, and every future addition to the documentation set will argue for
  a line there — with the translation cost now attached to each one.
* The exception invites company. A repository that pairs one file across the
  boundary can be asked to pair `CONTRIBUTING.md`, `SECURITY.md` and the rest of the
  root; the Decision names one document, and the reason — a renderer that fixes the
  name and the place of a page that is documentation — is what any future candidate
  has to argue.

## Follow-up Actions

* Restate the rule in [`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md),
  [`CONTRIBUTING.md`](../../CONTRIBUTING.md) and [`CLAUDE.md`](../../CLAUDE.md),
  where each currently says that `doc/` is the only bilingual place.
* Teach the documentation checks the one declared cross-folder pair, and carry the
  language banner on both halves.
* Merge the front door into the project README, translate the result, and keep the
  two halves in the same commit as every other pair.

## References

* [ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.en.md) —
  the bilingual policy this record extends, and the reasoning it is argued from.
* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.en.md) — a
  rule is recorded where the tooling that enforces it can read it.
* [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.en.md) —
  the same standard applied to what automation is allowed to merge.
* [`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md) — the layout, and what the
  documentation tests check.
* [`first-class-errors`](https://github.com/Reefact/first-class-errors) — the
  sibling project, whose root README is paired the same way.
