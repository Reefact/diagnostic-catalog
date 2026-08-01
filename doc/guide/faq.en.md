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
what happened ([ADR-0010](../adr/0010-carry-a-retired-rule-forward-as-obsolete.md)).

## Why does the nightly job open a pull request instead of merging?

Because an id or a category that moved upstream is a change to a **published contract**, and because
nothing validates a suppression's category, a wrong value merged unreviewed would stay invisible for
as long as it existed.

Automation finds the change; a human accepts it.

## Why must `--solution` projects declare a property?

Because guessing is not close enough. Measured on this repository: "references `Microsoft.CodeAnalysis`"
matches six projects of which one is an analyzer; "declares a `DiagnosticAnalyzer` subclass" matches
two of which one is a fixture written to *fail* construction.

A project missed means its rules are absent, an absent rule is indistinguishable from a retired one,
and they are published as `[Obsolete]` — telling that vendor's users something false.

## Is this affiliated with SonarSource, Microsoft or StyleCop?

No. The catalogues are unofficial mirrors, generated from the analyzers' own descriptors. They are not
affiliated with, endorsed by, or supported by any of those projects. "Sonar" and "SonarQube" are
trademarks of SonarSource S.A.

The rule *facts* are redistributed — id, category, help link, title. The vendors' rule prose is
deliberately not ([ADR-0011](../adr/0011-redistribute-rule-facts-only-never-the-vendors-prose.md),
[ADR-0014](../adr/0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.md)).

## Can I use this on .NET Framework?

Yes. The libraries target `netstandard2.0` and `net10.0`, and the floor is more than a compile-time
claim — CI runs the test suite on the real .NET Framework 4.7.2 CLR
([ADR-0001](../adr/0001-floor-the-libraries-on-net-framework-4-7-2.md)).

## Where do I ask something that is not here?

The [issue tracker](https://github.com/Reefact/diagnostic-catalog/issues). A question that needed
asking is usually a page that needed writing.

---

<div align="center">
<a href="./troubleshooting.en.md">← Troubleshooting</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./glossary.en.md">Glossary →</a>
</div>
