#!/bin/sh
# Regenerate build/sonar-profile.globalconfig from the project's SonarCloud C# Quality Profile.
#
# Usage:
#   tools/sonar-profile/sync-profile.sh            rewrite the file in place
#   tools/sonar-profile/sync-profile.sh --check    exit 1 if it would change, and print the diff
#
# Why this exists. The SonarAnalyzer.CSharp NuGet package does NOT ship the Quality Profile:
# measured on this repository, its default set leaves S3776 disabled although the profile
# activates it — the four methods SonarQube Cloud reported over the complexity threshold went
# unreported by the package until the rule was given an explicit severity. The profile lives on
# the server and is the thing the report is scored against, so the only way the build can agree
# with the report is to read the profile and write it down. This script is that reading; the
# generated file is that writing.
#
# The default is ENFORCE. Every rule the profile activates is written at `warning`, which the CI
# ratchet in Directory.Build.props promotes to an error. That direction is deliberate and was
# chosen after measuring the alternative: at `suggestion`, a Sonar diagnostic prints NOTHING in
# `dotnet build` at any verbosity — it reaches an IDE and the SARIF log and nobody else — so a
# generated list at `suggestion` would have been invisible to exactly the reader it was for.
#
# The exceptions are written by hand in .editorconfig, which takes precedence over a global
# AnalyzerConfig (verified in both directions). A rule with existing violations is demoted there
# to `suggestion`, with its site count, until they are cleared; a rule this codebase refuses is
# set to `none` with its reason. That list is the backlog, and it shrinks by deletion.
#
# Fails closed, three ways. An empty or short answer aborts without touching the file, so one API
# hiccup cannot rewrite the rule set. A project key that disagrees with sonar.yml aborts, because
# validating the wrong project would pass in green forever. And a count that disagrees with the
# profile's own activeRuleCount is reported loudly rather than accepted in silence.

set -eu

# Must match the scanner arguments in .github/workflows/sonar.yml. Overridable so the script can
# be pointed at another project without editing it.
PROJECT="${SONAR_PROJECT_KEY:-reefact_diagnostic-catalog}"
ORG="${SONAR_ORGANIZATION:-reefact}"
API="${SONAR_API_BASE:-https://sonarcloud.io/api}"

script_dir=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$script_dir/../.." && pwd)
target="$root/build/sonar-profile.globalconfig"

mode="${1:-write}"
case "$mode" in
  write | --check) ;;
  *)
    printf 'sync-profile: unknown argument "%s" (expected --check or nothing)\n' "$mode" >&2
    exit 2
    ;;
esac

fail() { printf 'sync-profile: %s\n' "$1" >&2; exit "${2:-1}"; }

command -v curl >/dev/null || fail "curl is required"
command -v jq   >/dev/null || fail "jq is required"

# The project this script reads must be the project sonar.yml analyses, or the check passes in
# green against something nobody looks at. Only enforced for the defaults: an explicit override
# is a deliberate act (pointing the script at a fork, or a scratch project).
if [ -z "${SONAR_PROJECT_KEY:-}" ] && [ -z "${SONAR_ORGANIZATION:-}" ]; then
  sonar_yml="$root/.github/workflows/sonar.yml"
  if [ -f "$sonar_yml" ]; then
    yml_key="$(sed -n 's|.*/k:"\([^"]*\)".*|\1|p' "$sonar_yml" | head -1)"
    yml_org="$(sed -n 's|.*/o:"\([^"]*\)".*|\1|p' "$sonar_yml" | head -1)"
    [ -z "$yml_key" ] || [ "$yml_key" = "$PROJECT" ] \
      || fail "project key disagrees with sonar.yml: this script says '${PROJECT}', the workflow analyses '${yml_key}'"
    [ -z "$yml_org" ] || [ "$yml_org" = "$ORG" ] \
      || fail "organization disagrees with sonar.yml: this script says '${ORG}', the workflow analyses '${yml_org}'"
  fi
fi

# fetch <url> — GET one endpoint. The project is public, so the API answers unauthenticated;
# SONAR_TOKEN is honoured when set, which is what keeps this working the day it stops being
# public. Retries cover a transient blip; a real outage aborts before anything is written.
# --proto '=https' --proto-redir '=https' matter more here than anywhere: the authenticated branch
# sends SONAR_TOKEN, and -L without them would follow a redirect to plaintext http and put the
# credential on the wire in clear. Refusing every non-HTTPS hop is the only version of this call
# that is safe to give a token to.
fetch() {
  # The token is prepended to this function's own positional parameters rather than duplicating the
  # curl call, so the protocol restrictions below are written ONCE and cannot drift between the
  # authenticated and anonymous paths — which is the branch that would leak the credential.
  if [ -n "${SONAR_TOKEN:-}" ]; then set -- --user "${SONAR_TOKEN}:" "$@"; fi

  curl --proto '=https' --proto-redir '=https' -sSfL --retry 3 --retry-delay 2 --max-time 60 "$@"
}

# --- the profile bound to this project, for C# ---------------------------------
profiles="$(fetch "${API}/qualityprofiles/search?project=${PROJECT}&organization=${ORG}")" \
  || fail "could not reach ${API} (profile lookup)"

qp_key="$(printf '%s' "$profiles" | jq -r '[.profiles[]? | select(.language == "cs") | .key] | join(" ")')"
qp_name="$(printf '%s' "$profiles" | jq -r '[.profiles[]? | select(.language == "cs") | .name] | join(" ")')"
qp_claimed="$(printf '%s' "$profiles" | jq -r '[.profiles[]? | select(.language == "cs") | .activeRuleCount] | first // 0')"
case "$qp_key" in
  '' )        fail "no C# quality profile is bound to ${PROJECT}" ;;
  *' '* )     fail "more than one C# quality profile reported for ${PROJECT}: ${qp_key}" ;;
  * )         ;; # exactly one key, which is the only shape this script can work with
esac

# --- every rule the profile activates -----------------------------------------
keys_file="$(mktemp)"
skipped_file="$(mktemp)"
trap 'rm -f "$keys_file" "$skipped_file"' EXIT INT TERM

page=1
fetched=0
total=0
while : ; do
  body="$(fetch "${API}/rules/search?activation=true&qprofile=${qp_key}&organization=${ORG}&ps=200&p=${page}")" \
    || fail "could not reach ${API} (rules page ${page})"

  total="$(printf '%s' "$body" | jq -r '.total // 0')"
  count="$(printf '%s' "$body" | jq -r '(.rules // []) | length')"
  [ "$count" -eq 0 ] && break

  # A Sonar C# rule key is `csharpsquid:S1234`; the Roslyn diagnostic id is the second half.
  # Anything not shaped like S<digits> has no diagnostic id to configure — a rule template or a
  # non-Roslyn check — and is reported rather than silently dropped.
  printf '%s' "$body" | jq -r '(.rules // [])[] | .key | split(":")[1]' | while IFS= read -r id; do
    case "$id" in
      S[0-9]*) printf '%s\n' "$id" >> "$keys_file" ;;
      *)       printf '%s\n' "$id" >> "$skipped_file" ;;
    esac
  done

  fetched=$((fetched + count))
  [ "$fetched" -ge "$total" ] && break
  page=$((page + 1))
done

rule_count="$(sort -u "$keys_file" | grep -c . || true)"
[ "$rule_count" -gt 0 ] || fail "the API reported no usable C# rules; refusing to write an empty config"
[ "$rule_count" -ge 100 ] || fail "only ${rule_count} rules reported, which is too few to be a real profile; refusing to write"

skipped_count="$(sort -u "$skipped_file" | grep -c . || true)"
if [ "$skipped_count" -gt 0 ]; then
  printf 'sync-profile: %s active rule(s) carry no Roslyn diagnostic id and are not configurable here: %s\n' \
    "$skipped_count" "$(sort -u "$skipped_file" | tr '\n' ' ')" >&2
fi

# The profile object's own activeRuleCount and the rules endpoint disagree by a few on this
# project (380 against 377, measured). It is the endpoint that enumerates, so it is the count
# that cannot be reconciled. Reported rather than swallowed: the rules it cannot show are rules
# this file cannot configure, and that is worth knowing.
if [ "$qp_claimed" -ne "$rule_count" ] && [ "$qp_claimed" -gt 0 ]; then
  printf 'sync-profile: the profile reports %s active rules but the rules endpoint enumerates %s; %s rule(s) cannot be read and are therefore not configured.\n' \
    "$qp_claimed" "$rule_count" "$((qp_claimed - rule_count))" >&2
fi

# --- render --------------------------------------------------------------------
rendered="$(mktemp)"
trap 'rm -f "$keys_file" "$skipped_file" "$rendered"' EXIT INT TERM

{
  printf '# GENERATED FILE - do not edit. Rewrite it with tools/sonar-profile/sync-profile.sh.\n'
  printf '#\n'
  printf '# Every rule the SonarCloud C# quality profile "%s" activates for %s, at\n' "$qp_name" "$PROJECT"
  printf '# severity "warning" - which the CI ratchet in Directory.Build.props promotes to an error.\n'
  printf '# The default here is ENFORCE: a rule the profile activates blocks, unless something says\n'
  printf '# otherwise.\n'
  printf '#\n'
  printf '# "suggestion" was measured and rejected as the default: at that severity a Sonar diagnostic\n'
  printf '# prints nothing in a dotnet build at any verbosity, so the list would have been invisible to\n'
  printf '# the reader it exists for.\n'
  printf '#\n'
  printf '# The EXCEPTIONS live in .editorconfig, which takes precedence over this file. A rule with\n'
  printf '# violations still outstanding is demoted there to "suggestion" with its count; a rule this\n'
  printf '# codebase refuses is set to "none" with its reason. That list is the backlog, and it shrinks\n'
  printf '# by deletion. Nothing about a fate is decided in this file.\n'
  printf '#\n'
  printf '# Rules: %s. Regenerate after any change to the profile; the weekly sonar-profile workflow\n' "$rule_count"
  printf '# fails when the two have drifted apart. Regenerating can turn CI red - that is the point:\n'
  printf '# a rule the profile adds now has to be cleaned or parked, deliberately, before it merges.\n'
  printf '\n'
  printf 'is_global = true\n'
  printf '\n'
  sort -u "$keys_file" | while IFS= read -r id; do
    printf 'dotnet_diagnostic.%s.severity = warning\n' "$id"
  done
} > "$rendered"

if [ "$mode" = "--check" ]; then
  if [ ! -f "$target" ]; then
    printf 'sync-profile: %s does not exist; run tools/sonar-profile/sync-profile.sh to create it.\n' "$target" >&2
    exit 1
  fi
  if diff -u "$target" "$rendered" > /dev/null 2>&1; then
    printf 'sync-profile: build/sonar-profile.globalconfig matches the "%s" profile (%s rules).\n' "$qp_name" "$rule_count"
    exit 0
  fi
  printf 'sync-profile: the quality profile has moved; build/sonar-profile.globalconfig is stale.\n' >&2
  printf '\n' >&2
  diff -u "$target" "$rendered" >&2 || true
  exit 1
fi

cp "$rendered" "$target"
printf 'sync-profile: wrote build/sonar-profile.globalconfig from "%s" (%s rules).\n' "$qp_name" "$rule_count"
