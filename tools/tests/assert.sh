# shellcheck shell=sh
# Assertions for the shell tests under tools/tests/.
#
# A directive rather than a shebang: this file is sourced, never executed, and a
# shebang would claim otherwise. shellcheck needs to be told the dialect either way.
#
# Meant to be SOURCED by a test-*.sh, not executed. Each test file runs as its own
# process, so the counters below are per-file and need no subshell gymnastics — a
# `while read` loop updating a counter in a pipeline would lose it, which is the
# usual way a shell test harness silently reports success.
#
# There is no framework here on purpose. tools/ is POSIX sh with no dependency
# beyond a shell, and a test harness that needed bats, or a package manager, would
# be a heavier thing to install than the code it checks.

_assert_failures=0
_assert_total=0

# assert_equals <description> <expected> <actual>
assert_equals() {
  _assert_total=$((_assert_total + 1))
  if [ "$2" = "$3" ]; then
    printf '    ok   %s\n' "$1"
  else
    printf '    FAIL %s\n           expected: [%s]\n           actual:   [%s]\n' "$1" "$2" "$3"
    _assert_failures=$((_assert_failures + 1))
  fi
}

# assert_empty <description> <actual> — the common shape here: a train that
# publishes nothing, a discovery that must find no project.
assert_empty() {
  assert_equals "$1" '' "$2"
}

# finish — print the file's tally and exit with the status the runner reads.
# Every test file ends with this; a file that forgets it exits on its last command's
# status, which would report success no matter how many assertions failed.
finish() {
  if [ "$_assert_failures" -eq 0 ]; then
    printf '    %d assertion(s) passed\n' "$_assert_total"
    exit 0
  fi
  printf '    %d of %d assertion(s) FAILED\n' "$_assert_failures" "$_assert_total"
  exit 1
}
