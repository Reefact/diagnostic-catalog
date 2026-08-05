using System.Collections.Immutable;

using CatalogGen.AbsentContract;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CatalogGen.PartialLoadFixture;

/// <summary>
/// An analyzer that loads and constructs normally, in an assembly that does not load entirely.
/// </summary>
/// <remarks>
/// It is the whole assertion: its rule has to arrive even though the type below never will. What
/// makes that safe to claim is the attribute — the compiler discovers analyzers by it and loads
/// nothing else, so an analyzer without one reports no diagnostic in any build and a catalogue that
/// listed it would be describing rules nobody can ever receive.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PartialLoadAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The rule this fixture declares, and the one the test asserts arrived.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: "FIX0002",
        title: "Partial load fixture rule",
        messageFormat: "Partial load fixture rule",
        category: "Fixture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}

/// <summary>
/// A type that cannot be materialised wherever CatalogGen.AbsentContract is not present, which is
/// everywhere this fixture is read from.
/// </summary>
/// <remarks>
/// It stands for what an analyzer package is mostly made of — code fixes, internal helpers, types
/// the compiler never loads because no attribute points at them. Nothing in a catalogue is derived
/// from any of them, so failing to load one costs the read nothing; what it used to cost was the
/// whole run, because a reader that materialises every type cannot tell which of them mattered.
/// </remarks>
public sealed class UnloadableHelper : IFixtureContract
{
}
