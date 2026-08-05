# ADR-0033 | Cap the icon badge at three letters, abbreviating the prefix when it is longer

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0033-cap-the-badge-at-three-letters.fr.md)

**Status:** Accepted
**Proposed:** 2026-08-05
**Accepted:** 2026-08-05
**Decision Makers:** Reefact

## Context

[ADR-0032](0032-badge-a-catalogues-icon-with-its-rule-prefix.en.md) settled what a catalogue's icon
badge says: the prefix of the rules the catalogue mirrors, never the vendor's name. It argued that
choice from what a reader is doing — holding a rule id the compiler just printed, scanning nuget.org
for the package that resolves it — and from the size the reader meets the icon at, which is the
128px a listing renders.

That record counted four catalogues, whose prefixes were `S`, `CA`, `IDE` and `SA`. Four more have
been published since: `IL` for the trimming warnings, and `xUnit`, `NUnit` and `MSTEST` for the
three test-framework analyzers. All four followed ADR-0032 exactly — their badges carry their rule
prefix and not their vendor's name, which for the test frameworks happen to be the same string.

The badge is set smaller as the word gets longer, so that it clears the plate's rounded corners.
That was already true of the first four and is recorded in the template. Measured on the eight icons
now published, as cap height in the 512px artifact and at the 128px a listing renders it at:

| badge | at 512px | at 128px |
| --- | --- | --- |
| `S`, `IL` | 68px | 17.0px |
| `CA`, `SA` | 48px | 12.0px |
| `IDE` | 39px | 9.8px |
| `xUnit` | 27px | 6.8px |
| `NUnit` | 26px | 6.5px |
| `MSTEST`, set as `MSTest` | 19px | 4.8px |

Nothing measures or bounds that shrinking. `PackageIconTests` asserts that no two catalogues ship
the same icon; `tools/icon/check-icon-template.py` asserts that every icon draws the family mark and
gradient. Neither reads the badge, by the deliberate choice ADR-0032 records.

The mark itself has not drifted across those eight, and neither have the letterforms: glyphs that an
older and a newer badge share overlap at 0.82–1.00 once normalised for size, against 0.36–0.66 for
two different letters in the same badge. What varies is only the type size, by a factor of 3.6.

## Decision

A catalogue's badge carries at most three letters, abbreviating the rule prefix when the prefix is
longer than that.

## Rationale

ADR-0032 justified the badge's content by what it does at 128px, and one of the eight icons no
longer does it. At a cap height of 4.8px a six-letter word is a smudge with the proportions of text;
it neither reads as `MSTEST` nor distinguishes that package from the two beside it, whose badges are
also long words in the same weight at nearly the same size. The reasoning that chose the prefix over
the vendor's name is the same reasoning that now bounds its length — the earlier record simply never
had to state the bound, because no prefix it covered was longer than three characters.

Three is where the measurement puts the floor rather than where a preference does. The three-letter
badge already in service, `IDE`, sets at 9.8px, which is small but reads; the four-letter step down
is not represented among the eight, and the five- and six-letter ones sit at 6.8px and below, which
do not. Capping at the last size that works is what keeps the rule derived from the medium instead
of from taste.

Abbreviating is better than the alternatives because it preserves what ADR-0032 was protecting. The
badge still answers from the rules rather than from the vendor: `XU` is what is left of `xUnit`
after the cap, not a rendering of the product's name, and a reader holding `xUnit1000` recognises it
for the same reason `SA` works for `SA1000`. The abbreviation is lossy, and that loss is the price
of being legible at all — an exact badge nobody can read conveys strictly less than a shortened one
they can.

The cap also gives the convention something it has not had: a property a check can assert without
reading glyphs. Cap height is measurable from the icon's own pixels, and a badge of at most three
letters has a floor below which it cannot be set. That closes, for the length at least, the gap
ADR-0032 recorded as deliberately open — the letters themselves still rest on review.

## Alternatives Considered

### Keep the prefix whole, and accept the size it forces

`MSTEST` is what the rules are actually called, and any shortening is a second name for the same
thing — one more string for a reader to learn, and one the package page does not print anywhere.

Rejected because it defends a property nobody can use. Exactness at 4.8px is not exactness a reader
receives; it is exactness the file has. ADR-0032 chose the badge's content by asking what a listing
can convey, and the same question answered honestly rules out a word this small.

### Set the long prefixes over two lines

`MS` above `TEST`, `x` above `Unit`. The full prefix survives and each line is set larger than the
single line would be.

Rejected because it changes the mark rather than the text. The badge is a small square plate whose
proportions are part of the family, and two lines of type inside it read as a different object at
listing size — a block of text rather than a tag. It also does not buy as much as it looks: two
lines inside a plate that must clear its own corner radius leaves each line shorter than the
three-letter single line already is.

### Enlarge the plate for longer prefixes

The badge could grow to fit whatever the prefix needs, keeping the type at a readable size.

Rejected because the plate's size and position are the family mark, checked as such on every pull
request. A catalogue whose badge is a different shape from its siblings' is no longer wearing the
same mark, and the check that keeps the eight coherent would have to be weakened to allow it —
trading the property that is verified for one that is not.

### Let each catalogue choose its own badge, long or short

The maintainer picks what reads best for that vendor, case by case.

Rejected because it is the state this record exists to leave. Four catalogues arrived in one day and
their badges were decided by whoever drew them; a rule that defers to judgement produces exactly the
spread measured above, and gives a reviewer nothing to check against.

## Consequences

### Positive

* Every badge is legible at the size it is actually seen, which is the property ADR-0032 argued from
  and did not secure.
* The rule stays derivable: the badge is still read off the catalogue's own rules, so a reviewer
  checks it against the generated source rather than against taste.
* Cap height becomes assertable from the pixels, so the length half of the convention can be checked
  by `tools/icon/check-icon-template.py` rather than left to review.
* The three long badges become distinguishable from each other, which at 6.8px and below they were
  not.

### Negative

* Three published icons change what they say — `xUnit`, `NUnit` and `MSTest` become `XU`, `NU` and
  `MST` — and the other five are redrawn with them, so that all eight come out of one command
  rather than five out of a drawing nobody recorded.
* The badge stops being the literal prefix for those three. `XU` appears in no rule id, so the
  reader recognises it rather than matches it, which is a weaker relation than the one ADR-0032
  described.
* An abbreviation has to be chosen for each long prefix, and two vendors could reasonably abbreviate
  to the same two letters. Nothing derives the abbreviation the way the prefix itself was derived.

### Risks

* The abbreviations are decided once and then copied. A future catalogue whose prefix shortens
  badly — or collides with one in service — has only this record to argue from, and the collision
  would not be reported by any check.
* The lettering changes face. The font the hand-drawn badges used is not recorded and matches none
  of the 66 available here — the best candidate reaches a mean shape overlap of 0.874 against a
  0.94 ceiling — so redrawing three of them would have left three faces beside five. Redrawing all
  eight settles that at the cost of moving the five that were fine; what replaces them is the font
  the template names, which is reproducible off the shelf.
* `MST` lands at a cap height of 32px, which is 8px on a listing — above the 4.8px it replaces and
  below the 9.8px `IDE` holds, because `M`, `S` and `T` are wider per unit of height than `I`, `D`
  and `E`. Three letters is a ceiling on the count, not a floor on the size.

## Follow-up Actions

* Redraw all eight icons from the template, so that the badge each wears and the face it is set in
  are both consequences of a command rather than of a drawing session.
* Keep the badge table in `tools/icon/render-icon.py` beside the catalogues it names: a project
  added without a badge is refused there, which is the closest this convention gets to a check on
  what the letters say.

## References

* [ADR-0032](0032-badge-a-catalogues-icon-with-its-rule-prefix.en.md) — the record this supersedes,
  whose choice of what the badge says is kept and whose bound on how long it may be was missing.
* [`doc/guide/packaging-a-catalogue.en.md`](../guide/packaging-a-catalogue.en.md) — where the rule is
  stated for a reader publishing a catalogue of their own.
* `tools/icon/check-icon-template.py` — what is checked about an icon today, and what is not.
