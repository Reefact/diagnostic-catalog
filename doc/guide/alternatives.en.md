# The alternatives

🌍 **Languages:**  
🇬🇧 English (this file) | 🇫🇷 [Français](./alternatives.fr.md)

For anyone comparing before adopting. Five other ways to solve the same problem, each with what it
actually buys — including doing nothing, which is a legitimate answer.

The problem, restated: `[SuppressMessage("Major Code Smell", "S1144")]` takes two strings that
nothing validates, and they fail differently — a wrong id eventually surfaces, a wrong category never
does. [Why magic strings fail](the-problem.en.md) is the long version.

## At a glance

| Approach | Wrong id caught | Wrong category caught | Rename follows | Find references | Retirement surfaces |
| --- | --- | --- | --- | --- | --- |
| Literals, as written today | no | no | no | text search | no |
| A constants file you maintain | at compile time | no — the value is still yours | yes | yes | no |
| `GlobalSuppressions.cs` | no | no | no | text search | no |
| `#pragma warning disable` | no | n/a — takes no category | no | text search | no |
| A grep before each upgrade | manually | no | no | n/a | manually |
| **A generated catalogue** | at compile time | **at generation time, from the descriptor** | yes | yes | `CS0618`, naming the release |

The column that separates them is the second one, and it is the column no approach on this list
except the last can fill. A category is a string that only the vendor publishes and that nothing in
the platform reads; the only way to be right about it is to read it from the thing that declares it.

## A constants file you maintain

The obvious move, and a good one as far as it goes:

```csharp
internal static class Rules
{
    public const string S1144Id = "S1144";
    public const string S1144Category = "Major Code Smell";
}
```

**What it buys.** Everything the compiler can give you: a typo is `CS0117`, rename works, *Find All
References* works. If your codebase has thirty suppressions over five rules, this is genuinely
enough, and you should probably write it rather than take a dependency.

**Where it stops.** The values are still yours. `"Major Code Smell"` was typed by someone, from a
source that was a snapshot — a blog post, an IDE's *Suppress → In Source*, another file. Being
consistent about a wrong value is not the same as being right; it is one wrong value in one place
instead of forty, which is better and is not the same thing.

It also does not know when a rule is retired. Upstream drops `S1144`, your constant stays, your
suppression keeps compiling and stops meaning anything.

**Where the line is.** Roughly: when you can no longer say, from memory, where each category value
came from. A generated catalogue is this file with the values read from the analyzer's own
`DiagnosticDescriptor` and regenerated when the vendor moves — which is
[ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.en.md), and the reason `dcat`
exists.

## `GlobalSuppressions.cs`

Moving suppressions out of the code and into one file:

```csharp
[assembly: SuppressMessage("Major Code Smell", "S1144", Scope = "member", Target = "~M:Contoso.Orders.Rebuild")]
```

**What it buys.** They are all in one place, so they can be read, counted and reviewed as a set —
which is a real benefit and orthogonal to everything on this page.

**Where it stops.** It changes *where* the strings live, not *what* they are. Both arguments are as
unchecked as before, and a third string joins them: `Target`, a documentation-comment id that will
not survive a rename either.

**These compose.** A `GlobalSuppressions.cs` written against catalogue constants is strictly better
than one written against literals, and this library has no opinion about which file your suppressions
live in.

## `#pragma warning disable`

```csharp
#pragma warning disable S1144
```

**What it buys.** Brevity, and reach: it works on statements and regions, where an attribute needs a
declaration to hang on.

**Where it stops.** The directive takes a bare identifier token, not an expression, so there is no
position where a constant could be substituted. This is **permanently out of reach** — a grammar
fact, not a missing feature. It also takes no category at all, so half this page's problem does not
apply to it.

If your codebase suppresses mostly this way, see
[when not to use this](when-not-to-use.en.md#you-suppress-with-pragma-not-with-attributes).

## A grep before each upgrade

The discipline answer: before bumping an analyzer package, search the codebase for every id you
suppress and check it against the release notes.

**What it buys.** Retirements, if the release notes list them and somebody actually runs it. On a
small codebase with a careful maintainer, this works.

**Where it stops.** It is manual, so it is done when someone remembers, which is not the upgrade
where it matters. It catches nothing about categories — the release notes do not list a
recategorisation as a breaking change, because for the vendor it is not one. And it scales with the
number of repositories, which is where teams give up.

The mechanised version of this is `dcat validate`, which computes the catalogue the current upstream
release would produce and compares it to what you have — exiting `2` when they differ, and `1` when
it could not tell, on purpose, so a feed outage is never reported as a drifted contract.

## Doing nothing

Worth listing, because it is the right answer more often than a library's documentation usually
admits.

**What it buys.** No dependency, no adoption cost, no migration, nothing new for a reviewer to learn.

**Where it stops.** Exactly where the number of suppressions makes one of them being quietly wrong a
cost you would pay. That threshold is a judgement, and
[when not to use this](when-not-to-use.en.md) is written to help you make it against yourself rather
than for the library.

## What this library is not competing with

Two things are sometimes offered as alternatives and solve different problems:

* **An analyzer that validates suppression strings.** This library ships one — `DCAT0006` and
  friends — and it is deliberately the smaller half. A check on a string can only judge strings it
  recognises, so `[SuppressMessage("Usage", "S1144")]` is reported by nothing: it may be a wrong
  category, or a rule from an analyzer you have not catalogued, and nothing can tell. The constant is
  what removes the ambiguity, not the check.
* **A tool that removes unnecessary suppressions.** `IDE0079` already does this, and it answers a
  different question — "is this suppression still needed?" rather than "does this suppression name
  what it claims to?". Run both.

## Where to go next

* [**Writing suppressions that the compiler checks**](writing-suppressions.en.md) — if the answer is
  yes, this is the practical guide.
* [**When not to use this**](when-not-to-use.en.md) — if the answer is not yet.

---

<div align="center">
<a href="./when-not-to-use.en.md">← When not to use this</a> · <a href="./README.en.md">↑ Table of contents</a> · <a href="./writing-suppressions.en.md">Writing suppressions that the compiler checks →</a>
</div>
