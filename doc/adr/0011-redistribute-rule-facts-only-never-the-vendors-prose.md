# ADR-0011 | Redistribute rule facts only, never the vendor's rule prose

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

The generated catalogs mirror analyzers owned by other people — SonarSource's,
Microsoft's, the StyleCop.Analyzers project's. Each of those projects ships under
its own licence; the packages produced here ship under this repository's. None of
the catalogs is affiliated with, endorsed by or supported by the vendor it
mirrors, and each says so.

A `DiagnosticDescriptor` carries an identifier, a category, a title, a message
format, a description and a help link. The identifier and the category are facts
about how the software behaves: what the analyzer reports and under which
category it reports it. The title, the message format and the description are
sentences the vendor wrote to explain the rule; they are the substance of that
vendor's own documentation. A help link is a pointer to where the vendor
publishes that documentation.

A catalog exists so that a suppression's two required arguments become
compile-checked references. Those two arguments are the identifier and the
category. No part of that function reads the prose.

The three mirrored packages are large: 465, 318 and 193 descriptors
respectively.

`SonarAnalyzer.CSharp` populates `HelpLinkUri` on none of its 465 descriptors.
`Microsoft.CodeAnalysis.NetAnalyzers` and `StyleCop.Analyzers` populate it on
every one of theirs. Vendors publish their rule pages at addresses that often
follow a pattern, so a help link could in principle be assembled rather than
read.

This repository's own documentation quotes a rule title in a couple of places, to
show the shape of the string an IDE's built-in suppression fix inserts.

## Decision

A generated third-party catalog ships only the identifiers, categories and help
links that its upstream descriptors declare, and never the vendor's rule titles,
message formats or descriptions.

## Rationale

A package is a redistribution channel, and the decision is about what this
repository is entitled to put through it. Stating that an analyzer reports a
given identifier under a given category is stating how somebody else's software
behaves; it reproduces none of their work, and no licence is needed to say it.
Titles and descriptions *are* the work — the sentences that constitute the
vendor's rule documentation. Bundling hundreds of them into a package under this
repository's own licence would make that package a derivative of the vendor's
corpus, and a permission this repository cannot grant on their behalf. Drawing
the line between a fact and authored text yields the one version of the mirror
that needs no licence from anybody.

The honesty argument runs alongside the licensing one and would hold even if the
licences were permissive. An unaffiliated mirror carrying the vendor's
explanatory text would read, to a consumer, as the vendor's documentation.
They would treat it as authoritative, and it would age against the vendor's own
pages with nothing to say so — the vendor cannot correct a copy this repository
shipped. Pointing at their documentation keeps the authoritative text where its
author maintains it, and keeps the catalog's claim to a modest and defensible
one: this is the rule's identifier and category, and here is where its owner
explains it.

Nothing the catalog is for is lost by the restriction. The prose plays no part in
making a suppression compile-checked; its only role would be convenience at the
moment of reading, and a link delivers that with a pointer rather than a copy —
one that is current by construction, which a snapshot never is.

Quoting a rule title inside documentation to illustrate a format is a different
act, and the distinction is not a convenient one. A citation appears in an
explanation, in a handful of instances, doing work the surrounding argument needs
and visibly attributable to its author; the reader can see what is being shown
and whose it is. Bundling the corpus into a distributed artifact makes the text
the payload rather than the illustration, at a scale where what is shipped simply
is the vendor's rule catalog restated. Placing the rule on what a *package*
ships, rather than on whether a title may ever be written down, is what keeps it
both applicable and sane.

The scale is part of the argument rather than incidental to it. One title inside
a paragraph illustrating a format is a quotation by any reading. A title and a
description for every descriptor of all three mirrored packages, emitted
mechanically and shipped as an artifact, is a republication whatever it is
called.

## Alternatives Considered

### Ship the whole descriptor, titles and descriptions included

Considered because it is what the descriptors actually contain, it would make
completion tooltips genuinely informative, and it would let a consumer learn what
a rule is about without leaving their editor — a real improvement to the product.

Rejected because it turns each package into a redistribution of the vendor's
authored corpus under a licence this repository cannot grant for them, and puts a
snapshot of their documentation into circulation that they cannot correct and
that carries no attribution at the point where it is read.

### Ship titles only, not descriptions

Considered because a title is short, is the part that makes a tooltip useful, and
reads more like a label than like documentation — the least of the prose for most
of the benefit.

Rejected because length is not the distinction that matters. A title is still a
sentence the vendor wrote, and several hundred of them is still their catalog. A
line drawn on brevity would have to be defended rule by rule and would move under
pressure; the line between a fact about the software and text authored about it
does not.

### Synthesise help links from a per-vendor URL pattern where descriptors declare none

Considered because it would remove the asymmetry between the catalogs and give
every rule somewhere to go, and because the vendors whose descriptors omit the
link do publish their rules at predictable addresses.

Rejected because a synthesised link is a value this repository invented and
presented as the vendor's. If the pattern is wrong, or changes later, the catalog
ships broken pointers carrying the vendor's name, and nothing in a consumer's
build would ever report it — the same silent inaccuracy ADR-0009 exists to
exclude, in a different field of the same descriptor.

### Publish the prose separately, in its own package or repository

Considered because it would isolate the licensing question from the code package
and let a consumer opt into the documentation deliberately rather than receive it
by default.

Rejected because redistribution does not change character with the artifact that
carries it: the text is the vendor's wherever it is shipped from, and the
question of permission is identical. It would also add a second surface that has
to be kept in step with upstream, for a benefit a help link already provides.

## Consequences

### Positive

* No package in this repository redistributes another project's authored content,
  so the licensing question does not have to be reopened per vendor.
* Consumers are sent to the vendor's own page, which is current by construction,
  rather than to a snapshot of it.
* Catalogs stay small, and a regeneration diff shows rules moving rather than
  descriptions being reworded.

### Negative

* Completion tooltips over a rule constant say less than they could; a consumer
  who wants to know what a rule is about follows the link, or the identifier.
* A catalog carries help links only where the upstream descriptors supply them.
  `SonarAnalyzer.CSharp` populates `HelpLinkUri` on none of its 465 descriptors,
  so the Sonar catalog carries none at all, while the .NET analyzer and StyleCop
  catalogs carry one per rule. The asymmetry is visible to consumers and cannot
  be repaired without synthesising links, which this decision excludes.
* The restriction has to be restated per catalog and cannot be checked by the
  build; nothing mechanical distinguishes a fact from a sentence.

### Risks

* A maintainer adds a title constant because the value is already in hand and the
  tooltip improvement is obvious. Mitigation: the restriction is recorded with
  the generator's rules and in each catalog's documentation, and each generated
  file states in its own header why the prose is absent.
* A vendor changes its documentation addresses and the links their descriptors
  supplied go stale. Mitigation: regeneration carries whatever the current
  descriptors declare; a link this repository never authored is one it never has
  to maintain.
* A vendor objects to the mirror itself rather than to any prose in it.
  Mitigation: each catalog states plainly that it is unofficial and unaffiliated
  and acknowledges the trademarks it refers to; beyond that the question is a
  maintainer's to answer, not a generator's.

## Follow-up Actions

* State in each catalog's consumer documentation what the package contains and
  where the vendor's own rule descriptions live.
* Keep the facts-only restriction recorded with the generator, where whoever
  changes generation next will read it.
* Revisit the position if a vendor publishes explicit terms for redistributing
  its rule metadata.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.md) — why a
  value that was never read must not be invented.
* [doc/specification.en.md](../specification.en.md) — §14.1, and Appendix A9 and
  A11.
* `src/DiagnosticCatalog.Sonar/README.md` and its counterparts — what each
  catalog tells its consumers.
* [LICENSE](../../LICENSE).
