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

# A project file that reached a build output — a test copying what the repository publishes
# beside its binary put these there, and `dotnet pack` was duly handed one and failed on it for
# want of a restore. It is a real .csproj by every test a grep can make; only where it sits says
# otherwise.
mkdir -p "$fixture/built/bin/Release/net10.0" "$fixture/built/obj/Debug"

cat > "$fixture/built/bin/Release/net10.0/Copied.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <ReleaseTrain>netanalyzers</ReleaseTrain>
  </PropertyGroup>
</Project>
XML

cat > "$fixture/built/obj/Debug/Copied.csproj" <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <ReleaseTrain>netanalyzers</ReleaseTrain>
  </PropertyGroup>
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

assert_empty 'a project file under bin/ or obj/ enrols nothing' \
  "$(projects_of netanalyzers)"

# --- declared_trains -----------------------------------------------------------
assert_equals 'only declared trains are reported' \
  'lib
sonar' "$(declared_trains)"

assert_empty 'a comment contributes no declared train' \
  "$(cd "$fixture/commented" && declared_trains)"

# 'only declared trains are reported' above covers this too — the copies declare netanalyzers,
# which that assertion would report were they read. Stated separately so a failure names the
# build output rather than the table.
assert_empty 'a project file under bin/ or obj/ declares no train' \
  "$(cd "$fixture/built" && declared_trains)"

# --- the row table ---------------------------------------------------------------
cd "$root"

assert_equals 'every train is routed by its own tag prefix' \
  'sonar' "$(train_of_tag sonar-v1.2.3)"

assert_empty 'a tag matching no train routes nowhere' \
  "$(train_of_tag v1.2.3)"

assert_equals 'a train id resolves to its prefix' 'lib-v' "$(prefix_of lib)"

assert_empty 'an unknown train resolves to nothing' "$(prefix_of nope)"

# verdict <command...> — echo the word a status means, so an exit code can be asserted with
# the same assert_equals as every other expectation. Spelling it out is what makes a failure
# readable: "expected: [refused] actual: [accepted]" says what went wrong, where a bare
# status would have to be decoded, and the assertion no longer has to be written once per
# branch with the answer already baked into each.
verdict() {
  if "$@"; then printf 'accepted\n'; else printf 'refused\n'; fi
}

# require_train writes to stderr and returns 1; both halves matter, since callers
# decide their own exit code from the status and the operator reads the message.
assert_equals 'an unknown train is refused' 'refused' "$(verdict require_train nope 2>/dev/null)"
assert_equals 'a known train is accepted' 'accepted' "$(verdict require_train lib 2>/dev/null)"

# Every train in the table must be routable from its own prefix, so a row added with
# a prefix that shadows another cannot pass unnoticed.
for id in $(train_ids); do
  assert_equals "the $id train routes its own tag" \
    "$id" "$(train_of_tag "$(prefix_of "$id")1.0.0")"
done

finish
