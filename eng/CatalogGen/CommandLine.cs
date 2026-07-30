namespace CatalogGen;

// Extracted from Program.cs so it can be exercised by tests: a static local function in a
// top-level-statements program is unreachable from another assembly. Static local functions
// cannot capture, so the move is a relocation and cannot alter behaviour.

internal static class CommandLine
{
    internal static Cli? ParseArgs(string[] args)
    {
        string? package = null, version = null, ns = null, container = null, output = null,
                date = null, language = null, manifest = null, summary = null;
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
                default:
                    Console.Error.WriteLine($"unknown argument: {args[i]}");
                    return null;
            }
        }

        bool singleComplete = package is not null && version is not null && ns is not null
                             && container is not null && output is not null;
        if (manifest is null && !singleComplete)
        {
            Console.Error.WriteLine(
                "usage: --manifest <catalogs.json> [--date yyyy-MM-dd] [--summary out.md]\n" +
                "   or: --package <id> --version <v|latest|latest-any> --namespace <ns> " +
                "--container <name> --output <path.g.cs> [--date yyyy-MM-dd] [--language cs] [--summary out.md]");
            return null;
        }

        // The date may be pinned so that regenerating the same inputs twice is byte-identical.
        // Left unset it defaults to today, which only ever reaches the file when something
        // else changed too.
        return new Cli(package, version, ns, container, output, date, language ?? "cs", manifest, summary);
    }
}
