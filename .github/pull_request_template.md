<!--
  Please write this PR in ENGLISH: title, summary, changes, testing notes, and related issue references.

  Title: name the whole change in English. A single-intention PR mirrors its commit
  header (type(scope): description); a multi-intention PR uses a short descriptive
  title. Issue links go in "Related issues" below, not the title.
  See CONTRIBUTING.md -> "Pull request titles".

  Fill in the applicable sections below.
  Do not invent information.
  Only check testing items that were actually run.
  Delete a section only if it truly does not apply.
-->

## Summary

<!-- One or two sentences: what does this PR change, and why? -->

## Type of change

* [ ] Bug fix
* [ ] New feature
* [ ] Breaking change
* [ ] Refactoring
* [ ] Rule catalog change (a vendor's rules added, removed, or re-described)
* [ ] Tests
* [ ] Documentation
* [ ] Build / CI / tooling

## Release train

<!-- Trains version independently and are selected by each commit's scope.
     See CONTRIBUTING.md -> "Scope". Tick every train this PR moves. -->

* [ ] `lib` — scopes `core`, `analyzers`, `cli`, `testing`
* [ ] `sonar`
* [ ] `netanalyzers`
* [ ] `stylecop`
* [ ] None — this PR carries no `feat` or `fix` (infrastructure, docs, chore)

## Changes

<!-- Bullet list of the concrete changes made in this PR. Keep it factual. -->

*

## Testing

<!-- Check only the commands/tests that were actually run. Add details if something was not run. -->

* [ ] `dotnet build -c Release`
* [ ] `dotnet test -c Release`
* [ ] .NET Framework 4.7.2 floor exercised (`-p:EnableNet472Floor=true -f net472`, Windows only)

## Compatibility

<!-- A rule identifier or a catalog entry key that a consumer references symbolically
     is part of the published contract. Renaming or removing one is a breaking change. -->

* [ ] No rule identifier or catalog entry key was renamed or removed
* [ ] Identifiers changed — the commit carries `!` and a `BREAKING CHANGE:` footer
* [ ] `netstandard2.0` compatibility preserved

## Documentation

* [ ] README / documentation updated
* [ ] No documentation change required

## Architecture decisions

<!-- Every pull request is checked against the ADR base (doc/adr/). Most embark no
     architectural decision — tick the first box. Agents draft ADRs as `Proposed`;
     the maintainer accepts or supersedes. See doc/adr/README.md. -->

* [ ] No architectural decision in this pull request
* [ ] New decision recorded — ADR drafted as `Proposed`: ADR-____
* [ ] Supersedes an existing ADR — successor proposed, status not flipped: ADR-____
* [ ] ⚠️ Conflicts with an existing ADR — flagged for the maintainer: ADR-____

## Related issues

<!-- e.g. Closes #123 -->
