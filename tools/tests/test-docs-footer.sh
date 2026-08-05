#!/bin/sh
# tools/commit-lint/check-docs-footer.sh — resolving a `Docs:` footer against a commit.
#
# The half of the rule that can lie. A message linter can only see that the footer is
# well formed; a footer naming the page somebody MEANT to write is well formed too,
# and reads in the log exactly like one that was honoured. This is what makes the
# difference observable, so it is checked against real commits rather than strings.
set -eu

root="$(cd "$(dirname "$0")/../.." && pwd)"
. "$root/tools/tests/assert.sh"

fixture="$(mktemp -d)"
trap 'rm -rf "$fixture"' EXIT

# A repository of its own. `--no-verify` and an inline identity so the fixture never
# reads the ambient git configuration: a global hooksPath or a signing key would make
# these results depend on whose machine they run on.
git -C "$fixture" init -q
mkdir -p "$fixture/doc/guide"

commit() {
  # commit <message> — commits everything currently in the tree.
  git -C "$fixture" add -A
  git -C "$fixture" \
    -c user.name='fixture' -c user.email='fixture@example.invalid' -c commit.gpgsign=false \
    commit -q --no-verify -m "$1"
}

# `ok` / `rejected` rather than a status code, so a failing assertion prints which way
# round it went. Named once rather than spelled at each call site: under `set -u` a typo
# in an expectation is then a shell error, where a mistyped literal would quietly expect
# a verdict nothing ever prints.
OK='ok'
REJECTED='rejected'

check() {
  if "$root/tools/commit-lint/check-docs-footer.sh" --commit "$1" >/dev/null 2>&1; then
    printf '%s' "$OK"
  else
    printf '%s' "$REJECTED"
  fi
}

head_of() {
  git -C "$fixture" rev-parse HEAD
}

# Run from inside the fixture: the script asks git about a commit, and git answers
# about whichever repository it is standing in.
cd "$fixture"

# --- a starting tree ------------------------------------------------------------------
printf 'the front door\n' > "$fixture/README.md"
printf 'the reference\n' > "$fixture/doc/guide/dcat-reference.en.md"
printf 'la reference\n' > "$fixture/doc/guide/dcat-reference.fr.md"
commit 'chore: seed the fixture'

# --- a footer that describes the commit ------------------------------------------------
printf 'the reference, with the new switch\n' > "$fixture/doc/guide/dcat-reference.en.md"
printf 'la reference, avec le nouveau commutateur\n' > "$fixture/doc/guide/dcat-reference.fr.md"
commit 'feat(cli): add the --dry-run switch

Docs: doc/guide/dcat-reference.en.md, doc/guide/dcat-reference.fr.md'
assert_equals 'both named pages were touched' "$OK" "$(check "$(head_of)")"

# --- a footer that names a page the commit never opened ----------------------------------
printf 'the front door, mentioning the switch\n' > "$fixture/README.md"
commit 'feat(cli): add the --verbose switch

Docs: doc/guide/dcat-reference.en.md, doc/guide/dcat-reference.fr.md'
assert_equals 'neither named page was touched' "$REJECTED" "$(check "$(head_of)")"

# --- half a bilingual pair ----------------------------------------------------------------
# Both files change, so BilingualPairTests sees a complete pair and says nothing. Only
# the footer can report that the author documented the feature in one language.
printf 'the reference, again\n' > "$fixture/doc/guide/dcat-reference.en.md"
printf 'la reference, encore\n' > "$fixture/doc/guide/dcat-reference.fr.md"
commit 'feat(cli): add the --quiet switch

Docs: doc/guide/dcat-reference.en.md'
assert_equals 'only the English half is named' "$REJECTED" "$(check "$(head_of)")"

# --- an English-only document has no sibling to name ---------------------------------------
printf 'the front door, again\n' > "$fixture/README.md"
commit 'feat(core): publish the catalogue marker

Docs: README.md'
assert_equals 'a root document names no translation' "$OK" "$(check "$(head_of)")"

# --- the project README is a pair whose halves do not sit beside each other -----------------
# Its English half is the root README, because GitHub composes the landing page from there
# and from nowhere else (ADR-0029). Deriving the French half's sibling by suffix would ask
# for doc/README.en.md, which does not exist — so naming both halves would be REJECTED and
# the pair would be undescribable. Named together, it resolves.
printf 'the front door, paired\n' > "$fixture/README.md"
printf 'la porte d entree\n' > "$fixture/doc/README.fr.md"
commit 'feat(core): name the catalogue set on the landing page

Docs: README.md, doc/README.fr.md'
assert_equals 'the project README pair crosses the doc boundary' "$OK" "$(check "$(head_of)")"

# --- and the French half still may not be named alone ---------------------------------------
# The asymmetry is deliberate: English is canonical (ADR-0022), so the half that must never
# travel without its counterpart is the translation.
printf 'the front door, moved on\n' > "$fixture/README.md"
printf 'la porte d entree, encore\n' > "$fixture/doc/README.fr.md"
commit 'feat(core): describe the set once more

Docs: doc/README.fr.md'
assert_equals 'the French README half is named alone' "$REJECTED" "$(check "$(head_of)")"

# --- a footer that names what the commit removed -------------------------------------------
rm "$fixture/README.md"
commit 'feat(core): retire the marker

Docs: README.md'
assert_equals 'the named document was deleted, not written' "$REJECTED" "$(check "$(head_of)")"

# --- nothing to resolve ---------------------------------------------------------------------
printf 'restored\n' > "$fixture/README.md"
commit 'feat(core): restore the marker

Docs: none — the marker is internal until the catalogue ships'
assert_equals 'a reasoned exemption names no file' "$OK" "$(check "$(head_of)")"

printf 'again\n' > "$fixture/README.md"
commit 'fix(core): stop trimming a nested category'
assert_equals 'a commit with no footer' "$OK" "$(check "$(head_of)")"

# --- the exemptions the linter also makes -------------------------------------------------------
# A placeholder carries the footer of the commit it will be folded into, and resolving
# it against the fixup's own diff would report a paragraph moved in a later rebase.
printf 'more\n' > "$fixture/README.md"
commit 'fixup! feat(cli): add the --dry-run switch

Docs: doc/guide/dcat-reference.en.md, doc/guide/dcat-reference.fr.md'
assert_equals 'an autosquash placeholder' "$OK" "$(check "$(head_of)")"

printf 'yet more\n' > "$fixture/README.md"
commit 'Merge pull request #68 from Reefact/claude/docs-reference-track

Docs: doc/guide/dcat-reference.en.md'
assert_equals 'a merge commit' "$OK" "$(check "$(head_of)")"

# --- a footer wrapped over several lines -----------------------------------------------------
# The failure this file exists for, in its purest form. Only the first line matches
# `^Docs: `, so the resolver reads the paths it can see and exits 0 on a list it read
# part of.
#
# Every path below is at the ROOT, deliberately. Under doc/ the bilingual-pair rule
# fires on a truncated footer for its own reason — a lone `.en.md` — and a case that
# passes because of THAT proves nothing about the parsing. Root paths take the pair
# rule out of the way, so the only thing left to reject is the wrapping. Both files are
# also genuinely touched, so a resolver reading the whole footer would accept them.
printf 'wrapped\n' > "$fixture/README.md"
printf 'wrapped too\n' > "$fixture/CONTRIBUTING.md"
commit 'feat(cli): add the --wrapped switch

Docs: README.md,
 CONTRIBUTING.md'
assert_equals 'a footer wrapped with an indented continuation' "$REJECTED" "$(check "$(head_of)")"

printf 'again\n' > "$fixture/README.md"
printf 'again too\n' > "$fixture/CONTRIBUTING.md"
commit 'feat(cli): add the --unindented switch

Docs: README.md,
CONTRIBUTING.md'
assert_equals 'a footer wrapped with an unindented continuation' "$REJECTED" "$(check "$(head_of)")"

# A list ending in a separator is a list with something after it, even when nothing
# follows in the message: whatever was meant to come next is unchecked either way.
printf 'trailing\n' > "$fixture/README.md"
commit 'feat(cli): add the --trailing switch

Docs: README.md,'
assert_equals 'a footer whose list ends in a comma' "$REJECTED" "$(check "$(head_of)")"

# The continuation is only a continuation directly under the footer; an indented body
# line further up is ordinary prose and must stay legal.
printf 'indented body\n' > "$fixture/README.md"
printf 'indented body too\n' > "$fixture/CONTRIBUTING.md"
commit 'feat(cli): add the --indented-body switch

The switch prints what would be written:

    dcat generate --dry-run

Docs: README.md, CONTRIBUTING.md'
assert_equals 'an indented body line above the footer' "$OK" "$(check "$(head_of)")"

finish
