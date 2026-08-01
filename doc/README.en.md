# Documentation

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./README.fr.md)

Four kinds of document live here, and they answer different questions. This page says which
one you want.

| If you want… | Read | Shape |
| --- | --- | --- |
| to *do* something | [**The guide**](guide/README.en.md) | 26 pages, threaded in one order, each with previous/next |
| the exact behaviour, normatively | [**The specification**](specification.en.md) | One long design document |
| to know *why* something is the way it is | [**The decision records**](adr/) | One file per decision, dated, never edited once accepted |
| to add a page here | [**The conventions**](CONVENTIONS.en.md) | The layout, and what the tests check |

## Start with the guide

[**The documentation map**](guide/README.en.md) picks a page by what you are trying to do,
and every page carries the previous and the next, so it can also be read straight through.

Six tracks, in reading order:

* **Discovery** — is this for me? The problem, the concepts, when *not* to use it, and the
  alternatives.
* **Using a catalogue** — the common case: writing suppressions the compiler checks,
  adopting one on an existing codebase, configuration, and what reaches the assembly you
  ship (nothing).
* **Publishing a catalogue** — the structural contract, closing the loop with your own
  analyzer, versioning, packaging.
* **Generating one** — the `dcat` tool, its full reference, the manifest, and keeping a
  catalogue current in CI.
* **Reference** — every `DCAT` diagnostic, the rule contract, troubleshooting by symptom,
  the FAQ, the glossary.
* **Internals** — for contributors only: the repository's architecture, the generator, the
  release trains, the testing strategy.

## The specification

[The specification](specification.en.md) is the canonical design document: the rule
contract, the platform behaviour it relies on, the generator, the analyzer diagnostics,
packaging. It is normative and longer than any guide — read it when you need the exact
answer rather than the usable one.

Its appendix is worth knowing about on its own: every behavioural claim the design rests on
was checked against the platform rather than assumed, and the appendix records what was
checked and how.

## The decision records

[The ADRs](adr/) record the decisions a future maintainer would question, with the context,
the alternatives that were rejected and why, and the consequences accepted. They are a
historical log: an accepted record is never edited, and a decision is revisited by writing a
successor that supersedes it.

Two are a good place to start, because most of the rest follow from them:

* [ADR-0008](adr/0008-express-a-rule-as-a-marked-static-class-of-constants.md) — why a rule
  is a marked static class of constants, rather than an interface or a base class.
* [ADR-0009](adr/0009-generate-catalog-content-from-analyzer-descriptors.md) — why a
  catalogue's content is read from the analyzers' own descriptors and never from their
  documentation.

## The conventions

[CONVENTIONS.en.md](CONVENTIONS.en.md) is the contract these documents follow: the file
layout, the language banner, the navigation footer, the writing and diagram rules — and,
beside each rule, how it is checked. Read it before adding a page.

## Both languages

Every document in this folder exists as an English and a French page, and the banner at the
top of each switches between them. **English is canonical**: where the two disagree, the
English version is right
([ADR-0022](adr/0022-maintain-every-document-under-doc-in-english-and-french.md)).

A page and its translation land in the same commit, and
`tests/DiagnosticCatalog.Documentation.UnitTests` fails a pair that is missing a half, a
link that does not resolve, or a page nothing navigates to.

Two things sit deliberately outside this rule. The [decision records](adr/) are English
only, like everything else this repository records as history. The package READMEs under
[`src/`](../src) are English only too, because nuget.org renders one file per package,
offers no language switch, and resolves no relative link.

---

<div align="center">
<a href="../README.md">← Project README</a> · <a href="./guide/README.en.md">The documentation map →</a>
</div>
