#!/bin/sh
# Restores the produced packages as a real consumer would, and asserts what they do.
#
#     tools/packaging/verify-consumption.sh <version> [artifacts-dir]
#
# The packaging tests of docs §21.7. Every other test in this repository compiles code
# in-process, against project references; none of them sees what NuGet actually hands a
# consumer. These things can only be observed from the far side of a restore:
#
#   * the analyzer activates for a direct consumer of the foundation;
#   * it activates for a consumer of a CATALOGUE, which is the arrangement ADR-0037 exists
#     for and which no reference in the consumer's project asks for;
#   * it does NOT activate one hop further out, which is the whole of ADR-0038;
#   * its assemblies stay out of the consumer's output folder (§16.1's opening line, and
#     the only check that catches a wrong DevelopmentDependency or PrivateAssets);
#   * the attribute assembly, by contrast, DOES reach it — the foundation carries both, and
#     the two halves must land on opposite sides of that line — and it keeps reaching the
#     consumer two hops out, who must still compile;
#   * a consumer of SEVERAL catalogues is checked by exactly ONE analyzer instance rather
#     than one per catalogue;
#   * and that both ends can overrule the default, in either direction.
#
# Several of these came out the opposite way round from what the packaging notes assumed,
# which is why §16.3 asks for a measurement rather than a reading of NuGet's documentation.
# See the comments above those checks.
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

# The fixture catalogues below are always 1.0.0, and a previous run of this script leaves them
# extracted in the global packages folder. Without this, a repacked fixture of the same id never
# reaches a build and the script silently measures whatever was packed first.
for id in acme.catalog.a acme.catalog.b acme.catalog.silent acme.catalog.hidden acme.library; do
  rm -rf "${NUGET_PACKAGES:-$HOME/.nuget/packages}/$id"
done

# And the foundation at THIS version, for the same reason and with sharper consequences: a run
# after a packaging change would restore the extraction of the package built before it, and every
# check below would report on a layout nobody had just produced. This is not hypothetical — it is
# how the first run of the ADR-0038 fixtures came back green on the old arrangement.
#
# The version is named rather than the whole folder, which also holds the PUBLISHED versions the
# repository's own build restores.
rm -rf "${NUGET_PACKAGES:-$HOME/.nuget/packages}/diagnosticcatalog/$version"

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

# The same offender, for the one consumer that cannot see the foundation at all — the
# catalogue below that hides it. §7.2 is what makes that case still measurable: the marker is
# matched by its fully qualified metadata name, so a consumer may declare its own and the
# analyzer recognises it. That is also the supported escape for anyone who wants the rules
# without the package dependency.
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

# Every consumer that is asked what reached its OUTPUT folder must be an application. A class
# library never copies package assemblies at all, so an in_output check against one measures the
# SDK's copy rules and passes whatever the package did.
cat > "$work/Program.cs" <<'EOF'
public static class Program
{
    public static void Main()
    {
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

# catalogue <package-id> <opt-in|silent|hidden> — packs a catalogue fixture into the feed.
#
# The opt-in flavour ships build/<its id>.props, which is what Directory.Build.targets packs
# into every catalogue this repository publishes. The file is COPIED from the repository rather
# than written here, so the fixture cannot drift from what we actually ship.
catalogue() {
  id="$1"
  dir="$work/pkg-$id"
  mkdir -p "$dir"
  cp "$work/NuGet.config" "$dir/"

  private=''
  optin=''
  case "$2" in
    opt-in)
      cp "$root/build/CatalogueAnalyzerOptIn.props" "$dir/OptIn.props"
      optin="    <None Include=\"OptIn.props\" Pack=\"true\" PackagePath=\"build/$id.props\" />"
      ;;
    hidden) private=' PrivateAssets="all"' ;;
  esac

  cat > "$dir/$id.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <PackageId>$id</PackageId>
    <Version>1.0.0</Version>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="DiagnosticCatalog" Version="$version"$private />
$optin
  </ItemGroup>
</Project>
EOF

  if ! ( cd "$dir" && dotnet pack -c Release -o "$feed" > "$work/pack-$id.log" 2>&1 ); then
    printf 'error: the %s catalogue fixture failed to pack\n' "$id" >&2
    sed -n '1,60p' "$work/pack-$id.log" >&2
    exit 1
  fi
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

printf '\nA direct consumer of the foundation\n'

# An APPLICATION, and without PrivateAssets. Both matter, and both were got wrong first:
#
#   * A class library never copies package assemblies to its output folder at all, so the checks
#     below would pass against a package that had put the analyzer in lib/ — measuring the SDK's
#     copy rules rather than the package. §16 says "the consuming application"; this is one.
#   * PrivateAssets="all" on the reference would suppress every asset, so again nothing could leak
#     however the package was built. What has to protect this consumer is the package's own
#     layout, which is only observable when the consumer took no precaution — and that is also the
#     reference most people actually write.
#
# Since ADR-0038 this case is no longer served by NuGet resolving an analyzers/ folder: the
# assemblies sit where NuGet resolves nothing, and buildTransitive/DiagnosticCatalog.targets adds
# them after recognising ITSELF among the project's PackageReferences. This is the check that says
# that recognition works.
project Direct \
"    <PackageReference Include=\"DiagnosticCatalog\" Version=\"$version\" />" \
"<OutputType>Exe</OutputType>"
cp "$work/Offender.cs" "$work/Program.cs" "$work/Direct/"

build Direct

check 'the analyzer activates for a direct consumer' yes "$(reported Direct)"
check 'a direct consumer gets exactly one analyzer instance' 1 "$(analyzer_instances Direct)"

# §16.1's opening line: an analysis assembly must never become a runtime dependency of the
# consuming application. A wrong DevelopmentDependency or PrivateAssets puts it here, and
# nothing else in the repository would notice.
#
# Sharper since the fold. One package now carries assemblies that must land on OPPOSITE sides of
# that line — the analyzers must not reach the output, lib/ must — so a packaging slip that moved
# an analyzer into lib/ would be invisible to every other test and caught only here.
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

printf '\nThrough a catalogue (ADR-0037)\n'

catalogue Acme.Catalog.A opt-in
catalogue Acme.Catalog.B opt-in
catalogue Acme.Catalog.Silent silent
catalogue Acme.Catalog.Hidden hidden

# The arrangement ADR-0037 exists for: one reference, to a catalogue, and the consumer is checked
# without naming an analyzer package anywhere. Nothing in this project file mentions the
# foundation.
project ConsumerA "    <PackageReference Include=\"Acme.Catalog.A\" Version=\"1.0.0\" />"
cp "$work/Offender.cs" "$work/ConsumerA/"
build ConsumerA

check 'a catalogue delivers the analyzer to its own consumer' yes "$(reported ConsumerA)"

# And what a catalogue has to do to earn that, which is the contract ADR-0038 puts on catalogue
# authors and doc/guide/packaging-a-catalogue documents. Before ADR-0038 this fixture would have
# been checked too, because NuGet flowed the analyzers folder to everyone: the cost of bounding
# the flow is that a catalogue now has to opt its consumers in.
#
# Silent rather than broken: this consumer compiles, references a catalogue, and is not checked.
# That is the failure mode a third-party catalogue hits by shipping nothing, which is why the
# guide leads with the file rather than mentioning it in passing.
project ConsumerSilent "    <PackageReference Include=\"Acme.Catalog.Silent\" Version=\"1.0.0\" />"
cp "$work/Offender.cs" "$work/ConsumerSilent/"
build ConsumerSilent

check 'a catalogue shipping no opt-in leaves its consumer unchecked' \
  no "$(reported ConsumerSilent)"

# A catalogue that hides the foundation hides BOTH halves, because there is one package and one
# lever. This consumer had to declare its own marker to compile at all — it is choosing the §7.2
# failure doc/guide/troubleshooting reports, not being polite about analysis.
project ConsumerHidden \
"    <PackageReference Include=\"Acme.Catalog.Hidden\" Version=\"1.0.0\" />" \
"<OutputType>Exe</OutputType>"
cp "$work/OffenderSelfDeclared.cs" "$work/Program.cs" "$work/ConsumerHidden/"
build ConsumerHidden

check 'a catalogue hiding the foundation withholds the attribute assembly' \
  '' "$(in_output ConsumerHidden DiagnosticCatalog.dll)"
check 'a catalogue hiding the foundation delivers no analyzer either' \
  no "$(reported ConsumerHidden)"

printf '\nSeveral catalogues at once (ADR-0037)\n'

# Somebody references two catalogues. Both reach the analyzers through the SAME package identity,
# so NuGet unifies it and the compiler is handed one analyzer.
#
# This is the check that would fail if the analyzers were ever folded into the catalogue packages
# instead — the alternative ADR-0037 rejected and ADR-0038 had to reject again, because a gate in
# each catalogue would ADD the assemblies by path and MSBuild has no identity to unify. There the
# assemblies arrive from packages that version independently, and which one runs is settled by
# whichever catalogue happens to carry the highest.
project TwoCatalogues \
"    <PackageReference Include=\"Acme.Catalog.A\" Version=\"1.0.0\" />
    <PackageReference Include=\"Acme.Catalog.B\" Version=\"1.0.0\" />"
cp "$work/Offender.cs" "$work/TwoCatalogues/"
build TwoCatalogues

check 'two catalogues still deliver the analyzer' yes "$(reported TwoCatalogues)"
check 'two catalogues deliver exactly one analyzer instance' \
  1 "$(analyzer_instances TwoCatalogues)"

printf '\nOne hop further out (ADR-0038)\n'

# A LIBRARY that references a catalogue for its own suppressions, and an application that
# references that library. Nobody in this chain chose the analyzer, and the application chose
# neither the catalogue nor the library's reasons for taking it.
#
# The library's reference is the ORDINARY one — no PrivateAssets, no precaution. That is the
# point: under ADR-0037 this application inherited error-severity diagnostics on its own
# suppressions, from a catalogue it had never heard of, and the only lever belonged to a library
# author who had no reason to think about it. The gate moves the lever to the producer.
project Library \
"    <PackageReference Include=\"Acme.Catalog.A\" Version=\"1.0.0\" />" \
"<PackageId>Acme.Library</PackageId><Version>1.0.0</Version>"
cp "$work/Offender.cs" "$work/Library/"
build Library

if ! ( cd "$work/Library" && dotnet pack -c Release -o "$feed" > "$work/pack-Library.log" 2>&1 ); then
  printf 'error: the library fixture failed to pack\n' >&2
  sed -n '1,60p' "$work/pack-Library.log" >&2
  exit 1
fi

# The library itself DID choose the catalogue, so it is checked. Asserting it here keeps the gate
# honest: a mechanism that stopped the flow by breaking it everywhere would pass the check below
# and fail this one.
check 'the library that chose the catalogue is itself checked' yes "$(reported Library)"

project TwoHops \
"    <PackageReference Include=\"Acme.Library\" Version=\"1.0.0\" />" \
"<OutputType>Exe</OutputType>"
cp "$work/Offender.cs" "$work/Program.cs" "$work/TwoHops/"
build TwoHops

check 'the analyzer does NOT reach a consumer two hops out' no "$(reported TwoHops)"
check 'that consumer is handed no analyzer at all' 0 "$(analyzer_instances TwoHops)"

# The attribute has to keep flowing where the analyzer no longer does. It is a runtime dependency
# of anything that reflects over a catalogue, and the consumer above compiles against a library
# whose public surface may name rule types — so bounding the ANALYZER must not bound this.
check 'the attribute assembly still reaches two hops out' \
  'DiagnosticCatalog.dll' "$(in_output TwoHops DiagnosticCatalog.dll)"

printf '\nOverruling the default, from either end\n'

# The application two hops out that WANTS the checks. Under ADR-0037 it had no way to decline
# them; under ADR-0038 it has a way to ask for them, which is the same property read from the
# other side.
project TwoHopsOptIn \
"    <PackageReference Include=\"Acme.Library\" Version=\"1.0.0\" />" \
"<EnableDiagnosticCatalogAnalyzers>true</EnableDiagnosticCatalogAnalyzers>"
cp "$work/Offender.cs" "$work/TwoHopsOptIn/"
build TwoHopsOptIn

check 'a consumer two hops out can opt IN' yes "$(reported TwoHopsOptIn)"

# And the direct consumer who wants the catalogue and not the analysis. ADR-0037 left them only
# .editorconfig, because declining the package meant declining [DiagnosticRule] with it. Here the
# attribute still arrives, which is what makes this a real alternative rather than the §7.2
# failure under another name.
project DirectOptOut \
"    <PackageReference Include=\"Acme.Catalog.A\" Version=\"1.0.0\" />" \
"<OutputType>Exe</OutputType><EnableDiagnosticCatalogAnalyzers>false</EnableDiagnosticCatalogAnalyzers>"
cp "$work/Offender.cs" "$work/Program.cs" "$work/DirectOptOut/"
build DirectOptOut

check 'a direct consumer can opt OUT' no "$(reported DirectOptOut)"
check 'opting out still leaves the attribute assembly' \
  'DiagnosticCatalog.dll' "$(in_output DirectOptOut DiagnosticCatalog.dll)"

printf '\n'

if [ "$failures" -eq 0 ]; then
  printf '%d check(s) passed\n' "$total"
  exit 0
fi

printf '%d of %d check(s) FAILED\n' "$failures" "$total" >&2
exit 1
