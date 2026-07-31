using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// Acquisition from a project: what MSBuild is asked, and what is refused rather than guessed.
/// </summary>
/// <remarks>
/// These evaluate real projects with the real <c>dotnet msbuild</c>, because the whole point of
/// <c>--project</c> is that MSBuild — not this tool — decides where the output is. A fake evaluator
/// would assert this tool's idea of a bin path, which is the thing it exists not to have.
/// <para>
/// They stay fast and leave nothing behind because <c>-getProperty</c> stops after evaluation: it
/// needs no restore, writes no <c>obj/</c>, and builds nothing. That is the same property that makes
/// <c>dcat validate --project</c> safe to run against somebody's working copy.
/// </para>
/// </remarks>
public sealed class ProjectSourceTests : IDisposable
{
    private readonly DirectoryInfo _work = Directory.CreateTempSubdirectory("projectsource");

    public void Dispose() => _work.Delete(recursive: true);

    [Fact]
    public void A_project_that_is_not_there_is_refused()
        => Assert.Null(ProjectSource.Acquire([Path.Combine(_work.FullName, "absent.csproj")],
                                             "Release", null, null));

    [Fact]
    public void No_project_at_all_is_refused()
        => Assert.Null(ProjectSource.Acquire([], "Release", null, null));

    [Theory]
    [InlineData("Everything.sln")]
    [InlineData("Everything.slnx")]
    [InlineData("Everything.slnf")]
    public void A_solution_is_refused_rather_than_enumerated(string name)
    {
        // Not "unsupported for now": deciding which of a solution's projects produce analyzers is a
        // guess, and guessing short emits a catalogue whose missing rules read as retired ones —
        // with nothing anywhere to report the omission (specification §3.2).
        string path = Path.Combine(_work.FullName, name);
        File.WriteAllText(path, "");

        Assert.Null(ProjectSource.Acquire([path], "Release", null, null));
    }

    [Fact]
    public void A_project_that_is_not_built_is_refused_rather_than_read_as_empty()
    {
        // MSBuild answers with a path either way; only the file tells them apart. Reading "no
        // assembly" as "no rules" would emit a catalogue retiring every rule the project declares.
        string project = Project("net8.0", version: "1.0.0");

        Assert.Null(ProjectSource.Acquire([project], "Release", null, null));
    }

    [Fact]
    public void A_built_project_resolves_to_the_assembly_MSBuild_names()
    {
        string project = Project("net8.0", version: "3.1.4");
        string assembly = Build(project, "Release", "net8.0");

        AnalyzerAssemblySet? acquired = ProjectSource.Acquire([project], "Release", null, null);

        Assert.NotNull(acquired);
        Assert.Equal([assembly], acquired.AssemblyPaths);
    }

    [Fact]
    public void The_source_is_the_release_the_project_declares_not_the_one_stamped_in_the_assembly()
    {
        // The two are usually the same number, and this fixture makes them differ on purpose:
        // AssemblyVersion is routinely pinned to a major while the package version moves, and a
        // catalogue that recorded the pinned one would claim a source that stood still while its
        // rules did not — which is exactly what the recorded version exists to detect.
        string project = Project("net8.0", version: "3.1.4");
        Build(project, "Release", "net8.0");

        AnalyzerAssemblySet? acquired = ProjectSource.Acquire([project], "Release", null, null);

        Assert.NotNull(acquired);
        Assert.Equal("Fixture", acquired.SourceName);
        Assert.Equal("3.1.4", acquired.SourceVersion);
        Assert.NotEqual(typeof(ProjectSourceTests).Assembly.GetName().Version!.ToString(),
                        acquired.SourceVersion);
    }

    [Fact]
    public void What_the_caller_states_still_wins_over_what_the_project_declares()
    {
        string project = Project("net8.0", version: "3.1.4");
        Build(project, "Release", "net8.0");

        AnalyzerAssemblySet? acquired = ProjectSource.Acquire([project], "Release", "Vendor.Rules", "9.9.9");

        Assert.NotNull(acquired);
        Assert.Equal("Vendor.Rules", acquired.SourceName);
        Assert.Equal("9.9.9", acquired.SourceVersion);
    }

    [Fact]
    public void The_configuration_selects_which_build_is_read()
    {
        string project = Project("net8.0", version: "1.0.0");
        string debug = Build(project, "Debug", "net8.0");

        Assert.Null(ProjectSource.Acquire([project], "Release", null, null));

        AnalyzerAssemblySet? acquired = ProjectSource.Acquire([project], "Debug", null, null);

        Assert.NotNull(acquired);
        Assert.Equal([debug], acquired.AssemblyPaths);
    }

    [Fact]
    public void A_multi_targeted_project_is_read_through_the_framework_its_analyzers_ship_as()
    {
        // TargetPath is a per-framework property, so a multi-targeted project evaluates to nothing
        // and has to be asked once per framework. netstandard2.0 wins when it is one of them because
        // that is the build a consumer's compiler actually loads; a modern target alongside it is
        // built for the project's own tests, and reading that one could read descriptors nobody is
        // ever served.
        //
        // Declared second on purpose. Taking the frameworks in the order the project lists them
        // would pass this test with netstandard2.0 first and prove nothing.
        string project = Project("net8.0;netstandard2.0", version: "1.0.0");
        string shipped = Build(project, "Release", "netstandard2.0");
        Build(project, "Release", "net8.0");

        AnalyzerAssemblySet? acquired = ProjectSource.Acquire([project], "Release", null, null);

        Assert.NotNull(acquired);
        Assert.Equal([shipped], acquired.AssemblyPaths);
    }

    [Fact]
    public void A_multi_targeted_project_falls_back_to_whichever_framework_was_built()
    {
        string project = Project("netstandard2.0;net8.0", version: "1.0.0");
        string only = Build(project, "Release", "net8.0");

        AnalyzerAssemblySet? acquired = ProjectSource.Acquire([project], "Release", null, null);

        Assert.NotNull(acquired);
        Assert.Equal([only], acquired.AssemblyPaths);
    }

    [Fact]
    public void Several_projects_are_read_together_and_the_first_names_the_source()
    {
        // The case --project is repeatable for: an analyzer and its code fixes in separate projects,
        // which is how StyleCop is laid out. One refusing takes the run with it, for the reason every
        // acquisition here refuses — a set short of an assembly is a catalogue short of its rules.
        string analyzer = Project("net8.0", version: "3.1.4", name: "Fixture", directory: "analyzer");
        string fixes = Project("net8.0", version: "0.0.1", name: "FixtureFixes", directory: "fixes");
        string analyzerDll = Build(analyzer, "Release", "net8.0", "Fixture");
        string fixesDll = Build(fixes, "Release", "net8.0", "FixtureFixes");

        AnalyzerAssemblySet? acquired = ProjectSource.Acquire([analyzer, fixes], "Release", null, null);

        Assert.NotNull(acquired);
        Assert.Equal([analyzerDll, fixesDll], acquired.AssemblyPaths.OrderBy(p => p, StringComparer.Ordinal));
        Assert.Equal("Fixture", acquired.SourceName);
        Assert.Equal("3.1.4", acquired.SourceVersion);
    }

    [Fact]
    public void One_project_that_is_not_built_refuses_the_whole_set()
    {
        string built = Project("net8.0", version: "1.0.0", name: "Fixture", directory: "built");
        string notBuilt = Project("net8.0", version: "1.0.0", name: "Other", directory: "absent");
        Build(built, "Release", "net8.0");

        Assert.Null(ProjectSource.Acquire([built, notBuilt], "Release", null, null));
    }

    [Fact]
    public void A_file_that_is_not_a_project_reports_what_MSBuild_said_about_it()
    {
        string path = Path.Combine(_work.FullName, "notaproject.csproj");
        File.WriteAllText(path, "{ \"this\": \"is json\" }");

        Assert.Null(ProjectSource.Acquire([path], "Release", null, null));
    }

    // A project file only, never built: -getProperty evaluates it without restoring, so this is
    // enough for MSBuild to answer where the output would be.
    private string Project(string frameworks, string version, string name = "Fixture", string directory = "p")
    {
        DirectoryInfo folder = Directory.CreateDirectory(Path.Combine(_work.FullName, directory));
        string element = frameworks.Contains(';') ? "TargetFrameworks" : "TargetFramework";
        string path = Path.Combine(folder.FullName, name + ".csproj");
        File.WriteAllText(path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <{element}>{frameworks}</{element}>
                <AssemblyName>{name}</AssemblyName>
                <Version>{version}</Version>
              </PropertyGroup>
            </Project>
            """);

        return path;
    }

    // Puts a real assembly where MSBuild says the build output goes, without running one. Compiling
    // a fixture would test the SDK; what is under test is that this tool asks MSBuild for the path
    // and reads what is at it, so a real assembly file at that path is the whole requirement —
    // LocalAssemblySource reads its manifest, and nothing loads it.
    private static string Build(string project, string configuration, string framework, string name = "Fixture")
    {
        string directory = Path.Combine(Path.GetDirectoryName(project)!, "bin", configuration, framework);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + ".dll");
        File.Copy(Assembly.GetExecutingAssembly().Location, path, overwrite: true);

        return path;
    }
}
