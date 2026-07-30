#!/bin/sh
# DiagnosticCatalog — history-hygiene hook.
#
# Wired from .claude/settings.json on two events so an agent never opens a pull
# request on — nor quietly grows — a branch whose commits would land messy in
# `main`. This repository merges pull requests with a merge commit, so every
# commit on a branch reaches protected history (CONTRIBUTING.md, "Branches"):
# a messy history is not squashed away on merge, it stays on `main` for good.
#
# It reuses the repository's single commit linter
# (tools/commit-lint/lint-commit-message.sh), so the hook, the local commit-msg
# hook and the CI gate can never disagree about what a conforming header is.
#
# Modes (argument 1):
#   pre-pr       PreToolUse on the pull-request-creation tool. BLOCKS (exit 2)
#                only when the branch carries CI-fatal history — pending
#                fixup!/squash!/amend! placeholders, or headers the linter
#                rejects — because that pull request would fail the commit-lint
#                CI job and cannot merge as-is. Softer mess (wip-ish commits
#                whose headers each lint clean) is left to the agent's judgement
#                (AGENTS.md); the hook does not block on taste.
#   post-commit  PostToolUse on Bash. After a git command that moved the branch
#                tip and left origin/main..HEAD messy, prints an advisory
#                reminder (exit 2 surfaces it to the agent; nothing is blocked,
#                the tool already ran).
#
# The hook never rewrites anything: it only reads and reminds. The judgement of
# "is this messy?" and the decision to rewrite stay with the agent and, for the
# rewrite itself, the maintainer.

set -u

mode="${1:-}"

# Always drain stdin (the harness pipes the hook payload); parsed only in
# post-commit. Draining avoids any broken-pipe noise on the writer side.
payload="$(cat 2>/dev/null || true)"

root="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel 2>/dev/null || true)}"
[ -n "$root" ] || exit 0                 # not a repo we understand: stay silent
cd "$root" 2>/dev/null || exit 0
linter="$root/tools/commit-lint/lint-commit-message.sh"

# The base we protect. The default branch is `main`; a branch is read against a
# freshly-known origin/main (CONTRIBUTING.md, "Branches").
base='origin/main'
git rev-parse --verify --quiet "$base" >/dev/null 2>&1 || exit 0

range="${base}..HEAD"
commits="$(git rev-list --no-merges "$range" 2>/dev/null || true)"
[ -n "$commits" ] || exit 0              # nothing ahead of main: clean by definition

# --- classify the range -------------------------------------------------------
# fatal: would fail the commit-lint CI job (blocks a pull request).
# soft:  wip-ish scaffolding; each header may still lint clean (advisory only).
fatal=''
soft=''
for sha in $commits; do
  subject="$(git log -1 --format=%s "$sha")"
  short="$(git log -1 --format='%h %s' "$sha")"

  case "$subject" in
    'fixup! '*|'squash! '*|'amend! '*)
      fatal="${fatal}  - ${short}  (autosquash placeholder, rejected by CI)
"
      continue ;;
    # Any other subject is not an autosquash placeholder; it still faces the linter below.
    *) ;;
  esac

  if [ -x "$linter" ] && ! git log -1 --format=%B "$sha" | "$linter" --ci - >/dev/null 2>&1; then
    fatal="${fatal}  - ${short}  (header does not follow CONTRIBUTING.md)
"
    continue
  fi

  # Soft signals: a subject that reads like scaffolding to squash before merge.
  low="$(printf '%s' "$subject" | tr '[:upper:]' '[:lower:]')"
  case "$low" in
    wip|wip:*|'wip '*|*' wip'|*' wip '*|\
    *typo*|*oops*|*'address review'*|*'review comment'*|*'review feedback'*|\
    *'fix review'*|*'apply review'*|*'self review'*|*'self-review'*|*'pr feedback'*)
      soft="${soft}  - ${short}
" ;;
    # Anything else reads as a real subject; nothing to flag.
    *) ;;
  esac
done

fatal_hint() {
  cat >&2 <<EOF
[!] History hygiene — ${range}

This branch carries history the commit-lint CI job will reject, so the pull
request cannot merge as-is:

${fatal}
Per AGENTS.md ("Tidying history before a pull request"): propose a cleanup and,
once the maintainer approves, run /tidy-history — autosquash the placeholders,
reword the non-conforming headers — while the branch is yours alone, publishing
with 'git push --force-with-lease'. Then retry.
EOF
}

soft_hint() {
  cat >&2 <<EOF
[i] History hygiene — ${range}

Commits that read like scaffolding to squash before merge:

${soft}
Advisory only, nothing was blocked. Judge whether origin/main..HEAD reads clean;
if not, propose a cleanup plan (AGENTS.md) or run /tidy-history.
EOF
}

case "$mode" in
  pre-pr)
    if [ -n "$fatal" ]; then
      fatal_hint
      exit 2                             # block PR creation: it would fail CI
    fi
    exit 0 ;;                            # CI-clean: the softer call is the agent's

  post-commit)
    # React only to git commands that could have moved the branch tip.
    cmd=''
    if command -v jq >/dev/null 2>&1; then
      cmd="$(printf '%s' "$payload" | jq -r '.tool_input.command // empty' 2>/dev/null || true)"
    fi
    [ -n "$cmd" ] || cmd="$(printf '%s' "$payload" | tr '\n' ' ')"   # fallback: raw scan
    case "$cmd" in
      *'git commit'*|*'git rebase'*|*'git push'*|*'git merge'*|*'git cherry-pick'*|*'git revert'*) : ;;
      *) exit 0 ;;                       # not a history-moving git command
    esac
    if [ -n "$fatal" ] || [ -n "$soft" ]; then
      [ -n "$fatal" ] && fatal_hint
      [ -n "$soft" ] && soft_hint
      exit 2                             # advisory; PostToolUse surfaces it to the agent
    fi
    exit 0 ;;

  *)
    exit 0 ;;
esac
