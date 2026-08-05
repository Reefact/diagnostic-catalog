#!/bin/sh
# Single source of truth for the release trains.
#
# The published trains version independently and each owns a tag prefix, a set of
# Conventional Commit scopes, and a package label. That mapping lives here, once;
# the packaging and release-notes scripts SOURCE this file, so what a release
# publishes and what its notes describe can never drift apart.
#
# This file is meant to be SOURCED (`. tools/trains.sh`), not executed — it only
# defines functions and mutates nothing.
#
# ── Which PROJECTS a train publishes ─────────────────────────────────────────
# Not listed here. A project joins a train by declaring it in its own .csproj:
#
#     <PropertyGroup>
#       <ReleaseTrain>sonar</ReleaseTrain>
#     </PropertyGroup>
#
# and `projects_of` below discovers it. Membership therefore lives in the one file
# that cannot be forgotten when the project is created, moved or renamed — the
# same reason the .NET Framework floor is joined by an import rather than by a
# list in a workflow (CONTRIBUTING.md, "The .NET Framework floor"). Declaring the
# train is also what makes a project packable and what gives it an embedded SBOM;
# see Directory.Build.props.
#
# ── Adding a train ───────────────────────────────────────────────────────────
# 1. add one row to trains_rows() below;
# 2. add its scope to SCOPES in tools/commit-lint/lint-commit-message.sh, and to
#    the scope and train tables in CONTRIBUTING.md;
# 3. add its tag pattern to the `on: push: tags:` list and its id to the
#    workflow_dispatch choice in .github/workflows/release.yml — GitHub requires
#    both to be literal, so they cannot be derived from this file;
# 4. add it to the "Release train" checklist in .github/pull_request_template.md,
#    which lists the trains literally for the same reason. Missed once already:
#    a train can exist, route and publish while every pull request describing it
#    still has to tick "None".
# A tag whose prefix is unknown here is rejected by the release workflow, so a
# missed step 1 fails the release rather than publishing something unrouted.
#
# Row format (pipe-separated, no spaces around the pipes except inside the label):
#   <id>|<tag-prefix>|<scopes csv>|<package label>
# The scopes here and the closed SCOPES list in
# tools/commit-lint/lint-commit-message.sh must name the SAME set: every scope the
# linter accepts routes to exactly one train, and every scope named here is one the
# linter accepts. That equality has not always held — `cataloggen` reached no train
# until the generator shipped inside `dcat` (ADR-0017), and `testing` outlived the
# test-support package it named — and neither gap was visible from either file
# alone. A scope on no row is silently dropped from the release notes and the
# changelog, which is a defect and not a design.
trains_rows() {
  cat <<'ROWS'
lib|lib-v|analyzers,core|the DiagnosticCatalog foundation, its analyzers and the catalogue of their own rules
cli|cli-v|cataloggen,cli|the DiagnosticCatalog CLI (the dcat .NET tool)
sonar|sonar-v|sonar|the SonarQube rule catalog
netanalyzers|netanalyzers-v|netanalyzers|the Microsoft .NET analyzer rule catalog
stylecop|stylecop-v|stylecop|the StyleCop rule catalog
codestyle|codestyle-v|codestyle|the Roslyn IDE code-style rule catalog
xunit|xunit-v|xunit|the xUnit.net analyzer rule catalog
nunit|nunit-v|nunit|the NUnit analyzer rule catalog
mstest|mstest-v|mstest|the MSTest analyzer rule catalog
trimming|trimming-v|trimming|the trimming, Native AOT and single-file rule catalog
aspnetcore|aspnetcore-v|aspnetcore|the ASP.NET Core and Blazor rule catalog
syslib|syslib-v|syslib|the .NET runtime source-generator rule catalog
ROWS
}

# _train_field <id> <field-name> — echo one field of a train's row, or nothing if
# the id is unknown. Fields: prefix | scopes | package.
_train_field() {
  _tf_id="$1"; _tf_field="$2"
  trains_rows | while IFS='|' read -r id prefix scopes package; do
    [ "$id" = "$_tf_id" ] || continue
    case "$_tf_field" in
      prefix)  printf '%s\n' "$prefix" ;;
      scopes)  printf '%s\n' "$scopes" ;;
      package) printf '%s\n' "$package" ;;
      # A caller asking for a field this row format does not carry is a bug in the caller, not a
      # missing value: say so on stderr rather than returning the empty string an unknown TRAIN
      # returns, which require_train reads as "no such train".
      *)       printf 'trains.sh: unknown field "%s"\n' "$_tf_field" >&2 ;;
    esac
  done
}

train_ids()  { trains_rows | cut -d'|' -f1; }
prefix_of()  { _train_field "$1" prefix; }
scopes_of()  { _train_field "$1" scopes; }
package_of() { _train_field "$1" package; }

# require_train <id> — succeed if <id> is a known train, else print the known ids
# to stderr and return 1. Callers decide the exit code.
require_train() {
  if [ -n "$(prefix_of "$1")" ]; then
    return 0
  fi
  printf 'unknown train "%s" (known: %s)\n' \
    "$1" "$(train_ids | tr '\n' ' ' | sed 's/ *$//')" >&2
  return 1
}

# train_of_tag <tag> — echo the train id a release tag belongs to, or nothing.
# Matches the tag against every known prefix rather than a hardcoded case, so a
# train added to trains_rows is routed without touching the release workflow's
# script. The longest matching prefix wins, so a prefix that is a prefix of
# another can never shadow it.
train_of_tag() {
  _tot_tag="$1"; _tot_best=''; _tot_len=0
  for _tot_id in $(train_ids); do
    _tot_prefix="$(prefix_of "$_tot_id")"
    case "$_tot_tag" in
      "${_tot_prefix}"*)
        if [ "${#_tot_prefix}" -gt "$_tot_len" ]; then
          _tot_best="$_tot_id"; _tot_len="${#_tot_prefix}"
        fi
        ;;
      *) ;; # this train's prefix does not match the tag
    esac
  done
  # Always succeed, even when nothing matched: callers read the RESULT through a
  # command substitution, and an assignment inheriting a non-zero status would
  # abort a `set -e` caller before it could print its own diagnostic.
  if [ -n "$_tot_best" ]; then printf '%s\n' "$_tot_best"; fi
  return 0
}

# _without_xml_comments <path> — echo the file with every <!-- ... --> region removed,
# including a region spanning several lines.
#
# Membership is a fact about what a project DECLARES, and an element shown inside a
# comment declares nothing. Without this, writing <ReleaseTrain>sonar</ReleaseTrain>
# in a comment — to say what a project will join later, or why it has not joined yet —
# enrols it for real: it becomes packable, and a release publishes it. That is not
# hypothetical. DiagnosticCatalog.Sonar.csproj carries a warning telling its own author
# never to spell the element in its prose, for exactly this reason. A rule that works by
# asking every future author to remember it is the kind CLAUDE.md exists to replace, so
# the discovery is made to ignore comments instead.
#
# Line-oriented tools cannot do this: a comment opened on one line and closed on another
# is invisible to grep and to sed. awk carries the state across lines.
_without_xml_comments() {
  awk '
    {
      _line = $0; _out = ""
      while (length(_line) > 0) {
        if (_inside) {
          _at = index(_line, "-->")
          if (_at == 0) { _line = "" } else { _line = substr(_line, _at + 3); _inside = 0 }
        } else {
          _at = index(_line, "<!--")
          if (_at == 0) { _out = _out _line; _line = "" }
          else { _out = _out substr(_line, 1, _at - 1); _line = substr(_line, _at + 4); _inside = 1 }
        }
      }
      print _out
    }
  ' "$1"
}

# projects_of <id> — echo the .csproj paths that declare this train, one per line.
# Empty output means the train publishes nothing yet, which is a normal state for
# a train whose project has not been created.
#
# bin/ and obj/ are skipped, because what a train publishes must be read from the
# SOURCE tree alone. A project file copied into a build output — a test that reads
# what this repository publishes does exactly that — is an ordinary .csproj to a
# tree-wide grep, and the copy would be packed: `dotnet pack` gets a path with no
# restore behind it and fails the release rehearsal, and a copy that HAD been
# restored would publish the same package twice from one train.
projects_of() {
  # grep first as a cheap filter over the tree, then re-check each candidate with its
  # comments removed. Only files that already mention the train pay for the second pass.
  grep -rl -E "<ReleaseTrain>[[:space:]]*$1[[:space:]]*</ReleaseTrain>" \
    --include='*.csproj' --exclude-dir=bin --exclude-dir=obj . 2>/dev/null \
    | sed 's|^\./||' | sort | while read -r _po_proj; do
    if _without_xml_comments "$_po_proj" \
         | grep -q -E "<ReleaseTrain>[[:space:]]*$1[[:space:]]*</ReleaseTrain>"; then
      printf '%s\n' "$_po_proj"
    fi
  done
}

# declared_trains — echo every train id declared by a .csproj anywhere in the
# tree, one per line, deduplicated. Used to catch a value that matches no train:
# such a project would simply never be packed, silently, and a typo in a property
# nothing validates is exactly the kind of mistake that surfaces at release time.
declared_trains() {
  # Build outputs are skipped here for the reason given on projects_of: a value only a
  # copy declares would be reported as if a project had chosen it.
  grep -rl -E "<ReleaseTrain>[^<]*</ReleaseTrain>" \
    --include='*.csproj' --exclude-dir=bin --exclude-dir=obj . 2>/dev/null \
    | while read -r _dt_proj; do _without_xml_comments "$_dt_proj"; done \
    | grep -o -E "<ReleaseTrain>[^<]*</ReleaseTrain>" \
    | sed -E 's|.*<ReleaseTrain>[[:space:]]*([^<[:space:]]*)[[:space:]]*</ReleaseTrain>.*|\1|' \
    | sort -u || true
}
