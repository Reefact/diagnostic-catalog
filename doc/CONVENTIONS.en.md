# Documentation conventions

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./CONVENTIONS.fr.md)

<!-- dcat-doc:missing SonarRule.S1144Id quoted below as the naming shape the design declined -->
<!-- dcat-doc:missing SonarRule.S1145 quoted below as the example of a deliberate counter-example -->

For whoever adds or edits a page — including an agent, which is why every rule below states how it
is verified rather than asking to be remembered. How the documents under [`doc/`](.) are laid out,
and what a test checks about them.

The rules are enforced by `tests/DiagnosticCatalog.Documentation.UnitTests`. A page that breaks one
fails the build, in the same way a rule declaration that breaks the structural contract fails it.
That is deliberate: this repository exists because a mistake nothing reports is worse than one that
breaks loudly, and a documentation set is exactly the kind of artifact where nothing reports
anything.

## What lives where

| Path | What it holds | Language |
| --- | --- | --- |
| [`doc/guide/`](guide/) | The reader-facing documentation set. One flat folder. | English **and** French |
| [`doc/specification.en.md`](specification.en.md) | The normative design document. Canonical. | English and French |
| [`doc/adr/`](adr/) | Architecture decision records. | English **and** French |
| `README.md` at the repository root | The shop window. | English only — outside `doc/`, see below |
| `src/*/README.md` | The package pages on nuget.org. | English only — see below |

**What the parity check actually sees** is any document whose name carries a language suffix —
`<name>.en.md` or `<name>.fr.md`. Everything under `doc/` carries one, decision records included:
`NNNN-short-title.en.md` and its French half. Nothing is exempted by a list, which is deliberate — a
check with an exception list drifts into a check with only exceptions. A document that should not be
paired is kept out by not carrying a suffix at all, which is why `doc/adr/template.md` — a skeleton
to copy, not a page to read — has none.

**What parity asserts** is that the two halves have the same *shape*: the same headings, the same
code samples, and the same number of list items, table rows and set-apart notes. Nothing here reads
French, and a translation is not a transcription — sentences merge, split and change length. What a
faithful translation cannot do is offer the reader a different number of things. A note is counted
as a block rather than as a line, because French runs longer and the same blockquote routinely wraps
onto one more line. This is what catches the page edited on one side only: a bullet added to the
English, a row appended to a table, and the French still passes every other check while quietly
saying less.

**The guide folder is flat on purpose.** Every cross-language link is then a plain sibling
(`./concepts.fr.md`) and every navigation link is a plain sibling too. A nested tree buys grouping
that the [documentation map](guide/README.en.md) already provides through prose, and costs a
`../` in every link — which is the one thing in a Markdown set that breaks silently when a file
moves.

**The package READMEs are not part of this set.** They are shipped inside the `.nupkg` as
`<PackageReadmeFile>` and rendered by nuget.org, which resolves no relative link and offers no
language switch. They stay English, single-file, and link outward with absolute
`https://github.com/Reefact/diagnostic-catalog/blob/main/...` addresses. Three tests already read
them — `DocumentedMirrorTests` and `DocumentedSiblingsTests` — so their content is constrained by
more than this file.

## Naming

A page is `<kebab-case-name>.<lang>.md`, where `<lang>` is `en` or `fr`:

```
doc/guide/getting-started.en.md
doc/guide/getting-started.fr.md
```

Kebab-case, because that is what the rest of the repository already uses — `specification.en.md`,
`0001-floor-the-libraries-on-net-framework-4-7-2.en.md` — and a set of files that names itself two ways
teaches the reader nothing except that nobody decided.

**The name is English in both languages.** `getting-started.fr.md`, never `demarrage.fr.md`. A file
name is an address: it appears in links from the English pages, in issues, in review comments, and
in this file. Translating it would double the addresses of one document and make every
cross-language link a lookup.

## Every page carries the same three things

The first two bind every document under `doc/` — the guide, the specification, the decision records.
The third binds [`doc/guide/`](guide/) only: it is the folder that has a reading order, and the
navigation footer is what expresses it.

### 1. One H1, then the language banner

The banner is the second block of the file, immediately after the title, and it is the only place
the other language is offered:

```markdown
# Getting started

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./getting-started.fr.md)
```

```markdown
# Démarrage

🌍 **Langues :**  
🇬🇧 [English](./getting-started.en.md) | 🇫🇷 Français (ce fichier)
```

The two trailing spaces after `**Languages:**` are a hard line break and are load-bearing; without
them the flag line joins the label line. `.editorconfig` already declines to trim trailing
whitespace in Markdown for this reason.

*Checked:* the banner follows the H1, names both languages, and its link resolves to the sibling
file.

### 2. A one-line statement of who the page is for

Directly under the banner, before any heading. Not a summary of the page — a filter, so a reader
who is not its audience stops here:

```markdown
For anyone who writes `[SuppressMessage(...)]`. You do not need to know anything about how
DiagnosticCatalog works to read this.
```

*Checked:* nothing. This one rests on review, because no test can tell a filter from a summary.

### 3. The navigation footer

The last block of the file:

```markdown
---

<div align="center">
<a href="./the-problem.en.md">← Why magic strings fail</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./concepts.en.md">Core concepts →</a>
</div>
```

* The middle link is always present and always points at the map — `README.en.md` or `README.fr.md`
  in the same folder. The map itself is the exception: it *is* the table of contents, so its footer
  carries only a link back out to the project README and a link forward to the first page.
* `←` is absent on the first page of the reading order; `→` is absent on the last. The map is not a
  step in that order — it is the way in — so the first page's `←` is genuinely absent rather than
  pointing back at a table of contents its `↑` already offers.
* The link text is the target page's own title, so a reader knows what they are about to open.
* `<div align="center">` rather than a Markdown construct: GitHub strips most inline styling from
  Markdown but honours this, and it is what the sibling project
  [`first-class-errors`](https://github.com/Reefact/first-class-errors) uses. Matching it means a
  reader moving between the two repositories meets one convention.

*Checked, and this is the strict one:* the footers of all English pages must describe **one total
order** — every page reachable, exactly one page with no predecessor, exactly one with no successor,
no cycle, and every `←` the exact inverse of the corresponding `→`. The French pages must describe
the same order. A page added without being threaded into the chain fails, which is what stops the
set from growing an orphan nobody links.

## The reading order is the map's order

[`guide/README.en.md`](guide/README.en.md) is the documentation map: it groups pages by what the
reader is trying to do, and its order is the order the footers thread. Adding a page means adding it
to the map **and** to the chain; the test compares the two and fails if they disagree.

## Writing rules

* **English is canonical.** Where the two versions disagree, the English one wins — the same rule
  the specification already states. A French page is a translation, never an independent document,
  and a change made in French alone is a change that will be lost.
* **Wrap at 100 columns.** That is what the existing guides do. Prose reflows; a 100-column file
  produces a readable diff, and a file with one paragraph per line produces a diff nobody can read.
* **Never translate an identifier.** Rule ids, package names, member names, MSBuild properties,
  `.editorconfig` keys, command names, exit codes and file paths are the same in both languages,
  because they are the same in the code. A French page explains `DCAT0006`; it does not rename it.
* **Code samples are shared, not translated.** The C# in a French page is the C# from the English
  page, character for character, including identifier names. Only comments inside a sample are
  translated — and only when the comment is prose. A sample that differs between languages is a
  sample one of the two got wrong.
* **C# samples follow the repository's own coding rules.** Write the type, never `var`
  ([`CLAUDE.md`](../CLAUDE.md)). A reader copies what they see, and a documentation set that teaches
  a style the build rejects is worse than one that teaches nothing.
* **A diagram's labels are prose.** The rule above binds C#, XML and `.editorconfig` samples, whose
  identifiers are the code's. The node labels of a mermaid diagram are sentences, and they are
  translated like any other sentence on the page.
* **Prefer a claim you can check.** "Measured against a real restore" beats "should work". Where a
  behaviour is asserted by a test, name the test.

## Diagrams

**Mermaid by default**, in a fenced ```` ```mermaid ```` block. GitHub renders it natively, in the
reader's own light or dark theme, and — the reason that matters here — it is text, so a diagram
changes in a reviewable diff instead of arriving as a new binary nobody can compare.

Reach for an SVG under `doc/images/` only when the figure is not a graph: a
before-and-after, an annotated illustration, anything where the layout carries the meaning. Then:

* the file is committed as SVG, never PNG, so it stays legible at any zoom and diffs as text;
* it works on **both** GitHub themes. Either the figure is theme-neutral, or the page offers two
  files through a `<picture>` element:

  ```html
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="../images/two-failures.dark.svg">
    <img alt="A wrong id lets the warning return; a wrong category does nothing, ever."
         src="../images/two-failures.light.svg">
  </picture>
  ```
* `alt` states what the figure *says*, not what it depicts. A reader using a screen reader gets the
  point of the figure or nothing at all.

The same figure serves both languages when it carries no words. When it does carry words, it takes
a `.en.svg` / `.fr.svg` pair — and that cost is the reason to keep words out of figures.

*Checked:* every image referenced by a page exists, and every image under `doc/images/` is
referenced by at least one page.

## Links

* Relative, always, between documents in this repository. Absolute
  `https://github.com/Reefact/diagnostic-catalog/...` addresses only in the package READMEs, where
  relative links do not resolve.
* An anchor (`#section-title`) must exist in the target document.

*Checked:* every relative link in `doc/` and in the root README resolves to a file that exists, and
every anchor resolves to a heading in the target.

## What the documentation is checked against

These assertions reach outside the documentation and into the code, and they are the point of the
whole test project:

* **Every `DCAT` diagnostic the analyzers ship is documented, and every `DCAT` the guide documents
  is shipped.** A new diagnostic cannot reach a release with no page describing it, and a page
  cannot describe one that was never implemented.
* **Every `dcat` option the documentation mentions exists on the tool's settings types, and every
  option the tool exposes appears in [`doc/guide/dcat-reference`](guide/dcat-reference.en.md).** A
  flag documented after being renamed fails; so does one shipped and never written down, which is the
  commoner mistake and the one whose only signal is nobody using it. The obligation names a single
  page on purpose — spread across every document that mentions the tool it is one no document could
  discharge.
* **Every `dcat` command the tool registers appears in that same page, and every command a document
  shows is registered.** The option check stopped at the flags, which left the coarsest thing the
  tool publishes — its command tree — as the one part of the CLI a change could move unnoticed.
* **Every public type a consumer can name is described in
  [`doc/specification`](specification.en.md).** The public API files are Roslyn's own tracking
  format, and `RS0016` already fails the build when one drifts from the compiled surface — so a new
  type is certain to be recorded in a file no consumer ever opens. Analyzers and code fix providers
  are excluded: Roslyn discovers them, nobody writes their names down, and what a reader actually
  meets is the `DCAT` id above.
* **Every key the catalogue manifest accepts is described in
  [`doc/guide/catalogs-manifest`](guide/catalogs-manifest.en.md), and every key that page lists is
  one the manifest accepts.** Read from `eng/catalogs.schema.json` rather than from
  `eng/catalogs.json`: the manifest is one instance and reaches for eight of the fifteen keys, so
  checking against it would stop asking about exactly the keys a reader needs the page for. The
  schema is not a second document — a test beside it holds it to the reader's own parameters.
* **Every rule a sample shows is one its catalogue publishes.** `SonarRule.S1144` resolves against
  the compiled `DiagnosticCatalog.Sonar`, and the container is never pluralised. This one exists
  because it had already been needed: sixteen samples across three documents spelled the container
  `SonarRules`, and every one of them was uncompilable.

Every one of them compares a document against the compiled truth rather than against another
document. That is the same reasoning as
[ADR-0009](adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md): the descriptors are what
the analyzer reports with, so they are what a claim about them is checked against.

### What no check here can reach

Each of those works by enumerating a set from a file something else already keeps true. That is what
makes them trustworthy, and it is exactly their boundary: a build property, a manifest key, a release
train, a workflow, a hook, a `tools/` script, a page of this guide — none of them can be enumerated
that way, and none of them is checked by anything in this project.

So the general case is carried by the commit convention instead. A `feat` states what it documented,
or states in words why it documented nothing:

```text
Docs: doc/guide/dcat-reference.en.md, doc/guide/dcat-reference.fr.md
Docs: none — the cache is internal; nothing a consumer can name has moved
```

The footer's shape is linted with the rest of the message; whether the files it names were really
touched is resolved against the commit in CI, and a page named in one language only is refused —
both files exist, so the parity check above cannot see it. The rule, and what it deliberately does
not guarantee, is
[ADR-0025](adr/0025-bind-every-feature-commit-to-the-documentation-it-changed.en.md); the wording is in
[`CONTRIBUTING.md`](../CONTRIBUTING.md).

### Showing a reference that does not exist

Some pages have to. The tutorial asks the reader to break a reference and read the `CS0117` it
produces — a rule that existed would ruin the step — and the concepts page shows
`SonarRule.S1144Id` as the naming shape the design declined. Declare it in the document, with the
reason:

```markdown
<!-- dcat-doc:missing SonarRule.S1145 the deliberate mistake of step 3 -->
```

Declared in the document and not in the test, so a reader of the source meets the reason where the
exemption is. Per document, so the same misspelling on any other page still fails. The reason is
required. And a declaration that names a reference the page no longer shows fails too — an exemption
nothing uses covers whatever gets written there next.

## Adding a page

1. Write `doc/guide/<name>.en.md` with the banner, the audience line and the footer.
2. Write `doc/guide/<name>.fr.md` in the same commit. A page merged with "French to follow" does not
   get its French, and the parity test declines to let it try.
3. Insert it into the reading order: add a row to the map, and adjust the `←`/`→` of its two
   neighbours in both languages.
4. Run `dotnet test -c Release` and read what the documentation tests say.
