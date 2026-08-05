using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace DiagnosticCatalog.Catalogs.UnitTests;

/// <summary>
/// The package icon of every generated catalogue.
/// </summary>
/// <remarks>
/// <para>
/// The convention is written where the icon is wired — <c>Directory.Build.targets</c>: a catalogue
/// carries its own <c>icon.png</c> showing the family mark with the prefix of the rules it mirrors,
/// so a consumer scanning nuget.org can tell them apart at the 128px the listing renders. Until
/// this file existed, nothing checked it, and a catalogue shipped for a while with the badge of a
/// DIFFERENT vendor: its icon had been copied from a sibling, and twelve CI checks passed over a
/// package of <c>IDExxxx</c> rules wearing StyleCop's <c>SA</c>.
/// </para>
/// <para>
/// That is the failure this guards, and it is invisible to every other check here. An icon is
/// opaque to the parity tests, to the mirror banner and to the compiler; the only thing that
/// distinguishes a right one from a borrowed one is that no two catalogues may look the same. So
/// what is asserted is distinctness rather than content — nothing here reads the badge.
/// </para>
/// <para>
/// Scoped to the CATALOGUES, not to every packable project. Nine projects declare a
/// <c>&lt;ReleaseTrain&gt;</c> and four carry an icon; the other five fall back to the repository's
/// own, which <c>Directory.Build.targets</c> chooses deliberately so that a project joining a train
/// ships the family mark rather than nuget.org's blank placeholder. Requiring one everywhere would
/// contradict that.
/// </para>
/// </remarks>
public sealed class PackageIconTests
{
    /// <summary>
    /// The catalogues are named by <see cref="DocumentedMirrorTests.Catalogues"/> rather than
    /// listed again here. One hand-written list is a decision this repository already made and
    /// explains; two of them is how the second one goes stale.
    /// </summary>
    [Theory]
    [MemberData(nameof(DocumentedMirrorTests.Catalogues), MemberType = typeof(DocumentedMirrorTests))]
    public void A_catalogue_carries_an_icon_that_is_its_own(string project, string source)
    {
        string icon = Path.Combine(Icons, project, "icon.png");

        Assert.True(
            File.Exists(icon),
            $"{project} generates {source} and should carry its own icon.png beside its .csproj, " +
            "showing the family mark with the prefix of the rules it mirrors");

        byte[] mine = File.ReadAllBytes(icon);

        // The repository's icon is the family mark WITHOUT a prefix, and it is what a project
        // gets by carrying none. Identical bytes here mean the file is present and says nothing
        // about which rules the package holds — the placeholder state, wearing a real file's name.
        Assert.False(
            Same(mine, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "repoicon", "icon.png"))),
            $"{project}'s icon is the repository's generic mark, so it carries no rule prefix");

        foreach ((string other, byte[] bytes) in OtherIcons(project))
        {
            Assert.False(
                Same(mine, bytes),
                $"{project} and {other} ship the same icon, so at least one of them wears the " +
                "other's rule prefix on nuget.org");
        }
    }

    private static string Icons => Path.Combine(AppContext.BaseDirectory, "catalogicons");

    // Read from the directory rather than from the list, so an icon that is copied INTO a
    // catalogue folder nobody declared is still compared against the ones that were.
    private static IEnumerable<(string Project, byte[] Bytes)> OtherIcons(string project)
        => Directory.Exists(Icons)
               ? Directory.GetFiles(Icons, "icon.png", SearchOption.AllDirectories)
                          .Select(path => (Project: Path.GetFileName(Path.GetDirectoryName(path))!, Path: path))
                          .Where(found => !string.Equals(found.Project, project, StringComparison.Ordinal))
                          .Select(found => (found.Project, File.ReadAllBytes(found.Path)))
               : [];

    // Byte equality, not an image comparison. Two icons that differ by a pixel are two icons
    // somebody drew; the failure this catches is a file copied whole.
    private static bool Same(byte[] left, byte[] right) => left.AsSpan().SequenceEqual(right);
}
