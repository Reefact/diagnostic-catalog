# Guides

Three, by what you are trying to do.

| If you… | Read |
| --- | --- |
| write `[SuppressMessage(...)]` and want the compiler to check it | [**Writing suppressions that the compiler checks**](consumers.md) |
| ship an analyzer, or want your team's own rules referenced this way | [**Publishing a catalogue**](catalogue-authors.md) |
| saw a `DCATxxxx` and want to know what it means | [**The `DCAT` diagnostics**](diagnostics.md) |

The worked example is [`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self): this
library's own `DCAT` rules, catalogued by this library's own generator, published on the same train
as the analyzers they mirror. It is not a mock-up — it is the product applied to itself, and CI
fails if it ever stops describing the analyzers that ship beside it.

The three vendor catalogues under `src/` are the same machinery at scale — 465, 318 and 193 rules —
mirroring other people's analyzers.

For the reasoning behind any of it, [the specification](../specification.en.md) is the canonical
document, and [the ADRs](../adr/) record the decisions that outlived their implementation.
