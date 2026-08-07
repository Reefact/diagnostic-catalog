#!/bin/sh
# tools/packaging/check-changelog.sh — refusing a release nothing documents.
#
# The check exists because fourteen packages shipped at 1.0.0 against changelogs that
# said they had not. Its own failure mode is the quiet one: a guard that answers "ok"
# to everything looks exactly like a repository that is in order, so the cases below
# are written to catch that rather than to confirm the happy path.
#
# Fixtures rather than the real tree: the answer for a real train changes every time
# somebody edits a changelog, and a test that green-lights itself out of the working
# copy proves nothing about the rule.
set -eu

root="$(cd "$(dirname "$0")/../.." && pwd)"
. "$root/tools/tests/assert.sh"

fixture="$(mktemp -d)"
trap 'rm -rf "$fixture"' EXIT

# A tree shaped like the repository: a catalogue project declaring its train, with its
# changelog beside it, and a root CHANGELOG.md standing for the lib train's — whose own
# projects carry none.
mkdir -p "$fixture/src/DiagnosticCatalog.Sonar" "$fixture/src/DiagnosticCatalog"

cat > "$fixture/src/DiagnosticCatalog.Sonar/DiagnosticCatalog.Sonar.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <ReleaseTrain>sonar</ReleaseTrain>
  </PropertyGroup>
</Project>
XML

cat > "$fixture/src/DiagnosticCatalog/DiagnosticCatalog.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <ReleaseTrain>lib</ReleaseTrain>
  </PropertyGroup>
</Project>
XML

cat > "$fixture/src/DiagnosticCatalog.Sonar/CHANGELOG.md" <<'MD'
# Changelog

## [Unreleased]

_No other change yet._

## [1.0.0] - 2026-08-07

**Mirrors `SonarAnalyzer.CSharp 10.31.0.145097`.**

## [0.2.1] - 2026-07-31

Something older.
MD

cat > "$fixture/CHANGELOG.md" <<'MD'
# Changelog

## [Unreleased]

_Nothing yet._

## [1.0.1] - 2026-08-07

The foundation.
MD

# `ok` / `rejected` rather than a status code, so a failing assertion prints which way
# round it went instead of leaving the reader to decode 0 and 1.
OK='ok'
REJECTED='rejected'

check() {
  # check <train> <version>
  if "$root/tools/packaging/check-changelog.sh" "$1" "$2" "$fixture" >/dev/null 2>&1; then
    printf '%s' "$OK"
  else
    printf '%s' "$REJECTED"
  fi
}

assert_equals 'a dated entry beside the project is accepted' \
  "$OK" "$(check sonar 1.0.0)"

assert_equals 'the lib train falls back to the root changelog' \
  "$OK" "$(check lib 1.0.1)"

assert_equals 'an older documented version is still accepted' \
  "$OK" "$(check sonar 0.2.1)"

assert_equals 'a version nothing documents is refused' \
  "$REJECTED" "$(check sonar 2.0.0)"

assert_equals 'the lib train is refused on an undocumented version' \
  "$REJECTED" "$(check lib 9.9.9)"

assert_equals 'an unknown train is refused rather than passed over' \
  "$REJECTED" "$(check nosuchtrain 1.0.0)"

# The defect that motivated the check: the content is written, the heading still says
# it never shipped. A guard reading the file for the version STRING rather than for a
# heading would answer ok here, because "1.0.0" does appear in the prose.
cat > "$fixture/src/DiagnosticCatalog.Sonar/CHANGELOG.md" <<'MD'
# Changelog

## [Unreleased]

**Mirrors `SonarAnalyzer.CSharp 10.31.0.145097`.** Everything below ships in 1.0.0.

### Added

* 400 rules.
MD

assert_equals 'content left under Unreleased is refused' \
  "$REJECTED" "$(check sonar 1.0.0)"

# An entry somebody opened and never closed. Undated, it is indistinguishable from one
# that was never released — which is the state 1.0.0-preview.1 was in for four trains.
cat > "$fixture/src/DiagnosticCatalog.Sonar/CHANGELOG.md" <<'MD'
# Changelog

## [1.0.0]

Written, never dated.
MD

assert_equals 'an undated heading is refused' \
  "$REJECTED" "$(check sonar 1.0.0)"

# A SemVer core is full of dots, and grep reads them as "any character". Unescaped,
# 1.0.0 also matches 1x0y0 — so a changelog documenting a neighbour would let the
# wrong release through, which is worse than letting nothing through.
cat > "$fixture/src/DiagnosticCatalog.Sonar/CHANGELOG.md" <<'MD'
# Changelog

## [1x0y0] - 2026-08-07

A version that does not exist.
MD

assert_equals 'the version is matched literally, not as a pattern' \
  "$REJECTED" "$(check sonar 1.0.0)"

# A pre-release publishes like any other version and is documented like one.
cat > "$fixture/src/DiagnosticCatalog.Sonar/CHANGELOG.md" <<'MD'
# Changelog

## [1.1.0-rc.1] - 2026-08-07

A rehearsal that ships.
MD

assert_equals 'a pre-release version is accepted when documented' \
  "$OK" "$(check sonar 1.1.0-rc.1)"

assert_equals 'the release core does not stand in for its pre-release' \
  "$REJECTED" "$(check sonar 1.1.0)"

# A train whose projects carry no changelog and no root file to fall back to. Refused
# rather than passed over: nothing to read is not the same as nothing to say.
missing="$(mktemp -d)"
trap 'rm -rf "$fixture" "$missing"' EXIT
mkdir -p "$missing/src/DiagnosticCatalog.Xunit"
cat > "$missing/src/DiagnosticCatalog.Xunit/DiagnosticCatalog.Xunit.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <ReleaseTrain>xunit</ReleaseTrain>
  </PropertyGroup>
</Project>
XML

if "$root/tools/packaging/check-changelog.sh" xunit 1.0.0 "$missing" >/dev/null 2>&1; then
  missing_verdict="$OK"
else
  missing_verdict="$REJECTED"
fi

assert_equals 'a train with no changelog at all is refused' \
  "$REJECTED" "$missing_verdict"

finish
