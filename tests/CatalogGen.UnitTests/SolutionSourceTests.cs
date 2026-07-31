using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// Acquisition from a solution: the projects in it that <em>declare</em> they produce rules.
/// </summary>
/// <remarks>
/// <para>
/// The distinction from guessing is the whole feature, and it is what these assert. Measured on this
/// repository, "references Microsoft.CodeAnalysis" matches six projects of which one is an analyzer,
/// and "declares a DiagnosticAnalyzer subclass" matches two of which one is a fixture written to
/// fail construction. Either heuristic reads the wrong set, and reading a set short of a project
/// emits a catalogue whose missing rules are published as <c>[Obsolete]</c> against a vendor that
/// still declares them — with nothing anywhere to report it.
/// </para>
/// <para>
/// So a project joins by saying so, in its own file, exactly as it joins a release train by
/// declaring <c>ReleaseTrain</c> and never by appearing in a list kept somewhere else.
/// </para>
/// </remarks>
public sealed class SolutionSourceTests : IDisposable
{
    private readonly DirectoryInfo _work = Directory.CreateTempSubdirectory("solutionsource");

    public void Dispose() => _work.Delete(recursive: true);

    [Fact]
    public void A_solution_that_is_not_there_is_refused()
        => Assert.Null(SolutionSource.Acquire(Path.Combine(_work.FullName, "absent.slnx"), "Release", null, null));

    [Fact]
    public void A_solution_whose_projects_declare_nothing_is_refused_rather_than_read_as_empty()
    {
        // The silent-success trap: finding no project, generating nothing and exiting zero would
        // read to a scheduled job exactly like a catalogue that was current. The manifest schema
        // refuses an empty `catalogs` array for the same reason.
        //
        // Asserted on the MESSAGE, not only on the refusal. An empty set refuses either way — it
        // reaches ProjectSource, which declines "no project given" — so a test that only checked for
        // null would pass against a tool that had lost the one thing this guard adds: telling the
        // reader that the property exists and that nobody in their solution declared it.
        string solution = Solution(("Quiet", false));

        string said = Refusal(() => SolutionSource.Acquire(solution, "Release", null, null));

        Assert.Contains(SolutionSource.Marker, said, StringComparison.Ordinal);
        Assert.Contains("no project in", said, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_the_projects_that_declare_it_are_read()
    {
        // Two projects, one declaring. The other is built and perfectly readable — what excludes it
        // is its own silence, not anything this tool inferred about it.
        string solution = Solution(("Rules", true), ("Silent", false));
        string expected = Built("Rules");
        Built("Silent");

        AnalyzerAssemblySet? acquired = SolutionSource.Acquire(solution, "Release", null, null);

        Assert.NotNull(acquired);
        Assert.Equal([expected], acquired.AssemblyPaths);
    }

    [Fact]
    public void Several_declaring_projects_are_read_together()
    {
        string solution = Solution(("Rules", true), ("MoreRules", true), ("Silent", false));
        string first = Built("Rules");
        string second = Built("MoreRules");
        Built("Silent");

        AnalyzerAssemblySet? acquired = SolutionSource.Acquire(solution, "Release", null, null);

        Assert.NotNull(acquired);
        Assert.Equal([second, first], acquired.AssemblyPaths.OrderBy(p => p, StringComparer.Ordinal));
    }

    [Fact]
    public void A_declaring_project_that_is_not_built_refuses_the_whole_run()
    {
        // It declared itself, so its rules belong in the catalogue; emitting one without them is the
        // shortfall every acquisition here refuses. Delegated to ProjectSource, which says which
        // project and which command would build it.
        string solution = Solution(("Rules", true));

        Assert.Null(SolutionSource.Acquire(solution, "Release", null, null));
    }

    [Fact]
    public void Declaring_false_is_not_declaring()
    {
        string solution = Solution(("Explicit", false));
        Built("Explicit");

        Assert.Null(SolutionSource.Acquire(solution, "Release", null, null));
    }

    [Fact]
    public void The_property_is_read_by_evaluation_so_an_unrestored_solution_still_answers()
    {
        // Nothing here restores or builds, which is what keeps `validate` safe against a working
        // copy. The fixture is never restored: no obj/ exists for it at any point.
        string solution = Solution(("Rules", true));
        Built("Rules");

        Assert.NotNull(SolutionSource.Acquire(solution, "Release", null, null));
        Assert.False(Directory.Exists(Path.Combine(_work.FullName, "Rules", "obj")),
                     "evaluating a project must not restore it");
    }

    [Fact]
    public void The_declaration_is_not_inferred_from_the_analyzer_authoring_switch()
    {
        // EnforceExtendedAnalyzerRules looks like the same statement and is not: it says "check this
        // project by the analyzer-authoring rules". Measured on this repository it is true of
        // DiagnosticCatalog.CodeFixes, which declares no DiagnosticDescriptor at all. What a project
        // is checked by and what it produces are different facts.
        string solution = Solution(("Enforcing", false, "<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>"));
        Built("Enforcing");

        Assert.Null(SolutionSource.Acquire(solution, "Release", null, null));
    }

    // A real solution, built by the SDK rather than written out here: .sln and .slnx are different
    // formats and both are the SDK's to produce, exactly as enumerating them is the SDK's to do.
    private string Solution(params (string Name, bool Declares, string Extra)[] projects)
    {
        foreach ((string name, bool declares, string extra) in projects)
        {
            DirectoryInfo folder = Directory.CreateDirectory(Path.Combine(_work.FullName, name));
            File.WriteAllText(Path.Combine(folder.FullName, name + ".csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <AssemblyName>{name}</AssemblyName>
                    <Version>1.0.0</Version>
                    {(declares ? "<ProducesDiagnosticRules>true</ProducesDiagnosticRules>" : "")}
                    {extra}
                  </PropertyGroup>
                </Project>
                """);
        }

        Sdk("new", "sln", "-n", "Fixture", "-o", ".");
        foreach ((string name, _, _) in projects) Sdk("sln", "add", Path.Combine(name, name + ".csproj"));

        return Directory.EnumerateFiles(_work.FullName, "Fixture.sln*").Single();
    }

    private string Solution(params (string Name, bool Declares)[] projects)
        => Solution([.. projects.Select(p => (p.Name, p.Declares, ""))]);

    // Puts a real assembly where MSBuild says the output goes, without running a build: what is under
    // test is which projects are selected, not whether the SDK can compile an empty one.
    private string Built(string name)
    {
        string directory = Path.Combine(_work.FullName, name, "bin", "Release", "net8.0");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + ".dll");
        File.Copy(Assembly.GetExecutingAssembly().Location, path, overwrite: true);

        return path;
    }

    // The refusal is the feature here, so it is read rather than inferred from a null. Console.Error
    // is process-global; this assembly disables test parallelisation for exactly that reason
    // (Parallelism.cs), so swapping it here is safe.
    private static string Refusal(Func<AnalyzerAssemblySet?> action)
    {
        TextWriter original = Console.Error;
        using StringWriter captured = new();
        Console.SetError(captured);
        try
        {
            Assert.Null(action());
        }
        finally
        {
            Console.SetError(original);
        }

        return captured.ToString();
    }

    private void Sdk(params string[] arguments)
    {
        ProcessStartInfo start = new()
        {
            FileName = DotnetCli.Host(),
            WorkingDirectory = _work.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);

        using Process process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"`dotnet {string.Join(' ', arguments)}` should succeed: {output}");
    }
}
