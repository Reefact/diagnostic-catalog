; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------
DCAT0002 | DiagnosticCatalog | Warning | A diagnostic rule must be declared as a static non-generic class
DCAT0003 | DiagnosticCatalog | Warning | A diagnostic rule must expose a public constant string named Id
DCAT0004 | DiagnosticCatalog | Warning | A diagnostic rule must expose a public constant string named Category
