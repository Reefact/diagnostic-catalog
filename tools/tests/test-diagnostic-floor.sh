#!/bin/sh
# tools/analysis/check-diagnostic-floor.sh — the guard of last resort (ADR-0024).
#
# It is the only thing in the repository that sees a Roslyn diagnostic reported BELOW
# warning. The ratchet cannot: `info` is not a warning. `dotnet build` prints nothing
# about one at any verbosity. SonarQube Cloud imports it regardless.
#
# So the property under test here is not only "does it find a violation" — it is "can
# it ever announce that it found none WITHOUT having looked". A guard is allowed to
# fail. It is not allowed to fail green, because a green line is indistinguishable
# from the answer everyone expects and nobody goes to check.
set -eu

root="$(cd "$(dirname "$0")/../.." && pwd)"
. "$root/tools/tests/assert.sh"

check="$root/tools/analysis/check-diagnostic-floor.sh"

fixture="$(mktemp -d)"
trap 'rm -rf "$fixture"' EXIT

# A well-formed log carrying one diagnostic below warning. `note` is what SARIF calls
# the severity the .NET SDK spells `info`, and it is carried on the rule's default
# configuration rather than on the result — the shape a result takes when it matches
# its rule's own default, which is the common one.
sarif_with_a_violation() {
  cat <<'JSON'
{
  "version": "1.0.0",
  "runs": [
    {
      "tool": { "driver": { "rules": [
        { "id": "IDE0008", "defaultConfiguration": { "level": "note" } }
      ] } },
      "results": [
        {
          "ruleId": "IDE0008",
          "message": "Use explicit type instead of 'var'",
          "locations": [
            { "resultFile": { "uri": "file:///src/Thing.cs", "region": { "startLine": 42 } } }
          ]
        }
      ]
    }
  ]
}
JSON
}

# The same shape with nothing below warning: one `error`, which the ratchet owns.
sarif_that_is_clean() {
  cat <<'JSON'
{
  "version": "1.0.0",
  "runs": [
    {
      "tool": { "driver": { "rules": [
        { "id": "CS0219", "defaultConfiguration": { "level": "error" } }
      ] } },
      "results": [
        {
          "ruleId": "CS0219",
          "message": "The variable is assigned but never used",
          "locations": [
            { "resultFile": { "uri": "file:///src/Thing.cs", "region": { "startLine": 7 } } }
          ]
        }
      ]
    }
  ]
}
JSON
}

# Runs the check over a directory and reports its exit status, keeping the output.
# `set -e` would abort this file the moment the check exits non-zero — which is the
# expected outcome of half these cases — so the status is captured rather than let
# through.
run_check() {
  status=0
  "$check" "$1" >"$fixture/out.txt" 2>&1 || status=$?
}

printf '  the check finds a diagnostic reported below warning\n'

mkdir -p "$fixture/violating"
sarif_with_a_violation > "$fixture/violating/one.sarif"

run_check "$fixture/violating"
assert_equals "a note-level diagnostic fails the check" 1 "$status"
assert_equals "it names the rule" \
  "yes" \
  "$(grep -q 'IDE0008' "$fixture/out.txt" && echo yes || echo no)"

printf '  the check passes a build whose diagnostics are all at least warnings\n'

mkdir -p "$fixture/clean"
sarif_that_is_clean > "$fixture/clean/one.sarif"

run_check "$fixture/clean"
assert_equals "an error-level diagnostic passes" 0 "$status"
assert_equals "and it says so" \
  "yes" \
  "$(grep -q 'at least a warning' "$fixture/out.txt" && echo yes || echo no)"

printf '  an unreadable log fails the check rather than emptying it\n'

# The defect this file was written for. A build that was cancelled, ran out of disk, or
# emitted a SARIF shape jq cannot parse leaves a log like this one. Reading every log in
# a single jq invocation means one of them ends the read for all of them; piping into
# `sort` means the status the script tests is SORT's, and sort is perfectly happy with
# an empty stream. The violation below sat unread while the check printed
# "every diagnostic this build reports is at least a warning (2 log(s) read)" and
# exited 0 — counting, in that sentence, a log it never opened.
#
# Named to sort BEFORE the valid one, so nothing at all reaches the output. Sorting
# after it would lose only the logs behind it, which is the same defect with a smaller
# blast radius and a less obvious assertion.
mkdir -p "$fixture/truncated"
sarif_with_a_violation > "$fixture/truncated/b-real.sarif"
printf '{ "version": "1.0.0", "runs": [ { "tool": { "dri' > "$fixture/truncated/a-cut-short.sarif"

run_check "$fixture/truncated"
assert_equals "an unparseable log fails the check" 1 "$status"
assert_equals "the check never claims the build is clean" \
  "no" \
  "$(grep -q 'at least a warning' "$fixture/out.txt" && echo yes || echo no)"
assert_equals "the failure names the log that could not be read" \
  "yes" \
  "$(grep -q 'a-cut-short.sarif' "$fixture/out.txt" && echo yes || echo no)"

printf '  an empty log fails the check\n'

# Zero bytes is the other half of the same build interruption, and it is quieter than a
# truncated log: jq reads an empty file as zero documents rather than as an error, so it
# reports success having produced nothing. Nothing downstream can tell that apart from a
# log which genuinely held no diagnostic.
#
# The empty log is ALONE here, and that is the whole design of the case. Put a violating
# log beside it and the check fails on the violation, which says nothing about whether
# the empty one was noticed — the assertion would pass against the unfixed script.
#
# Measured against a real `dotnet build -c Release` of this repository: 29 .sarif logs,
# none of them empty, the smallest 333 bytes. So refusing an empty one refuses nothing
# the build legitimately produces.
mkdir -p "$fixture/empty"
: > "$fixture/empty/a-empty.sarif"

run_check "$fixture/empty"
assert_equals "an empty log fails the check" 1 "$status"
assert_equals "the check never claims the build is clean" \
  "no" \
  "$(grep -q 'at least a warning' "$fixture/out.txt" && echo yes || echo no)"
assert_equals "the failure names the log that was empty" \
  "yes" \
  "$(grep -q 'a-empty.sarif' "$fixture/out.txt" && echo yes || echo no)"

printf '  a log that cannot be read stops the check, and does not skip the rest\n'

# The first draft of the repair carried `[ -e "$sarif" ] || break` into the reading loop,
# copied from the counting loop above it where that line is the POSIX idiom for "the glob
# matched nothing". One loop lower it means something else: drop this log and every log
# sorting after it, then go on and pronounce the build clean. Which it duly did — the
# violation below went unreported and the check exited 0, announcing "every diagnostic
# this build reports is at least a warning (1 log(s) read)".
#
# So the fixture is built to make that outcome the LOUD one. The unreadable entry sits in
# the middle; the log before it is clean, so nothing else can fail the check; the log
# after it carries the only violation. A check that skips ahead reports success.
mkdir -p "$fixture/unreadable"
sarif_that_is_clean > "$fixture/unreadable/a-clean.sarif"
ln -s /nonexistent/gone.sarif "$fixture/unreadable/b-unreadable.sarif"
sarif_with_a_violation > "$fixture/unreadable/c-violating.sarif"

run_check "$fixture/unreadable"
assert_equals "an unreadable log fails the check" 1 "$status"
assert_equals "the check never claims the build is clean" \
  "no" \
  "$(grep -q 'at least a warning' "$fixture/out.txt" && echo yes || echo no)"
assert_equals "the failure names the log it could not read" \
  "yes" \
  "$(grep -q 'b-unreadable.sarif' "$fixture/out.txt" && echo yes || echo no)"

printf '  a directory the caller supplied is never removed\n'

# The cleanup handler removes the log directory only when the script made it itself. That
# distinction is one variable wide, and on the wrong side of it the script `rm -rf`s a
# directory its caller passed in and still owns. Nothing else here would notice: every
# other case reads its fixture once and never looks again.
mkdir -p "$fixture/owned-by-caller"
sarif_that_is_clean > "$fixture/owned-by-caller/one.sarif"

run_check "$fixture/owned-by-caller"
assert_equals "the caller's directory survives" \
  "yes" \
  "$([ -d "$fixture/owned-by-caller" ] && echo yes || echo no)"
assert_equals "the caller's log survives" \
  "yes" \
  "$([ -f "$fixture/owned-by-caller/one.sarif" ] && echo yes || echo no)"

printf '  the check leaves no temporary directory behind\n'

# The branch that builds for itself — the one CI takes, since ci.yml invokes the script
# with no argument. It mktemp -d's a directory for the compiler's logs and traps its
# removal; it then mktemp's a file for the findings and traps THAT. The second trap
# replaced the first, so every run leaked a full build's worth of SARIF logs.
#
# Exercised with a stub `dotnet` on PATH rather than a real build: the leak is in the
# script's cleanup, not in the compiler, and a test that took four minutes to observe
# a missing `rm` would not be run.
stub="$fixture/stub"
mkdir -p "$stub"

cat > "$stub/dotnet" <<'STUB'
#!/bin/sh
# Writes one clean SARIF log wherever the script asked for it, and reports success.
for arg in "$@"; do
  case "$arg" in
    -p:DiagnosticLogDirectory=*) out="${arg#-p:DiagnosticLogDirectory=}" ;;
    *) ;;
  esac
done
[ -n "${out:-}" ] || { printf 'stub dotnet: no DiagnosticLogDirectory\n' >&2; exit 1; }
cat > "${out}/Stub.sarif" <<'JSON'
{ "version": "1.0.0", "runs": [ { "tool": { "driver": { "rules": [] } }, "results": [] } ] }
JSON
STUB
chmod +x "$stub/dotnet"

# TMPDIR is what mktemp reads, so pointing it at an empty directory makes whatever the
# script fails to remove the only thing left in it.
sandbox="$fixture/sandbox"
mkdir -p "$sandbox"

status=0
( cd "$root" && TMPDIR="$sandbox" PATH="$stub:$PATH" "$check" ) >"$fixture/out.txt" 2>&1 || status=$?

assert_equals "the self-building run passes over a clean log" 0 "$status"
assert_empty "nothing is left in TMPDIR" \
  "$(find "$sandbox" -mindepth 1 2>/dev/null)"

finish
