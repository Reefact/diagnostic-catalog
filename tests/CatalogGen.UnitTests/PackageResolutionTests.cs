using System;
using NuGet.Versioning;
using Xunit;

namespace CatalogGen.UnitTests;

/// <summary>
/// Which release a feed's answer resolves to, and what the run says when none of them qualifies.
/// </summary>
/// <remarks>
/// The decision reached only through a live feed before it was named; these exercise it directly,
/// over the version list a feed would have returned. What it decides is not a detail — the release
/// a catalogue mirrors is the release its consumers are told their rules come from, and picking a
/// preview, or the wrong one of two feeds' answers, misstates that on every rule at once.
/// </remarks>
public sealed class PackageResolutionTests
{
    private static NuGetVersion V(string version) => NuGetVersion.Parse(version);

    [Fact]
    public void Latest_means_the_newest_stable_release()
    {
        // A preview is on the feed and is newer, and is still not what "latest" mirrors: a
        // catalogue states the release it came from, and consumers read that as one they can take.
        Assert.Equal(V("2.0.0"), NuGetPackageSource.Candidate([V("1.0.0"), V("2.0.0"), V("3.0.0-beta")],
                                                              pinned: null, stableOnly: true));
    }

    [Fact]
    public void Latest_any_takes_the_newest_release_whether_it_is_stable_or_not()
        => Assert.Equal(V("3.0.0-beta"), NuGetPackageSource.Candidate([V("1.0.0"), V("2.0.0"), V("3.0.0-beta")],
                                                                     pinned: null, stableOnly: false));

    [Fact]
    public void A_pinned_version_is_taken_exactly()
        => Assert.Equal(V("1.0.0"), NuGetPackageSource.Candidate([V("1.0.0"), V("2.0.0")],
                                                                 pinned: V("1.0.0"), stableOnly: true));

    [Fact]
    public void A_pinned_version_the_feed_does_not_have_resolves_to_nothing()
        => Assert.Null(NuGetPackageSource.Candidate([V("1.0.0"), V("2.0.0")], pinned: V("9.9.9"), stableOnly: true));

    [Fact]
    public void A_feed_carrying_only_previews_offers_nothing_to_latest()
        => Assert.Null(NuGetPackageSource.Candidate([V("1.0.0-beta"), V("2.0.0-rc")], pinned: null, stableOnly: true));

    [Fact]
    public void Releases_are_ordered_by_version_not_as_strings()
    {
        // "9.0.0" sorts after "10.0.0" as text, and a catalogue pinned to 9 by that mistake would
        // look perfectly current for as long as 10 stayed the newest.
        Assert.Equal(V("10.0.0"), NuGetPackageSource.Candidate([V("1.0.0"), V("10.0.0"), V("9.0.0")],
                                                               pinned: null, stableOnly: true));
    }

    [Fact]
    public void A_package_no_source_has_at_all_is_reported_as_absent()
    {
        string message = NuGetPackageSource.NotResolved("Vendor.Analyzers", "latest", pinned: null,
                                                        newestOverall: null);

        Assert.Equal("Vendor.Analyzers was not found on any configured source", message);

        // Naming latest-any here would send the reader to change a switch when there is nothing on
        // any source to fall back to.
        Assert.DoesNotContain("latest-any", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pinned_version_that_is_absent_names_the_newest_there_is()
    {
        string message = NuGetPackageSource.NotResolved("Vendor.Analyzers", "1.2.3", pinned: V("1.2.3"),
                                                        newestOverall: V("2.0.0"));

        Assert.Equal("Vendor.Analyzers 1.2.3 was not found on any configured source; " +
                     "the newest available is 2.0.0", message);
    }

    [Fact]
    public void A_package_with_only_previews_is_told_how_to_ask_for_one()
    {
        string message = NuGetPackageSource.NotResolved("Vendor.Analyzers", "latest", pinned: null,
                                                        newestOverall: V("3.0.0-beta"));

        Assert.Equal("Vendor.Analyzers has no stable version on any configured source " +
                     "(newest is 3.0.0-beta, a prerelease); use latest-any or an explicit version", message);
    }

    [Fact]
    public void The_resolved_line_stays_quiet_when_it_took_the_newest_release()
        => Assert.Equal("resolved Vendor.Analyzers => 2.0.0",
                        NuGetPackageSource.Resolution("Vendor.Analyzers", V("2.0.0"), newestOverall: V("2.0.0")));

    [Fact]
    public void The_resolved_line_says_what_it_passed_over()
        => Assert.Equal("resolved Vendor.Analyzers => 2.0.0 (newest overall is 3.0.0-beta, a prerelease)",
                        NuGetPackageSource.Resolution("Vendor.Analyzers", V("2.0.0"),
                                                      newestOverall: V("3.0.0-beta")));
}
