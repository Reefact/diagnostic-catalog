#!/bin/sh
# Runs every tools/tests/test-*.sh and exits non-zero if any of them failed.
#
#     sh tools/tests/run.sh
#
# The scripts under tools/ decide what a release publishes: trains.sh answers which
# projects belong to a train, and pack.sh packs exactly what it is told. Nothing the
# C# build does can check any of that, and the discovery rule has already been wrong
# twice — once here, once in the .NET Framework floor's own workflow — each time in a
# way that would have surfaced as a bad release rather than a red build.
#
# Each test file runs as a SEPARATE process. They change directory into throwaway
# fixtures, and one leaking its cwd into the next would produce failures that depend
# on the order the files happen to be read in.
set -eu

cd "$(dirname "$0")/../.."

status=0
files=0

for test in tools/tests/test-*.sh; do
  # The glob matches itself literally when nothing matches, which is a normal state
  # for a directory whose tests have not been written yet.
  [ -f "$test" ] || continue
  files=$((files + 1))
  printf '%s\n' "$test"
  if sh "$test"; then :; else status=1; fi
done

if [ "$files" -eq 0 ]; then
  printf 'no test file under tools/tests/\n' >&2
  exit 1
fi

if [ "$status" -eq 0 ]; then
  printf '\n%d test file(s) passed\n' "$files"
else
  printf '\nFAILED\n' >&2
fi

exit "$status"
