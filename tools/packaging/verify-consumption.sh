#!/bin/sh
# Restores the produced packages as a real consumer would, and asserts what they do.
#
#     tools/packaging/verify-consumption.sh <version> [artifacts-dir]
#
# The packaging tests of docs §21.7. Every other test in this repository compiles code
# in-process, against project references; none of them sees what NuGet actually hands a
# consumer. Six things can only be observed from the far side of a restore:
#
#   * the analyzer activates for a direct consumer;
#   * its assemblies stay out of the consumer's output folder (§16.1's opening line, and
#     the only check that catches a wrong DevelopmentDependency or PrivateAssets);
#   * the attribute assembly, by contrast, DOES reach it — the foundation carries both, and
#     the two halves must land on opposite sides of that line;
#   * whether the analyzer flows transitively through a package that references it —
#     which §16.3 says to TEST rather than assume, in either direction;
#   * which PrivateAssets value actually decides that;
#   * and, since ADR-0037 folded the analyzers into the foundation, that a consumer of
#     SEVERAL catalogues is checked by exactly ONE analyzer instance rather than one per
#     catalogue, plus what happens one hop further out.
#
# Two of these came out the opposite way round from what the packaging notes assumed, which
# is the whole reason §16.3 asks for a measurement. See the comments above those checks.
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

# ONE package since ADR-0037: the analyzers and code fixes ride inside the foundation rather
# than beside it. There is no DiagnosticCatalog.Analyzers.nupkg to look for, which is itself
# worth asserting — a stray one would mean the packaging split had crept back.
foundation="$artifacts/DiagnosticCatalog.$version.nupkg"

# Packing when the package is absent keeps a local run a single command, while a CI run
# that has just packed does not pay for it twice.
if [ ! -f "$foundation" ]; then
  printf 'packing the lib train at %s\n' "$version"
  "$root/tools/packaging/pack.sh" "$version" lib >/dev/null
fi

if [ ! -f "$foundation" ]; then
  printf 'error: %s was not produced; nothing to consume\n' "$foundation" >&2
  exit 1
fi

work="$(mktemp -d)"
# shellcheck disable=SC2064  # $work must expand now: the trap fires after it leaves scope.
trap "rm -rf '$work'" EXIT

feed="$work/feed"
mkdir -p "$feed"
cp "$foundation" "$feed/"

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

# The same offender, for a consumer that cannot see the foundation at all.
#
# Needed only since ADR-0037, and its necessity is a finding rather than a fixture detail. A
# catalogue opting out of imposing analysis writes PrivateAssets="all", and that used to hide the
# analyzer package while the foundation kept flowing through its own separate reference. One
# package means one lever: the opt-out now hides the ATTRIBUTE too, and a consumer written the
# ordinary way stops compiling — CS0246 on [DiagnosticRule], not a missing diagnostic.
#
# §7.2 is what makes the case still measurable: the marker is matched by its fully qualified
# metadata name, so a consumer may declare its own and the analyzer recognises it. That is also
# the supported escape for anyone who wants the rules without the package dependency.
cat > "$work/OffenderSelfDeclared.cs" <<'EOF'
namespace DiagnosticCatalog
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    internal sealed class DiagnosticRuleAttribute : System.Attribute
    {
    }
}

public static class Malformed
{
    [DiagnosticCatalog.DiagnosticRule]
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

# analyzer_instances <name> — how many DiagnosticCatalog.Analyzers.dll the compiler is handed.
#
# Asked of MSBuild rather than counted in the build log, because a log cannot answer it: MSBuild
# echoes each warning a second time in its summary, so a raw count reads 2 for a single analyzer,
# and Roslyn may collapse two identical diagnostics into one, so a count can also read 1 for two
# analyzers. The item list is the compiler's actual input and says neither more nor less.
analyzer_instances() {
  ( cd "$work/$1" && dotnet msbuild "$1.csproj" -t:ResolveReferences -getItem:Analyzer 2>/dev/null ) \
    | grep '"Identity"' \
    | grep -c 'DiagnosticCatalog\.Analyzers\.dll' || true
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
"    <PackageReference Include=\"DiagnosticCatalog\" Version=\"$version\" />" \
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

# ONE reference, and it is the foundation. Before ADR-0037 this fixture named a second package;
# that it no longer has to is the whole point of the record, so the check reads as its own proof.
check 'the analyzer activates for a direct consumer' yes "$(reported Direct)"

# §16.1's opening line: an analysis assembly must never become a runtime dependency of the
# consuming application. A wrong DevelopmentDependency or PrivateAssets puts it here, and
# nothing else in the repository would notice.
#
# Sharper since the fold. One package now carries assemblies that must land on OPPOSITE sides of
# that line — analyzers/ must not reach the output, lib/ must — so a packaging slip that moved an
# analyzer into lib/ would be invisible to every other test and caught only here.
check 'the analyzer assembly stays out of the output folder' \
  '' "$(in_output Direct DiagnosticCatalog.Analyzers.dll)"
check 'the code fixes stay out of the output folder' \
  '' "$(in_output Direct DiagnosticCatalog.CodeFixes.dll)"

# The other side of that line, and not symmetrical with it. §16.1 requires the attribute assembly
# to REACH the consumer: a catalogue's rule types carry [DiagnosticRule] and reflection over them
# resolves it at run time. Making the foundation a DevelopmentDependency — the obvious way to keep
# the analyzers out of the output — would break exactly this, silently.
check 'the attribute assembly does reach the output folder' \
  'DiagnosticCatalog.dll' "$(in_output Direct DiagnosticCatalog.dll)"

printf '\nTransitivity through a catalogue package (§16.3)\n'

# Three catalogue packages differing ONLY in PrivateAssets, so the comparison isolates that
# one setting. §16.3 refuses to assume either answer; this reports whichever it is.
#
# The setting now sits on the reference every catalogue in this repository already carries —
# the one to the foundation, which a catalogue may not hide because [DiagnosticRule] has to
# resolve for its own consumers. So the flavours below are no longer three ways of wiring an
# extra package: they are three ways of treating the only dependency a catalogue has, and
# `Default` is what all thirteen of ours are written as today.
for flavour in Default None All; do
  private=''
  [ "$flavour" = 'None' ] && private=' PrivateAssets="none"'
  [ "$flavour" = 'All' ] && private=' PrivateAssets="all"'

  project "Catalog$flavour" \
"    <PackageReference Include=\"DiagnosticCatalog\" Version=\"$version\"$private />" \
"<PackageId>Acme.Catalog.$flavour</PackageId><Version>1.0.0</Version>"

  if ! ( cd "$work/Catalog$flavour" && dotnet pack -c Release -o "$feed" > "$work/pack-$flavour.log" 2>&1 ); then
    printf 'error: the %s catalogue fixture failed to pack\n' "$flavour" >&2
    sed -n '1,60p' "$work/pack-$flavour.log" >&2
    exit 1
  fi

  project "Consumer$flavour" \
"    <PackageReference Include=\"Acme.Catalog.$flavour\" Version=\"1.0.0\" />"

  # The All consumer cannot see the foundation — its catalogue hid it — so it declares the marker
  # itself. See the fixture's comment: this is the coupling ADR-0037 introduced, not a workaround.
  if [ "$flavour" = 'All' ]; then
    cp "$work/OffenderSelfDeclared.cs" "$work/Consumer$flavour/"
  else
    cp "$work/Offender.cs" "$work/Consumer$flavour/"
  fi

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

# And what that opt-out now costs, which is the price of one package rather than two. Hiding the
# foundation hides the attribute with the analyzer, so this consumer had to declare its own marker
# to compile at all. A catalogue writing PrivateAssets="all" is therefore choosing the failure §7.2
# describes and doc/guide/troubleshooting reports — it is no longer a way to be polite about
# analysis.
check 'hiding the foundation also withholds the attribute assembly' \
  '' "$(in_output ConsumerAll DiagnosticCatalog.dll)"

printf '\nSeveral catalogues at once (ADR-0037)\n'

# The scenario the fold exists for, and the one nothing measured before: somebody references two
# catalogues. Because both reach the analyzers through the SAME package identity, NuGet unifies it
# and the compiler is handed one analyzer.
#
# This is the check that would fail if the analyzers were ever folded into the catalogue packages
# instead — the alternative ADR-0037 rejected. There the assemblies arrive from different package
# identities, so NuGet has nothing to unify, and which version runs is settled by conflict
# resolution between packages that version independently. That failure is silent in every other
# test: the diagnostics do not duplicate, one assembly simply wins, and the losing catalogue's
# consumers are checked by a version it never shipped.
project TwoCatalogues \
"    <PackageReference Include=\"Acme.Catalog.Default\" Version=\"1.0.0\" />
    <PackageReference Include=\"Acme.Catalog.None\" Version=\"1.0.0\" />"
cp "$work/Offender.cs" "$work/TwoCatalogues/"
build TwoCatalogues

check 'two catalogues still deliver the analyzer' yes "$(reported TwoCatalogues)"
check 'two catalogues deliver exactly one analyzer instance' \
  1 "$(analyzer_instances TwoCatalogues)"

printf '\nOne hop further out\n'

# A LIBRARY that references a catalogue for its own suppressions, and an application that
# references that library. Nobody in this chain chose the analyzer, and the application chose
# neither the catalogue nor the library's reasons for taking it.
#
# §16.3 measured one hop and stopped there, so this is the first time the second is asked. The
# expected value below is the measured answer, not a preference: it is recorded here so that a
# NuGet release changing it fails a pull request instead of changing what every consumer of every
# library built on a catalogue sees.
project Library \
"    <PackageReference Include=\"Acme.Catalog.Default\" Version=\"1.0.0\" />" \
"<PackageId>Acme.Library</PackageId><Version>1.0.0</Version>"

if ! ( cd "$work/Library" && dotnet pack -c Release -o "$feed" > "$work/pack-Library.log" 2>&1 ); then
  printf 'error: the library fixture failed to pack\n' >&2
  sed -n '1,60p' "$work/pack-Library.log" >&2
  exit 1
fi

project TwoHops \
"    <PackageReference Include=\"Acme.Library\" Version=\"1.0.0\" />"
cp "$work/Offender.cs" "$work/TwoHops/"
build TwoHops

# MEASURED: yes. It travels the second hop as readily as the first, so a library that took a
# catalogue for its own suppressions hands error-severity diagnostics to every application that
# references it — an application that chose neither the catalogue nor the library's reasons for it.
#
# The lever is on the library, and it is not the one a catalogue has. A catalogue may not hide the
# foundation, because [DiagnosticRule] has to resolve for its consumers; a library owes nobody
# that, so PrivateAssets="all" on its reference is free and correct. The check below is what says
# the mitigation actually works rather than merely sounding right.
check 'the analyzer reaches a consumer two hops from the foundation' \
  yes "$(reported TwoHops)"

project QuietLibrary \
"    <PackageReference Include=\"Acme.Catalog.Default\" Version=\"1.0.0\" PrivateAssets=\"all\" />" \
"<PackageId>Acme.QuietLibrary</PackageId><Version>1.0.0</Version>"

if ! ( cd "$work/QuietLibrary" && dotnet pack -c Release -o "$feed" > "$work/pack-QuietLibrary.log" 2>&1 ); then
  printf 'error: the quiet library fixture failed to pack\n' >&2
  sed -n '1,60p' "$work/pack-QuietLibrary.log" >&2
  exit 1
fi

project QuietHops \
"    <PackageReference Include=\"Acme.QuietLibrary\" Version=\"1.0.0\" />"
cp "$work/OffenderSelfDeclared.cs" "$work/QuietHops/"
build QuietHops

check 'a library can decline to pass the analyzer on' no "$(reported QuietHops)"

printf '\n'

if [ "$failures" -eq 0 ]; then
  printf '%d check(s) passed\n' "$total"
  exit 0
fi

printf '%d of %d check(s) FAILED\n' "$failures" "$total" >&2
exit 1
