# ADR-0035 | Badge a catalogue whose rule prefix is already in service with its subject instead

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0035-badge-a-shared-prefix-catalogue-with-its-subject.fr.md)

**Status:** Accepted
**Proposed:** 2026-08-05
**Accepted:** 2026-08-05
**Decision Makers:** Reefact

## Context

[ADR-0033](0033-cap-the-badge-at-three-letters.en.md) is the record in force. Its decision is one
sentence carrying two clauses — a **source** and a **cap**:

> A catalogue's badge carries at most three letters, abbreviating the rule prefix when the prefix is
> longer than that.

The source clause is inherited from [ADR-0032](0032-badge-a-catalogues-icon-with-its-rule-prefix.en.md),
which ADR-0033 supersedes and whose choice of what the badge says it kept deliberately: *"the badge
still answers from the rules rather than from the vendor"*.

**ADR-0033 already named the case this record decides, and left it open on purpose.** Twice, in its
own Consequences:

> * An abbreviation has to be chosen for each long prefix, and two vendors could reasonably
>   abbreviate to the same two letters. Nothing derives the abbreviation the way the prefix itself
>   was derived.

> * The abbreviations are decided once and then copied. A future catalogue whose prefix shortens
>   badly — **or collides with one in service** — has only this record to argue from, and the
>   collision would not be reported by any check.

ADR-0032 had recorded the same hole one step earlier, and closed off the obvious escape:

> * A vendor whose rules carry no distinctive prefix, or one that collides with a prefix already in
>   use, leaves the rule with nothing to derive from — and the fallback is the vendor name this
>   record rejects.

So the gap is not an oversight in either record. It is a stated risk that has now been realised, and
what "has only this record to argue from" produced in practice was a judgement taken twice, by hand,
ahead of any record.

**What realised it.** Three catalogues mirror rules that share the prefix `RS`, and the ids partition
cleanly between them:

| Catalogue | Mirrors | Ids | Rules |
| --- | --- | --- | ---: |
| `DiagnosticCatalog.Roslyn` | `Microsoft.CodeAnalysis.Analyzers` | `RS1xxx`, `RS2xxx` | 52 |
| `DiagnosticCatalog.PublicApi` | `Microsoft.CodeAnalysis.PublicApiAnalyzers` | `RS0016`–`RS0061` | 23 |
| `DiagnosticCatalog.BannedApi` | `Microsoft.CodeAnalysis.BannedApiAnalyzers` | `RS0030`, `RS0031`, `RS0035` | 3 |

Applying ADR-0033 literally gives all three the badge `RS`. The mark, the plate and the gradient are
the family's and do not vary, so three identical badges are three byte-identical icons — which
`PackageIconTests` fails by design, on the rule that no two catalogues may ship the same icon. For
the second and third catalogue of a shared prefix the decision in force is therefore not merely
silent; it is unsatisfiable.

**What shipped instead.** `DiagnosticCatalog.PublicApi` wears `API` and `DiagnosticCatalog.BannedApi`
wears `BAN`, each settled by the maintainer as the catalogue was added. Measured over the thirteen
badges in `tools/icon/badges.py` against the prefixes in each catalogue's generated source:

* **seven** are the rule prefix exactly — `S`, `CA`, `IL`, `RS`, `IDE`, `SA`, `ASP`;
* **four** are an abbreviation of a longer one, which is ADR-0033 working as written — `XU`, `NU`,
  `MST`, `SYS`;
* **two** are neither — `API` and `BAN`, on catalogues whose rules are `RS`.

Those two are the whole of what this record decides.

## Decision

When a catalogue's rule prefix is already worn by another catalogue's badge, its badge names the
subject of the package it mirrors instead, within the three-letter cap, and the prefix stays with the
catalogue already publishing it.

## Rationale

**The cap is untouched; only the source clause moves.** ADR-0033's measurement — cap height at the
128px a listing renders, and a six-letter word reduced to a 4.8px smudge — is about length and is
unaffected by where the letters come from. Eleven of the thirteen badges keep reading exactly as
ADR-0033 says they should. This record changes the answer for the case ADR-0033 could not answer, and
supersedes it because the two clauses live in one sentence: a badge that is three letters but not the
prefix satisfies the cap and contradicts the source.

**The prefix stays with the incumbent because moving it costs more than keeping it.** A badge that has
been published is on nuget.org, in a listing, beside a package a consumer already installed; changing
it changes an icon somebody recognises. First-in-service also needs no judgement and no measurement —
it is a fact about the repository, checkable by reading `tools/icon/badges.py`. Every other tie-break
considered (most rules, narrowest id range, oldest package) moves when upstream moves, which would
make a published icon a function of somebody else's release.

**The subject, because it is what has to be told apart.** Once the prefix cannot distinguish two
catalogues, the badge has one job left, and `PublicApi` against `BannedApi` is exactly the distinction
a reader holding `RS0030` needs. It is also derivable, in the sense ADR-0032 valued: the subject comes
from the package named in `eng/catalogs.json`, so a reviewer checks the badge against the manifest
rather than against taste — a weaker relation than reading it off a rule id, and a real one.

**The cost this pays, stated rather than glossed.** ADR-0032 rejected the vendor's name partly because
it *"repeats what nuget.org already prints beside the icon"*, and `API` beside a package called
`DiagnosticCatalog.PublicApi` does partly repeat it. That is a genuine loss and it is accepted here,
for two reasons. The alternative is three identical icons, which is worse and does not build. And what
is spent is the **catalogue's subject**, not the vendor's name: `Microsoft` appears in neither badge,
so the prohibition ADR-0032 wrote and ADR-0033 carried forward is intact — a reader still learns
something from the badge that the package id does not shout, namely which of three `RS` catalogues
this is.

## Alternatives Considered

### Let the three `RS` catalogues share the badge `RS`

Rejected because it does not build. The badge is the only part of the icon that varies, so three `RS`
badges are three identical files and `PackageIconTests` fails them. Even suspending that check, a
badge that is the same on three packages answers none of the question it exists to answer.

### Lengthen the prefix until it separates them — `RS1`, `RS0`, `RS2`

Attractive, and measured to fail. It works for `DiagnosticCatalog.Roslyn`, whose rules are `RS1xxx`
and `RS2xxx`, but `PublicApiAnalyzers` issues `RS0016`–`RS0061` and `BannedApiAnalyzers` issues
`RS0030`, `RS0031` and `RS0035`: both are `RS0`, so the two catalogues that actually collide are
exactly the two this does not separate. It would also make a badge a function of upstream numbering —
one new `RS00xx` rule in the authoring package and the three-way split is wrong.

### Badge the collided catalogues with the vendor's name

Rejected already, and useless here besides. ADR-0032 rejected vendor-name badges and ADR-0033 kept
that; and all three packages are Microsoft's, so the vendor separates nothing.

### Merge the three into one catalogue

Rejected on shape rather than preference. A catalogue mirrors one package: `package` in
`eng/catalogs.json` is a single string, unlike `projects` and `assemblies`, and
`[assembly: CatalogSource]` records one source and one version. A catalogue reading three packages
cannot be expressed, and what it would name as its source is an open question this record does not
need to answer.

### Leave it to the maintainer, case by case

Rejected because it is the state this record exists to leave, and because ADR-0033 rejected the same
shape one clause over: *"a rule that defers to judgement produces exactly the spread measured above,
and gives a reviewer nothing to check against"*. The judgement has now been exercised twice with
nothing written down, which is how `API` and `BAN` came to be defensible only by asking.

## Consequences

### Positive

* The second and the third catalogue of a shared prefix are decidable without asking, which is the
  property ADR-0032 argued for and neither record secured for this case.
* `API` and `BAN` stop being two undocumented judgements and become consequences of a record a
  reviewer can check.
* The prefix keeps its meaning for the catalogue that holds it: `RS` on `DiagnosticCatalog.Roslyn`
  still matches rule ids the reader is holding.
* Nothing already published moves. The rule is written so that the incumbent keeps its badge, so
  adopting it redraws no icon.

### Negative

* For a collided catalogue the badge is no longer read off the catalogue's own rules. A reviewer
  checks it against `eng/catalogs.json` instead of the generated source — a weaker relation than
  ADR-0032 described, and weaker than the eleven other badges keep.
* `API` and `BAN` partly repeat the package id printed beside them, which is the cost ADR-0032 used
  to reject vendor-name badges. Two of thirteen badges now pay it.
* "The subject of the package" needs a judgement in the way an abbreviation does. `PublicApiAnalyzers`
  yielding `API` rather than `PUB` was decided, not derived, and this record does not make that step
  mechanical.

### Risks

* **Nothing checks any of this.** `PackageIconTests` asserts only that no two icons match, and
  `tools/icon/check-icon-template.py` reads the mark and the gradient and deliberately not the
  lettering. A badge that is neither the prefix nor the subject merges green, exactly as ADR-0033
  recorded for the collision it foresaw.
* First-in-service is stable only while badges are not renamed. A catalogue renamed, retired, or
  merged into another leaves the question of who inherits the bare prefix, and this record does not
  answer it.
* A fourth catalogue of `RS` rules would need a third distinct subject inside three letters. The
  supply is finite, and nothing here says what happens when it runs out.

## Follow-up Actions

* State the rule where a reader publishing a catalogue of their own meets it —
  [`doc/guide/packaging-a-catalogue.en.md`](../guide/packaging-a-catalogue.en.md) and its French half.
* Cite this record beside ADR-0033 in the docstring of `tools/icon/badges.py`, which is the table the
  rule is applied in.
* A check on what a badge says remains absent, and is worth its own decision rather than an
  improvised assertion — the argument for where such a check can live is the one issue #149 makes for
  the package survey: a scheduled job reading the tree, not a per-pull-request gate.

## References

* [ADR-0033](0033-cap-the-badge-at-three-letters.en.md) — the record this supersedes, whose cap is
  kept and whose source clause could not answer a shared prefix, as its own Risks foresaw.
* [ADR-0032](0032-badge-a-catalogues-icon-with-its-rule-prefix.en.md) — where the badge was first
  bound to the rule prefix, and where the collision was first written down as a risk.
* `tests/DiagnosticCatalog.Catalogs.UnitTests/PackageIconTests.cs` — the check that makes three
  identical badges a build failure rather than a matter of taste.
* `tools/icon/badges.py` — the roster the rule is applied in, and the only place a badge is declared.
* [`eng/catalogs.json`](../../eng/catalogs.json) — where the package a badge names its subject from is
  recorded.
