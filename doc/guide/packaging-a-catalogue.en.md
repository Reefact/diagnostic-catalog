# Packaging a catalogue

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./packaging-a-catalogue.fr.md)

For anyone about to publish one. What to reference, what propagates to your consumers whether you
meant it or not, and what nuget.org will do to your README.

## Reference the foundation the ordinary way

```xml
<PackageReference Include="DiagnosticCatalog" Version="0.1.0" />
```

Not `PrivateAssets="all"`. Your consumers need `DiagnosticRuleAttribute` to be resolvable in their
own compilation, and hiding your dependency is what takes it away from them.

Since [ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.en.md) that one line
delivers more than the attribute: `DiagnosticCatalog` carries the `DCAT` analyzers and their code
fixes beside it. There is no second package for you or for your consumers to reference.

It is necessary, and since
[ADR-0038](../adr/0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.en.md) it is
not sufficient. The analyzers reach a compiler only where a catalogue asks for them, and asking is
[one file you pack](#ship-the-opt-in-that-checks-your-consumers) — three lines, in the next section.
Skip it and your consumers are **silently** unchecked: their build succeeds and nothing reports.

> **A correction, stated rather than quietly fixed.** This guide used to say that hiding the
> foundation leaves the analyzers finding **no rules at all** and reporting nothing. That is not what
> happens, and it is now asserted rather than argued: with the foundation absent from a consumer's
> compilation and present in the catalogue's metadata, `DCAT0006` is still reported. Two mechanisms
> make it survive — the pre-filter admits your assembly because its module still *lists*
> `DiagnosticCatalog` among its references, and the marker is matched by fully qualified metadata
> name, so an attribute that cannot be resolved is an error type that still carries its name. The
> test is `MarkerRecognitionTests.A_referenced_catalogue_is_found_although_the_consumer_cannot_resolve_the_marker`.
> What hiding the foundation decides now is something else, below: whether an analyzer runs there at
> all.

So what does `PrivateAssets="all"` actually cost? Three things, and the first is why this section is
now a rule rather than a preference:

* **Your consumers are not checked at all.** The analyzers ride inside `DiagnosticCatalog`, so the
  reference that hides it hides them too. Measured, as
  `a catalogue hiding the foundation delivers no analyzer either` in
  `tools/packaging/verify-consumption.sh`.
* **A consumer who declares rules of their own cannot.** `[DiagnosticRule]` does not resolve in their
  source, and they get `CS0246` until they add the foundation by hand — a dependency your package
  already had and declined to declare.
* **Anything reading your catalogue reflectively at run time** — a documentation generator, an
  inventory script, `dcat list` against your assembly — meets an attribute type it cannot bind.

The last two fail loudly, which is what once made this advice rather than a rule. The first does
not: a codebase nothing checks looks exactly like a codebase with nothing to report, which is the
silence this library exists to remove.

## Ship the opt-in that checks your consumers

Pack this file into your catalogue as `build/<your package id>.props`:

```xml
<Project>
  <PropertyGroup>
    <EnableDiagnosticCatalogAnalyzers Condition="'$(EnableDiagnosticCatalogAnalyzers)' == ''">true</EnableDiagnosticCatalogAnalyzers>
  </PropertyGroup>
</Project>
```

```xml
<ItemGroup>
  <None Include="DiagnosticCatalogOptIn.props"
        Pack="true" PackagePath="build/$(PackageId).props" />
</ItemGroup>
```

**The name matters.** NuGet imports `build/<package id>.props` and ignores a file called anything
else, so a typo here is a catalogue that checks nobody and says nothing about it.

**Why it is your file and not ours.** NuGet imports a package's `build/` folder for a **direct**
reference and for nothing further out. That is the one place in the whole mechanism where "somebody
referenced *this*" is distinguishable from "somebody is downstream of this", and only your package
sits at that point: the foundation is transitive for your consumers and transitive again for
theirs, so it cannot tell the two apart. Your three lines are what stop an application that
references a library that references you from being analysed by a catalogue it never chose.

The property is read by `buildTransitive/DiagnosticCatalog.targets` inside `DiagnosticCatalog`,
which is where the analyzer assemblies live. You ship no analyzer of your own, which is what keeps a
consumer of several catalogues on exactly one analyzer instance at one version.

**Your consumers can overrule it, in both directions**, and it costs you nothing to let them: a
project setting `EnableDiagnosticCatalogAnalyzers` to `false` keeps your catalogue and declines the
analysis, and one setting it to `true` is asking for the checks from further out than a direct
reference. Neither is a case you have to handle.

In this repository the file is [`build/CatalogueAnalyzerOptIn.props`](../../build/CatalogueAnalyzerOptIn.props)
and `Directory.Build.targets` packs it into every packable project that depends on the foundation,
so a fourteenth catalogue carries it without anybody remembering. Outside this repository, it is
three lines in your `.csproj`.

## Not taking the dependency at all

If you would rather ship a catalogue with **no** dependencies whatsoever, declare the marker yourself:

```csharp
namespace DiagnosticCatalog
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    internal sealed class DiagnosticRuleAttribute : System.Attribute { }
}
```

This is supported and tested, not a trick. The analyzers match the marker by its **fully qualified
name**, never by symbol identity, so your copy is recognised exactly like the real one. It is the same
pattern PolySharp uses for `IsExternalInit`, and
`MarkerRecognitionTests.A_catalogue_declaring_its_own_marker_is_still_analysed` is what keeps it
working.

The name must be exact — `DiagnosticCatalog.DiagnosticRuleAttribute`, in that namespace. An attribute
of the same short name somewhere else is somebody else's, and is deliberately not matched.

`internal` is right: nothing outside your assembly needs to apply it, and a public copy would collide
with the real one for any consumer who references both.

**One thing the copy takes away.** A type's identity in .NET is its assembly *plus* its name, so your
copy and the real one are two unrelated types that merely agree on a name — invisible until something
reads your catalogue reflectively at run time and matches **by type**, because
`GetCustomAttribute<DiagnosticRuleAttribute>()` binds the foundation's attribute, never yours, and
returns `null` on every rule you ship. Matching on `GetType().FullName` finds them all instead, which
is how the analyzers, `dcat` and this repository's own `GeneratedCatalogTests` read a catalogue. Worth
a line in your README, because unlike `PrivateAssets="all"` above this one fails quietly: the tool
reports a catalogue of zero rules, indistinguishable from an assembly that declares none.

**And one thing it takes away from your consumers.** The analyzers travel with the foundation, so a
catalogue that depends on nothing delivers nothing that reports: your users get the constants, and
no `DCAT0006` on the literals they have not converted yet. They can reference `DiagnosticCatalog`
themselves to get the checking back — which is a second line for that README, because nothing else
will tell them.

## What propagates to your consumers

Referencing your catalogue checks **your consumers**, and stops there. Every row below was measured
against a real restore rather than read from NuGet's documentation, in
`tools/packaging/verify-consumption.sh`:

| Who is compiling | The analyzers run |
| --- | --- |
| a project referencing your catalogue | **yes**, if you shipped the opt-in |
| a project referencing your catalogue, and you shipped no opt-in | no, silently |
| a project referencing a library that references your catalogue | **no** |
| that same project, having set `EnableDiagnosticCatalogAnalyzers=true` | yes |
| a project referencing your catalogue with `EnableDiagnosticCatalogAnalyzers=false` | no; it keeps `[DiagnosticRule]` |
| a project referencing your catalogue, which hid the foundation with `PrivateAssets="all"` | no, and `[DiagnosticRule]` stops resolving for them |

**The third row is why the opt-in is your file.** An application referencing an ordinary library
that took your catalogue for its own suppressions chose neither you nor the analyzers, and
`DCAT0006` ships as an **error** — so before
[ADR-0038](../adr/0038-stop-the-analyzers-at-the-project-that-references-a-catalogue.en.md) that
application's build stopped on its own suppressions, with nothing in its own project file to point
at. The library author had the only lever and no reason to reach for it.

**The last row is not an opt-out a catalogue can afford.** One package means one lever: withholding
the analyzers withholds the attribute with them, so a consumer written the ordinary way stops
compiling rather than merely going unchecked — the `CS0246`
[troubleshooting](troubleshooting.en.md#cs0246-the-type-or-namespace-name-diagnosticrule-could-not-be-found)
already reports. The check that says so is
`a catalogue hiding the foundation withholds the attribute assembly`, whose consumer fixture has to
declare its own marker to compile at all.

**A library that references your catalogue is checked itself** — it did choose it. What it no longer
does is pass that on, and it needs to write nothing to get that. `PrivateAssets="all"` on its own
reference still works and is now redundant.

## The summary table

| Who you are | What you reference | How |
| --- | --- | --- |
| **Consumer** — writes suppressions | a catalogue | ordinary reference; the checks arrive with it |
| **Consumer** — wants the checks and no catalogue | `DiagnosticCatalog` | ordinary reference |
| **Consumer** — wants a catalogue and no analysis | a catalogue | ordinary reference, plus `EnableDiagnosticCatalogAnalyzers=false` |
| **Consumer** — wants the checks a library's catalogue no longer passes on | nothing more | `EnableDiagnosticCatalogAnalyzers=true` |
| **Catalogue author** | `DiagnosticCatalog` | **ordinary reference**, never `PrivateAssets="all"`, **plus the opt-in above** |
| **Library author** — took a catalogue, will not impose it | that catalogue | nothing; it no longer travels |
| **Analyzer author** — owns both | `DiagnosticCatalog` in the catalogue project; the catalogue in the analyzer project | see [closing the loop](first-party-analyzers.en.md) |

## Your README is your package page

`<PackageReadmeFile>` is rendered by nuget.org, and that renderer has two constraints most people
meet the hard way.

**It resolves no relative link.** `[the author's guide](../../doc/guide/authoring-a-catalogue.en.md)`
is a dead link on the package page, however correct it is in the repository. Link outward with
absolute addresses:

```markdown
[the author's guide](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.en.md)
```

This repository had five of those, live on published pages, until a test started refusing them.

**It offers no language switch.** One file per package, in one language — which decides which half
of a bilingual README a package carries, not whether the other half exists. The pages here are
maintained as `README.en.md` and `README.fr.md`, `<PackageReadmeFile>` names the English one, and
the banner offering the French one is a full address like every other link they write
([ADR-0034](../adr/0034-pair-every-package-readme-in-english-and-french.en.md)).

Two things a catalogue's README should carry that nothing else will tell a reader:

* **Which upstream release it mirrors, and when it was generated.** It is the first thing anyone
  evaluating the package needs, and a package page has no sibling beside it. In this repository the
  generator writes it between `<!-- mirror:begin -->` markers, and `DocumentedMirrorTests` fails a
  document whose banner does not match the `CatalogSource` attribute the generator wrote — a banner
  nothing can reach states nothing. `dcat` writes into whichever README your catalogue folder
  actually keeps: `README.md` if that is your convention, `README.en.md` and `README.fr.md` if you
  maintain a pair. A spelling you do not keep is not reported.
* **The other catalogues you publish, by package id.** A reader landing from a search sees that
  catalogue and nothing else.

## The icon above it

nuget.org renders a package icon at 128px, above the title, in every listing and every search
result. It is the first thing anybody sees of your package and about the last thing anybody thinks
about while building it — and at that size it holds roughly three characters.

The catalogues here spend those characters on the **prefix of the rules the catalogue mirrors**,
never on the vendor's name. StyleCop's badge reads `SA` rather than `SC`
because `SA1000` is what a reader types inside `[SuppressMessage(...)]` and `SC` is what nobody
types, so the icon answers "does this package hold my rule?" without the page being opened. The mark
itself is [`assets/icon-template.svg`](../../assets/icon-template.svg), where the badge text is the
one thing left to edit.

Three characters is a ceiling here, not an observation: a longer prefix is abbreviated — `xUnit`
becomes `XU`, `MSTEST` becomes `MST`. The type shrinks to clear the plate's corners, so the word
that fits exactly is the word nobody can read; measured on the catalogues published here, a
six-letter badge lands at under 5px in that listing while a three-letter one holds 9.8px. The
record in force is [ADR-0035](../adr/0035-badge-a-shared-prefix-catalogue-with-its-subject.en.md);
the cap itself was first stated by
[ADR-0033](../adr/0033-cap-the-badge-at-three-letters.en.md), which ADR-0035 supersedes and whose
cap it keeps, and the choice of what the badge says by
[ADR-0032](../adr/0032-badge-a-catalogues-icon-with-its-rule-prefix.en.md) before that.

**And the prefix may already be taken.** Three catalogues here mirror `RS` rules, so the rule above
cannot give all three the same badge — the mark does not vary, so identical badges are identical
files, which `PackageIconTests` refuses. When a prefix is already worn, the newcomer's badge names
the subject of the package it mirrors instead, and the prefix stays with the catalogue already
publishing it: `DiagnosticCatalog.Roslyn` keeps `RS`, while `DiagnosticCatalog.PublicApi` reads `API`
and `DiagnosticCatalog.BannedApi` reads `BAN`. That is the second half of ADR-0035.

Worth knowing how far the check around this reaches, because it is narrower than it looks.
`PackageIconTests` fails a catalogue that carries no `icon.png` of its own, one whose icon is
byte-identical to another catalogue's, and one still wearing the repository's unbadged fallback. It
never reads the badge: distinctness is the property it can assert, and what the letters actually say
rests on that template and on review.

## What packing gives you here

For reference, if you are looking at this repository's own projects: a project joins a release train
by declaring `<ReleaseTrain>` in its own `.csproj`, and that single declaration is the whole
membership — it makes the project packable and gives it an embedded SPDX SBOM. Nothing lists the
projects a second time, so a renamed or moved one cannot silently drop out of its own release.

The rule that comes with it: a project on one train must not carry a `<ProjectReference>` to a project
on another, because `dotnet pack` would stamp a dependency on a version that was never published
([ADR-0007](../adr/0007-depend-across-trains-through-published-packages.en.md)). It is why the catalogues
here take the foundation as a `PackageReference` even though its source sits in the same repository.

## Where to go next

* [**Versioning a catalogue**](versioning-a-catalogue.en.md) — the rule about `const` that decides
  what a release may and may not change.
* [**The `DCAT` diagnostics**](diagnostics.en.md) — what your users will be told, and when.
* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — release trains, and how a catalogue is added here.

---

<div align="center">
<a href="./versioning-a-catalogue.en.md">← Versioning a catalogue</a> · <a href="./README.en.md">↑ Table of contents</a>
</div>
