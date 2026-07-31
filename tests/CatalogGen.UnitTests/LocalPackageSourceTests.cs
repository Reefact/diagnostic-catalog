using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// Acquisition from a .nupkg already on disk.
/// </summary>
/// <remarks>
/// The identity is the interesting part. A package states its own id and version in its
/// <c>.nuspec</c>, and that is what a catalogue records as the release it was generated from — so
/// reading it from the file name instead would let a renamed file quietly rewrite a catalogue's
/// provenance, which is the one thing provenance exists to prevent.
/// </remarks>
public sealed class LocalPackageSourceTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("cataloggen-nupkg-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    [Fact]
    public void The_package_names_itself_from_its_nuspec_not_its_file_name()
    {
        // Deliberately misleading on disk: the file says one thing, the manifest inside says another.
        string package = Pack("something-else-entirely.nupkg", id: "Vendor.Analyzers", version: "3.1.4");

        AnalyzerAssemblySet? set = LocalPackageSource.Acquire(package, null, null, "cs", Work());

        Assert.NotNull(set);
        Assert.Equal("Vendor.Analyzers", set.SourceName);
        Assert.Equal("3.1.4", set.SourceVersion);
    }

    [Fact]
    public void A_given_name_and_version_win_over_the_nuspec()
    {
        // A .nupkg can be rebuilt without its version moving, so what the manifest says is a default
        // rather than the last word.
        string package = Pack("v.nupkg", id: "Vendor.Analyzers", version: "3.1.4");

        AnalyzerAssemblySet? set = LocalPackageSource.Acquire(package, "Chosen", "9.9.9", "cs", Work());

        Assert.NotNull(set);
        Assert.Equal("Chosen", set.SourceName);
        Assert.Equal("9.9.9", set.SourceVersion);
    }

    [Fact]
    public void The_analyzer_assemblies_are_extracted_for_the_requested_language()
    {
        string package = Pack("v.nupkg", id: "Vendor.Analyzers", version: "1.0.0");

        AnalyzerAssemblySet? set = LocalPackageSource.Acquire(package, null, null, "cs", Work());

        Assert.NotNull(set);
        Assert.Equal("Vendor.Analyzers.dll", Path.GetFileName(Assert.Single(set.AssemblyPaths)));
    }

    [Fact]
    public void A_package_carrying_no_analyzer_is_refused_rather_than_read_as_empty()
    {
        // An empty result would emit a catalogue with no rules, and against an existing one would
        // retire every rule in it.
        string package = Path.Combine(_temp, "empty.nupkg");
        using (ZipArchive zip = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            Write(zip, "Vendor.Analyzers.nuspec", Nuspec("Vendor.Analyzers", "1.0.0"));
            Write(zip, "lib/netstandard2.0/Vendor.dll", "not an analyzer");
        }

        Assert.Null(LocalPackageSource.Acquire(package, null, null, "cs", Work()));
    }

    [Fact]
    public void A_path_that_does_not_resolve_is_refused()
        => Assert.Null(LocalPackageSource.Acquire(
                           Path.Combine(_temp, "absent.nupkg"), null, null, "cs", Work()));

    [Fact]
    public void A_file_that_is_not_a_package_is_refused_rather_than_crashing_the_run()
    {
        string notAPackage = Path.Combine(_temp, "broken.nupkg");
        File.WriteAllText(notAPackage, "this is not a zip");

        // A truncated download, an LFS pointer checked out without its content, or simply the wrong
        // path. All plausible, none of them a defect here — so it refuses and names the file, rather
        // than leaving the run's catch-all to report a zip format it never mentioned.
        Assert.Null(LocalPackageSource.Acquire(notAPackage, null, null, "cs", Work()));
    }

    private string Work()
    {
        string work = Path.Combine(_temp, "work", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);

        return work;
    }

    private string Pack(string fileName, string id, string version)
    {
        string path = Path.Combine(_temp, fileName);
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Write(zip, $"{id}.nuspec", Nuspec(id, version));
        Write(zip, $"analyzers/dotnet/cs/{id}.dll", "stand-in for an analyzer assembly");
        Write(zip, $"analyzers/dotnet/vb/{id}.VisualBasic.dll", "another language");

        return path;
    }

    // The versioned namespace is deliberate: a real .nuspec declares one, and which one is an
    // accident of the SDK that produced it, so the reader must not match on it.
    private static string Nuspec(string id, string version)
        => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{id}</id>
                <version>{version}</version>
                <authors>Vendor</authors>
                <description>A fixture.</description>
              </metadata>
            </package>
            """;

    private static void Write(ZipArchive zip, string entryName, string content)
    {
        using Stream stream = zip.CreateEntry(entryName).Open();
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}
