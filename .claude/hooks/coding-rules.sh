#!/bin/sh
# DiagnosticCatalog — coding-rules hook.
#
# Wired from .claude/settings.json on PostToolUse for the file-editing tools, so
# an agent learns it broke a coding rule at the moment it wrote the file — not
# once CI is red, and not once a human is reading the diff.
#
# Why a hook and not just a written rule. A rule recorded only in a ReSharper/Rider
# .DotSettings file is read by Rider and by nothing else: no compiler, no CI job,
# and no agent. Delegating to such a file ("follow it") reads like an instruction
# without being one for a reader that cannot open it — in the sibling repository
# this convention comes from, the explicit-type rule drifted to 203 violations
# under exactly that arrangement. The rules an agent must follow are therefore
# written out in CLAUDE.md ("Coding rules"), and this hook is what makes them
# observable rather than merely stated.
#
# It complements, and does not replace, the build-time enforcement: .editorconfig
# plus EnforceCodeStyleInBuild report the same explicit-type rule as IDE0008, which
# CI turns into an error. That gate is authoritative but late — it fires only when
# someone builds. This hook fires on the edit itself.
#
# The hook never rewrites anything: it reads the file that was just written and
# reports. Fixing is the agent's job, which keeps the agent's own output the thing
# under correction rather than something a formatter quietly patched behind it.
#
# Adding a rule. Write a `rule_<name>` function that prints one line per offence
# (empty output means clean), then add it to RULES below. Every rule must be a
# pure read of the edited file and cost milliseconds: this runs after every single
# edit. Anything needing a build or a solution-wide analysis belongs in CI, not
# here.

set -u

# Rules to run, in order. See "Adding a rule" above.
RULES='explicit_types'

# Always drain stdin (the harness pipes the hook payload). Draining avoids any
# broken-pipe noise on the writer side even when we exit early.
payload="$(cat 2>/dev/null || true)"

# The file that was just written. jq when available, a raw scan otherwise, so the
# hook degrades to silence rather than to false positives.
file=''
if command -v jq >/dev/null 2>&1; then
  file="$(printf '%s' "$payload" | jq -r '.tool_input.file_path // empty' 2>/dev/null || true)"
fi
[ -n "$file" ] || exit 0
[ -f "$file" ] || exit 0                 # deleted, moved, or a path we cannot resolve

case "$file" in
  *.cs) : ;;
  *) exit 0 ;;                           # C# rules only, for now
esac

display="${file##*/}"

# --- rules --------------------------------------------------------------------

# Explicit types: the type is written out, never inferred. Three exclusions, all
# cases where reporting would be wrong rather than merely noisy: a comment line
# (documentation routinely shows `var` in sample code), an anonymous type (C#
# gives no other spelling), and an occurrence inside a string literal (the
# analyzer suites embed C# fixtures that deliberately use `var`).
# shellcheck disable=SC2317,SC2329  # reached through the `"rule_${rule}"` dispatch in the run section below
rule_explicit_types() {
  awk -v name="$display" '
    {
      # Raw string literals ("""...""") carry code this repository does not own:
      # the generated HTML pages embed JavaScript, and the analyzer suites embed C#
      # fixtures that use `var` on purpose. Track the delimiters so the whole block,
      # and the delimiter lines themselves, are out of scope.
      fences = gsub(/"""/, "\"\"\"")
      was_raw = in_raw
      if (fences % 2 == 1) in_raw = !in_raw
      if (was_raw || in_raw) next

      body = $0
      sub(/^[ \t]*/, "", body)

      if (body ~ /^\/\//) next                                  # // or /// comment
      if (body ~ /^\*/)   next                                  # continuation of a /* */ block

      if (!match($0, /(^|[^A-Za-z0-9_.])var[ \t]+[A-Za-z_]/)) next   # no inferred declaration
      if ($0 ~ /=[ \t]*new[ \t]*\{/) next                            # anonymous type: mandatory

      # Everything up to and including the character before `var`. Counting quotes
      # in it tells us whether the occurrence sits inside a string literal; the
      # separator must be kept, since it is often the opening quote itself.
      prefix = substr($0, 1, RSTART)
      if (gsub(/"/, "\"", prefix) % 2 == 1) next                     # unclosed quote: inside a literal

      printf "  %s:%d  %s\n", name, FNR, body
    }
  ' "$file"
}

# shellcheck disable=SC2317,SC2329  # reached through the `"${rule}_hint"` dispatch in the run section below
explicit_types_hint() {
  printf '%s' "Coding rule — explicit types (CLAUDE.md, \"Coding rules\"). This file now declares
inferred types:

${1}
Write the type out. \`var\` is reserved for the declarations C# gives no other
spelling, which in practice means anonymous types. The build reports the same
thing as IDE0008 and CI turns it into an error, so this does not merge as-is.
"
}

# --- run ----------------------------------------------------------------------

report=''
for rule in $RULES; do
  offences="$("rule_${rule}" 2>/dev/null || true)"
  [ -n "$offences" ] || continue
  report="${report}$("${rule}_hint" "$offences")
"
done

[ -n "$report" ] || exit 0               # clean: stay silent

printf '%s' "$report" >&2
exit 2                                   # advisory; PostToolUse surfaces it to the agent
