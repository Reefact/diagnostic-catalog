# The `DCAT` diagnostics

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./diagnostics.fr.md)

For anyone who saw a `DCATxxxx` and wants to know what it means. Every diagnostic
`DiagnosticCatalog.Analyzers` reports: what triggers it, why it exists, and how to configure it.

That assembly ships inside the `DiagnosticCatalog` package rather than in one of its own, so nothing
has to be referenced to get these. Every catalogue depends on the foundation and may not hide it, so
referencing any catalogue turns them on
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.en.md)); referencing
`DiagnosticCatalog` alone is the way to be checked with no catalogue at all.

They fall into two groups. **Definition** diagnostics look at a rule you declared; you only see them
if you write a catalogue. **Use-site** diagnostics look at a suppression you wrote, which is most
people.

| Id | Looks at | Title | Default | Fix |
| --- | --- | --- | --- | --- |
| [`DCAT0001`](#dcat0001) | use site | Category and Id must reference the same diagnostic rule | **Error** | two, unranked |
| [`DCAT0002`](#dcat0002) | definition | A diagnostic rule must be declared as a static non-generic class | Warning | yes, conditionally |
| [`DCAT0003`](#dcat0003) | definition | A diagnostic rule must expose a public constant string named `Id` | Warning | yes, conditionally |
| [`DCAT0004`](#dcat0004) | definition | A diagnostic rule must expose a public constant string named `Category` | Warning | yes, conditionally |
| [`DCAT0005`](#dcat0005) | definition | The diagnostic rule type name should match its `Id` | Info | — |
| [`DCAT0006`](#dcat0006) | use site | Use a diagnostic catalog reference instead of string literals | **Error** | yes |
| [`DCAT0007`](#dcat0007) | use site | Suppression mixes a catalog reference with a string literal | **Error** | yes, conditionally |
| [`DCAT0009`](#dcat0009) | use site | `UnconditionalSuppressMessage` only accepts `IL####` identifiers | Warning | — |
| [`DCAT0011`](#dcat0011) | definition | A diagnostic rule's category must reference a declared category constant | Warning | — |
| [`DCAT0012`](#dcat0012) | definition | A rule identifier should be written as `nameof` | Warning | yes, conditionally |
| [`DCAT0013`](#dcat0013) | definition | The diagnostic rule type name does not say its `Id` | Warning | — |
| [`DCAT0014`](#dcat0014) | use site | A suppression must carry a justification | Warning | — |

`DCAT0008` and `DCAT0010` are specified but deliberately not in 1.0.

---

## Use-site diagnostics

### `DCAT0001`

**The category and the identifier come from two different rules.**

```csharp
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S2094.Id)]
//               ^^^^^ from S1144         ^^^^^ from S2094
```

Copy-paste, nearly always: you duplicated a working suppression and changed one half.

It is reported **even when the two rules share a category**, and that case is the one worth
understanding. The line compiles to exactly the same thing a correct suppression would, and works
perfectly — until the vendor recategorises either rule, at which point it silently carries the wrong
category with nothing in the platform to say so. A check that compared values instead of rules would
miss precisely this.

**Two fixes, neither recommended.** Only you know which half was the typo:

```text
Use SonarRule.S1144.Id        — keep the category, correct the identifier
Use SonarRule.S2094.Category  — keep the identifier, correct the category
```

Worth knowing while you choose: Roslyn matches a suppression on the **identifier alone** and never
consults the category. So correcting the category leaves what is suppressed exactly as it is, while
correcting the identifier changes it.

**The other fault under this id: a member in the wrong slot.** The same rule, both members
referenced, and still nothing suppressed:

```csharp
[SuppressMessage(SonarRule.S1144.Id, SonarRule.S1144.Category)]   // swapped
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.HelpLinkUri)]
```

A rule type carries more than the pair, so completion offers every member of it in one list. Both
lines compile and resolve; by the paragraph above, the identifier slot decides what is suppressed,
and neither line puts an identifier there. **No fix is offered here** — whether you wrote the wrong
member or the wrong rule is not something a tool can know.

### `DCAT0006`

**These string literals match a rule your project can see.**

```csharp
[SuppressMessage("Major Code Smell", "S1144")]
```

Reported only when a known rule matches the pair, so a codebase that has adopted no catalogue stays
completely silent. The fix rewrites it as a reference and adds any `using` needed.

The identifier is truncated at the first colon before matching, exactly as Roslyn does, so the form
Visual Studio's *Suppress → In Source* generates is recognised:

```csharp
[SuppressMessage("Major Code Smell", "S1144:Unused private members should be removed")]
```

The suffix is dropped by the fix. It duplicated the rule's own title, which the catalogue carries as
XML documentation.

If **two** catalogues describe the same rule, you get the diagnostic and no automatic fix — choosing
between them is yours.

> **On adoption.** This fires on every literal suppression at once, the day you add a catalogue —
> and the catalogue brings the analyzer with it, so there is no second reference standing between
> you and that. It is an **error** by default
> ([ADR-0027](../adr/0027-ship-the-use-site-diagnostics-as-errors.en.md)), so the build that adds
> the catalogue is the build that fails. Lower it to `suggestion`, migrate with *Fix all
> occurrences*, then raise it.

### `DCAT0007`

**One half migrated, one half still a literal.**

```csharp
[SuppressMessage(SonarRule.S1144.Category, "S1144")]
```

The most common half-done state, and the only one where the intended rule is known without
ambiguity — the migrated argument names it. Completed from that rule, rewriting only the literal:
whatever spelling you chose for the other side, an alias included, is left alone.

**Unless the literal names something else.** `"S9999"` beside `SonarRule.S1144.Category` gets the
diagnostic and **no** fix, because completing it would silence a different rule than the one
silenced today — and let the original warning back in. That is a decision, not a migration.

### `DCAT0009`

**A non-`IL` rule used in `UnconditionalSuppressMessage`.**

```csharp
[UnconditionalSuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]
```

That attribute is read by the trimmer, from your compiled assembly, long after the compiler has
finished. Its decoder accepts only identifiers shaped like `IL####` and **discards everything else
outright**. Roslyn does not process the attribute either. So this suppression is a no-op that no
other tool in the toolchain reports.

The check mirrors the trimmer's decoder rather than a stricter pattern, so identifiers it *does*
honour are left alone — including its own `IL2026:FriendlyName` form. Reporting those would be
telling you to change something that works.

### `DCAT0014`

**The suppression names a rule and never says why.**

```csharp
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]
```

Everything else on this page is about *which* diagnostic a line silences. This one is about the other
half. The pair is checked by the compiler now; the reason the warning was acceptable is written
nowhere, and it cannot be recovered later — the warning is gone, and whoever decided it did not
matter is the only person who knew. Six months on, nobody can tell a considered suppression from one
somebody pasted.

```csharp
[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Called by the serializer through reflection.")]
```

**Presence is the whole contract.** The value is read for its length, never for its meaning: a
justification of one word passes, and so does one you would have written better. Judging what a
justification *says* is out of scope on purpose, and stays there — it is a human question, and a tool
that scored prose would be wrong in both directions.

One non-blank value is refused: `"<Pending>"`, the placeholder Visual Studio writes when it generates
a suppression for you. It is that tool's own word for *nobody has filled this in yet*, matched exactly
and nothing like it — `"n/a"` and `"obvious"` pass, because ruling on those would be reading prose.
An empty string, whitespace, and `Justification = null` are blank and reported as such.

**Every suppression is held to it, including one written entirely in literals.** This is the only
diagnostic here that needs nothing from a catalogue: a literal suppression silences a warning exactly
as a reference does, and says exactly as little about why.

```csharp
[SuppressMessage("Usage", "xUnit1004")]   // reported, even with no catalogue in sight
```

That line matters more than it looks. [`DCAT0006`](#dcat0006) reports a literal pair only when a rule
your project can see matches it, so a suppression naming a rule no catalogue describes was, before
this, reported by nothing at all. `UnconditionalSuppressMessage` is held to it too — a suppression
read by a tool that runs long after the compiler is the one that most needs to say why it exists.

The one shape left alone is an identifier that names nothing, `[SuppressMessage("Usage", null)]`:
Roslyn matches on the identifier, so that line silences nothing and has nothing to justify.

A line being migrated therefore reports twice — `DCAT0006` for the pair, this for the reason — and
that is deliberate: converting a suppression does not answer the question it never answered. If you
already run StyleCop's `SA1404`, you will see both; they ask the same question, and one
`.editorconfig` line silences whichever you do not want
([ADR-0039](../adr/0039-require-a-justification-on-every-suppression.en.md)).

**No fix, and none is possible.** What belongs there is the one thing in the attribute that cannot be
read off the code ([ADR-0018](../adr/0018-a-code-fix-never-decides-what-only-the-author-can.en.md)).

It ships as a `Warning` rather than an error, unlike its three use-site neighbours: it reports lines
that are otherwise entirely correct, and a project that adopted a catalogue before this rule existed
should not have its build fail on them overnight. One line of `.editorconfig` raises it the day you
want it to.

---

## Definition diagnostics

These fire on code that declares rules. See [the catalogue author's guide](authoring-a-catalogue.en.md).

They fall into two groups. `DCAT0002`, `DCAT0003`, `DCAT0004` and `DCAT0011` say the rule is
**unusable or unanchored**; `DCAT0005`, `DCAT0012` and `DCAT0013` say it works and its name does not
tell you what it is.

Those that offer a fix offer it **when the repair is written in the code already**, and stay silent
about it otherwise. That line is not caution for its own sake: a fix that guessed would produce a rule
the compiler accepts and nobody checks, which is the failure this library exists to remove. Where a fix
is refused below, the diagnostic still names the type and the member — you finish it with what you know
and the tool does not.

`DCAT0011` offers none at all, for the same reason taken one step further: the repair is a class that
does not exist yet, holding a constant nobody has named.

### `DCAT0002`

**Marked `[DiagnosticRule]` but not a static non-generic class.** A rule holds constants and is never
instantiated; a generic one has no constant members to offer at all.

**Fix — *Make 'X' static*.** Offered for a plain class that could carry the keyword: no type
parameters, no base type or interface, no instance member, no instance constructor, not `partial`. A
redundant `sealed` or `abstract` goes with it, since the compiler rejects either beside `static`.

Nothing is offered for a generic type or for a `struct`, `interface`, `enum` or `record` — removing the
type parameters, or changing what kind of type it is, is not a repair of what you wrote but a
replacement of it. A `partial` class is refused because the parts the fix cannot see may hold the
instance members that decide the question.

### `DCAT0003`

**No public `const string Id`.** The usual cause is `static readonly` instead of `const`: it has a
value at run time but cannot be an attribute argument, which is the entire purpose. An empty or
whitespace-only value counts as absent.

Use `nameof(TheRuleType)`, which cannot drift from the type it names.

**Fix — *Make 'Id' a public constant*.** Offered when the member is there and only its modifiers are
wrong: a private, `internal`, `static readonly`, or otherwise non-constant-but-constant-valued `string`
field becomes `public const string` in one step. Both faults at once, deliberately — repairing the
accessibility of a `private static readonly` and stopping there would leave the warning on the member
just edited.

**Fix — *Declare 'public const string Id'*.** Offered when the member is absent, and it writes
`nameof(TheRuleType)`. That is the recommended form rather than a placeholder: it is read off the
declaration, and for a catalogue whose types are named after their rules it is already the right value.

Neither is offered when the value is the thing that is wrong — a `const int`, a blank string, an
initialiser that is not constant, or a property rather than a field. The code says nothing about what
the identifier should have been.

### `DCAT0004`

**No public `const string Category`.** Same rules as `Id`.

Its *value* should be the one the originating analyzer's `DiagnosticDescriptor` declares. Nothing in
the platform verifies that — which is exactly why the constant is worth having.

**Fix — *Make 'Category' a public constant*.** Exactly as for `Id`.

**Fix — *Declare 'public const string Category'*.** Writes the placeholder `"TODO"`. Read that word
literally: the category belongs to the analyzer this rule mirrors and the fix has no way to know it, so
it scaffolds the member and leaves the value to you.

> **What the placeholder costs.** `"TODO"` is a non-blank string, so `DCAT0004` stops being reported as
> soon as you apply the fix. What replaces it is `DCAT0011`: the placeholder is written as a literal, so
> the build now asks you to declare the category where your catalogue declares its categories. The
> unfinished work stays named — but note what neither rule can see, because Roslyn matches a suppression
> on its id alone: a category that is declared and simply *wrong* is invisible in every build, forever.
> Apply it when you are about to fill it in, not to make the list shorter.

### `DCAT0005`

**The identifier cannot be a type name, so the type is named the closest thing to it.**

```csharp
[DiagnosticRule]
public static class RULE_0001
{
    public const string Id = "RULE-0001";  // a hyphen is legal in a diagnostic id, not in a type name
}
```

**There is nothing to do here, and that is the whole message.** `RULE_0001` and `RULE0001` are both
faithful renderings of `"RULE-0001"`, and this library has no ground to elect one — so it asks for
neither, offers no fix, and stays at `Info`, out of your build output.

Why report it at all, then? Because [`DCAT0013`](#dcat0013) fails this same comparison one step later
and *is* a warning. `DCAT0005` is the exception being visible: it marks the declarations where the
divergence was imposed rather than chosen. An exception nobody can see, inside a rule that reports, is
the one shape a reader cannot reason about — and it leaves you no id to raise in `.editorconfig` if you
decide you want to know about these after all.

An identifier is read as far as its first colon, exactly as a suppression is
([`DCAT0006`](#dcat0006)). So the trimmer's friendly-name form lands here rather than under
`DCAT0013`, and a type named for the head of it is doing everything a name can:

```csharp
public static class IL2026Annotated
{
    public const string Id = "IL2026:Members annotated with RequiresUnreferencedCode";
}
```

### `DCAT0011`

**The category is not reached through a declared category constant.** `DCAT0004` asks whether the
member exists; this asks where its value comes from. It must resolve to a `const string` declared in a
class marked `[DiagnosticCategory]`:

```csharp
[DiagnosticCategory]
internal static class ContosoCategory
{
    public const string Usage = "Usage";
}

[DiagnosticRule]
public static class CT0001
{
    public const string Id = nameof(CT0001);
    public const string Category = ContosoCategory.Usage;   // ← not a literal
}
```

Nothing is broken when you write the literal instead. The rule compiles, folds to the same string in
metadata, and suppresses exactly what it should — which is why this ships as a `Warning` and not as an
error. What it costs is a **single spelling per category**: a catalogue repeats very few distinct
values across very many rules, and every transcription is a place for one of them to drift. It also
costs the marker, which is what lets tooling tell a category constant from any other string constant in
the assembly — without it, no tool can offer the named constant when replacing a literal.

Accepted alike are every spelling that binds to the same field: a qualified name, an aliased container,
a `using static`, a container declared in another assembly. Rejected are the forms that are constant
but not one reference — a literal, `nameof(...)`, two constants concatenated — because none of them
leaves the value with a single declaration to be its source.

**No fix is offered.** The repair is a class that may not exist yet, holding a constant nobody has
named; a fix that invented both would be guessing at the catalogue's vocabulary. The diagnostic names
the rule and you write the container.

**Reported on source only**, like every definition diagnostic — and here by construction rather than by
policy, since the check reads the initialiser and a rule reaching you through metadata has none.
### `DCAT0012`

**The identifier is a literal that happens to equal the type name.** Write `nameof` instead:

```csharp
public const string Id = "JD0007";        // reported
public const string Id = nameof(JD0007);  // held together
```

Nothing is wrong with the literal today — that is the point. It agrees with the type name *now*, and
nothing keeps it there. Rename the type and the literal stays behind: the declaration still compiles,
and every use site goes on naming a rule the type no longer is. `nameof` cannot come apart.

This is the one definition diagnostic that reads your **source** rather than your symbols.
`nameof(JD0007)` and `"JD0007"` compile to the same constant, so a rule reaching this analyzer from a
referenced assembly carries no trace of which was written — and nothing is reported there, because at
that point there is no longer a form to recommend.

Any `nameof` counts, qualified or not: `nameof(Vendor.JD0007)` is held together by the same operator.

**Fix — *Use `nameof`*.** Offered whenever `Id` is a field of its own. Declined when one field
declaration carries several constants — `public const string Id = "JD0007", Category = "Usage";` —
because rewriting a shared declaration touches a member this diagnostic never mentioned.

### `DCAT0013`

**The type is named something its identifier does not say.**

```csharp
[DiagnosticRule]
public static class RuleSeven
{
    public const string Id = "JD0007";  // reported
}
```

`JD0007` is a perfectly legal type name. It was available, and the type is called something else, so
every use site reads `Vendor.RuleSeven.Id` and suppresses `JD0007`. The reference compiles, resolves,
works — and tells its reader nothing true. That is a worse failure than a broken rule, which at least
announces itself.

It is reported whenever the name does not say the identifier and nothing forced that. Both of these are
reported, for the same reason:

```csharp
public static class RULE001 { public const string Id = "RULE_001"; }   // RULE_001 was available
public static class RULE42  { public const string Id = "RULE-0001"; }  // not a legalisation of it
```

The second is the one worth knowing about. `"RULE-0001"` cannot be a type name at all — but `RULE42` is
not a rendering of it either, and being unable to spell the identifier exactly does not license spelling
something else.

**No fix.** Two repairs exist and only you can choose: renaming the type changes a name your consumers
have written down, and rewriting the identifier changes which diagnostic is suppressed. A tool that
picked one would be deciding which of them was the typo.

---

## Configuring them

Standard Roslyn mechanisms, no proprietary format:

```ini
# .editorconfig
[*.cs]

# A suppression the trimmer discards. Not an error by default only because
# DCAT0009 still misses an identifier reached through a constant.
dotnet_diagnostic.DCAT0009.severity = error

# A suppression that never says why. Shipped as a warning because it reports
# lines that are otherwise correct; raise it once yours all carry a reason.
dotnet_diagnostic.DCAT0014.severity = error

# Declaring rules — you only need these if you publish a catalogue.
dotnet_diagnostic.DCAT0002.severity = error
dotnet_diagnostic.DCAT0003.severity = error
dotnet_diagnostic.DCAT0004.severity = error
dotnet_diagnostic.DCAT0011.severity = error
dotnet_diagnostic.DCAT0012.severity = error
dotnet_diagnostic.DCAT0013.severity = error

# A name that could not have said its id. Raise it if you would rather review
# every such declaration than let it pass.
dotnet_diagnostic.DCAT0005.severity = warning

# Migrating an existing codebase: keep it visible in the IDE, out of the build.
# Delete the line when the last literal is gone.
dotnet_diagnostic.DCAT0006.severity = suggestion
```

`DCAT0001`, `DCAT0006` and `DCAT0007` are already errors, so nothing above raises them —
the only one of the three worth touching is the last, and only while migrating
([ADR-0027](../adr/0027-ship-the-use-site-diagnostics-as-errors.en.md)).

The category is `DiagnosticCatalog`, so you can also set them all at once:

```ini
dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = error
```

Scope a section to a path in the ordinary `.editorconfig` way when generated code or a legacy folder
needs different treatment.

That same key set to `none` is how you turn the whole set **off**. Since the analyzers ship inside
`DiagnosticCatalog`, there is no package reference left to decline: a project that wants the markers
and none of the checking says so here rather than in its dependencies
([ADR-0037](../adr/0037-ship-the-analyzers-inside-the-foundation-package.en.md)).

## What is deliberately not checked

The analyzers verify that a suppression is **structurally coherent** — that it names one real rule,
coherently. They do not, and will not:

* validate an arbitrary string. `[SuppressMessage("Usage", "S1144")]` with a wrong category matches
  no known rule and is reported by nothing. What makes a wrong category impossible is the
  *constant*, which the compiler checks — these diagnostics get you to the constants and keep you
  there;
* judge whether suppressing a rule *there* was reasonable. `DCAT0014` requires that a
  `Justification` be written, and reads it for its length alone — what it says is weighed by people,
  never by these analyzers;
* reach `#pragma warning disable` or `.editorconfig` severity keys, which take bare text outside the
  C# compilation model. No constant can ever be substituted into either.

---

<div align="center">
<a href="./adopting-a-catalogue.en.md">← Adopting a catalogue on an existing codebase</a> · <a href="./README.en.md">↑ Table of contents</a>
</div>
