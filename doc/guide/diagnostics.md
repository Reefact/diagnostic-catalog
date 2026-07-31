# The `DCAT` diagnostics

Every diagnostic `DiagnosticCatalog.Analyzers` reports: what triggers it, why it exists, and how to
configure it.

They fall into two groups. **Definition** diagnostics look at a rule you declared; you only see them
if you write a catalogue. **Use-site** diagnostics look at a suppression you wrote, which is most
people.

| Id | Looks at | Title | Default | Fix |
| --- | --- | --- | --- | --- |
| [`DCAT0001`](#dcat0001) | use site | Category and Id must reference the same diagnostic rule | Warning | two, unranked |
| [`DCAT0002`](#dcat0002) | definition | A diagnostic rule must be declared as a static non-generic class | Warning | yes, conditionally |
| [`DCAT0003`](#dcat0003) | definition | A diagnostic rule must expose a public constant string named `Id` | Warning | yes, conditionally |
| [`DCAT0004`](#dcat0004) | definition | A diagnostic rule must expose a public constant string named `Category` | Warning | yes, conditionally |
| [`DCAT0006`](#dcat0006) | use site | Use a diagnostic catalog reference instead of string literals | Warning | yes |
| [`DCAT0007`](#dcat0007) | use site | Suppression mixes a catalog reference with a string literal | Warning | yes, conditionally |
| [`DCAT0009`](#dcat0009) | use site | `UnconditionalSuppressMessage` only accepts `IL####` identifiers | Warning | — |

`DCAT0005`, `DCAT0008` and `DCAT0010` are specified but deliberately not in 1.0.

---

## Use-site diagnostics

### `DCAT0001`

**The category and the identifier come from two different rules.**

```csharp
[SuppressMessage(SonarRules.S1144.Category, SonarRules.S2094.Id)]
//               ^^^^^ from S1144          ^^^^^ from S2094
```

Copy-paste, nearly always: you duplicated a working suppression and changed one half.

It is reported **even when the two rules share a category**, and that case is the one worth
understanding. The line compiles to exactly the same thing a correct suppression would, and works
perfectly — until the vendor recategorises either rule, at which point it silently carries the wrong
category with nothing in the platform to say so. A check that compared values instead of rules would
miss precisely this.

**Two fixes, neither recommended.** Only you know which half was the typo:

```text
Use SonarRules.S1144.Id        — keep the category, correct the identifier
Use SonarRules.S2094.Category  — keep the identifier, correct the category
```

Worth knowing while you choose: Roslyn matches a suppression on the **identifier alone** and never
consults the category. So correcting the category leaves what is suppressed exactly as it is, while
correcting the identifier changes it.

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

> **On adoption.** This fires on every literal suppression at once, the day you add a catalogue.
> Under `TreatWarningsAsErrors` that fails the build immediately. Lower it to `suggestion`, migrate
> with *Fix all occurrences*, then raise it.

### `DCAT0007`

**One half migrated, one half still a literal.**

```csharp
[SuppressMessage(SonarRules.S1144.Category, "S1144")]
```

The most common half-done state, and the only one where the intended rule is known without
ambiguity — the migrated argument names it. Completed from that rule, rewriting only the literal:
whatever spelling you chose for the other side, an alias included, is left alone.

**Unless the literal names something else.** `"S9999"` beside `SonarRules.S1144.Category` gets the
diagnostic and **no** fix, because completing it would silence a different rule than the one
silenced today — and let the original warning back in. That is a decision, not a migration.

### `DCAT0009`

**A non-`IL` rule used in `UnconditionalSuppressMessage`.**

```csharp
[UnconditionalSuppressMessage(SonarRules.S1144.Category, SonarRules.S1144.Id)]
```

That attribute is read by the trimmer, from your compiled assembly, long after the compiler has
finished. Its decoder accepts only identifiers shaped like `IL####` and **discards everything else
outright**. Roslyn does not process the attribute either. So this suppression is a no-op that no
other tool in the toolchain reports.

The check mirrors the trimmer's decoder rather than a stricter pattern, so identifiers it *does*
honour are left alone — including its own `IL2026:FriendlyName` form. Reporting those would be
telling you to change something that works.

---

## Definition diagnostics

These fire on code that declares rules. See [the catalogue author's guide](catalogue-authors.md).

All three offer a fix **when the repair is written in the code already**, and stay silent about it
otherwise. That line is not caution for its own sake: a fix that guessed would produce a rule the
compiler accepts and nobody checks, which is the failure this library exists to remove. Where a fix is
refused below, the diagnostic still names the type and the member — you finish it with what you know
and the tool does not.

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
> soon as you apply the fix. You have traded a warning that named the problem for a marker only a reader
> will notice — and a wrong category is invisible in every build, forever, because Roslyn matches a
> suppression on its id alone. Apply it when you are about to fill it in, not to make the list shorter.

---

## Configuring them

Standard Roslyn mechanisms, no proprietary format:

```ini
# .editorconfig
[*.cs]

# A suppression that names two rules is not doing what it looks like.
dotnet_diagnostic.DCAT0001.severity = error
dotnet_diagnostic.DCAT0007.severity = error

# A suppression the trimmer discards.
dotnet_diagnostic.DCAT0009.severity = error

# Migrating gradually: keep it visible in the IDE, out of the build.
dotnet_diagnostic.DCAT0006.severity = suggestion

# Declaring rules — you only need these if you publish a catalogue.
dotnet_diagnostic.DCAT0002.severity = error
dotnet_diagnostic.DCAT0003.severity = error
dotnet_diagnostic.DCAT0004.severity = error
```

The category is `DiagnosticCatalog`, so you can also set them all at once:

```ini
dotnet_analyzer_diagnostic.category-DiagnosticCatalog.severity = error
```

Scope a section to a path in the ordinary `.editorconfig` way when generated code or a legacy folder
needs different treatment.

## What is deliberately not checked

The analyzers verify that a suppression is **structurally coherent** — that it names one real rule,
coherently. They do not, and will not:

* validate an arbitrary string. `[SuppressMessage("Usage", "S1144")]` with a wrong category matches
  no known rule and is reported by nothing. What makes a wrong category impossible is the
  *constant*, which the compiler checks — these diagnostics get you to the constants and keep you
  there;
* judge whether suppressing a rule *there* was reasonable. That is what `Justification` is for, and
  it stays a human question;
* reach `#pragma warning disable` or `.editorconfig` severity keys, which take bare text outside the
  C# compilation model. No constant can ever be substituted into either.
