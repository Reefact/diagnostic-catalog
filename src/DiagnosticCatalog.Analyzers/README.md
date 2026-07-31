# DiagnosticCatalog.Analyzers

Roslyn analyzers for [DiagnosticCatalog](https://github.com/Reefact/diagnostic-catalog).

They check two things: that a rule **declaration** satisfies the structural contract, and that a
**suppression** referencing one is coherent — a category and an id taken from two different rules, a
half-migrated suppression mixing a reference with a literal, a literal that a catalogue reference
would replace.

## Referencing it

Analysis assemblies must never become runtime dependencies, so reference it privately:

```xml
<PackageReference Include="DiagnosticCatalog.Analyzers" Version="0.1.0" PrivateAssets="all" />
```

A catalogue package may bring these analyzers to its own consumers instead, so that referencing the
catalogue is enough. That is a decision the catalogue makes, not a default of this package.

## What it does not do

It does not validate an arbitrary string. `[SuppressMessage("Usage", "S1144")]` with the wrong
category matches no known rule, and nothing is reported — the mechanism that makes a wrong category
impossible is the constant itself, which the compiler checks. These analyzers get you to the
constants and keep you there.

## Licence

Apache-2.0. Unofficial; not affiliated with any analyzer vendor.
