#!/bin/sh
# Resolve a commit's `Docs:` footer against what that commit actually changed.
#
# The companion to tools/commit-lint/lint-commit-message.sh, split from it for the
# same reason lint.yml splits linting the shell scripts from testing them: one asks
# whether the message is well formed, this one asks whether it is true. A message
# linter reads a string and nothing else — it cannot know that
# `doc/guide/dcat-reference.en.md` was named by a commit that never opened it, and
# a footer nobody resolves is a checkbox with a colon in it.
#
# Usage:
#   check-docs-footer.sh --commit <sha>       # CI, once per pull-request commit
#   check-docs-footer.sh --commit HEAD        # by hand, before pushing
#
# Exit status: 0 = the footer holds (or there is none to resolve), 1 = it does not,
# 2 = the script was called wrongly.
#
# Why this runs in CI and not in the commit-msg hook. The hook fires while the
# commit does not exist yet, so the only file list available is the index — and on
# `git commit --amend` the index holds the reword and nothing else, which would
# report every path in a perfectly good footer as untouched. The hook therefore
# keeps the half it can answer (the footer's shape, via the linter) and this keeps
# the half that needs a commit, which is the same division commit-lint already
# draws between the hook and the workflow.
#
# What it does NOT do: require the footer. Whether a `feat` carries one is a
# property of the message, and the linter answers it. This script is silent on a
# commit that has no footer, so the two can never disagree about the same rule.

set -u

usage() {
  printf 'usage: check-docs-footer.sh --commit <sha>\n' >&2
}

# --- arguments ------------------------------------------------------------------
case "${1:-}" in
  --commit) shift ;;
  *) usage; exit 2 ;;
esac

if [ "$#" -lt 1 ] || [ -z "${1:-}" ]; then
  usage
  exit 2
fi
target="$1"

# --- the message, and what the commit touched -------------------------------------
if ! msg="$(git log -1 --format=%B "$target" 2>/dev/null)"; then
  printf 'check-docs-footer: no such commit: %s\n' "$target" >&2
  exit 2
fi

# `--format=` with nothing after it, rather than `--pretty=format:` — the latter
# emits a leading blank line that would land in the file list as an empty entry.
changed="$(git show --name-only --format= "$target")"

# Strip what git itself strips, exactly as the linter does: the scissors block of a
# verbose commit, and the comment lines.
msg="$(printf '%s\n' "$msg" | sed -E -e '/^# -+ >8 -+$/,$d' -e '/^#/d')"

subject="$(printf '%s\n' "$msg" | sed -n '1p' | sed 's/[[:space:]]*$//')"

# --- exemptions -------------------------------------------------------------------
# The same two the linter exempts. A merge message is generated, and an autosquash
# placeholder carries the footer of the commit it will be folded into.
case "$subject" in
  'Merge '*|'fixup! '*|'squash! '*|'amend! '*) exit 0 ;;
  *) ;; # an ordinary commit: fall through
esac

# The body, never the header — `docs` is also a type, and the linter reads the
# footer the same way.
footer="$(printf '%s\n' "$msg" | sed -n '2,$p' | grep -E '^Docs: ' | sed -n '1p' || true)"
[ -n "$footer" ] || exit 0

value="$(printf '%s' "$footer" | sed 's/^Docs: //' | sed 's/[[:space:]]*$//')"

# A footer WRAPPED over several lines is refused before anything is resolved. The line
# above takes the first `^Docs: ` and nothing else, so the paths under a folded footer
# never reach the resolution below — and this script would then exit 0 having checked
# part of a list, which is the one direction it must never fail in. The message linter
# refuses the same shape at the hook; this is the second half, because the script is
# documented as runnable by hand on any commit and a commit can reach CI without ever
# meeting the hook.
#
# Two tells, one per shape of continuation. An indented line directly under the footer
# is the classic fold. A value ending in a comma is a list with something after it,
# wherever that something went, and it is what catches the unindented fold — which is
# otherwise indistinguishable from an ordinary body line. A continuation with neither a
# comma nor an indent stays legal, deliberately: nothing tells it apart from a
# paragraph written after the footer.
wrapped="$(printf '%s\n' "$msg" | sed -n '2,$p' | awk '
  after && /^[ \t]/ && /[^ \t]/ { print "wrapped"; exit }
  { after = ($0 ~ /^Docs: /) }
')"
case "$value" in
  *,) wrapped='wrapped' ;;
  *) ;; # the list ends on a path, as it should
esac
if [ -n "$wrapped" ]; then
  printf 'check-docs-footer: this Docs: footer is wrapped over several lines\n\n  - only the first line is read, so the paths under it are resolved by nothing. Keep the footer on ONE line and separate paths with commas, however long the line gets\n\n  footer:  %s\n  subject: %s\n' \
    "$footer" "$subject" >&2
  exit 1
fi

# `Docs: none — <reason>` names no file, so there is nothing here to resolve. That
# the reason is present is the linter's business.
case "$value" in
  none|none[!A-Za-z0-9]*) exit 0 ;;
  *) ;; # a list of paths: resolve it
esac

# --- resolution ---------------------------------------------------------------------
errors=0
errmsgs=''
err() {
  errmsgs="${errmsgs}  - ${1}
"
  errors=$((errors + 1))
  return 0
}

# Whole-line, fixed-string: a path is a line of the file list or it is not, and a
# substring match would let `doc/guide/faq.en.md` be discharged by `faq.en.md.bak`.
touched() {
  printf '%s\n' "$changed" | grep -Fxq "$1"
}

named() {
  printf '%s\n' "$value" | tr ',' '\n' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' | grep -Fxq "$1"
}

OLDIFS=$IFS
IFS=','
for entry in $value; do
  path="$(printf '%s' "$entry" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
  [ -n "$path" ] || continue

  if ! touched "$path"; then
    err "the footer names ${path} and the commit does not touch it — name what this commit documented, not where it should be documented one day"
    continue
  fi

  # A path is looked up in the commit's OWN tree, not in the working tree: a later
  # commit on the branch may have renamed it, and the question here is what this
  # commit did. Touched but absent means the commit deleted it, so the footer says
  # the opposite of what happened.
  if ! git cat-file -e "${target}:${path}" 2>/dev/null; then
    err "the footer names ${path} and the commit removes it"
    continue
  fi

  # doc/ is bilingual and the two halves land together (ADR-0022). BilingualPairTests
  # already fails a pair missing a half; nothing but this notices a page updated in
  # one language and left stale in the other, because both files still exist.
  case "$path" in
    doc/*.en.md) sibling="${path%.en.md}.fr.md" ;;
    doc/*.fr.md) sibling="${path%.fr.md}.en.md" ;;
    *) sibling='' ;; # the package READMEs and the root documents are English-only
  esac

  if [ -n "$sibling" ] && ! named "$sibling"; then
    err "the footer names ${path} and not ${sibling} — under doc/ a page and its translation land in the same commit (ADR-0022)"
  fi
done
IFS=$OLDIFS

# --- verdict --------------------------------------------------------------------------
if [ "$errors" -gt 0 ]; then
  printf 'check-docs-footer: this Docs: footer does not describe the commit\n\n%s\n  footer:  %s\n  subject: %s\n' \
    "$errmsgs" "$footer" "$subject" >&2
  exit 1
fi
exit 0
