#!/bin/sh
# tools/commit-lint/lint-commit-message.sh — the `Docs:` footer rule.
#
# The rest of the linter is exercised by every commit anyone makes, and a mistake in
# it announces itself the next time somebody commits. This rule is different: it is
# the only one that can be wrong in the SILENT direction. A footer requirement that
# accepts anything looks exactly like a footer requirement that works, and the way
# it would be discovered is a feature reaching a release with nothing to read.
set -eu

root="$(cd "$(dirname "$0")/../.." && pwd)"
. "$root/tools/tests/assert.sh"

# `ok` / `rejected` rather than a status code, so a failing assertion prints which
# way round it went instead of `expected: [0] actual: [1]`. Named once rather than
# spelled at each call site: under `set -u` a typo in an expectation is then a shell
# error, where a mistyped literal would quietly expect a verdict nothing ever prints.
OK='ok'
REJECTED='rejected'

lint() {
  if printf '%s\n' "$1" | "$root/tools/commit-lint/lint-commit-message.sh" --ci - >/dev/null 2>&1; then
    printf '%s' "$OK"
  else
    printf '%s' "$REJECTED"
  fi
}

# --- a feat must carry the footer ---------------------------------------------------
assert_equals 'a feat naming the documentation it changed' "$OK" \
  "$(lint 'feat(cli): add the --dry-run switch

Docs: doc/guide/dcat-reference.en.md, doc/guide/dcat-reference.fr.md')"

assert_equals 'a feat with no Docs: footer at all' "$REJECTED" \
  "$(lint 'feat(cli): add the --dry-run switch

The switch prints what would be written and writes nothing.')"

assert_equals 'a feat declining documentation, with a reason' "$OK" \
  "$(lint 'feat(core): widen the internal descriptor cache

Docs: none — no consumer-visible surface, the cache is an internal detail')"

assert_equals 'a feat declining documentation and giving no reason' "$REJECTED" \
  "$(lint 'feat(core): widen the internal descriptor cache

Docs: none')"

# The separator is whatever the author reached for. An em dash is what the prose in
# this repository uses; a hyphen is what a keyboard offers, and refusing it would
# turn a rule about documentation into a rule about typography.
assert_equals 'a reason introduced by an ASCII hyphen' "$OK" \
  "$(lint 'feat(core): widen the internal descriptor cache

Docs: none - internal only, nothing a consumer can name')"

# --- only a feat is bound -----------------------------------------------------------
# A fix restores behaviour the documentation already promises, so requiring the
# footer there would make "none" the commonest answer and the footer a reflex.
assert_equals 'a fix with no footer' "$OK" \
  "$(lint 'fix(core): stop trimming the category of a nested rule')"

assert_equals 'a docs commit with no footer' "$OK" \
  "$(lint 'docs: add the reference track to the guide')"

assert_equals 'a ci commit with no footer' "$OK" \
  "$(lint 'ci: pin actionlint by checksum')"

# --- the footer names documentation, and says so in one line ------------------------
assert_equals 'a footer naming source rather than documentation' "$REJECTED" \
  "$(lint 'feat(cli): add the --dry-run switch

Docs: src/DiagnosticCatalog.Cli/GenerateCommand.cs')"

assert_equals 'a footer naming an absolute path' "$REJECTED" \
  "$(lint 'feat(cli): add the --dry-run switch

Docs: /doc/guide/dcat-reference.en.md')"

assert_equals 'a footer climbing out of the repository' "$REJECTED" \
  "$(lint 'feat(cli): add the --dry-run switch

Docs: ../elsewhere/reference.md')"

assert_equals 'a footer split over two Docs: lines' "$REJECTED" \
  "$(lint 'feat(cli): add the --dry-run switch

Docs: doc/guide/dcat-reference.en.md
Docs: doc/guide/dcat-reference.fr.md')"

assert_equals 'a footer with an empty entry between two commas' "$REJECTED" \
  "$(lint 'feat(cli): add the --dry-run switch

Docs: doc/guide/dcat-reference.en.md,,doc/guide/dcat-reference.fr.md')"

# --- a footer wrapped over several lines ---------------------------------------------
# The shape that reads as a courtesy to a 72-column log and is not one. Only the first
# line matches `^Docs: `, so every path below it is invisible to both halves of the
# rule: the linter validates the shape of what it can see, check-docs-footer.sh
# resolves what it can see, and both report success on a list they only partly read.
# Refused rather than supported, because a single line is the format CONTRIBUTING.md
# describes and a rejection an author can read beats a check that quietly does less.
assert_equals 'a footer wrapped with an indented continuation' "$REJECTED" \
  "$(lint 'feat(cli): add the --dry-run switch

Docs: doc/guide/dcat-reference.en.md,
 doc/guide/dcat-reference.fr.md')"

assert_equals 'a footer wrapped with an unindented continuation' "$REJECTED" \
  "$(lint 'feat(cli): add the --dry-run switch

Docs: doc/guide/dcat-reference.en.md,
doc/guide/dcat-reference.fr.md')"

# The trailing comma is the tell on its own: a list that ends in a separator is a list
# with something after it, wherever that something went.
assert_equals 'a footer whose list ends in a comma' "$REJECTED" \
  "$(lint 'feat(cli): add the --dry-run switch

Docs: doc/guide/dcat-reference.en.md,')"

# The continuation is only a continuation directly under the footer. An ordinary
# indented body line further down — a list, a quoted snippet — must stay legal.
assert_equals 'an indented body line above the footer' "$OK" \
  "$(lint 'feat(cli): add the --dry-run switch

The switch prints what would be written:

    dcat generate --dry-run

Docs: doc/guide/dcat-reference.en.md, doc/guide/dcat-reference.fr.md')"

# --- the footer is spelled one way --------------------------------------------------
# Reported rather than ignored, for the same reason `Refs:` is: a footer the tooling
# does not recognise reads, to its author, exactly like one it does.
assert_equals 'a lowercase docs: footer' "$REJECTED" \
  "$(lint 'feat(cli): add the --dry-run switch

docs: doc/guide/dcat-reference.en.md')"

assert_equals 'a singular Doc: footer' "$REJECTED" \
  "$(lint 'feat(cli): add the --dry-run switch

Doc: doc/guide/dcat-reference.en.md')"

# --- the footer composes with the rest of the convention ----------------------------
assert_equals 'a breaking feat carrying both footers' "$OK" \
  "$(lint 'feat(core)!: drop the obsolete rule aliases

BREAKING CHANGE: an alias removed in 1.0 no longer resolves; name the rule.

Docs: doc/guide/rule-contract.en.md, doc/guide/rule-contract.fr.md
Refs: #142')"

assert_equals 'an unscoped feat is still refused, footer or not' "$REJECTED" \
  "$(lint 'feat: add the --dry-run switch

Docs: none — nothing to write yet')"

finish
