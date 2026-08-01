# ADR-0025 | Bind every feature commit to the documentation it changed

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./0025-bind-every-feature-commit-to-the-documentation-it-changed.fr.md)

**Status:** Accepted
**Proposed:** 2026-08-01
**Accepted:** 2026-08-01
**Decision Makers:** Reefact

## Context

The repository enforces documentation coverage on four surfaces, all of them
enumerable from a file something else already keeps true:

* every `DCAT` id the analyzers declare in their `AnalyzerReleases` files is
  documented on a named page, and every id that page documents is declared;
* every long option the `dcat` settings types declare appears in the reference
  page, and every option that page mentions is declared;
* every rule reference a document shows resolves against the catalogue that
  publishes it;
* every catalogue README names its siblings and carries its mirror banner.

Four further checks — bilingual parity, link resolution, the reading order, the
language banner — constrain a page once it exists. None of them causes a page to
be written.

Everything else a change can add reaches a release with no check. A `dcat`
command, a public type, an MSBuild property, a manifest key, a release train, a
workflow, a hook, a `tools/` script, an entry in a changelog: nothing in the
build, the test suite or the pipeline observes whether any of them was written
down.

The only general-purpose device in place is a pull-request checkbox reading
*README / documentation updated* beside one reading *No documentation change
required*. Nothing reads either. A pull request merges with both ticked, with
neither, or with the first ticked and no documentation in the diff.

[`CONTRIBUTING.md`](../../CONTRIBUTING.md) states the expectation in prose: *"A
feature arrives with its tests, its API documentation, its sample: the commit
stays a `feat`."* The same document defines `feat` as *"A new capability, visible
to the consumer of the package"*, and requires a scope on `feat` and `fix`
because an unscoped one is silently dropped from the release record.

Two accepted decisions already bear on enforcement.
[ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.en.md) records
that a rule delegated to an artifact nothing reads drifts — in the sibling
repository it names, to 203 violations.
[ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.en.md)
records that a guarantee resting on anything but an enforcing check is not a
guarantee.

The repository already has an idiom for a written exemption. A document that
shows a rule reference no catalogue publishes declares it in a comment carrying a
reason, and a declaration whose reference the page no longer shows fails
([`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md)).

Commit messages are already linted against a closed convention by a script shared
by the local hook and by CI
([ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.en.md)), and
that convention already carries footers whose shape is checked: `Refs:` and
`BREAKING CHANGE:`.

## Decision

Every `feat` commit carries a `Docs:` footer naming the documentation it changed,
or stating in words why it changed none.

## Rationale

The obligation is close to a tautology on this repository's own terms. A `feat`
is defined by what the consumer of the package can observe; a capability the
consumer can observe and cannot read about is either undocumented or mistyped.
Making the author answer which of the two it is costs one line and is the whole
content of the rule.

Enumerable coverage cannot be the answer on its own. Each of the four existing
checks works by comparing a document against a set something else keeps true, and
that is exactly why they are trustworthy — but it is also why they will never
reach a build property, a workflow, or a page of the guide that nothing but a
reader depends on. The set of things a feature can add is open; the set a check
can enumerate is not. Extending coverage is worth doing wherever the enumeration
exists, and it leaves the general case untouched.

A footer is what the repository can actually enforce about the general case. It
does not assert that the documentation is good, or even that it is right — no
mechanism can. It asserts that somebody decided, and that the decision is in the
permanent record next to the change it belongs to. That is a lower bar than a
coverage check and a far higher one than an unread checkbox, and it is the bar
ADR-0004 and ADR-0005 already set: state the rule where something reads it.

The commit is the right carrier rather than the pull request. The convention
already treats the commit as the unit of the change and already puts the release
record, the breaking-change signal and the issue reference in its footers, so
this reuses a place authors and tools both already look. A pull request is a
place a reviewer looks once.

The exemption has to be a sentence, for the reason the documentation tests
already give: an exemption without a reason is a hole nobody can judge. Requiring
words rather than a keyword also makes the dishonest case visible — "none" with a
reason that does not survive reading is a thing a reviewer can point at, which
"none" alone is not.

Binding the rule to `feat` and not to `fix` follows from what the two types mean
here. A fix restores behaviour the documentation already promises, so the honest
answer would almost always be that nothing changed; a footer whose usual value is
"nothing" trains everyone to write "nothing", and the rule stops being read at
the moment it matters. `feat` is where the documentation debt is actually
incurred.

Splitting the check in two — the message's shape where the message is linted, the
footer's truth where the commit exists — follows the division the pipeline
already draws between asking whether a script is well formed and asking whether
it is right. It also settles a limit rather than hiding it: the hook fires before
the commit exists and cannot resolve a path against a diff, so that half runs
where it can be answered honestly instead of being approximated where it cannot.

## Alternatives Considered

### Leave the pull-request checkbox as the whole rule

Considered because it is already there, costs nothing, and puts the question in
front of the author at the moment they open the request.

Rejected because nothing reads it. It is satisfied by ticking it, which makes it
indistinguishable from a rule that works, and it disappears from the record the
moment the pull request is merged. This is precisely the failure ADR-0004 records
and ADR-0005 generalises.

### Require the pull request's diff to touch a documentation file

Considered because it needs no new convention and no footer: a job could read the
changed paths of the request and fail when a `feat` lands with nothing under
`doc/`.

Rejected because it measures the wrong thing in both directions. A pull request
carrying a feature and an unrelated typo fix in the guide passes while
documenting nothing, and a feature that genuinely needs no page fails with no way
to say so short of writing a page nobody wants. Neither outcome leaves a trace a
future reader can weigh; the footer records the author's answer, which is the
thing worth keeping.

### Bind the footer to `fix` as well as `feat`

Considered because a fix can change documented behaviour, and the rule would then
be uniform across the two version-driving types.

Rejected because the usual honest answer on a fix is that the documentation
already says what the code now does. A required field whose commonest correct
value is "nothing" is filled in without being read, and it would devalue the
footer on the type where it carries weight. A fix that does change what is
documented may carry the footer; nothing forbids it.

### Add coverage tests only, and no footer

Considered because a coverage test is a truth check and a footer is a
declaration, and truth checks are strictly better where they are possible.

Rejected because it answers a different question from the one asked. Coverage can
be extended to the public API and to the command tree, and both are worth having,
but that leaves every unenumerable surface exactly where it is: with no check at
all. Choosing only the mechanism that cannot cover the general case is choosing
not to cover it.

### Require the footer on every commit type

Considered because it removes a judgement — no author has to decide whether their
change is the kind that needs it.

Rejected because most types cannot incur documentation debt by construction:
`style`, `test`, `refactor` and `perf` all promise constant observable behaviour,
and `docs` is documentation. The footer would be noise on the majority of commits
and would be skimmed past on the minority where it matters.

## Consequences

### Positive

* A feature can no longer reach a release with nobody having decided whether it
  needed documentation, and the decision is in the history rather than in a
  merged pull request's checkboxes.
* The general case is covered — a build property, a workflow, a hook, a page of
  the guide — where no enumerable check can reach.
* The declined case is visible and reviewable, because it is a sentence rather
  than an unticked box.
* A pull request that documents a page in one language only is reported, which
  nothing did before: both files exist, so the parity check is satisfied.

### Negative

* Every feature commit carries one more line, and an author who forgets it has to
  rewrite the message rather than add a commit.
* The footer records a claim, not a fact. It can be discharged dishonestly by
  writing a reason that does not survive scrutiny, and no check will say so.
* The rule reaches the local hook only as a shape check; the half that resolves
  the footer against the commit runs in CI, so an author who never pushes learns
  late.

### Risks

* The exemption becomes a reflex — `Docs: none` with a formulaic reason on every
  feature. Mitigation: the reason is a sentence in the permanent record, which is
  reviewable in a way a checkbox is not; the review guidelines already treat a
  missing mandatory process step as a blocking finding.
* The footer is read as replacing the coverage checks, and a surface that could
  be enumerated is left to a declaration instead. Mitigation: this record states
  that coverage is preferred wherever the enumeration exists, and the two new
  checks land with it.
* A repository that merges with a merge commit accumulates history, so a footer
  convention introduced now applies to no commit already on `main`, and a reader
  of the log will find features without one. Mitigation: none needed — the
  convention is dated by this record.

## Follow-up Actions

* Extend the enumerable coverage wherever a source the build already keeps true
  exists. Done. The public API files and the `dcat` command tree are covered by
  the change that carries this record; the keys of `eng/catalogs.schema.json` are
  covered by `CatalogManifestKeyTests`, which binds them to
  [`doc/guide/catalogs-manifest`](../guide/catalogs-manifest.en.md) in both
  directions. That exhausts the surfaces this repository can enumerate.

No further candidate is named, and looking for one is what showed where the
boundary actually falls. The files under `build/` declare ordinary MSBuild
properties; this repository's own knobs — `ReleaseTrain`, `EnableNet472Floor` —
are declared per project, across ten `.csproj` files. That is a set no single
file states, so it is not a set any check can read. Adding one is exactly the
kind of change the `Docs:` footer exists to catch, and it will not be caught
another way.

> **Corrected after acceptance**, on the maintainer's decision. This section
> previously named "the MSBuild properties under `build/`" as the next candidate,
> and no such set exists; it also asked for a pull-request template rewrite the
> same change had already delivered. Follow-up Actions are a task list, not the
> decision — the decision sentence and the rationale are untouched, which is why
> this was corrected in place rather than superseded.

## References

* [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.en.md) — the
  commit convention this footer joins, and the linter shared by the hook and CI.
* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.en.md) — a
  rule stated where nothing reads it drifts.
* [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.en.md) — a
  guarantee that does not rest on an enforcing check is not one.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.en.md) — the
  standard the coverage checks meet: never compare a claim against another claim.
* [ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.en.md) —
  why the footer requires both halves of a bilingual pair.
* [`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md) — what the documentation is
  checked against, and the written-exemption idiom this rule reuses.
* `tools/commit-lint/lint-commit-message.sh` and
  `tools/commit-lint/check-docs-footer.sh` — the two halves of the check.
