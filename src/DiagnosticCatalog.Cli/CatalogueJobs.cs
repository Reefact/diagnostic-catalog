using CatalogGen;

namespace DiagnosticCatalog.Cli;

/// <summary>
/// Turns a validated command line into the catalogues it names.
/// </summary>
/// <remarks>
/// Shared by <c>generate</c> and <c>validate</c> because they read the same command line and differ
/// only in what they do with the answer. A second copy would be a second place for the manifest's
/// path resolution to drift.
/// </remarks>
internal static class CatalogueJobs
{
    /// Null when the manifest could not be read, which the caller reports as a failure. Everything
    /// else has already been refused by the settings' own validation.
    internal static async Task<IReadOnlyList<Job>?> ReadAsync(
        CatalogueSettings settings, CancellationToken cancellation)
    {
        IReadOnlyList<Job> jobs;
        try
        {
            jobs = await FromAsync(settings, cancellation);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or System.Text.Json.JsonException or ManifestException)
        {
            // A manifest that cannot be read or does not parse is the caller's file, not a defect
            // here: report the reason on one line rather than a stack trace.
            // A ManifestException already names the file and the entry, so prefixing the path
            // again would say it twice.
            Console.Error.WriteLine(ex is ManifestException
                                        ? $"error: {ex.Message}"
                                        : $"error: {settings.Manifest}: {ex.Message}");

            return null;
        }

        if (jobs.Count == 0)
        {
            Console.Error.WriteLine("error: the manifest declares no catalogue.");

            return null;
        }

        return jobs;
    }

    private static async Task<IReadOnlyList<Job>> FromAsync(
        CatalogueSettings settings, CancellationToken cancellation)
    {
        if (settings.Manifest is not null)
        {
            string path = Path.GetFullPath(settings.Manifest);
            IReadOnlyList<Job> jobs = CatalogRun.JobsFromManifest(await File.ReadAllTextAsync(path, cancellation), path);
            Console.WriteLine($"manifest {settings.Manifest}: {jobs.Count} catalogue(s)");

            return jobs;
        }

        // Validation has already established that exactly one source is named, so "not the other
        // two" is enough to identify it here.
        bool fromFeed = settings.Assemblies.Length == 0 && settings.Nupkg is null;

        return
        [
            new Job(
                Package: fromFeed ? settings.Package : null,
                Version: fromFeed ? settings.PackageVersion : null,
                Namespace: settings.Namespace!,
                Container: settings.Container!,
                Output: Path.GetFullPath(settings.Output!),
                Language: settings.Language,
                Assemblies: settings.Assemblies.Length > 0 ? settings.Assemblies : null,
                SourceName: settings.SourceName,
                SourceVersion: settings.SourceVersion,
                Nupkg: settings.Nupkg is null ? null : Path.GetFullPath(settings.Nupkg),
                Source: settings.Source),
        ];
    }
}
