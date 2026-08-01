#!/bin/sh
# Fail when the build reports an analyzer diagnostic the warning ratchet cannot see.
#
# Usage:
#   tools/analysis/check-diagnostic-floor.sh              build, then check
#   tools/analysis/check-diagnostic-floor.sh <directory>  check SARIF logs already produced
#
# The blind spot this closes. The ratchet in Directory.Build.props promotes WARNINGS to errors, so
# nothing that warns can merge. A Roslyn analyzer diagnostic reported BELOW warning — `info`, which
# SARIF calls `note` — is not a warning, so the ratchet never sees it, `dotnet build` prints nothing
# about it at any verbosity, and the build goes green. SonarQube Cloud imports it anyway: its
# scanner reads what the compiler reported, not what the console showed. Measured on this
# repository: 23 diagnostics at `note` produced a green build and 23 issues on the dashboard, rule
# for rule.
#
# Why a floor rather than a list of rules. build/sonar-profile.globalconfig can name every Sonar
# rule because a quality profile enumerates them; nothing enumerates the Roslyn rules the .NET SDK
# turns on by default, and that set moves with every SDK release. Raising all of them instead is not
# an option either — measured, `dotnet_analyzer_diagnostic.severity = warning` reports 1065 sites,
# most of them from rules the SDK deliberately leaves off, including 698 of a naming rule this
# repository's test convention contradicts on purpose. So this asserts the INVARIANT instead: what
# the build reports, the build must be able to fail on. A rule the next SDK adds at `info` is caught
# here the first time it fires, named, and has to be dealt with — cleared, or raised to `warning`
# so the ratchet owns it, or suppressed at the site with a reason. Decision: ADR-0024.
#
# Suppressed diagnostics are ignored, and that is not a loophole: a `#pragma warning disable` or a
# [SuppressMessage] is a decision written down at the site, which is exactly what this asks for.
# The compiler still lists them in the log, marked, so they are told apart here rather than assumed.

set -eu

fail() { printf 'check-diagnostic-floor: %s\n' "$1" >&2; exit "${2:-1}"; }

command -v jq >/dev/null || fail "jq is required"

script_dir=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$script_dir/../.." && pwd)

logs="${1:-}"
if [ -z "$logs" ]; then
  command -v dotnet >/dev/null || fail "dotnet is required"
  logs="$(mktemp -d)"
  trap 'rm -rf "$logs"' EXIT INT TERM

  # DiagnosticLogDirectory is read by Directory.Build.props, which names one log per project and
  # per target framework. It is set there rather than here because MSBuild does not expand
  # $(...) inside a command-line property value — see the comment beside the declaration.
  printf 'check-diagnostic-floor: building to collect the compiler'"'"'s own diagnostic log...\n'
  ( cd "$root" && dotnet build -c Release --no-incremental \
      "-p:DiagnosticLogDirectory=${logs}" ) >"${logs}/build.txt" 2>&1 \
    || { cat "${logs}/build.txt" >&2; fail "the build failed; fix that before reading its diagnostics"; }
fi

[ -d "$logs" ] || fail "no such directory: ${logs}"

count=0
for sarif in "$logs"/*.sarif; do
  [ -e "$sarif" ] || break
  count=$((count + 1))
done
[ "$count" -gt 0 ] || fail "no .sarif log in ${logs}; the build must run with -p:DiagnosticLogDirectory=<path>"

found="$(mktemp)"
trap 'rm -f "$found"' EXIT INT TERM

# Both SARIF shapes are read. The .NET SDK emits version 1 by default (suppressionStates,
# resultFile) and 2.1 on request (suppressions, physicalLocation/artifactLocation); which one
# arrives is the SDK's choice, not this repository's, so neither is assumed.
#
# A result carries no `level` when it matches the rule's own default configuration, so the rule
# table is consulted before falling back to `warning` — reading an absent level as `warning` for a
# rule whose default is `note` is precisely how this check would report nothing forever.
jq -r '
  [ .runs[]? as $run
    | ($run.tool.driver.rules // [] | map({key: .id, value: (.defaultConfiguration.level // "warning")}) | from_entries) as $default
    | $run.results[]?
    | select((.suppressionStates // .suppressions // []) | length == 0)
    | . as $r
    | (($r.level // $default[$r.ruleId] // "warning")) as $level
    | select($level != "warning" and $level != "error")
    | (($r.locations[0].resultFile // $r.locations[0].physicalLocation) // {}) as $loc
    | (($loc.uri // $loc.artifactLocation.uri) // "?") as $uri
    | (($loc.region.startLine) // 0) as $line
    | "\($level)\t\($r.ruleId)\t\($uri)\t\($line)\t\(if ($r.message|type) == "string" then $r.message else ($r.message.text // "") end)"
  ] | .[]
' "$logs"/*.sarif 2>/dev/null | sort -u > "$found" || fail "could not read the diagnostic logs"

leaks="$(grep -c . < "$found" || true)"

if [ "$leaks" -eq 0 ]; then
  printf 'check-diagnostic-floor: every diagnostic this build reports is at least a warning (%s log(s) read).\n' "$count"
  exit 0
fi

printf 'check-diagnostic-floor: %s diagnostic(s) are reported BELOW warning.\n' "$leaks" >&2
printf '\n' >&2
printf 'The build stays green on these and SonarQube Cloud imports them anyway.\n' >&2
printf '\n' >&2

while IFS="$(printf '\t')" read -r level rule uri line message; do
  printf '  %-6s %-9s %s:%s\n         %s\n' \
    "$level" "$rule" "$(printf '%s' "$uri" | sed "s|^file://${root}/||; s|^file://||")" "$line" "$message" >&2
done < "$found"

printf '\n' >&2
printf 'Each needs one of three answers, and all three are visible in the tree:\n' >&2
printf '  * clear the violation;\n' >&2
printf '  * raise the rule to warning severity in .editorconfig, so the ratchet owns it;\n' >&2
printf '  * suppress it at the site, with the reason, where the next reader will meet it.\n' >&2
exit 1
