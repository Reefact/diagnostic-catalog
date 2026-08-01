# ADR-0016 | Mirror StyleCop's prerelease line, not its stale stable release

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0016-mirror-stylecops-prerelease-line.fr.md)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

A catalogue mirrors one upstream analyzer package, and `eng/catalogs.json` resolves
`"latest"` to the latest **stable** release — a default chosen so that a catalogue is
never silently pinned to a preview.

`StyleCop.Analyzers` defeats that default. It has published four stable releases in its
life (`1.0.0`, `1.0.1`, `1.0.2`, `1.1.118`) against 63 prereleases. The latest stable,
`1.1.118`, was published in **April 2019**; the project has lived on `1.2.0-beta` ever
since, the most recent being `1.2.0-beta.556` from December 2023. That beta is what
projects install.

`StyleCop.Analyzers 1.2.0-beta` is a metapackage carrying no analyzer assembly; the
descriptors live in `StyleCop.Analyzers.Unstable`, whose own 24 published versions all
carry a plain three-or-four-segment number and no prerelease tag.

Measured between the two, `1.1.118` against `1.2.0.556`:

* four rules exist only in the beta line — `SA1141`, `SA1142`, `SA1316`, `SA1414`, all
  about tuples;
* **no rule was removed**;
* one rule disagrees: `SA1413` is declared under `StyleCop.CSharp.ReadabilityRules` in the
  stable and under `StyleCop.CSharp.MaintainabilityRules` in the beta;
* one title differs by a trailing full stop, which the generator normalises away.

The platform never validates a suppression's category (§3.2): a wrong one produces no
error, no warning, no failed suppression and no failing test, at any point in any
consumer's lifecycle.

The other two catalogues are unaffected: `SonarAnalyzer.CSharp` and
`Microsoft.CodeAnalysis.NetAnalyzers` both publish stable releases on a normal cadence.

`DiagnosticCatalog.StyleCop 0.2.0`, which mirrors `1.1.118`, is published and stays
available.

## Decision

The StyleCop catalogue mirrors `StyleCop.Analyzers.Unstable` — the `1.2.0-beta` line —
rather than the latest stable release of `StyleCop.Analyzers`.

## Rationale

A catalogue's value is that a consumer does not have to look a value up, and that
proposition fails entirely if the catalogue describes a different build from the one their
analyzer is running. `SA1413` is that failure in the present tense, not in theory: a
consumer on the beta reading `StyleCopRule.SA1413.Category` from a stable-based catalogue
gets a string their analyzer does not declare, and nothing in their build disagrees with
it. That is precisely the silent, symptomless error
[ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.en.md) exists to exclude,
reappearing through the choice of release rather than the choice of source.

The stable-only default was chosen to keep a catalogue off a preview that few people run.
Here it produces the opposite: it pins the catalogue to a seven-year-old release that few
people run, while the "preview" is the de facto release. Applying the rule mechanically
would honour its wording against its purpose. The default stays right for the other two
catalogues, which is why this is an exception recorded for one vendor rather than a change
to the rule.

The move is unusually safe to make. No rule disappears, so no constant is deleted and
[ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.en.md) is not even engaged; the
stable's 193 rules are a subset of the beta's 197, with identical identifiers. What a
consumer of `0.2.0` loses by upgrading is nothing, and what they gain is four rules and
one corrected category.

Mirroring a prerelease does not weaken what the package promises. That promise is that an
identifier and a category are the ones the analyzer declares, and that a published
constant is never renamed or deleted ([ADR-0012](0012-a-catalogue-never-renames-a-member-it-published.en.md),
§23.1). Those hold regardless of how the vendor labels the release they came from, and
`[assembly: CatalogSource]` names that release exactly, so nothing is hidden.

## Alternatives Considered

### Keep mirroring the latest stable

Considered because it is the repository's default, it needs no exception recorded, and
shipping a package built from someone's prerelease invites the question this ADR exists to
answer.

Rejected because it describes a build almost nobody runs, and because it is not merely
incomplete — `SA1413` makes it wrong, in the one way a consumer can never detect.

### Publish a second catalogue for the beta line, and keep this one on the stable

Considered because each package would then say plainly what it mirrors, neither audience
would be asked to switch, and the generator supports it with one more manifest entry and
no new code.

Rejected as disproportionate to what was measured: four rules and one category separate
the two lines. A second train — its own version line, changelog, README, release and
nightly regeneration — is a permanent cost for a difference that small, carried for a
vendor that has published one stable release in seven years. The stable mirror remains
available as `0.2.0` for anyone who needs it, which is what a second package would have
provided.

### Encode the mirrored release in the package version instead

Considered because it would let both lines coexist under one package id, and make the
mirrored release visible without opening anything.

Rejected by [ADR-0015](0015-a-catalogues-version-runs-on-its-own-line.en.md): a version
derived from upstream leaves no number for a change of this repository's own, and the
release workflow accepts exactly three segments where `1.2.0.556` has four.

## Consequences

### Positive

* The catalogue describes the build its consumers actually run, which is the whole
  proposition.
* `SA1413`'s category becomes correct for the majority of users.
* Four rules that had no constant get one.

### Negative

* A package presented as stable mirrors a vendor's prerelease, which has to be explained
  wherever the catalogue is documented rather than being self-evident.
* A consumer still on `1.1.118` must pin `0.2.0` rather than take the latest.
* `"latest"` now resolves within the `.Unstable` line, so the day StyleCop publishes a real
  `1.2.0` stable, the manifest has to be pointed back by hand — nothing detects it.

### Risks

* The beta line moves faster or less carefully than a stable one, so a regeneration could
  carry a change a stable release would have held back. Mitigation: regeneration opens a
  pull request carrying the diff and publishes nothing on its own, exactly as for the other
  catalogues.
* `StyleCop.Analyzers` finally ships a stable and the catalogue keeps mirroring
  `.Unstable` unnoticed. Mitigation: recorded as a follow-up action below, and the
  mirrored package id is stated in the catalogue's own README and in every generated file's
  header.

## Follow-up Actions

* Revisit if `StyleCop.Analyzers` publishes a stable release after `1.1.118`: the reason
  for this exception disappears with it.
* State in the catalogue's README which upstream line it mirrors and where the stable
  mirror remains available.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.en.md) — why a value that
  cannot be wrong is worth the trouble of reading descriptors.
* [ADR-0015](0015-a-catalogues-version-runs-on-its-own-line.en.md) — why the mirrored release
  is not encoded in the package version.
* [doc/specification.en.md](../specification.en.md) — §3.2, §14.1 and §23.1.
* `eng/catalogs.json` — where the choice is expressed.
