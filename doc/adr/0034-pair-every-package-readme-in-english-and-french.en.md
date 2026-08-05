# ADR-0034 | Pair every package README in English and French

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0034-pair-every-package-readme-in-english-and-french.fr.md)

**Status:** Accepted
**Proposed:** 2026-08-05
**Accepted:** 2026-08-05
**Decision Makers:** Reefact

## Context

[ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.en.md) made every
document under [`doc/`](..) bilingual and named one exclusion in its Decision: *the
package READMEs under `src/` stay English-only*. The argument was the renderer.
nuget.org shows one file per package, offers no language switch, and resolves no
relative link — so a bilingual page there would either duplicate every section inside
one document or link to a translation the reader cannot reach.

[ADR-0029](0029-pair-the-project-readme-across-the-doc-boundary.en.md) then met the same
shape of constraint at GitHub and settled it differently. GitHub composes a repository's
landing page from a `README.md` at the root and from nothing else; that removes the
*suffix* and fixes the *location* of one half, and it does not remove the *pair*. Its
Risks section named what would come next: *"The exception invites company … the reason —
a renderer that fixes the name and the place of a page that is documentation — is what
any future candidate has to argue."*

The second half of ADR-0022's argument no longer describes these files. Every package
README already links outward with absolute
`https://github.com/Reefact/diagnostic-catalog/blob/main/...` addresses, and
`LinkTests.A_package_readme_carries_no_relative_link` fails one that does not: there a
relative link is always broken, however carefully written, so the requirement is that
they carry none. A reader on nuget.org therefore *can* reach a page in this repository —
that is how the guides, the specification and the sibling catalogues are already offered
to them.

What has not changed is the first half. nuget.org renders one file. Whatever a package
carries, it carries alone, with no switch and no sibling beside it.

The audience is the one ADR-0022 argued hardest about. It justified translating the
guides because they are read by whoever is asked to migrate the code rather than by
whoever chose the library — and a package README is the page that reader meets first,
before any guide, often from a search result. Eleven of the fourteen are catalogue pages
whose whole subject is the failure this repository exists to remove: a suppression whose
category is wrong compiles, runs, and reports nothing, forever. That argument is worth
nothing to a reader who does not follow it.

The parity checks in `tests/DiagnosticCatalog.Documentation.UnitTests` key on the
language suffix, and they already read `src/`: `Repository` scans `doc/` and `src/`
alike, so a page named `README.en.md` there is in the bilingual set with no list to add
it to. What those files need instead is for two checks to learn the new shape — the
one that selects the package READMEs by name, and the one that resolves a language
banner's link.

The generator writes into these files. `CatalogEmitter` rewrites the marked block that
states which upstream release a catalogue mirrors, in the README and in the changelog,
and `DocumentedMirrorTests` fails a document whose block disagrees with the catalogue's
own `[assembly: CatalogSource]`. That generator also ships inside `dcat`, where it runs
against repositories that keep a single `README.md` and have never heard of a language
suffix.

## Decision

Every package README under `src/` is maintained in English and French, as
`README.en.md` and `README.fr.md`, with the English version canonical and shipped: a
package's `<PackageReadmeFile>` names the English half, and both halves write every
address out in full, the language banner included. The per-package `CHANGELOG.md` stays
English-only.

## Rationale

The renderer decides which half a package carries — it does not decide whether a
translation exists. That is the whole of it. ADR-0022 read one constraint as two: nuget.org
shows one file *and* resolves no relative link, and the second was what made a pair
useless, because a banner offering the other language would have pointed at nothing. But
these pages had already stopped writing relative links for exactly that reason, and an
absolute address is not merely tolerated there — it is the only kind that has ever
worked. The banner is one more link of the kind the whole page is already made of.

This is ADR-0029's move applied to the other renderer, and the symmetry is worth stating
because it is what keeps the language policy from becoming a list of places. GitHub
fixes the *name and location* of a half and the pair survives; nuget.org fixes *how many
halves travel inside a package* and the pair survives. Neither renderer was ever asked
whether the repository may hold a translation.

The audience argument lands harder here than at the root README. A package page is
reached from a search result, from a `PackageReference` somebody else wrote, from a
transitive dependency nobody chose — the reader arrives already using the analyzer,
which is precisely the reader ADR-0022 said had to follow the argument and had not
chosen the library. The root README at least belongs to somebody evaluating; these
pages are read by whoever has to fix the build.

Declaring the pair to the checks rather than exempting it is the same move both records
made before, and for the same reason: every argument above fails the moment the French
half lags, and lagging is the normal outcome of a policy that rests on remembering. The
suffix is what buys this — no list, no exception, no per-file decision. `README.en.md`
under `src/` is in the set because of its name, exactly as `getting-started.en.md` is.

Keeping the generator writing both halves is what makes the pair survive a nightly. The
mirrored release is the one statement in a catalogue README that nobody edits by hand,
and a translation nothing refreshes states last month's release to the reader least
equipped to notice — neither the assembly attribute it contradicts nor the guides that
would correct it are in their language. Writing only English into a French page would
have been worse than leaving it stale, so each half gets its own banner and only the
prose differs; the package id and the version are the same sentence in both.

The generator writing into whichever README spellings exist, rather than into a fixed
name, is what keeps this repository's convention from reaching into other people's. A
`dcat` user's catalogue folder holds `README.md`, and it must keep getting its banner;
ours holds a pair, and both halves must. A spelling that is absent is another
repository's convention rather than a missing document, so it is not reported — a note
on every run for a file nobody meant to keep is how a reader learns to stop reading the
notes.

The per-package changelogs are left alone because the argument above does not reach
them. A changelog is a log of released versions rather than a page anybody reads to
understand the library; it ships in no package, so no renderer constrains it, and the
audience argument — the reader who arrives already using the analyzer — is about the
page that explains what the rules are.

## Alternatives Considered

### Keep the package READMEs English-only, as ADR-0022 decided

It needs no ADR, no translation, and no change to the generator, the packaging or the
checks. Their audience is arguably evaluating rather than learning, and a reader who
wants more is one link from a fully bilingual set.

Rejected because "one link away" is the claim the pair makes true rather than an
argument against it: those links exist and are absolute, which is exactly why a banner
can be one of them. And the audience description does not survive contact with how
these pages are reached — a catalogue page is met by somebody who already has the
analyzer, through a dependency they did not choose.

### Keep `README.md` and add `README.fr.md` beside it

The shipped file would keep the name the packaging, the generator and every downstream
`dcat` user already use, and nothing but the new file would move.

Rejected because it puts one half outside the parity checks and the other inside. A
document with no language suffix is not in the bilingual set, so `README.fr.md` would be
checked against a `README.en.md` that does not exist — the pair would have to be
declared file by file, which is the exception list ADR-0022 refused, this time with
fourteen entries instead of one.

### Fold both languages into the single file nuget.org renders

One document per package, English then French, with an anchor at the top. Nothing moves,
nothing is renamed, and a reader on nuget.org needs no link at all.

Rejected for the reason ADR-0022 gave when it rejected it: it duplicates every section
inside one document. It also doubles the page every reader scrolls in order to serve
each of them half of it, and the parity checks — which compare two documents — would
have nothing to compare.

### Pair the per-package `CHANGELOG.md` as well

It sits under `src/` beside the README, the generator already writes a banner into it,
and leaving it monolingual makes the folder inconsistent.

Rejected because consistency of folder is not the argument. A changelog is read to find
out what changed in a version, ships in no package, and is appended to at every release
— the one document here whose translation cost recurs on a schedule, for the least
explanatory prose in the repository.

## Consequences

### Positive

* The page a reader of a catalogue meets first exists in their language, including the
  argument about why a wrong category produces no symptom.
* The language policy stops naming a folder and starts naming a renderer: `doc/` is
  bilingual, the root README is bilingual with its name fixed by GitHub, and the package
  READMEs are bilingual with their shipped half fixed by nuget.org.
* The pair is checked by the same theories as every other page — a missing half, a
  section dropped, a table row added on one side only, a banner that points nowhere —
  because the suffix puts it in the set with no list to maintain.
* The mirrored release cannot go stale in one language, because the generator writes
  both halves and `DocumentedMirrorTests` reads both.

### Negative

* Fourteen more pages to keep true, and they are the pages most likely to change: a
  catalogue's README states its rule count, its category table and the release it
  mirrors.
* A package README can no longer be edited alone, and the commit linter enforces it —
  `check-docs-footer.sh` refuses a `Docs:` footer naming one half of a pair.
* Browsing a package folder on GitHub renders no README, because GitHub renders
  `README.md` in a directory listing and neither half is called that. `doc/guide/`
  already lives with this.
* The `.nupkg` now contains a file called `README.en.md`, which reads as though a
  `README.fr.md` were missing from a package that deliberately carries one file.

### Risks

* The French half drifts in meaning while holding its shape. The parity theories count
  headings, samples, list items and table rows; they do not read French, and a
  catalogue README is where a stale figure is most likely to be believed.
* A future catalogue is added with one half. Nothing in the generator creates a README,
  so the pair is created by hand, and the checks that would catch a missing half are the
  documentation tests rather than anything the generator says at the time.
* The exception invites company, again. This record answers ADR-0029's question for the
  package READMEs, and the reason it argues from — a renderer that decides how a page is
  shown rather than whether it exists — is what the next candidate has to argue.

## Follow-up Actions

* Restate the rule in [`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md),
  [`CONTRIBUTING.md`](../../CONTRIBUTING.md), [`CLAUDE.md`](../../CLAUDE.md) and the
  project README, where each currently says the package READMEs are English-only.
* Teach `LinkTests` to select the package READMEs by their new names, and `Repository`
  to resolve an address into this repository so a language banner written in full is
  checked like any other.
* Teach `CatalogEmitter` to write the mirror banner into whichever README spellings a
  catalogue folder holds, and `DocumentedMirrorTests` to read both halves.
* Teach `check-docs-footer.sh` that `src/*/README.en.md` and `src/*/README.fr.md` are
  siblings, in both directions.

## References

* [ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.en.md) — the
  bilingual policy, and the exclusion this record replaces.
* [ADR-0029](0029-pair-the-project-readme-across-the-doc-boundary.en.md) — the same
  question asked of GitHub, and the answer this record follows.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.en.md) — why the
  mirrored release is written by the generator rather than by hand.
* [ADR-0017](0017-publish-the-generator-as-a-cli-on-its-own-release-train.en.md) — why
  the generator is somebody else's tool as well as ours.
* [`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md) — the layout, and what the
  documentation tests check.
