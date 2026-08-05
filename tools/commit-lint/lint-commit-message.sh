#!/bin/sh
# Validate a commit message against the DiagnosticCatalog commit convention.
#
# This is the single source of truth shared by the local `commit-msg` hook
# (.githooks/commit-msg) and the CI check (.github/workflows/commit-lint.yml),
# so the two can never diverge. The rules it enforces are documented in
# CONTRIBUTING.md; this script only mirrors them.
#
# Usage:
#   lint-commit-message.sh <path-to-message-file>        # the hook passes $1
#   git log -1 --format=%B <sha> | lint-commit-message.sh --ci -   # CI, per commit
#
# Options:
#   --ci / --strict   CI mode: reject autosquash placeholders (fixup!/squash!/
#                     amend!) instead of skipping them (see the exemptions below).
#
# Exit status: 0 = conforming (or intentionally exempt), 1 = violations found.
#
# Scope note: the header is validated in full (this is where the whole value
# lives). Bodies are prose and are left alone, except for three safe, high-value
# footer checks: the breaking-change double signal, the shape of a `Refs:` footer
# when one is present, and the `Docs:` footer a `feat` must carry.
#
# The `Docs:` footer is checked here for SHAPE only. Whether the files it names
# were really touched is a question about a commit, not about a message, and
# tools/commit-lint/check-docs-footer.sh answers it.

set -u

TYPES='feat|fix|build|chore|ci|docs|perf|refactor|revert|style|test'
SCOPES='analyzers|cataloggen|cli|codestyle|core|netanalyzers|sonar|stylecop'
TYPES_HUMAN='feat, fix, build, chore, ci, docs, perf, refactor, revert, style, test'
SCOPES_HUMAN='analyzers, cataloggen, cli, codestyle, core, netanalyzers, sonar, stylecop'
MAX=72

# --- options ------------------------------------------------------------------
strict=0
case "${1:-}" in
  --ci|--strict) strict=1; shift ;;
  *) ;; # any other first argument is the message file / '-', handled below
esac

# --- read the message ---------------------------------------------------------
if [ "$#" -lt 1 ] || [ "$1" = "-" ]; then
  msg="$(cat)"
elif [ -f "$1" ]; then
  msg="$(cat "$1")"
else
  printf 'commit-lint: message file not found: %s\n' "$1" >&2
  exit 2
fi

# Strip what git itself strips: the scissors block (verbose commits) and any
# comment lines. Issue refs like "#142" never start a line with '#', so this is
# safe.
msg="$(printf '%s\n' "$msg" | sed -E -e '/^# -+ >8 -+$/,$d' -e '/^#/d')"

subject="$(printf '%s\n' "$msg" | sed -n '1p' | sed 's/[[:space:]]*$//')"

# --- exemptions ---------------------------------------------------------------
# Merge commits carry a git/GitHub-generated message and are always exempt (CI
# also filters them out with --no-merges).
case "$subject" in
  'Merge '*) exit 0 ;;
  *) ;; # not a merge commit: fall through to validation
esac
# Autosquash placeholders are rewritten by a later `git rebase --autosquash`, so
# the local hook lets them through. CI (--ci) rejects them instead: this repo
# merges pull requests with a merge commit, so a placeholder merged before its
# rebase would otherwise land, unlinted, in protected history.
case "$subject" in
  'fixup! '*|'squash! '*|'amend! '*)
    if [ "$strict" = 0 ]; then
      exit 0
    fi
    printf 'commit-lint: autosquash placeholder must be squashed away before merge: %s\n' "$subject" >&2
    exit 1
    ;;
  *) ;; # not an autosquash placeholder: fall through to validation
esac

errors=0
errmsgs=''
err() {
  errmsgs="${errmsgs}  - ${1}
"
  errors=$((errors + 1))
  return 0
}

# --- header: presence ---------------------------------------------------------
if [ -z "$subject" ]; then
  err "the commit message is empty"
else
  # header length
  len=${#subject}
  if [ "$len" -gt "$MAX" ]; then
    err "the header is ${len} characters; keep the whole line within ${MAX}"
  fi

  # canonical shape: <type>[(<scope>[,<scope>...])][!]: <lowercase description>
  if printf '%s' "$subject" | grep -Eq "^(${TYPES})(\((${SCOPES})(,(${SCOPES}))*\))?!?: [a-z]"; then
    # Well-formed header. The two version-driving types must ALSO carry a scope:
    # the release trains are partitioned by scope (CONTRIBUTING.md -> "Scope"),
    # so an unscoped feat/fix matches no train and is silently dropped from the
    # release notes and the changelog.
    case "$subject" in
      feat:*|feat!:*|fix:*|fix!:*)
        vtype="${subject%%[:!(]*}"
        err "a '${vtype}' commit must carry a scope, e.g. '${vtype}(core): …' — the release trains are partitioned by scope, and an unscoped ${vtype} is silently dropped from the release notes and the changelog"
        ;;
      *) ;; # already scoped, or a non-version-driving type: nothing to require
    esac
  else
    # --- targeted diagnostics so the author knows exactly what to fix ---
    if ! printf '%s' "$subject" | grep -Eq '^[^:]+: .'; then
      err "expected '<type>[(scope)][!]: <description>' — no ': ' after the type"
    fi

    typ="$(printf '%s' "$subject" | sed -E 's/^([a-zA-Z]+).*/\1/')"
    if ! printf '%s' "$typ" | grep -Eq "^(${TYPES})$"; then
      err "unknown or malformed type '${typ}' — use one of: ${TYPES_HUMAN}"
    fi

    if printf '%s' "$subject" | grep -Eq '^[a-z]+\([^)]*, '; then
      err "scopes are comma-separated with no space: '(cli,core)', not '(cli, core)'"
    fi

    if printf '%s' "$subject" | grep -Eq '^[a-zA-Z]+\('; then
      grp="$(printf '%s' "$subject" | sed -E 's/^[a-zA-Z]+\(([^)]*)\).*/\1/')"
      OLDIFS=$IFS
      IFS=','
      for s in $grp; do
        st="$(printf '%s' "$s" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
        if ! printf '%s' "$st" | grep -Eq "^(${SCOPES})$"; then
          err "unknown scope '${st}' — use one of: ${SCOPES_HUMAN} (a scope names a component, never a file or a class)"
        fi
      done
      IFS=$OLDIFS
    fi

    desc_start="$(printf '%s' "$subject" | sed -E 's/^[^:]*: ?//' | cut -c1)"
    case "$desc_start" in
      [A-Z]) err "the description must start with a lowercase letter (imperative: 'add', not 'Add'/'Added')" ;;
      *) ;; # already lowercase (or non-letter): nothing to report
    esac
  fi

  # trailing period
  case "$subject" in
    *.) err "the header must not end with a period" ;;
    *) ;; # no trailing period: nothing to report
  esac

  # scope order / duplicates (only meaningful when the group is well-formed)
  if printf '%s' "$subject" | grep -Eq "^(${TYPES})\((${SCOPES})(,(${SCOPES}))*\)"; then
    grp="$(printf '%s' "$subject" | sed -E 's/^[a-z]+\(([^)]*)\).*/\1/')"
    sorted="$(printf '%s' "$grp" | tr ',' '\n' | sort -u | tr '\n' ',' | sed 's/,$//')"
    if [ "$grp" != "$sorted" ]; then
      err "scopes must be unique and alphabetical: write '(${sorted})'"
    fi
  fi
fi

# --- blank line between header and body ---------------------------------------
nlines="$(printf '%s\n' "$msg" | awk 'END { print NR }')"
line2="$(printf '%s\n' "$msg" | sed -n '2p')"
if [ "$nlines" -ge 2 ] && [ -n "$line2" ]; then
  err "leave a blank line between the header and the body"
fi

# --- breaking-change double signal --------------------------------------------
prefix="${subject%%: *}"
has_bang=0
case "$prefix" in *!) has_bang=1 ;; *) ;; esac

has_breaking=0
if printf '%s\n' "$msg" | grep -Eq '^BREAKING CHANGE: '; then
  has_breaking=1
fi
if printf '%s\n' "$msg" | grep -Eq '^(BREAKING[-_]CHANGE|[Bb]reaking[ -][Cc]hange):'; then
  err "the breaking-change footer must read exactly 'BREAKING CHANGE:'"
fi
if [ "$has_bang" = 1 ] && [ "$has_breaking" = 0 ]; then
  err "a '!' in the header requires a 'BREAKING CHANGE:' footer describing the migration"
fi
if [ "$has_bang" = 0 ] && [ "$has_breaking" = 1 ]; then
  err "a 'BREAKING CHANGE:' footer requires a '!' before the colon in the header"
fi

# --- Refs: footer shape -------------------------------------------------------
# Anything that looks like the issue footer must read exactly 'Refs: #<number>'.
bad_refs="$(printf '%s\n' "$msg" | grep -Ei '^ref(s)?:' | grep -Ev '^Refs: #[0-9]+$' || true)"
if [ -n "$bad_refs" ]; then
  err "the issue footer must read 'Refs: #<number>' (e.g. 'Refs: #142')"
fi

# --- Docs: footer ---------------------------------------------------------------
# A `feat` is, by this repository's own definition, "a new capability, visible to
# the consumer of the package" (CONTRIBUTING.md -> "Types"). A capability the
# consumer can see and cannot read about is either undocumented or mistyped, and
# nothing else in the toolchain is in a position to say so: no compiler reads
# prose, and the documentation tests bind only the surfaces they can enumerate —
# the DCAT ids, the dcat options and command tree, the public API. Everything
# else a feature can add — a build property, a manifest key, a workflow, a page
# of the guide itself — reaches a release with no check at all.
#
# So a feat records what it documented, or records that it documented nothing and
# why. The exemption is deliberately a sentence somebody wrote, the same shape the
# documentation tests use for a reference a page shows on purpose
# (doc/CONVENTIONS.en.md -> "Showing a reference that does not exist"): an
# exemption without a reason is a hole nobody can judge.
#
# Bound to `feat` and not to `fix`: a fix restores behaviour the documentation
# already promises, so the commonest honest answer would be "none" and the footer
# would decay into a reflex. See ADR-0025.

# The BODY, never the header. `docs` is also a type, so a perfectly good
# `docs: add the reference track` opens with the very shape a footer scan is
# looking for — and the header has already been validated in full above.
docs_body="$(printf '%s\n' "$msg" | sed -n '2,$p')"

# Anything in the body that looks like the footer must read exactly `Docs: <value>`,
# so a `docs:` or a `Doc :` is reported rather than silently ignored — the same
# reason `Refs:` is spelled one way.
malformed_docs="$(printf '%s\n' "$docs_body" | grep -Ei '^doc(s)?[[:space:]]*:' | grep -Ev '^Docs: [^[:space:]]' || true)"
if [ -n "$malformed_docs" ]; then
  err "the documentation footer must read 'Docs: <path>[, <path>…]' or 'Docs: none — <reason>'"
fi

docs_footer="$(printf '%s\n' "$docs_body" | grep -E '^Docs: ' || true)"

is_feat=0
case "$subject" in
  feat:*|'feat!:'*|'feat('*) is_feat=1 ;;
  *) ;; # not a feature: the footer is welcome but not required
esac

if [ "$is_feat" = 1 ] && [ -z "$docs_footer" ]; then
  err "a 'feat' must carry a 'Docs:' footer naming the documentation it changes — 'Docs: doc/guide/dcat-reference.en.md, doc/guide/dcat-reference.fr.md' — or 'Docs: none — <reason>'. A capability the consumer can see and cannot read about is either undocumented or mistyped"
fi

if [ -n "$docs_footer" ]; then
  docs_count="$(printf '%s\n' "$docs_footer" | awk 'END { print NR }')"
  if [ "$docs_count" -gt 1 ]; then
    err "keep the documentation footer to a single 'Docs:' line; separate several paths with commas"
  fi

  # A footer WRAPPED over several lines is refused rather than half-read. Only the
  # first line matches `^Docs: `, so every path below it is invisible here and equally
  # invisible to check-docs-footer.sh — both would report success on a list they read
  # part of, which is the one direction this rule must never fail in. A single line is
  # the format CONTRIBUTING.md describes; folding it to fit a 72-column log reads as a
  # courtesy and is not one.
  #
  # Two tells, because a continuation comes in two shapes. An indented line directly
  # under the footer is the classic fold. A value ending in a comma is a list that has
  # something after it, wherever that something went — and it catches the unindented
  # fold, which is otherwise indistinguishable from an ordinary body line. The pair a
  # continuation with no comma AND no indent stays legal, deliberately: nothing tells
  # it apart from a paragraph somebody wrote after the footer.
  docs_wrapped="$(printf '%s\n' "$docs_body" | awk '
    after && /^[ \t]/ && /[^ \t]/ { print "wrapped"; exit }
    { after = ($0 ~ /^Docs: /) }
  ')"
  case "$(printf '%s\n' "$docs_footer" | sed -n '1p' | sed 's/[[:space:]]*$//')" in
    *,) docs_wrapped='wrapped' ;;
    *) ;; # the list ends on a path, as it should
  esac
  if [ -n "$docs_wrapped" ]; then
    err "keep the documentation footer on ONE line — a wrapped one is only read as far as its first line, so the paths under it are checked by nothing. Separate several paths with commas, however long the line gets"
  fi

  docs_value="$(printf '%s\n' "$docs_footer" | sed -n '1s/^Docs: //p' | sed 's/[[:space:]]*$//')"

  case "$docs_value" in
    none|none[!A-Za-z0-9]*)
      # Everything up to the first alphanumeric byte is the separator, whatever the
      # author reached for — an em dash, a hyphen, a colon. Matching the bytes that
      # are NOT a reason keeps the pattern ASCII, so it reads the same under any
      # locale the hook happens to run in.
      docs_reason="$(printf '%s' "${docs_value#none}" | sed -E 's/^[^A-Za-z0-9]*//')"
      if [ -z "$docs_reason" ]; then
        err "'Docs: none' must give a reason — 'Docs: none — <why this feature needs no documentation>'. An exemption without one is a hole nobody can judge"
      fi
      ;;
    *)
      OLDIFS=$IFS
      IFS=','
      for d in $docs_value; do
        dt="$(printf '%s' "$d" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
        if [ -z "$dt" ]; then
          err "the documentation footer has an empty entry — separate paths with a single comma"
          continue
        fi
        case "$dt" in
          /*) err "'${dt}' must be written relative to the repository root" ; continue ;;
          *..*) err "'${dt}' must not climb out of the repository" ; continue ;;
          *) ;;
        esac
        case "$dt" in
          *.md) ;;
          *) err "'${dt}' is not a Markdown document — this repository documents in Markdown, and a footer naming anything else records nothing a reader can read" ;;
        esac
      done
      IFS=$OLDIFS
      ;;
  esac
fi

# --- verdict ------------------------------------------------------------------
if [ "$errors" -gt 0 ]; then
  printf 'commit-lint: this message does not follow CONTRIBUTING.md\n\n%s\n  subject: %s\n' "$errmsgs" "$subject" >&2
  exit 1
fi
exit 0
