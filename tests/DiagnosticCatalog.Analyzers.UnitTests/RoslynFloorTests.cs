using System;
using System.Linq;
using System.Reflection;

using Xunit;

namespace DiagnosticCatalog.Analyzers.UnitTests;

/// <summary>
/// Enforces the analyzer's Roslyn load contract.
/// </summary>
/// <remarks>
/// The analyzer is loaded by each consumer's host compiler, so the Microsoft.CodeAnalysis version it is
/// COMPILED against is the minimum Roslyn able to load it. Build against a higher one and it fails to
/// load with CS8032 on every older SDK and IDE — reporting nothing at all, which reads as a codebase
/// with no problems.
///
/// Nothing in the build enforces this on its own: the central pin is newer, and only the csproj's
/// VersionOverride holds the line. A merge that dropped the override, or a package brought in
/// transitively at a newer version, would raise the floor with no visible symptom. This test is what
/// makes that a red build.
///
/// The floor is declared once in Directory.Build.props and surfaced as assembly metadata, so this test
/// and the csproj pin read the same value and cannot drift apart.
/// </remarks>
public sealed class RoslynFloorTests
{
    [Fact]
    public void The_analyzer_stays_on_the_supported_Roslyn_floor()
    {
        Assembly analyzer = typeof(DiagnosticRuleDefinitionAnalyzer).Assembly;
        Version floor = ReadFloor(analyzer);

        // The whole Microsoft.CodeAnalysis family is bounded, not one assembly by name. Only the
        // references a compilation actually USES are recorded, so an analyzer written against the
        // language-agnostic API may carry no reference to Microsoft.CodeAnalysis.CSharp at all —
        // and a test naming that one assembly would then pass by finding nothing.
        AssemblyName[] roslyn = analyzer
            .GetReferencedAssemblies()
            .Where(reference => reference.Name is not null
                                && reference.Name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
            .ToArray();

        // If the family ever disappears from the metadata, this test proves nothing. Fail loudly
        // rather than pass vacuously.
        Assert.NotEmpty(roslyn);

        string[] tooNew = roslyn
            .Where(reference => OnMajorMinorBuild(reference.Version) > floor)
            .Select(reference => reference.Name + " " + reference.Version)
            .ToArray();

        Assert.True(tooNew.Length == 0, "compiled against Roslyn newer than the floor: " + string.Join(", ", tooNew));
    }

    private static Version ReadFloor(Assembly analyzer)
    {
        AssemblyMetadataAttribute floor = analyzer
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(metadata => metadata.Key == "RoslynFloorVersion");

        return OnMajorMinorBuild(Version.Parse(floor.Value!));
    }

    // Roslyn assemblies carry a four-part version (x.y.z.0) while the floor is written x.y.z. Comparing
    // on the first three keeps 4.8.0.0 from reading as newer than a 4.8.0 floor.
    private static Version OnMajorMinorBuild(Version? version) =>
        new(version?.Major ?? 0, version?.Minor ?? 0, version is { Build: >= 0 } ? version.Build : 0);
}
