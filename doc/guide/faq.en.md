# FAQ

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./faq.fr.md)

For anyone weighing a question rather than chasing a symptom. If your build is telling you something,
[troubleshooting](troubleshooting.en.md) is the other page.

## Does this add anything to what I ship?

No. `SuppressMessageAttribute` is `[Conditional("CODE_ANALYSIS")]`, so unless you define that symbol
the compiler does not write it into your assembly at all; the constants fold before that. Nothing is
loaded, nothing runs, and no assembly reference survives.

Asserted rather than promised — [the zero-footprint guarantee](zero-footprint.en.md) says exactly what
the test establishes and what it does not.

## Why is the category argument worth all this? Nothing reads it.

Precisely because nothing reads it.

A wrong **id** eventually surfaces: the suppression stops matching and the warning comes back. A wrong
**category** has no fate at all — the line compiles, the warning is still suppressed, and the file now
claims a category the vendor does not use. No build fails, no test reddens, no tool reports.

A mistake with no symptom is not a small mistake; it is one that cannot be found. The record is what
degrades, and the first person to trust it — grepping for every `"Major Code Smell"` suppression
before an upgrade — gets an answer that is quietly short.

## Why not take the constants from the analyzer packages themselves?

Because there is nothing to reference, and nothing in it to reference.

**Nothing to reference.** Eight of these packages ship their assemblies under `analyzers/`, with no
`lib/` and no `ref/` folder, and declare `<developmentDependency>true</developmentDependency>`.
NuGet hands such an assembly to the compiler as an analyzer plugin; it never enters the consumer's
reference set. There is no `using` to write, whatever the assembly holds.

The other three arrive through the SDK rather than through a `PackageReference`, and two of them —
the ASP.NET Core and .NET runtime targeting packs — do carry a `ref/` folder every project compiles
against. Their analyzers are not in it: they sit beside it under `analyzers/dotnet/cs/`, handed over
as plugins like all the rest. Reading both packs whole, every reference assembly included, turns up
no rule-id constant outside that folder — the half you can reference is the half with nothing on it.

**Nothing in it to reference.** Measured over the metadata of every analyzer assembly in the eleven
packages the catalogues mirror, satellite resources aside:

| Package | Public types | `public const` | Rule-id or category constants |
| --- | ---: | ---: | ---: |
| `SonarAnalyzer.CSharp` 10.31.0.145097 | 1801 | 861 | 0 |
| `StyleCop.Analyzers.Unstable` 1.2.0.556 | 6 | 12 | 0 |
| `Microsoft.CodeAnalysis.NetAnalyzers` 10.0.302 | 740 | 128 | 9 |
| `Microsoft.CodeAnalysis.CSharp.CodeStyle` 5.6.0 | 105 | 28 | 0 |
| `xunit.analyzers` 1.27.0 | 178 | 219 | 0 |
| `NUnit.Analyzers` 4.14.0 | 103 | 1 | 0 |
| `MSTest.Analyzers` 4.3.3 | 182 | 0 | 0 |
| `Microsoft.NET.ILLink.Tasks` 10.0.10 | 80 | 262 | 0 |
| `Microsoft.AspNetCore.App.Ref` 10.0.10 | 96 | 435 | 0 |
| `Microsoft.NETCore.App.Ref` 10.0.10 | 260 | 369 | 37 |
| `Microsoft.CodeAnalysis.Analyzers` 5.6.0 | 155 | 1820 | 72 |

StyleCop is the clearest: 1314 types across its two assemblies, six of them public, and not one of
those an analyzer. MSTest is the flattest: 182 public types with not a single public constant among
them. `xunit.analyzers` is the sharpest: more public constants than it has public types — 219
against 178 — and not one of them a rule id.

Three packages leak, and none leaks a contract. NetAnalyzers declares nine rule ids as public
constants — seven named `RuleId` (`CA1008`, `CA1052`, `CA1069`, `CA1708`, `CA1715`, `CA1821`,
`CA2214`) and two more on the P/Invoke analyzer (`CA1401`, `CA2101`) — against the 318 rules its
catalogue holds. The runtime pack leaks the other way round: its source generators declare 37 such
constants, 31 distinct `SYSLIB` ids, against the 13 rules its catalogue holds. More ids than the
catalogue carries, and still nothing to take: every one of them sits in a generator assembly the
compiler loads as a plugin, which is the paragraph above.

`Microsoft.CodeAnalysis.Analyzers` leaks the whole thing, and is the interesting case. A public
`DiagnosticIds` class holds every one of the 52 rule ids its catalogue mirrors, `RS1001` through
`RS2008`, plus `IDE0055`; a public `DiagnosticCategory` class holds 19 categories. That is, to the
value, what a generated catalogue publishes — written by the vendor, complete, and correct. And it
is unreachable: the package declares `<developmentDependency>true</developmentDependency>` and
ships no `lib/`, so both assemblies reach the compiler as plugins and no `using` resolves to
either. The one vendor that did the work put it where it cannot be referenced.

**And a category is a constant in exactly one of the eleven** — those 19, as unreachable as the
ids beside them. Everywhere else it is zero. A category exists only on
the `DiagnosticDescriptor` instances an analyzer builds at run time, out of localisable resources.
An attribute argument must be a compile-time constant, so even reflecting over
`SupportedDiagnostics` yields a `string` that cannot occupy the position.

Which is why generation happens where it does: constructing the analyzers and reading their
descriptors ([ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md)) is
the only way to obtain a category at all, and it has to happen before the consumer compiles.

None of this is a law. A vendor could publish a package of constants beside its analyzer tomorrow —
one the compiler would actually reference. None has.

## Can I just write my own constants file?

Yes, and for thirty suppressions over five rules you probably should.

What it does not give you is where the values came from. `"Major Code Smell"` was typed by someone,
from a snapshot — a blog post, an IDE's *Suppress → In Source*, another file. Being consistent about a
wrong value is not the same as being right. It also does not know when a rule is retired.

[The alternatives](alternatives.en.md) compares the two properly, including where the line sits.

## Why not just an analyzer that validates the strings?

This library ships one, and it is deliberately the smaller half.

A check on a string can only judge strings it recognises. `[SuppressMessage("Usage", "S1144")]`
matches no rule any catalogue describes — so is the category wrong, or is it a rule from an analyzer
you have not catalogued? Nothing can tell, and an analyzer that guessed would report a false positive
against every unmirrored analyzer. So it stays quiet, which is right and is not a solution.

A constant has no such problem: it either resolves or it is a compile error. The validation is the C#
compiler's, and it has been there all along.

## Does this work with `#pragma warning disable`?

No, and it never will. The directive takes a bare identifier token, not an expression, so there is no
position a constant could occupy. That is the C# grammar rather than a missing feature.

Same for `.editorconfig` severity keys — plain text, outside the compilation model entirely.

If most of your suppressions are `#pragma`, [when not to use this](when-not-to-use.en.md) says so
plainly.

## Do I need the analyzers package?

Not for the guarantee. A misspelled rule is a compile error because `SonarRule.S1144.Id` is a member
the compiler resolves — no analyzer involved.

`DiagnosticCatalog.Analyzers` finds the suppressions you have **not** converted yet, catches a pair
naming two different rules, and offers the fixes. It is a migration aid rather than the mechanism —
and it has no version on nuget.org today.

## Why is `dcat` a separate tool rather than a source generator?

A source generator runs inside every consumer's build, which is the wrong place for something that
downloads a NuGet package and constructs third-party analyzers.

Generation happens once, in the repository that publishes the catalogue, and its output is committed
and reviewed. That is what makes a recategorisation something a human reads in a pull request rather
than something that changes silently in everybody's `obj/`.

## Can a catalogue cover Visual Basic?

Not today. Constructing a Visual Basic analyzer needs a Roslyn the descriptor worker does not carry,
so `--language vb` would refuse after downloading the package. The key exists so the refusal is
explicit rather than a guess.

## Why is the generated date a string?

Because an attribute argument must be a compile-time constant, and no date type can be one. Same
reason `Id` and `Category` are `const string`. Use `yyyy-MM-dd`.

## Why does a catalogue never delete a retired rule?

Constants are inlined into your consumers' assemblies at **their** compile time. Deleting one breaks
their recompilation with a bare `CS0117` that names a type and a missing member and explains nothing.

Carried forward as `[Obsolete]`, the same upgrade gives them `CS0618` — which names the rule and says
what happened ([ADR-0010](../adr/0010-carry-a-retired-rule-forward-as-obsolete.en.md)).

## Why does the nightly job open a pull request instead of merging?

Because an id or a category that moved upstream is a change to a **published contract**, and because
nothing validates a suppression's category, a wrong value merged unreviewed would stay invisible for
as long as it existed.

Automation finds the change; a human accepts it.

## Why must `--solution` projects declare a property?

Because guessing is not close enough. Measured on this repository: "references `Microsoft.CodeAnalysis`"
matches eight projects of which one is an analyzer; "declares a `DiagnosticAnalyzer` subclass" matches
three of which one is an analyzer — the other two are fixtures, one written to *fail* construction,
one in an assembly written not to load whole.

A project missed means its rules are absent, an absent rule is indistinguishable from a retired one,
and they are published as `[Obsolete]` — telling that vendor's users something false.

## Is this affiliated with SonarSource, Microsoft or StyleCop?

No. The catalogues are unofficial mirrors, generated from the analyzers' own descriptors. They are not
affiliated with, endorsed by, or supported by any of those projects. "Sonar" and "SonarQube" are
trademarks of SonarSource S.A.

The rule *facts* are redistributed — id, category, help link, title. The vendors' rule prose is
deliberately not ([ADR-0011](../adr/0011-redistribute-rule-facts-only-never-the-vendors-prose.en.md),
[ADR-0014](../adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.en.md)).

## Can I use this on .NET Framework?

Yes. The libraries target `netstandard2.0` and `net10.0`, and the floor is more than a compile-time
claim — CI runs the test suite on the real .NET Framework 4.7.2 CLR
([ADR-0001](../adr/0001-floor-the-libraries-on-net-framework-4-7-2.en.md)).

## Where do I ask something that is not here?

The [issue tracker](https://github.com/Reefact/diagnostic-catalog/issues). A question that needed
asking is usually a page that needed writing.

---

<div align="center">
<a href="./troubleshooting.en.md">← Troubleshooting</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./glossary.en.md">Glossary →</a>
</div>
