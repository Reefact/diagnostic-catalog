using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DiagnosticCatalog.Cli;

/// <summary>
/// What <c>dcat generate</c> accepts.
/// </summary>
/// <remarks>
/// <para>
/// A run names a <em>source</em> and a <em>destination</em>, or a manifest that carries both for
/// several catalogues at once. The source is a package to fetch or assemblies already on disk, and
/// naming both is refused rather than resolved by precedence — see <see cref="Validate"/>.
/// </para>
/// <para>
/// The upstream release is <c>--package-version</c> rather than <c>--version</c>. On a .NET tool
/// <c>--version</c> is universally read as "which version of the tool am I running", and a switch
/// that answered a different question under the name everybody already knows would be a trap laid
/// for the first user. <see cref="CliApplication"/> gives <c>--version</c> its conventional meaning.
/// </para>
/// </remarks>
internal sealed class GenerateSettings : CommandSettings
{
    [CommandOption("--manifest <PATH>")]
    [Description("Generate every catalogue declared in a manifest. Paths inside it are relative to the manifest.")]
    public string? Manifest { get; init; }

    [CommandOption("--package <ID>")]
    [Description("The NuGet package whose analyzers to read.")]
    public string? Package { get; init; }

    [CommandOption("--package-version <VERSION>")]
    [Description("Which release of --package to read: an exact version, 'latest' (latest stable) or 'latest-any'.")]
    [DefaultValue("latest")]
    public string PackageVersion { get; init; } = "latest";

    [CommandOption("--assembly <PATH>")]
    [Description("An analyzer assembly already on disk. Repeat to read several together.")]
    public string[] Assemblies { get; init; } = [];

    [CommandOption("--namespace <NAMESPACE>")]
    [Description("The namespace the generated catalogue declares.")]
    public string? Namespace { get; init; }

    [CommandOption("--container <NAME>")]
    [Description("The name of the static class holding the rules.")]
    public string? Container { get; init; }

    [CommandOption("--output <PATH>")]
    [Description("Where to write the generated C# source.")]
    public string? Output { get; init; }

    [CommandOption("--language <LANG>")]
    [Description("Which language's analyzers to read out of a package: cs, vb or fs.")]
    [DefaultValue("cs")]
    public string Language { get; init; } = "cs";

    [CommandOption("--source-name <NAME>")]
    [Description("What to record as the source. Defaults to the first assembly's name. Only with --assembly.")]
    public string? SourceName { get; init; }

    [CommandOption("--source-version <VERSION>")]
    [Description("What to record as the source's release. Defaults to the first assembly's version. Only with --assembly.")]
    public string? SourceVersion { get; init; }

    [CommandOption("--date <yyyy-MM-dd>")]
    [Description("The generation date to stamp. Pin it to make regenerating the same inputs byte-identical.")]
    public string? Date { get; init; }

    [CommandOption("--summary <PATH>")]
    [Description("Write a Markdown report of what changed, for a pull request body to carry.")]
    public string? Summary { get; init; }

    /// <summary>
    /// Refuses a command line that names no source, no destination, or two sources.
    /// </summary>
    public override ValidationResult Validate()
    {
        bool fromPackage = Package is not null;
        bool fromAssemblies = Assemblies.Length > 0;

        if (Manifest is not null)
        {
            // A manifest carries every source and destination itself, so anything naming one
            // alongside it is a command line that contradicts its own input.
            return fromPackage || fromAssemblies
                ? ValidationResult.Error("--manifest already names its sources; drop --package and --assembly.")
                : ValidationResult.Success();
        }

        if (fromPackage && fromAssemblies)
        {
            return ValidationResult.Error("--package and --assembly name two different sources; give one.");
        }

        if (!fromPackage && !fromAssemblies)
        {
            return ValidationResult.Error("nothing to read: give --package, --assembly or --manifest.");
        }

        // Checked apart from the source so the message says which half is missing.
        List<string> missing = [];
        if (Namespace is null) missing.Add("--namespace");
        if (Container is null) missing.Add("--container");
        if (Output is null) missing.Add("--output");
        if (missing.Count > 0)
        {
            return ValidationResult.Error($"nowhere to write the catalogue: {string.Join(", ", missing)} missing.");
        }

        // Reported rather than ignored: a caller who passed one of these with --package believes it
        // took effect, and a catalogue whose recorded source silently came from somewhere else is
        // exactly the drift a recorded source exists to prevent.
        if (fromPackage && (SourceName is not null || SourceVersion is not null))
        {
            return ValidationResult.Error(
                "--source-name and --source-version describe assemblies on disk; a package states its own.");
        }

        return ValidationResult.Success();
    }
}
