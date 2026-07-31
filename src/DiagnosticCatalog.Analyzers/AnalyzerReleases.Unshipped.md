; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------
DCAT0001 | DiagnosticCatalog | Warning | Category and Id must reference the same diagnostic rule
DCAT0002 | DiagnosticCatalog | Warning | A diagnostic rule must be declared as a static non-generic class
DCAT0003 | DiagnosticCatalog | Warning | A diagnostic rule must expose a public constant string named Id
DCAT0004 | DiagnosticCatalog | Warning | A diagnostic rule must expose a public constant string named Category
DCAT0006 | DiagnosticCatalog | Warning | Use a diagnostic catalog reference instead of string literals
DCAT0007 | DiagnosticCatalog | Warning | Suppression mixes a catalog reference with a string literal
DCAT0009 | DiagnosticCatalog | Warning | UnconditionalSuppressMessage only accepts IL#### identifiers
