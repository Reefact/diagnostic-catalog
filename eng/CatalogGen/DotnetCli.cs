namespace CatalogGen;

// Where the `dotnet` host is. Two things here shell out to it — the descriptor worker and MSBuild
// evaluation — and both need the same answer, so it is resolved once rather than in each.
internal static class DotnetCli
{
    // DOTNET_HOST_PATH is set by the SDK and by `dotnet` itself, and is the authoritative answer
    // when present. Failing that, this process may already BE the host — a framework-dependent app
    // launched by `dotnet` reports it as its own path — and failing that, the name on PATH is all
    // that is left. Guessing wrong is survivable and reported: the process simply fails to start.
    internal static string Host()
    {
        string? declared = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(declared) && File.Exists(declared)) return declared;

        string? current = Environment.ProcessPath;
        if (current is not null)
        {
            string name = Path.GetFileNameWithoutExtension(current);
            if (string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase)) return current;
        }

        return "dotnet";
    }
}
