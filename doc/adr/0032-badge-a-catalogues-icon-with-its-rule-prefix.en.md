# ADR-0032 | Badge a catalogue's icon with its rule prefix, never the vendor's name

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0032-badge-a-catalogues-icon-with-its-rule-prefix.fr.md)

**Status:** Accepted
**Proposed:** 2026-08-05
**Accepted:** 2026-08-05
**Decision Makers:** Reefact

## Context

Four catalogue packages are published from this repository, each mirroring a third party's
analyzers: Sonar, the .NET analyzers, the Roslyn code styles and StyleCop. Every one of them carries
its own `icon.png` beside its `.csproj`, and the icons are the same mark — a bracketed `C` — with a
badge in the corner. The repository's own `icon.png` is that mark unbadged, and
`Directory.Build.targets` hands it to any project that joins a release train without carrying an
icon of its own, so that such a project ships the family mark rather than nuget.org's blank
placeholder.

nuget.org renders that icon at 128px, above the title, in every listing and every search result. At
that size the badge holds about three characters, and it is the only part of the icon that differs
between the four packages.

The rules each catalogue holds are named by a prefix, and it is the prefix rather than the vendor
that a consumer writes: `S1144`, `CA1822`, `IDE0008`, `SA1000`, inside `[SuppressMessage(...)]` or
after `#pragma warning disable`. The vendor's name appears beside the icon already — it is in the
package id, the title and the description that nuget.org lays out next to it.

The badges in use read `S`, `CA`, `IDE` and `SA`. They were drawn that way but the rule was never
written down: the closest thing to a statement was a comment in `Directory.Build.targets` saying
that each icon "shows the family mark with the prefix of the rules it mirrors". That comment also
records why it does **not** list the prefixes — an enumeration in a comment ages, this one had read
"S, CA, SA" when a fourth catalogue arrived, and a reader copying an icon from a sibling had nothing
to contradict them.

`PackageIconTests` is the only check in the area. It fails a catalogue that carries no icon of its
own, one whose icon is byte-identical to another catalogue's, and one still wearing the repository's
unbadged fallback. Its own remarks say what it deliberately does not do: it asserts distinctness
rather than content, and nothing in it reads a badge.

Nothing else in the repository mentioned an icon at all — not
[`CONTRIBUTING.md`](../../CONTRIBUTING.md), whose *Adding a catalogue* section enumerates the steps
a contributor would otherwise forget, and not any page under [`doc/`](..). Nor was the mark
reproducible: it existed as four 512×512 PNGs with no vector source, no generating script and no
metadata, so a fifth icon could only be produced by redrawing the family mark from sight.

One catalogue's rules do not all share a prefix. The StyleCop catalogue publishes 194 rules named
`SA` and 3 named `SX`, and its badge reads `SA`.

## Decision

A catalogue package's icon wears the family mark badged with the prefix of the rules the catalogue
mirrors, never with the vendor's name.

## Rationale

The badge is the whole of what distinguishes one catalogue from another at the size the reader
actually meets them, so what it spends its three characters on is the only decision the icon makes.
Spending them on the vendor repeats what nuget.org already prints beside the icon; spending them on
the prefix says the one thing the surrounding text does not.

It also answers the question a reader arrives with. Somebody scanning a listing is holding a rule
id — the compiler just printed one at them — and asking which package resolves it. `SA` answers that
without the page being opened. `SC` would name the product, which is not what anybody types and not
what anybody is looking for.

A single reading rule is what makes the next icon decidable without asking. The catalogues are
generated from other people's analyzers and the set grows by copying an existing project; a
convention stated as "the prefix of the rules it mirrors" settles the fifth badge from the rules
themselves, whereas "some abbreviation of the vendor" would need a judgement, and a different one,
every time. That the rule is derivable is also what lets a reviewer check a badge against the
catalogue instead of against taste.

Recording it is the point of this entry, more than choosing it. The convention was already being
followed; what did not exist was any place a reader could learn it, which is how the question "why
does StyleCop's icon read `SA` and not `SC`?" came to have no answer in the repository. A convention
followed by four artifacts and stated nowhere is one redraw away from being lost, and the comment
that came closest to stating it is — correctly — the one place that refuses to enumerate the
prefixes.

The decision deliberately stops short of an enforcing check, which is a departure from how this
repository usually settles a rule and is worth stating as a choice rather than leaving as a gap.
Reading a badge means recognising glyphs in a bitmap, or trusting a declaration written beside the
icon; the first is fragile machinery aimed at a defect whose cost is a redraw, and the second is a
second thing to keep true, which is the failure mode the `Directory.Build.targets` comment already
avoids. What a byte comparison can assert honestly is that no two catalogues look the same, and that
is what `PackageIconTests` asserts. The letters rest on this record and on review, and that is the
trade being accepted.

## Alternatives Considered

### Badge the icon with the vendor's name

`SC` for StyleCop, `CA` or `MS` for the .NET analyzers, `SQ` for Sonar. It names the thing the
package is *about*, it is what somebody would guess from the package id, and it needs no knowledge
of the rules to decide.

Rejected because it duplicates its neighbour. The vendor is already in the package id, the title and
the description that nuget.org sets beside the icon, so the badge would repeat the one fact the
reader has and withhold the one they do not. It is also undecidable in the general case: two
vendors' abbreviations can collide, and a vendor whose name has no short form leaves the badge to
taste.

### Draw each catalogue an icon of its own, with no shared mark

Each package would get an icon designed for it, free of a family grammar, and `PackageIconTests`
would keep them distinct as it does today.

Rejected because it turns every new catalogue into a design task and loses the reading the family
buys. Four packages published from one repository, mirroring four vendors, are recognisable as a set
at a glance today; individually drawn icons would say nothing about what they have in common, and
distinctness — the only property a check can assert — would be the only property left.

### Enforce the badge, by reading its letters or by declaring them beside the icon

The convention could be checked rather than reviewed: recognise the glyphs in the PNG, or commit an
`icon.txt` next to it and compare that against the prefix the catalogue's generated source actually
uses.

Rejected for now, on cost rather than on principle. Glyph recognition is a large, brittle dependency
aimed at a defect that costs one redraw and that review catches; a declared sidecar is a second
artifact that can itself be wrong, and it would assert that the file says `SA`, never that the
picture does. Should a wrong badge ever reach review, the sidecar becomes the cheaper of the two and
this decision is worth revisiting.

### Badge the icon with every prefix the catalogue publishes

StyleCop's icon would read `SA/SX` rather than `SA`, and the badge would be true of the whole
package rather than of most of it.

Rejected because it does not survive the size it is drawn for. Three characters is what the badge
holds at 128px; `SA/SX` is five and would be set small enough to be unreadable exactly where the
icon is doing its job. The majority prefix is what a reader recognises, and the package page — which
is where somebody goes once the icon has done its work — states the full set.

## Consequences

### Positive

* The badge for a new catalogue follows from the rules it mirrors, so it is decided rather than
  discussed, and a reviewer can check it against the generated source.
* The reader's actual question — which package resolves the id in front of me — is answered at
  listing size, without the page being opened.
* The family stays legible as a family: one mark, one grammar, and one thing that varies.
* The rule is now written where each audience meets it: the contributor's checklist in
  `CONTRIBUTING.md`, the reader's page in the packaging guide, and the mark's own source.

### Negative

* A catalogue whose rules carry more than one prefix is badged with the majority one, so its badge
  understates what the package holds — StyleCop's `SA` says nothing about its three `SX` rules.
* Nothing enforces the letters. A wrong badge ships exactly as easily as a right one, and only
  review stands between the two.
* The convention is now stated in more than one place, and the copies can drift from each other in
  a way nothing will report.

### Risks

* A vendor whose rules carry no distinctive prefix, or one that collides with a prefix already in
  use, leaves the rule with nothing to derive from — and the fallback is the vendor name this record
  rejects.
* The four badges in use are not reproducible letter for letter. The font they were set in is not
  recorded, so a fifth icon can match the mark exactly and the letterforms only approximately, and
  the family drifts by the width of that gap.

## Follow-up Actions

* Commit the family mark as a vector source whose only variable is the badge text, so that a new
  icon is an edit rather than a redraw.
* State the rule in `CONTRIBUTING.md`, in the *Adding a catalogue* steps, and in
  [the packaging guide](../guide/packaging-a-catalogue.en.md) for the reader who is publishing one
  of their own.
* Revisit the enforcement question if a wrong badge ever reaches review, starting from the declared
  sidecar rather than from glyph recognition.

## References

* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.en.md) — a rule is recorded
  where whoever must follow it will meet it, rather than left to attention.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.en.md) — the rule prefixes this
  record badges are read from the analyzers themselves, not transcribed.
* [`doc/guide/packaging-a-catalogue.en.md`](../guide/packaging-a-catalogue.en.md) — what nuget.org
  shows of a package, and where this rule is stated for a reader.
* `tests/DiagnosticCatalog.Catalogs.UnitTests/PackageIconTests.cs` — the check that exists, and its
  own account of what it deliberately does not assert.
