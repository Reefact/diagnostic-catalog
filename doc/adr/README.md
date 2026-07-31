# Architecture Decision Records

Dated records of significant decisions — their context, the option chosen, and
the consequences. An ADR is a historical log: once accepted it is not edited in
place; a decision is revisited by writing a **new** ADR that supersedes the old
one, and the old one's status changes to *Superseded* with a link to its
successor.

## When is an ADR written?

Every pull request is checked against this base — the moment new decisions enter
the codebase. Most pull requests embark no architectural decision and add no ADR;
the check is what is mandatory, not the artifact. The test for "significant": *if
the implementation changed but the decision stood, the ADR should not need
editing.* A new decision is **recorded** here, a decision that replaces another is
written as a **superseding** ADR, and a change that **conflicts** with an accepted
ADR is raised for the maintainer. The agent procedure — draft as *Proposed*, never
flip a status unilaterally — is in [`AGENTS.md`](../../AGENTS.md).

## An ADR is a decision record, not a specification

An ADR captures a **decision and the reasoning behind it** — not how that
decision is implemented. Implementation mechanics (code, configuration, YAML,
exact flags, XML or command snippets, guard-by-guard or step-by-step
walkthroughs) live in the code and in the comments that sit next to it — never in
the ADR itself. In particular, **Rationale is argument, not a design document**:
if a paragraph explains *how something is built* rather than *why the decision is
right*, it belongs beside the code, and the ADR links to it. A useful test: if
the implementation changed but the decision stood, the ADR should not need
editing.

## File conventions

* One file per decision, under `doc/adr/`, named `NNNN-short-title.md` — a
  four-digit sequence number and a lowercase, kebab-case title:
  `0001-floor-the-libraries-on-net-framework-4-7-2.md`.
* ADRs are written in **English**, like everything else recorded in this
  repository.
* Every ADR follows the format below; [`template.md`](template.md) is a
  copy-ready skeleton.
* The index at the bottom of this file lists every ADR and its status. Adding an
  ADR means adding its row.

## Format

### Title and header

```markdown
# ADR-{number} | {Short Title}

**Status:** Proposed | Accepted | Superseded | Deprecated
**Proposed:** YYYY-MM-DD
**Accepted:** YYYY-MM-DD
**Decision Makers:** {Names or team}
```

The header carries **one dated line per state the decision actually reached in
this repository**, and no date is ever overwritten. A record drafted as
*Proposed* carries `Proposed:` alone; accepting it adds `Accepted:` below and
leaves the first line untouched. Both dates then stay for good: when the thinking
happened and when it was ratified are different facts, and a log that keeps only
the second cannot say how long a decision waited, nor which ones were ratified on
sight.

A supersession adds nothing — **it moves no date and introduces none**. The
decision was taken when it was taken, and that is what the record keeps; the new
date belongs to the successor. What connects the two is the link, not the date: a
*Superseded* ADR links to the ADR that supersedes it, next to the status.

### Context

Describe all information that led to the decision. The objective is that
someone unfamiliar with the project can understand why this decision had to be
made.

Include every relevant aspect when applicable:

* business context;
* functional requirements;
* technical constraints;
* architectural constraints;
* operational constraints;
* security requirements;
* performance requirements;
* cost considerations;
* team skills and experience;
* existing system limitations;
* organizational or political constraints;
* external dependencies;
* deadlines or delivery constraints;
* known risks.

This section contains **facts only**. It does not justify or explain the
chosen solution.

### Decision

Describe the decision in **one single sentence**.

Rules:

* one sentence only;
* no justification;
* no alternatives;
* no historical explanation;
* no implementation details unless they are part of the decision itself.

Example:

> The application will use PostgreSQL as its primary relational database.

### Rationale

Explain why this decision is the best choice given the context. Each argument
must be traceable to information already described in the Context section; if
an argument is missing from the Context, add the missing factual information
there first.

This section explains:

* why the decision satisfies the requirements;
* which constraints it addresses;
* which trade-offs were accepted;
* why the expected benefits outweigh the drawbacks.

It is **argument only**. It does **not** contain implementation detail — no
code, configuration, YAML, exact flags, or XML/command snippets, and no
guard-by-guard or step-by-step "how it is built". That is specification: link
to where it actually lives instead of pasting it here. Naming a guard's *role*
and *why it exists* is argument and belongs here; documenting *how the guard is
wired* is specification and does not.

### Alternatives Considered

Document every serious alternative that was evaluated. Each alternative
explains **why it was considered** and **why it was ultimately rejected** —
not simply that it was rejected.

```markdown
### {Alternative 1}

Why it was considered.

Why it was ultimately rejected.
```

### Consequences

Describe the consequences of adopting this decision — both positive and
negative impacts — under three subheadings:

* **Positive** — the benefits the decision delivers;
* **Negative** — the costs and limitations accepted with it;
* **Risks** — what could go wrong later, and any mitigation in place.

### Follow-up Actions

List any work that becomes necessary because of this decision. Examples:

* update documentation;
* migrate existing components;
* create technical guidelines;
* monitor performance after deployment;
* add automated tests;
* schedule a future review.

### References

Optional supporting material:

* related ADRs;
* RFCs;
* specifications;
* benchmarks;
* design documents;
* pull requests;
* issue trackers;
* diagrams.

## Index

| ADR | Title | Status |
|---|---|---|
| [ADR-0001](0001-floor-the-libraries-on-net-framework-4-7-2.md) | Floor the libraries' .NET Framework support at 4.7.2 | Accepted |
| [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.md) | Partition releases into trains by commit scope | Accepted |
| [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.md) | Adopt and enforce a Conventional Commits convention | Accepted |
| [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.md) | State the coding rules where an agent can act on them | Accepted |
| [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.md) | Require an enforcing check before any automation merges | Accepted |
| [ADR-0006](0006-publish-through-trusted-publishing-with-provenance-and-an-sbom.md) | Publish through trusted publishing, with signed provenance and an embedded SBOM | Accepted |
| [ADR-0007](0007-depend-across-trains-through-published-packages.md) | Depend across trains through published packages, never project references | Accepted |
| [ADR-0008](0008-express-a-rule-as-a-marked-static-class-of-constants.md) | Express a rule as a marked static class of constants, never an interface | Accepted |
| [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.md) | Generate catalog content from analyzer descriptors, never from documentation | Accepted |
| [ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.md) | Carry a retired rule forward as obsolete, never delete its constant | Accepted |
| [ADR-0011](0011-redistribute-rule-facts-only-never-the-vendors-prose.md) | Redistribute rule facts only, never the vendor's rule prose | Superseded by [ADR-0014](0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.md) |
| [ADR-0012](0012-a-catalogue-never-renames-a-member-it-published.md) | A catalogue never renames a member it published | Accepted |
| [ADR-0013](0013-write-the-shell-tooling-for-posix-sh-not-bash.md) | Write the shell tooling for POSIX sh, not bash | Accepted |
| [ADR-0014](0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.md) | Ship the vendor's rule title as a catalogue's documentation | Accepted |
| [ADR-0015](0015-a-catalogues-version-runs-on-its-own-line.md) | A catalogue's package version runs on its own line, never the upstream's | Accepted |
| [ADR-0016](0016-mirror-stylecops-prerelease-line.md) | Mirror StyleCop's prerelease line, not its stale stable release | Accepted |
| [ADR-0017](0017-publish-the-generator-as-a-cli-on-its-own-release-train.md) | Publish the generator as a CLI, on its own release train | Accepted |
