#!/bin/sh
# tools/trains.sh — the single source of truth for the release trains.
#
# projects_of decides what a release publishes. A project it finds is packed and
# pushed to nuget.org; a project it misses is silently absent from its own release.
# Neither mistake shows up as a red build.
set -eu

root="$(cd "$(dirname "$0")/../.." && pwd)"
. "$root/tools/tests/assert.sh"
. "$root/tools/trains.sh"

fixture="$(mktemp -d)"
trap 'rm -rf "$fixture"' EXIT

mkdir -p "$fixture/declared" "$fixture/commented" "$fixture/spanning" "$fixture/other"

cat > "$fixture/declared/Declared.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <ReleaseTrain>sonar</ReleaseTrain>
  </PropertyGroup>
</Project>
XML

# The shape DiagnosticCatalog.Sonar.csproj could not write: a comment naming the
# train the project will join later. It declares nothing, so it must enrol nothing —
# that project still carries a cross-train ProjectReference ADR-0007 forbids packing.
cat > "$fixture/commented/Commented.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <!--
    This will belong on <ReleaseTrain>sonar</ReleaseTrain> once lib ships, but not
    yet: it still holds a ProjectReference across trains.
  -->
</Project>
XML

# The same, with the element split over several lines — invisible to a line-oriented
# matcher whichever way it is written, and the reason comment stripping needs state.
cat > "$fixture/spanning/Spanning.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <!-- one day:
    <ReleaseTrain>
      sonar
    </ReleaseTrain>
  -->
</Project>
XML

cat > "$fixture/other/Other.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <ReleaseTrain>lib</ReleaseTrain>
  </PropertyGroup>
</Project>
XML

cd "$fixture"

# --- projects_of ---------------------------------------------------------------
assert_equals 'a declared train enrols the project' \
  'declared/Declared.csproj' "$(projects_of sonar)"

assert_empty 'a train named only inside a comment enrols nothing' \
  "$(cd "$fixture/commented" && projects_of sonar)"

assert_empty 'a train named inside a multi-line comment enrols nothing' \
  "$(cd "$fixture/spanning" && projects_of sonar)"

assert_equals 'trains do not bleed into each other' \
  'other/Other.csproj' "$(projects_of lib)"

assert_empty 'a train nothing declares publishes nothing' \
  "$(projects_of stylecop)"

# --- declared_trains -----------------------------------------------------------
assert_equals 'only declared trains are reported' \
  'lib
sonar' "$(declared_trains)"

assert_empty 'a comment contributes no declared train' \
  "$(cd "$fixture/commented" && declared_trains)"

# --- the row table ---------------------------------------------------------------
cd "$root"

assert_equals 'every train is routed by its own tag prefix' \
  'sonar' "$(train_of_tag sonar-v1.2.3)"

assert_empty 'a tag matching no train routes nowhere' \
  "$(train_of_tag v1.2.3)"

assert_equals 'a train id resolves to its prefix' 'lib-v' "$(prefix_of lib)"

assert_empty 'an unknown train resolves to nothing' "$(prefix_of nope)"

# require_train writes to stderr and returns 1; both halves matter, since callers
# decide their own exit code from the status and the operator reads the message.
if require_train nope 2>/dev/null; then
  assert_equals 'an unknown train is refused' 'refused' 'accepted'
else
  assert_equals 'an unknown train is refused' 'refused' 'refused'
fi

if require_train lib 2>/dev/null; then
  assert_equals 'a known train is accepted' 'accepted' 'accepted'
else
  assert_equals 'a known train is accepted' 'accepted' 'refused'
fi

# Every train in the table must be routable from its own prefix, so a row added with
# a prefix that shadows another cannot pass unnoticed.
for id in $(train_ids); do
  assert_equals "the $id train routes its own tag" \
    "$id" "$(train_of_tag "$(prefix_of "$id")1.0.0")"
done

finish
