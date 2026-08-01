namespace DiagnosticCatalog.Usage;

using System.Diagnostics.CodeAnalysis;
using DiagnosticCatalog.Sonar;

/// <summary>The plainest use there is: one suppression, naming one catalogue rule.</summary>
internal static class Plainest
{
    [SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "Reflected over.")]
    internal static int Unused() => 42;
}
