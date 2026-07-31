using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace CatalogGen;

// Acquisition from a NuGet feed: resolve the release, fetch the package, and hand the reader the
// analyzer assemblies it carries for one language.
//
// It goes through NuGet's own client rather than calling api.nuget.org by hand, and the reason is
// not tidiness. A hardcoded flat-container URL reaches exactly one feed, so a shop whose analyzers
// live on a private one could not use this mode at all — which hollows out the argument for
// publishing the tool in the first place (ADR-0017). Sources, the NuGet.config hierarchy above the
// working directory, and credentials are what the client library is for; the encrypted and
// provider-supplied kinds of credential cannot be read by hand at all.
//
// The layout question a package then asks — which assemblies are this language's analyzers — is the
// same one a local .nupkg asks, and lives in NupkgReader rather than here.
internal static class NuGetPackageSource
{
    internal static async Task<AnalyzerAssemblySet?> AcquireAsync(
        string packageId, string requestedVersion, string language, string workDir, string? requestedSource,
        CancellationToken cancellation = default)
    {
        IReadOnlyList<SourceRepository>? repositories = Repositories(requestedSource);
        if (repositories is null) return null;

        using SourceCacheContext cache = new();
        ILogger log = NullLogger.Instance;

        Resolved? resolved = await ResolveAsync(packageId, requestedVersion, repositories, cache, log, cancellation);
        if (resolved is null) return null;

        string nupkg = Path.Combine(workDir, "package.nupkg");
        Console.WriteLine($"downloading {packageId} {resolved.Version} from {resolved.Repository.PackageSource.Name}");
        await using (FileStream file = File.Create(nupkg))
        {
            FindPackageByIdResource finder =
                await resolved.Repository.GetResourceAsync<FindPackageByIdResource>(cancellation);
            bool copied = await finder.CopyNupkgToStreamAsync(
                packageId, resolved.Version, file, cache, log, cancellation);
            if (!copied)
            {
                Console.Error.WriteLine($"{packageId} {resolved.Version} could not be downloaded");

                return null;
            }
        }

        IReadOnlyList<string>? paths = NupkgReader.ExtractAnalyzerAssemblies(
            nupkg, $"{packageId} {resolved.Version}", workDir, language);

        return paths is null
            ? null
            : new AnalyzerAssemblySet(paths, packageId, resolved.Version.ToNormalizedString());
    }

    // The sources to look in: the one asked for, or every enabled source the machine is configured
    // with. Settings are loaded from the current directory, so a repository's own NuGet.config —
    // and every one above it — is honoured exactly as `dotnet restore` would honour it.
    private static IReadOnlyList<SourceRepository>? Repositories(string? requestedSource)
    {
        ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory());
        PackageSourceProvider provider = new(settings);
        List<PackageSource> configured = provider.LoadPackageSources().Where(s => s.IsEnabled).ToList();

        if (requestedSource is not null)
        {
            // By name first, because that is what a NuGet.config gives a private feed and what a
            // caller will have to hand; a URL is accepted too, and anything that matches neither is
            // taken as a URL rather than refused — an unreachable one fails with its own message.
            PackageSource source =
                configured.FirstOrDefault(s => string.Equals(s.Name, requestedSource, StringComparison.OrdinalIgnoreCase))
                ?? configured.FirstOrDefault(s => string.Equals(s.Source, requestedSource, StringComparison.OrdinalIgnoreCase))
                ?? new PackageSource(requestedSource);

            Console.WriteLine($"source: {source.Name} ({source.Source})");

            return [Repository.Factory.GetCoreV3(source)];
        }

        if (configured.Count == 0)
        {
            Console.Error.WriteLine(
                "no enabled package source is configured; name one with --source or add it to NuGet.config");

            return null;
        }

        Console.WriteLine($"sources: {string.Join(", ", configured.Select(s => s.Name))}");

        return [.. configured.Select(Repository.Factory.GetCoreV3)];
    }

    // The release to mirror, and the source that has it. Null when no source offers one that
    // qualifies — which is a failure rather than an empty result.
    private static async Task<Resolved?> ResolveAsync(
        string packageId, string requested, IReadOnlyList<SourceRepository> repositories,
        SourceCacheContext cache, ILogger log, CancellationToken cancellation)
    {
        bool anyRelease = requested is "latest" or "latest-any";
        NuGetVersion? pinned = anyRelease ? null : NuGetVersion.Parse(requested);

        Resolved? best = null;
        NuGetVersion? newestOverall = null;
        foreach (SourceRepository repository in repositories)
        {
            FindPackageByIdResource finder = await repository.GetResourceAsync<FindPackageByIdResource>(cancellation);
            IReadOnlyList<NuGetVersion> available =
                [.. await finder.GetAllVersionsAsync(packageId, cache, log, cancellation)];
            if (available.Count == 0) continue;

            NuGetVersion sourceNewest = available.Max()!;
            if (newestOverall is null || sourceNewest > newestOverall) newestOverall = sourceNewest;

            // "latest" means latest *stable*. A catalogue mirrors a release people actually
            // consume; resolving to a preview would silently pin the catalogue to one.
            NuGetVersion? candidate = pinned is not null
                ? available.FirstOrDefault(v => v == pinned)
                : (requested == "latest" ? available.Where(v => !v.IsPrerelease) : available).Max();

            // Ordered by NuGetVersion rather than by position in the feed's answer: SemVer ordering
            // is not string ordering, and taking the last element of a list was only ever correct
            // because one feed happened to return it sorted.
            if (candidate is not null && (best is null || candidate > best.Version))
                best = new Resolved(candidate, repository);
        }

        if (best is null)
        {
            // Three different failures, and telling them apart is the whole value of the message.
            // "Not on this source at all" is the one a private feed makes common, and answering it
            // with "use latest-any" — which cannot help, since there is nothing to fall back to —
            // sends the reader to change a switch instead of looking at their sources.
            Console.Error.WriteLine(
                newestOverall is null
                    ? $"{packageId} was not found on any configured source"
                    : pinned is not null
                        ? $"{packageId} {requested} was not found on any configured source; " +
                          $"the newest available is {newestOverall.ToNormalizedString()}"
                        : $"{packageId} has no stable version on any configured source " +
                          $"(newest is {newestOverall.ToNormalizedString()}, a prerelease); " +
                          "use latest-any or an explicit version");

            return null;
        }

        Console.WriteLine($"resolved {packageId} => {best.Version.ToNormalizedString()}" +
                          (newestOverall is null || best.Version == newestOverall
                               ? ""
                               : $" (newest overall is {newestOverall.ToNormalizedString()}, a prerelease)"));

        return best;
    }

    private sealed record Resolved(NuGetVersion Version, SourceRepository Repository);
}
