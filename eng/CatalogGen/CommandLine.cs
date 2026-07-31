namespace CatalogGen;

// Extracted from Program.cs so it can be exercised by tests: a static local function in a
// top-level-statements program is unreachable from another assembly. Static local functions
// cannot capture, so the move is a relocation and cannot alter behaviour.

internal static class CommandLine
{
    internal static Cli? ParseArgs(string[] args)
    {
        string? package = null, version = null, ns = null, container = null, output = null,
                date = null, language = null, manifest = null, summary = null,
                sourceName = null, sourceVersion = null;
        List<string> assemblies = [];
        for (int i = 0; i + 1 < args.Length; i += 2)
        {
            switch (args[i])
            {
                case "--package": package = args[i + 1]; break;
                case "--version": version = args[i + 1]; break;
                case "--namespace": ns = args[i + 1]; break;
                case "--container": container = args[i + 1]; break;
                case "--output": output = args[i + 1]; break;
                case "--date": date = args[i + 1]; break;
                case "--language": language = args[i + 1]; break;
                case "--manifest": manifest = args[i + 1]; break;
                case "--summary": summary = args[i + 1]; break;
                // Repeatable, because a vendor's rules are routinely split across assemblies that
                // must be read together — StyleCop declares its rules across the analyzer and the
                // code-fix assembly. Accumulating rather than overwriting is what lets one catalogue
                // be generated from several.
                case "--assembly": assemblies.Add(args[i + 1]); break;
                case "--source-name": sourceName = args[i + 1]; break;
                case "--source-version": sourceVersion = args[i + 1]; break;
                default:
                    Console.Error.WriteLine($"unknown argument: {args[i]}");
                    return null;
            }
        }

        if (manifest is not null)
            return Build();

        // A catalogue needs somewhere to go and something to read. The two are checked apart so the
        // message says which half is missing.
        bool destinationComplete = ns is not null && container is not null && output is not null;
        bool fromPackage = package is not null && version is not null;
        bool fromAssemblies = assemblies.Count > 0;

        if (fromPackage && fromAssemblies)
        {
            // Refused rather than resolved by precedence: both name a source, and picking one
            // silently would generate a catalogue from something the caller did not ask for.
            Console.Error.WriteLine("--package and --assembly name two different sources; give one");
            return null;
        }

        if (!destinationComplete || !(fromPackage || fromAssemblies))
        {
            Console.Error.WriteLine(
                "usage: --manifest <catalogs.json> [--date yyyy-MM-dd] [--summary out.md]\n" +
                "   or: --package <id> --version <v|latest|latest-any> --namespace <ns> " +
                "--container <name> --output <path.g.cs> [--date yyyy-MM-dd] [--language cs] [--summary out.md]\n" +
                "   or: --assembly <path.dll> [--assembly <path.dll> …] --namespace <ns> " +
                "--container <name> --output <path.g.cs> [--source-name <name>] [--source-version <v>] " +
                "[--date yyyy-MM-dd] [--summary out.md]");
            return null;
        }

        return Build();

        // The date may be pinned so that regenerating the same inputs twice is byte-identical.
        // Left unset it defaults to today, which only ever reaches the file when something
        // else changed too.
        Cli Build() => new(package, version, ns, container, output, date, language ?? "cs", manifest, summary,
                           assemblies, sourceName, sourceVersion);
    }
}
