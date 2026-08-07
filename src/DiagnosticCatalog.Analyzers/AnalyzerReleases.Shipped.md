; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------
DCAT0001 | DiagnosticCatalog | Error | Category and Id must reference the same diagnostic rule
DCAT0002 | DiagnosticCatalog | Error | A diagnostic rule must be declared as a static non-generic class
DCAT0003 | DiagnosticCatalog | Error | A diagnostic rule must expose a public constant string named Id
DCAT0004 | DiagnosticCatalog | Error | A diagnostic rule must expose a public constant string named Category
DCAT0005 | DiagnosticCatalog | Info | The diagnostic rule type name should match its Id
DCAT0006 | DiagnosticCatalog | Error | Use a diagnostic catalog reference instead of string literals
DCAT0007 | DiagnosticCatalog | Error | Suppression mixes a catalog reference with a string literal
DCAT0009 | DiagnosticCatalog | Error | UnconditionalSuppressMessage only accepts IL#### identifiers
DCAT0011 | DiagnosticCatalog | Warning | A diagnostic rule's category must reference a declared category constant
DCAT0012 | DiagnosticCatalog | Warning | A rule identifier should be written as nameof
DCAT0013 | DiagnosticCatalog | Warning | The diagnostic rule type name does not say its Id
DCAT0014 | DiagnosticCatalog | Error | A suppression must carry a justification
DCAT0015 | DiagnosticCatalog | Error | A catalogue package must ship the analyzer opt-in
