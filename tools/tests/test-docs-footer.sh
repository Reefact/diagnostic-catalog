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

check() {
  if "$root/tools/commit-lint/check-docs-footer.sh" --commit "$1" >/dev/null 2>&1; then
    printf 'ok'
  else
    printf 'rejected'
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
assert_equals 'both named pages were touched' 'ok' "$(check "$(head_of)")"

# --- a footer that names a page the commit never opened ----------------------------------
printf 'the front door, mentioning the switch\n' > "$fixture/README.md"
commit 'feat(cli): add the --verbose switch

Docs: doc/guide/dcat-reference.en.md, doc/guide/dcat-reference.fr.md'
assert_equals 'neither named page was touched' 'rejected' "$(check "$(head_of)")"

# --- half a bilingual pair ----------------------------------------------------------------
# Both files change, so BilingualPairTests sees a complete pair and says nothing. Only
# the footer can report that the author documented the feature in one language.
printf 'the reference, again\n' > "$fixture/doc/guide/dcat-reference.en.md"
printf 'la reference, encore\n' > "$fixture/doc/guide/dcat-reference.fr.md"
commit 'feat(cli): add the --quiet switch

Docs: doc/guide/dcat-reference.en.md'
assert_equals 'only the English half is named' 'rejected' "$(check "$(head_of)")"

# --- an English-only document has no sibling to name ---------------------------------------
printf 'the front door, again\n' > "$fixture/README.md"
commit 'feat(core): publish the catalogue marker

Docs: README.md'
assert_equals 'a root document names no translation' 'ok' "$(check "$(head_of)")"

# --- a footer that names what the commit removed -------------------------------------------
rm "$fixture/README.md"
commit 'feat(core): retire the marker

Docs: README.md'
assert_equals 'the named document was deleted, not written' 'rejected' "$(check "$(head_of)")"

# --- nothing to resolve ---------------------------------------------------------------------
printf 'restored\n' > "$fixture/README.md"
commit 'feat(core): restore the marker

Docs: none — the marker is internal until the catalogue ships'
assert_equals 'a reasoned exemption names no file' 'ok' "$(check "$(head_of)")"

printf 'again\n' > "$fixture/README.md"
commit 'fix(core): stop trimming a nested category'
assert_equals 'a commit with no footer' 'ok' "$(check "$(head_of)")"

# --- the exemptions the linter also makes -------------------------------------------------------
# A placeholder carries the footer of the commit it will be folded into, and resolving
# it against the fixup's own diff would report a paragraph moved in a later rebase.
printf 'more\n' > "$fixture/README.md"
commit 'fixup! feat(cli): add the --dry-run switch

Docs: doc/guide/dcat-reference.en.md, doc/guide/dcat-reference.fr.md'
assert_equals 'an autosquash placeholder' 'ok' "$(check "$(head_of)")"

printf 'yet more\n' > "$fixture/README.md"
commit 'Merge pull request #68 from Reefact/claude/docs-reference-track

Docs: doc/guide/dcat-reference.en.md'
assert_equals 'a merge commit' 'ok' "$(check "$(head_of)")"

finish
