# Architecture Decision Records

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./README.fr.md)

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

* One decision per number, under `doc/adr/`, named `NNNN-short-title.{en,fr}.md`
  — a four-digit sequence number, a lowercase, kebab-case title, and a language:
  `0001-floor-the-libraries-on-net-framework-4-7-2.en.md`.
* **A number is never reused.** Two records drafted in parallel can collide on
  one; the record accepted first keeps it, and the other is renumbered before it
  is accepted — a link to `ADR-0022` has to reach one decision.
* Every ADR exists in **English and French**, and the pair lands in the same
  commit ([ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.en.md)).
  **English is canonical**: where the two disagree, the English version is right.
  A translation records no decision its English page does not, and a correction
  to one is a correction to both.
* Every ADR follows the format below; [`template.md`](template.md) is a
  copy-ready skeleton.
* The index at the bottom of this file lists every ADR and its status. Adding an
  ADR means adding its row — here **and** in the French counterpart.

## Format

### Title and header

The H1 comes first, then the language banner, then the header block — the layout
every page under `doc/` follows ([`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md)):

```markdown
# ADR-{number} | {Short Title}

🌍 **Languages:**
🇬🇧 English (this file) | 🇫🇷 [Français](./{number}-{short-title}.fr.md)

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
| [ADR-0001](0001-floor-the-libraries-on-net-framework-4-7-2.en.md) | Floor the libraries' .NET Framework support at 4.7.2 | Accepted |
| [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.en.md) | Partition releases into trains by commit scope | Accepted |
| [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.en.md) | Adopt and enforce a Conventional Commits convention | Accepted |
| [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.en.md) | State the coding rules where an agent can act on them | Accepted |
| [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.en.md) | Require an enforcing check before any automation merges | Accepted |
| [ADR-0006](0006-publish-through-trusted-publishing-with-provenance-and-an-sbom.en.md) | Publish through trusted publishing, with signed provenance and an embedded SBOM | Accepted |
| [ADR-0007](0007-depend-across-trains-through-published-packages.en.md) | Depend across trains through published packages, never project references | Accepted |
| [ADR-0008](0008-express-a-rule-as-a-marked-static-class-of-constants.en.md) | Express a rule as a marked static class of constants, never an interface | Accepted |
| [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.en.md) | Generate catalog content from analyzer descriptors, never from documentation | Accepted |
| [ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.en.md) | Carry a retired rule forward as obsolete, never delete its constant | Accepted |
| [ADR-0011](0011-redistribute-rule-facts-only-never-the-vendors-prose.en.md) | Redistribute rule facts only, never the vendor's rule prose | Superseded by [ADR-0014](0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md) |
| [ADR-0012](0012-a-catalogue-never-renames-a-member-it-published.en.md) | A catalogue never renames a member it published | Accepted |
| [ADR-0013](0013-write-the-shell-tooling-for-posix-sh-not-bash.en.md) | Write the shell tooling for POSIX sh, not bash | Accepted |
| [ADR-0014](0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md) | Ship the vendor's rule title as a catalogue's documentation | Accepted |
| [ADR-0015](0015-a-catalogues-version-runs-on-its-own-line.en.md) | A catalogue's package version runs on its own line, never the upstream's | Accepted |
| [ADR-0016](0016-mirror-stylecops-prerelease-line.en.md) | Mirror StyleCop's prerelease line, not its stale stable release | Accepted |
| [ADR-0017](0017-publish-the-generator-as-a-cli-on-its-own-release-train.en.md) | Publish the generator as a CLI, on its own release train | Accepted |
| [ADR-0018](0018-a-code-fix-never-decides-what-only-the-author-can.en.md) | A code fix never decides what only the author can decide | Accepted |
| [ADR-0019](0019-resolve-packages-through-the-users-own-nuget-configuration.en.md) | Resolve packages through the user's own NuGet configuration | Accepted |
| [ADR-0020](0020-a-catalogue-is-generated-for-c-sharp-only.en.md) | A catalogue is generated for C# only | Accepted |
| [ADR-0021](0021-derive-the-build-rule-set-from-the-quality-profile.en.md) | Derive the build's Sonar rule set from the server's quality profile | Accepted |
| [ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.en.md) | Maintain every document under `doc/` in English and French | Accepted |
| [ADR-0023](0023-acquire-a-solutions-analyzers-by-declaration.en.md) | Acquire a solution's analyzers by declaration, never by discovery | Accepted |
| [ADR-0024](0024-fail-on-any-diagnostic-the-ratchet-cannot-see.en.md) | Fail on any diagnostic the warning ratchet cannot see | Accepted |
| [ADR-0025](0025-bind-every-feature-commit-to-the-documentation-it-changed.en.md) | Bind every feature commit to the documentation it changed | Accepted |
| [ADR-0026](0026-reach-a-category-only-through-the-rule-that-carries-it.en.md) | Reach a category only through the rule that carries it | Accepted |
| [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.en.md) | Ship the use-site diagnostics as errors | Accepted |
| [ADR-0028](0028-require-every-rule-to-reach-its-category-through-a-declared-constant.en.md) | Require every rule to reach its category through a declared constant | Proposed |
| [ADR-0029](0029-pair-the-project-readme-across-the-doc-boundary.en.md) | Pair the project README across the `doc/` boundary | Accepted |
