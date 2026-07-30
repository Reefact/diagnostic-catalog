using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// The generator writes a catalogue and, on its next run, reads that same file back to work out
/// what upstream changed. Those two halves must agree exactly: if the parser cannot recover
/// something the emitter wrote, the nightly job reports a rule as added or retired when it was
/// neither — and because nothing in the platform validates a suppression's category, a bad diff
/// merged on that basis leaves no symptom anywhere.
/// </summary>
public sealed class CatalogRoundTripTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("cataloggen-").FullName;

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    /// <summary>
    /// Every catalogue the repository actually ships, copied beside this assembly by the csproj.
    /// Using the shipped files rather than a fixture means the test grows with the catalogues and
    /// exercises the real shapes — hundreds of rules, punctuation in categories, help links present
    /// on some entries and absent on others.
    /// </summary>
    public static TheoryData<string> ShippedCatalogues()
    {
        TheoryData<string> data = [];
        foreach (string path in Directory.EnumerateFiles(
                     Path.Combine(AppContext.BaseDirectory, "catalogs"), "*.g.cs"))
        {
            data.Add(path);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ShippedCatalogues))]
    public void Regenerating_a_catalogue_from_its_own_content_reproduces_it_byte_for_byte(string path)
    {
        string original = File.ReadAllText(path).ReplaceLineEndings("\n");

        Previous? parsed = CatalogParser.ReadPrevious(path);
        Assert.NotNull(parsed);

        Match source = Regex.Match(
            original,
            """\[assembly: CatalogSource\(\s*source:\s*"([^"]*)",\s*sourceVersion:\s*"([^"]*)",\s*generatedOn:\s*"([^"]*)"\)\]""");
        Assert.True(source.Success, "the catalogue should declare its own provenance");

        string package = source.Groups[1].Value;
        string version = source.Groups[2].Value;
        string generatedOn = source.Groups[3].Value;

        // The parser is the only route by which the emitter learns what the previous run produced,
        // so the version it recovers has to be the one written.
        Assert.Equal(version, parsed!.SourceVersion);

        string ns = Regex.Match(original, "(?m)^namespace (\\S+);$").Groups[1].Value;
        // Two classes sit at column 0: the categories first, the rules second.
        string container = Regex.Matches(original, "(?m)^public static class (\\w+)$")[^1].Groups[1].Value;

        string output = Path.Combine(_temp, Path.GetFileName(path));
        Job job = new(package, version, ns, container, output, "cs");

        GenerateResult result = CatalogEmitter.Emit(
            job, package, version, parsed.Rules, previous: null, dateOverride: generatedOn);

        Assert.True(result.Changed);
        Assert.Equal(original, File.ReadAllText(output).ReplaceLineEndings("\n"));
    }
}
