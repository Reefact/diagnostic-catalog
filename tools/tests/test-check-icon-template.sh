#!/bin/sh
# tools/icon/check-icon-template.py — the icon template held to the icons it claims to draw.
#
# The check exists to notice drift between assets/icon-template.svg and the catalogue
# icons, and a check that cannot go red notices nothing. Its passing case runs on every
# pull request already; what needs proving here is the other direction — that a mark which
# is NOT the template's is reported rather than waved through.
#
# The negative fixture is the repository's own icon.png. It is the same family mark without
# a badge and at a different scale, so it is a real near-miss rather than a synthetic one:
# exactly the file somebody would reach for by mistake when a catalogue needs an icon.
set -eu

root="$(cd "$(dirname "$0")/../.." && pwd)"
. "$root/tools/tests/assert.sh"

cd "$root"

# `matches` / `rejected` rather than a status code, so a failing assertion prints which way
# round it went instead of leaving 0 and 1 to be decoded.
verdict() {
  if python3 tools/icon/check-icon-template.py "$@" >/dev/null 2>&1; then
    printf 'matches\n'
  else
    printf 'rejected\n'
  fi
}

assert_equals 'the shipped catalogue icons are drawn by the template' \
  'matches' "$(verdict)"

assert_equals "the repository's unbadged mark is not one of them" \
  'rejected' "$(verdict icon.png)"

# A file it cannot decode must fail rather than be skipped: a check that skips what it
# cannot read reports success over the very thing it did not look at.
assert_equals 'a file that is not a PNG is refused, not skipped' \
  'rejected' "$(verdict CONTRIBUTING.md)"

finish
