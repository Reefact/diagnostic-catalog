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

> **A correction, stated rather than quietly fixed.** This guide used to say that hiding the
> foundation leaves the analyzers finding **no rules at all** and reporting nothing. That is not what
> happens, and it is now asserted rather than argued: with the foundation absent from a consumer's
> compilation and present in the catalogue's metadata, `DCAT0006` is still reported. Two mechanisms
> make it survive — the pre-filter admits your assembly because its module still *lists*
> `DiagnosticCatalog` among its references, and the marker is matched by fully qualified metadata
> name, so an attribute that cannot be resolved is an error type that still carries its name. The
> test is `MarkerRecognitionTests.A_referenced_catalogue_is_found_although_the_consumer_cannot_resolve_the_marker`.

So what does `PrivateAssets="all"` actually cost? Two things, and both are worth avoiding:

* **A consumer who declares rules of their own cannot.** `[DiagnosticRule]` does not resolve in their
  source, and they get `CS0246` until they add the foundation by hand — a dependency your package
  already had and declined to declare.
* **Anything reading your catalogue reflectively at run time** — a documentation generator, an
  inventory script, `dcat list` against your assembly — meets an attribute type it cannot bind.

Loud failures rather than silent ones, which is why the advice is "do not" rather than "never, on
pain of the thing this library exists to prevent".

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

## What propagates to your consumers

If your catalogue references `DiagnosticCatalog.Analyzers`, the analyzers reach **your consumers**
too — so referencing your catalogue is enough for them to get the checking.

That was measured against a real restore rather than read from NuGet's documentation, which says the
opposite:

| Your reference to `DiagnosticCatalog.Analyzers` | The analyzers run for your consumers |
| --- | --- |
| no `PrivateAssets` | **yes** |
| `PrivateAssets="none"` | yes |
| `PrivateAssets="all"` | no |

**Silence propagates.** If you would rather not impose analysis on everyone downstream, say so
explicitly with `PrivateAssets="all"` — and know that you are choosing it, rather than discovering
later that you did.

The choice is a real one. Imposing analysis gets your users the migration fix and the coherence
checks without them knowing the package exists; it also puts warnings in builds that did not ask for
them, on the day they upgrade your catalogue. Whichever you pick, say which in your README.

## The summary table

| Who you are | What you reference | How |
| --- | --- | --- |
| **Consumer** — writes suppressions | a catalogue | ordinary reference |
| **Consumer** — wants the checks | `DiagnosticCatalog.Analyzers` | `PrivateAssets="all"` |
| **Catalogue author** | `DiagnosticCatalog` | **ordinary reference** |
| **Catalogue author** — wants consumers checked too | `DiagnosticCatalog.Analyzers` | ordinary reference, deliberately |
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

**It offers no language switch.** One file per package, in one language. That is why `doc/` here is
bilingual and the package READMEs are not
([ADR-0022](../adr/0022-maintain-every-document-under-doc-in-english-and-french.en.md)).

Two things a catalogue's README should carry that nothing else will tell a reader:

* **Which upstream release it mirrors, and when it was generated.** It is the first thing anyone
  evaluating the package needs, and a package page has no sibling beside it. In this repository the
  generator writes it between `<!-- mirror:begin -->` markers, and `DocumentedMirrorTests` fails a
  document whose banner does not match the `CatalogSource` attribute the generator wrote — a banner
  nothing can reach states nothing.
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
reasoning is [ADR-0033](../adr/0033-cap-the-badge-at-three-letters.en.md), which supersedes
[ADR-0032](../adr/0032-badge-a-catalogues-icon-with-its-rule-prefix.en.md) on that point alone.

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
<a href="./versioning-a-catalogue.en.md">← Versioning a catalogue</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./dcat.en.md">The dcat tool →</a>
</div>
