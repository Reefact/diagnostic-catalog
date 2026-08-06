using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Xunit;

namespace DiagnosticCatalog.Packaging.IntegrationTests;

/// <summary>
/// Packs the foundation, writes a set of consumers outside this repository, restores them from a feed
/// holding nothing else, and remembers what each build was handed.
/// </summary>
/// <remarks>
/// <para>
/// Built once for the whole assembly, because the expensive part is real: one <c>dotnet pack</c>, three
/// fixture packs and five consumer builds. Everything the tests assert is read off the result, so a test
/// added later costs nothing.
/// </para>
/// <para>
/// The work directory sits under the system temp folder, never under the repository. A consumer has no
/// <c>Directory.Build.props</c>, no <c>.editorconfig</c> chain and no warnings-as-errors ratchet
/// overhead, and a fixture written inside the tree would silently inherit all three — measuring this
/// repository's build rather than a stranger's.
/// </para>
/// <para>
/// The feed is the only package source, declared with <c>&lt;clear /&gt;</c>. Without it a restore
/// would resolve the PUBLISHED <c>DiagnosticCatalog</c> from nuget.org and every assertion here would
/// describe a package nobody just built.
/// </para>
/// </remarks>
internal sealed class PackagedConsumption
{
    /// <summary>
    /// The version this suite packs under. Unmistakably not a release, and distinct from the dry-run
    /// version <c>tools/packaging/verify-consumption.sh</c> uses, so the two never race over the same
    /// extraction in the global packages folder.
    /// </summary>
    internal const string Version = "0.0.0-pkgtest";

    internal const string CatalogueA = "Acme.Fixer.Catalog.A";

    internal const string CatalogueB = "Acme.Fixer.Catalog.B";

    internal const string Library = "Acme.Fixer.Library";

    /// <summary>The container the fixture catalogues publish their rules under.</summary>
    internal const string Container = "AcmeRule";

    /// <summary>The namespace that container lives in.</summary>
    internal const string CatalogueNamespace = "Acme.Fixer.Rules";

    private static readonly Lazy<PackagedConsumption> Instance = new(Build, isThreadSafe: true);

    private PackagedConsumption(string work, IReadOnlyDictionary<string, Consumer> consumers)
    {
        Work = work;
        Consumers = consumers;
    }

    internal static PackagedConsumption Current => Instance.Value;

    internal string Work { get; }

    /// <summary>Each consumer that was built, by name.</summary>
    internal IReadOnlyDictionary<string, Consumer> Consumers { get; }

    /// <summary>One consumer project, and what its restore handed the compiler.</summary>
    /// <param name="Name">The project name, which is also its folder.</param>
    /// <param name="Directory">The output folder, which a restored catalogue assembly is read from.</param>
    /// <param name="Analyzers">Every <c>@(Analyzer)</c> item the build resolved, by full path.</param>
    /// <param name="Output">The file names that reached the output folder.</param>
    internal sealed record Consumer(
        string Name, string Directory, IReadOnlyList<string> Analyzers, IReadOnlyList<string> Output)
    {
        /// <summary>How many resolved analyzer items carry a given file name.</summary>
        internal int Instances(string fileName) =>
            Analyzers.Count(path =>
                string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase));

        /// <summary>The single resolved item with that file name, failing when there is not exactly one.</summary>
        internal string Single(string fileName)
        {
            List<string> matches = Analyzers
                .Where(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(
                matches.Count == 1,
                $"{Name} resolved {matches.Count} analyzer items named {fileName}; expected exactly one.");

            return matches[0];
        }

        internal bool Reached(string fileName) =>
            Output.Any(file => string.Equals(file, fileName, StringComparison.OrdinalIgnoreCase));
    }

    private static string RepositoryRoot
    {
        get
        {
            AssemblyMetadataAttribute? stamp = typeof(PackagedConsumption).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(metadata =>
                    string.Equals(metadata.Key, "PackagingRepositoryRoot", StringComparison.Ordinal));

            if (stamp is null)
            {
                Assert.Fail(
                    "This assembly carries no PackagingRepositoryRoot metadata, so the suite cannot "
                    + "find the project it has to pack. The stamp is written by this project's .csproj.");
            }

            return stamp.Value ?? string.Empty;
        }
    }

    private static PackagedConsumption Build()
    {
        string root = RepositoryRoot;
        string work = Path.Combine(Path.GetTempPath(), "dcat-packaging-tests");

        if (Directory.Exists(work)) { Directory.Delete(work, recursive: true); }

        Directory.CreateDirectory(work);

        string feed = Path.Combine(work, "feed");
        Directory.CreateDirectory(feed);

        Purge();

        // The foundation, packed from source. Its .csproj orders the analyzer and code-fix builds and
        // packs their output into dcat-analyzers/, so this one command produces everything under test.
        Run(
            root,
            "pack",
            Path.Combine(root, "src", "DiagnosticCatalog", "DiagnosticCatalog.csproj"),
            "-c", "Release", "-o", feed, $"-p:Version={Version}");

        string package = Path.Combine(feed, $"DiagnosticCatalog.{Version}.nupkg");
        Assert.True(File.Exists(package), $"the foundation was not packed: {package}");

        WriteNuGetConfig(work, feed);

        PackCatalogue(work, feed, CatalogueA);
        PackCatalogue(work, feed, CatalogueB);

        Dictionary<string, Consumer> consumers = [];

        // One reference, to one catalogue, and nothing else. This is the arrangement the whole design
        // exists for, and every code-fix assertion is made against what THIS project was handed.
        consumers["Consumer"] = BuildConsumer(
            work,
            "Consumer",
            references: [Reference(CatalogueA)],
            properties: "<OutputType>Exe</OutputType>");

        consumers["TwoCatalogues"] = BuildConsumer(
            work,
            "TwoCatalogues",
            references: [Reference(CatalogueA), Reference(CatalogueB)]);

        PackLibrary(work, feed);

        consumers["TwoHops"] = BuildConsumer(
            work,
            "TwoHops",
            references: [Reference(Library)],
            properties: "<OutputType>Exe</OutputType>");

        consumers["TwoHopsOptIn"] = BuildConsumer(
            work,
            "TwoHopsOptIn",
            references: [Reference(Library)],
            properties: "<EnableDiagnosticCatalogAnalyzers>true</EnableDiagnosticCatalogAnalyzers>");

        consumers["DirectOptOut"] = BuildConsumer(
            work,
            "DirectOptOut",
            references: [Reference(CatalogueA)],
            properties: "<OutputType>Exe</OutputType>"
                        + "<EnableDiagnosticCatalogAnalyzers>false</EnableDiagnosticCatalogAnalyzers>");

        return new PackagedConsumption(work, consumers);
    }

    /// <summary>
    /// Removes the extractions a previous run left in the global packages folder.
    /// </summary>
    /// <remarks>
    /// Without this a repacked fixture of the same id never reaches a build, and the suite silently
    /// measures whatever was packed first. The same trap <c>verify-consumption.sh</c> documents, and it
    /// bites harder here: the fixtures are always <c>1.0.0</c>, so nothing about the version says the
    /// bytes changed.
    /// </remarks>
    private static void Purge()
    {
        string packages = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                          ?? Path.Combine(
                              Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                              ".nuget", "packages");

        foreach (string id in new[] { CatalogueA, CatalogueB, Library })
        {
            string extracted = Path.Combine(packages, id.ToLowerInvariant());
            if (Directory.Exists(extracted)) { Directory.Delete(extracted, recursive: true); }
        }

        string foundation = Path.Combine(packages, "diagnosticcatalog", Version);
        if (Directory.Exists(foundation)) { Directory.Delete(foundation, recursive: true); }
    }

    private static string Reference(string id) =>
        id == Library || id == CatalogueA || id == CatalogueB
            ? $"    <PackageReference Include=\"{id}\" Version=\"1.0.0\" />"
            : throw new ArgumentOutOfRangeException(nameof(id));

    private static void WriteNuGetConfig(string work, string feed) =>
        File.WriteAllText(
            Path.Combine(work, "NuGet.config"),
            $"""
             <?xml version="1.0" encoding="utf-8"?>
             <configuration>
               <packageSources>
                 <clear />
                 <add key="local" value="{feed}" />
               </packageSources>
             </configuration>
             """);

    /// <summary>
    /// Packs a catalogue fixture: rules a consumer can name, and the three-line opt-in every catalogue
    /// this repository publishes carries.
    /// </summary>
    /// <remarks>
    /// The opt-in props is COPIED from <c>build/CatalogueAnalyzerOptIn.props</c> rather than written
    /// here, exactly as <c>verify-consumption.sh</c> copies it: a fixture that spelled it its own way
    /// could keep passing after the shipped one changed.
    /// </remarks>
    private static void PackCatalogue(string work, string feed, string id)
    {
        string directory = Path.Combine(work, "pkg-" + id);
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(work, "NuGet.config"), Path.Combine(directory, "NuGet.config"));

        File.Copy(
            Path.Combine(RepositoryRoot, "build", "CatalogueAnalyzerOptIn.props"),
            Path.Combine(directory, "OptIn.props"));

        // Two rules, because DCAT0006's fix has to add a using and DCAT0001 needs two rules to
        // disagree about. Both are well formed: any other definition diagnostic reported here would
        // fail the fixture's own pack, since ADR-0040 makes them errors.
        File.WriteAllText(
            Path.Combine(directory, "Rules.cs"),
            $$"""
              using DiagnosticCatalog;

              namespace {{CatalogueNamespace}}
              {
                  [DiagnosticCategory]
                  internal static class AcmeCategory
                  {
                      public const string MajorCodeSmell = "Major Code Smell";

                      public const string CriticalCodeSmell = "Critical Code Smell";
                  }

                  public static class {{Container}}
                  {
                      [DiagnosticRule]
                      public static class S1144
                      {
                          public const string Id = nameof(S1144);

                          public const string Category = AcmeCategory.MajorCodeSmell;
                      }

                      [DiagnosticRule]
                      public static class S2094
                      {
                          public const string Id = nameof(S2094);

                          public const string Category = AcmeCategory.CriticalCodeSmell;
                      }
                  }
              }
              """);

        File.WriteAllText(
            Path.Combine(directory, id + ".csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>netstandard2.0</TargetFramework>
                 <PackageId>{id}</PackageId>
                 <Version>1.0.0</Version>
               </PropertyGroup>
               <ItemGroup>
                 <PackageReference Include="DiagnosticCatalog" Version="{Version}" />
                 <None Include="OptIn.props" Pack="true" PackagePath="build/{id}.props" />
               </ItemGroup>
             </Project>
             """);

        Run(directory, "pack", "-c", "Release", "-o", feed);
    }

    /// <summary>
    /// A library that took a catalogue for its own suppressions, packed so a consumer can reference it.
    /// Its reference is the ORDINARY one — no <c>PrivateAssets</c>, no precaution — because that is the
    /// arrangement the boundary has to hold for.
    /// </summary>
    private static void PackLibrary(string work, string feed)
    {
        string directory = Path.Combine(work, "pkg-" + Library);
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(work, "NuGet.config"), Path.Combine(directory, "NuGet.config"));

        File.WriteAllText(
            Path.Combine(directory, "Api.cs"),
            """
            namespace Acme.Fixer.LibraryApi
            {
                public static class Reporting
                {
                    public static string Render()
                    {
                        return "report";
                    }
                }
            }
            """);

        File.WriteAllText(
            Path.Combine(directory, Library + ".csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>netstandard2.0</TargetFramework>
                 <PackageId>{Library}</PackageId>
                 <Version>1.0.0</Version>
               </PropertyGroup>
               <ItemGroup>
                 <PackageReference Include="{CatalogueA}" Version="1.0.0" />
               </ItemGroup>
             </Project>
             """);

        Run(directory, "pack", "-c", "Release", "-o", feed);
    }

    /// <summary>
    /// Writes, restores and builds a consumer, then asks MSBuild what the compiler was handed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The analyzer count is asked of MSBuild rather than counted in a build log, for the reason
    /// <c>verify-consumption.sh</c> gives: a log cannot answer it. MSBuild echoes each warning again in
    /// its summary, so a raw count reads two for one analyzer, and Roslyn may collapse two identical
    /// diagnostics into one, so a count can read one for two analyzers. The item list is the compiler's
    /// actual input and says neither more nor less.
    /// </para>
    /// <para>
    /// Every consumer carries only clean code. The delivery questions — how many analyzer instances,
    /// what reached the output folder — are answered by the item list and the bin folder, and a
    /// deliberate defect would only add a severity to manage: since ADR-0040 the diagnostics that would
    /// prove "the analyzer ran" are errors, and a consumer whose build stopped has no output folder to
    /// ask the other half of the questions of. What the analyzer DOES with the assemblies it was handed
    /// is proven directly instead, by loading them.
    /// </para>
    /// </remarks>
    private static Consumer BuildConsumer(
        string work, string name, IReadOnlyList<string> references, string properties = "")
    {
        string directory = Path.Combine(work, name);
        Directory.CreateDirectory(directory);
        File.Copy(Path.Combine(work, "NuGet.config"), Path.Combine(directory, "NuGet.config"));

        // Every consumer asked what reached its OUTPUT folder must be an application: a class library
        // never copies package assemblies at all, so the same check against one would measure the SDK's
        // copy rules and pass whatever the package did.
        File.WriteAllText(
            Path.Combine(directory, "Program.cs"),
            """
            public static class Program
            {
                public static void Main()
                {
                }
            }
            """);

        File.WriteAllText(
            Path.Combine(directory, name + ".csproj"),
            $"""
             <Project Sdk="Microsoft.NET.Sdk">
               <PropertyGroup>
                 <TargetFramework>net10.0</TargetFramework>
                 <Nullable>enable</Nullable>
                 <ImplicitUsings>disable</ImplicitUsings>
                 {properties}
               </PropertyGroup>
               <ItemGroup>
             {string.Join(Environment.NewLine, references)}
               </ItemGroup>
             </Project>
             """);

        Run(directory, "build", "-c", "Release");

        string output = Path.Combine(directory, "bin", "Release", "net10.0");

        return new Consumer(name, output, ResolvedAnalyzers(directory, name), OutputFiles(output));
    }

    private static List<string> ResolvedAnalyzers(string directory, string name)
    {
        string json = Run(
            directory, "msbuild", name + ".csproj", "-t:ResolveReferences", "-getItem:Analyzer");

        using JsonDocument document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("Items", out JsonElement items) ||
            !items.TryGetProperty("Analyzer", out JsonElement analyzers))
        {
            return [];
        }

        List<string> resolved = [];
        foreach (JsonElement analyzer in analyzers.EnumerateArray())
        {
            if (analyzer.TryGetProperty("FullPath", out JsonElement full))
            {
                resolved.Add(full.GetString() ?? string.Empty);
            }
            else if (analyzer.TryGetProperty("Identity", out JsonElement identity))
            {
                resolved.Add(identity.GetString() ?? string.Empty);
            }
        }

        return resolved;
    }

    private static IReadOnlyList<string> OutputFiles(string output) =>
        Directory.Exists(output)
            ? [.. Directory.EnumerateFiles(output).Select(Path.GetFileName).Where(name => name is not null)!]
            : [];

    /// <summary>Runs the .NET CLI in a directory, failing the suite with its output if it errors.</summary>
    private static string Run(string directory, params string[] arguments)
    {
        ProcessStartInfo start = new("dotnet")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }

        // A consumer has no MSBuild node reuse to inherit and no telemetry to send; both make a nested
        // run slower and noisier without changing what it measures.
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";

        using Process process = Process.Start(start)
                                ?? throw new InvalidOperationException("dotnet could not be started");

        StringBuilder standard = new();
        StringBuilder error = new();

        process.OutputDataReceived += (_, line) => { if (line.Data is not null) { standard.AppendLine(line.Data); } };
        process.ErrorDataReceived += (_, line) => { if (line.Data is not null) { error.AppendLine(line.Data); } };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        Assert.True(
            process.ExitCode == 0,
            $"`dotnet {string.Join(' ', arguments)}` failed in {directory} with exit code "
            + $"{process.ExitCode}.\n{standard}\n{error}");

        return standard.ToString();
    }
}
