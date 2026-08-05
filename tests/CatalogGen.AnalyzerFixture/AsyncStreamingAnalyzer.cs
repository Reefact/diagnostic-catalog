using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CatalogGen.AnalyzerFixture;

/// <summary>
/// An analyzer that declares one rule and needs nothing unusual to be constructed.
/// </summary>
/// <remarks>
/// Its job is to be READABLE while the assembly around it is not entirely loadable, because that is
/// the shape the defect actually takes upstream: the types that fail to load are code fixes and
/// internal helpers, never the analyzers, so the descriptors arrive complete and the run is refused
/// anyway. A fixture whose analyzer also failed would prove a different, easier thing.
/// </remarks>
// RS1001 wants [DiagnosticAnalyzer] on a DiagnosticAnalyzer subclass, which is right for one
// somebody ships and beside the point here: the reader selects on the base type alone, and
// declaring the attribute would mark this project as a compiler extension for rules that then fire
// on facts about a fixture. Same trade DescriptorReaderTests makes for the same reason.
#pragma warning disable RS1001 // Missing 'DiagnosticAnalyzerAttribute' attribute
public sealed class AsyncStreamingAnalyzer : DiagnosticAnalyzer
#pragma warning restore RS1001
{
    /// <summary>The one rule this fixture declares, and the one the test asserts arrived.</summary>
    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        id: "FIX0001",
        title: "Fixture rule",
        messageFormat: "Fixture rule",
        category: "Fixture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    // Never registered with a compilation and never run: the assembly is loaded reflectively and
    // asked what it declares. Written out rather than left empty because RS1025 and RS1026 are
    // static checks on the override.
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}

/// <summary>
/// A type this assembly cannot load unless the host supplies Microsoft.Bcl.AsyncInterfaces.
/// </summary>
/// <remarks>
/// <para>
/// It IMPLEMENTS the interface rather than merely mentioning it, and the distinction is the test.
/// A field or a parameter of an unresolvable type is resolved lazily, when something touches it —
/// which reflection over an assembly never does. An interface is resolved when the type itself is
/// materialised, because the CLR builds the interface map at that moment. So this is what turns
/// <c>Assembly.GetTypes()</c> into a <c>ReflectionTypeLoadException</c>, which is precisely what
/// the worker meets on Microsoft.CodeAnalysis.CSharp.CodeStyle, Meziantou.Analyzer and
/// Microsoft.CodeAnalysis.PublicApiAnalyzers.
/// </para>
/// <para>
/// <c>ValueTask</c> comes from System.Threading.Tasks.Extensions, and it is deliberately left out of
/// the story: the shared framework ships that facade, so it resolves in the worker with or without
/// the fix. Microsoft.Bcl.AsyncInterfaces is the one compat facade it does not ship, which is why
/// this fixture isolates that identity and no other.
/// </para>
/// </remarks>
public sealed class AsyncRuleStream : IAsyncEnumerable<string>
{
    /// <inheritdoc />
    public IAsyncEnumerator<string> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new Enumerator();

    private sealed class Enumerator : IAsyncEnumerator<string>
    {
        public string Current => string.Empty;

        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(false);

        public ValueTask DisposeAsync() => default;
    }
}
