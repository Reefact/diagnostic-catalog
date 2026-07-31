#!/bin/sh
# Read the SonarCloud Quality Gate verdict for this project and fail when it is not green.
#
# Usage:
#   tools/sonar-profile/check-gate.sh
#
# Why this exists. `dotnet-sonarscanner end` uploads the analysis and returns; it neither waits
# for the gate nor reads it, and no GitHub check carries the verdict. The gate was therefore
# computed and enforced by nothing, while the workflow that produced it stayed green as long as
# the upload succeeded.
#
# Why here and not in the sonar workflow. Making the scanner wait would couple MERGE
# AVAILABILITY to a third-party service: the sonar job runs on every pull request, so a
# SonarCloud outage would stop every merge while a red gate still would not. Reading the verdict
# from a SCHEDULED job separates the two — the verdict is enforced, and an outage costs a red
# nightly instead of a frozen repository (decision: ADR-0021).
#
# What this catches that the build cannot. build/sonar-profile.globalconfig enforces the C# rules
# the NuGet analyzer implements, and that is a strict subset of what the gate measures:
#   - symbolic-execution rules (S2583 and its family) that SonarCloud's engine runs and the
#     analyzer package does not;
#   - every non-C# rule family: githubactions, shell, secrets, xml, json, yaml — this repository
#     already suppresses three shell rules server-side in sonar.yml, which the build never sees;
#   - coverage, duplication and security-hotspot review, which no analyzer can answer.
# That last class is why this check exists here and now: at the time it was written the gate was
# red on new_coverage alone, with every issue condition green — a state no build-time rule set
# can report. The build hardening and this check are complements, not alternatives.
#
# The project is public, so no token is required; SONAR_TOKEN is honoured when set.

set -eu

PROJECT="${SONAR_PROJECT_KEY:-reefact_diagnostic-catalog}"
API="${SONAR_API_BASE:-https://sonarcloud.io/api}"

fail() { printf 'check-gate: %s\n' "$1" >&2; exit "${2:-1}"; }

command -v curl >/dev/null || fail "curl is required"
command -v jq   >/dev/null || fail "jq is required"

# --proto '=https' --proto-redir '=https' refuse every non-HTTPS hop, including on a redirect,
# which matters because the token below would otherwise be sent in clear (Sonar
# githubactions:S6506 flagged exactly this shape elsewhere in the repository). The token is
# prepended to this function's own positional parameters rather than duplicating the curl call, so
# those restrictions are written ONCE and cannot drift between the authenticated and anonymous
# paths — the authenticated one being exactly where a drift would leak the credential. Same shape
# as sync-profile.sh, deliberately.
fetch() {
  if [ -n "${SONAR_TOKEN:-}" ]; then set -- --user "${SONAR_TOKEN}:" "$@"; fi

  curl --proto '=https' --proto-redir '=https' -sSfL --retry 3 --retry-delay 2 --max-time 60 "$@"
}

body="$(fetch "${API}/qualitygates/project_status?projectKey=${PROJECT}")" \
  || fail "could not reach ${API}"

status="$(printf '%s' "$body" | jq -r '.projectStatus.status // empty')"
[ -n "$status" ] || fail "no gate status in the response; the project key may be wrong: ${PROJECT}"

# A rating is reported as 1..5 where the dashboard shows A..E, so the raw number is translated:
# "3" tells a reader nothing, "C" tells them where they are.
render() {
  printf '%s' "$body" | jq -r '
    def letter: {"1":"A","2":"B","3":"C","4":"D","5":"E"};
    .projectStatus.conditions[]?
    | select(.status != "OK")
    | . as $c
    | ($c.metricKey | test("_rating$")) as $isRating
    | "  \($c.metricKey): \(if $isRating then (letter[$c.actualValue] // $c.actualValue) else $c.actualValue end)"
      + " (needs \(if $c.comparator == "GT" then "at most" else "at least" end)"
      + " \(if $isRating then (letter[$c.errorThreshold] // $c.errorThreshold) else $c.errorThreshold end))"
  '
}

if [ "$status" = "OK" ]; then
  printf 'check-gate: the SonarCloud quality gate is green for %s.\n' "$PROJECT"
  exit 0
fi

printf 'check-gate: the SonarCloud quality gate is %s for %s.\n' "$status" "$PROJECT" >&2
printf '\n' >&2
printf 'Failing conditions:\n' >&2
render >&2
printf '\n' >&2
printf 'A rating worse than A means the new-code period carries at least one issue of that kind:\n' >&2
printf '  reliability -> a Bug, security -> a Vulnerability, maintainability -> a Code Smell.\n' >&2
printf 'Open https://sonarcloud.io/project/issues?id=%s&resolved=false&inNewCodePeriod=true\n' "$PROJECT" >&2
exit 1
