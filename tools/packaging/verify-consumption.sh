#!/bin/sh
# Restores the produced packages as a real consumer would, and asserts what they do.
#
#     tools/packaging/verify-consumption.sh <version> [artifacts-dir]
#
# The packaging tests of docs §21.7. Every other test in this repository compiles code
# in-process, against project references; none of them sees what NuGet actually hands a
# consumer. Four things can only be observed from the far side of a restore:
#
#   * the analyzer activates for a direct consumer;
#   * its assemblies stay out of the consumer's output folder (§16.1's opening line, and
#     the only check that catches a wrong DevelopmentDependency or PrivateAssets);
#   * whether the analyzer flows transitively through a package that references it —
#     which §16.3 says to TEST rather than assume, in either direction;
#   * which PrivateAssets value actually decides that.
#
# The last one came out the opposite way round from what the packaging notes assumed, which
# is the whole reason §16.3 asks for a measurement. See the comment above those checks.
#
# The fixtures are written to a throwaway directory rather than checked in. They must
# compile OUTSIDE this repository's Directory.Build.props — a consumer has no such file,
# and one of them deliberately carries a rule the analyzer reports, which the repository's
# warnings-as-errors ratchet would turn into a failed build.
set -eu

usage() {
  printf 'usage: tools/packaging/verify-consumption.sh <version> [artifacts-dir]\n' >&2
  exit 2
}

[ $# -ge 1 ] || usage

version="$1"
root="$(cd "$(dirname "$0")/../.." && pwd)"
artifacts="${2:-$root/artifacts}"

foundation="$artifacts/DiagnosticCatalog.$version.nupkg"
analyzers="$artifacts/DiagnosticCatalog.Analyzers.$version.nupkg"

# Packing when the packages are absent keeps a local run a single command, while a CI run
# that has just packed does not pay for it twice.
if [ ! -f "$foundation" ] || [ ! -f "$analyzers" ]; then
  printf 'packing the lib train at %s\n' "$version"
  "$root/tools/packaging/pack.sh" "$version" lib >/dev/null
fi

for package in "$foundation" "$analyzers"; do
  if [ ! -f "$package" ]; then
    printf 'error: %s was not produced; nothing to consume\n' "$package" >&2
    exit 1
  fi
done

work="$(mktemp -d)"
# shellcheck disable=SC2064  # $work must expand now: the trap fires after it leaves scope.
trap "rm -rf '$work'" EXIT

feed="$work/feed"
mkdir -p "$feed"
cp "$foundation" "$analyzers" "$feed/"

# Only the local feed, and an absolute path to it. A consumer restoring from nuget.org
# would silently resolve a PUBLISHED DiagnosticCatalog instead of the one just built, and
# the test would report on a package nobody changed.
cat > "$work/NuGet.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$feed" />
  </packageSources>
</configuration>
EOF

# A rule missing its Id: the analyzer reports DCAT0003 on it. Warning severity, so the
# consumer still builds — the diagnostic is the observation, not a failure.
cat > "$work/Offender.cs" <<'EOF'
using DiagnosticCatalog;

public static class Malformed
{
    [DiagnosticRule]
    public static class MissingItsId
    {
        public const string Category = "Usage";
    }
}
EOF

failures=0
total=0

# check <description> <expected> <actual>
check() {
  total=$((total + 1))
  if [ "$2" = "$3" ]; then
    printf '  ok   %s\n' "$1"
  else
    printf '  FAIL %s\n         expected: [%s]\n         actual:   [%s]\n' "$1" "$2" "$3"
    failures=$((failures + 1))
  fi
}

# project <name> <item-group-xml> [property-xml] — writes a buildable consumer.
project() {
  mkdir -p "$work/$1"
  cp "$work/NuGet.config" "$work/$1/NuGet.config"
  cat > "$work/$1/$1.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    ${3:-}
  </PropertyGroup>
  <ItemGroup>
$2
  </ItemGroup>
</Project>
EOF
}

# build <name> — build it; the log is always written, success or not.
build() {
  ( cd "$work/$1" && dotnet build -c Release -v normal > "$work/$1.log" 2>&1 ) || true

  if ! grep -q 'Build succeeded' "$work/$1.log"; then
    printf 'error: %s did not build; the rest cannot be interpreted\n' "$1" >&2
    sed -n '1,80p' "$work/$1.log" >&2
    exit 1
  fi
}

# reported <name> — 'yes' when the analyzer fired in that build, 'no' otherwise.
reported() {
  if grep -q 'DCAT0003' "$work/$1.log"; then printf 'yes\n'; else printf 'no\n'; fi
}

# in_output <name> <file> — echoes the file name when it reached the output folder.
in_output() {
  if [ -f "$work/$1/bin/Release/net10.0/$2" ]; then printf '%s\n' "$2"; fi
}

printf '\nA direct consumer\n'

# An APPLICATION, and without PrivateAssets. Both matter, and both were got wrong first:
#
#   * A class library never copies package assemblies to its output folder at all, so the checks
#     below would pass against a package that had put the analyzer in lib/ — measuring the SDK's
#     copy rules rather than the package. §16 says "the consuming application"; this is one.
#   * PrivateAssets="all" on the reference would suppress every asset, so again nothing could leak
#     however the package was built. What has to protect this consumer is the package's own
#     DevelopmentDependency, which is only observable when the consumer took no precaution — and
#     that is also the reference most people actually write.
project Direct \
"    <PackageReference Include=\"DiagnosticCatalog\" Version=\"$version\" />
    <PackageReference Include=\"DiagnosticCatalog.Analyzers\" Version=\"$version\" />" \
"<OutputType>Exe</OutputType>"
cp "$work/Offender.cs" "$work/Direct/"
cat > "$work/Direct/Program.cs" <<'PROGRAM'
public static class Program
{
    public static void Main()
    {
    }
}
PROGRAM

build Direct

check 'the analyzer activates for a direct consumer' yes "$(reported Direct)"

# §16.1's opening line: an analysis assembly must never become a runtime dependency of the
# consuming application. A wrong DevelopmentDependency or PrivateAssets puts it here, and
# nothing else in the repository would notice.
check 'the analyzer assembly stays out of the output folder' \
  '' "$(in_output Direct DiagnosticCatalog.Analyzers.dll)"
check 'the code fixes stay out of the output folder' \
  '' "$(in_output Direct DiagnosticCatalog.CodeFixes.dll)"

printf '\nTransitivity through a catalogue package (§16.3)\n'

# Three catalogue packages differing ONLY in PrivateAssets, so the comparison isolates that
# one setting. §16.3 refuses to assume either answer; this reports whichever it is.
for flavour in Default None All; do
  private=''
  [ "$flavour" = 'None' ] && private=' PrivateAssets="none"'
  [ "$flavour" = 'All' ] && private=' PrivateAssets="all"'

  project "Catalog$flavour" \
"    <PackageReference Include=\"DiagnosticCatalog\" Version=\"$version\" />
    <PackageReference Include=\"DiagnosticCatalog.Analyzers\" Version=\"$version\"$private />" \
"<PackageId>Acme.Catalog.$flavour</PackageId><Version>1.0.0</Version>"

  if ! ( cd "$work/Catalog$flavour" && dotnet pack -c Release -o "$feed" > "$work/pack-$flavour.log" 2>&1 ); then
    printf 'error: the %s catalogue fixture failed to pack\n' "$flavour" >&2
    sed -n '1,60p' "$work/pack-$flavour.log" >&2
    exit 1
  fi

  project "Consumer$flavour" \
"    <PackageReference Include=\"Acme.Catalog.$flavour\" Version=\"1.0.0\" />"
  cp "$work/Offender.cs" "$work/Consumer$flavour/"

  build "Consumer$flavour"
done

# THE MEASURED ANSWER, and it is not the one NuGet's documentation implies. Analyzers are
# documented as non-transitive by default, and DiagnosticCatalog.Analyzers additionally sets
# DevelopmentDependency — yet the analyzer DOES reach a consumer of a catalogue that took no
# position. This is the behaviour NuGet/Home#13813 reports, and it is exactly why §16.3 says
# to test rather than assume.
#
# The consequence is inverted from what the packaging notes assumed: propagation is the
# DEFAULT, and the lever a catalogue needs is PrivateAssets="all" to opt OUT of imposing
# analysis on its consumers. PrivateAssets="none" is confirmed to work but changes nothing.
check 'a catalogue that takes no position propagates the analyzer' \
  yes "$(reported ConsumerDefault)"
check 'PrivateAssets="none" propagates the analyzer to a catalogue consumer' \
  yes "$(reported ConsumerNone)"
check 'PrivateAssets="all" is what stops a catalogue propagating it' \
  no "$(reported ConsumerAll)"

printf '\n'

if [ "$failures" -eq 0 ]; then
  printf '%d check(s) passed\n' "$total"
  exit 0
fi

printf '%d of %d check(s) FAILED\n' "$failures" "$total" >&2
exit 1
