using System.ComponentModel;
using CatalogGen;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DiagnosticCatalog.Cli;

/// <summary>
/// What a command that reads a source into a catalogue accepts.
/// </summary>
/// <remarks>
/// <para>
/// A run names a <em>source</em> and a <em>destination</em>, or a manifest that carries both for
/// several catalogues at once. The source is a package to fetch, a package on disk, a project you
/// build, or assemblies already built, and naming more than one is refused rather than resolved by
/// precedence — see <see cref="Validate"/>.
/// </para>
/// <para>
/// The upstream release is <c>--package-version</c> rather than <c>--version</c>. On a .NET tool
/// <c>--version</c> is universally read as "which version of the tool am I running", and a switch
/// that answered a different question under the name everybody already knows would be a trap laid
/// for the first user. <see cref="CliApplication"/> gives <c>--version</c> its conventional meaning.
/// </para>
/// </remarks>
internal abstract class CatalogueSettings : CommandSettings
{
    /// <summary>The release <c>--package-version</c> resolves to when nobody names one.</summary>
    /// <remarks>
    /// A constant rather than three spellings, because the default is written three times — the
    /// attribute the parser reads, the initialiser the property carries, and the comparison that
    /// tells a typed value from an omitted one. Two of them drifting apart would make a manifest
    /// run refuse itself.
    /// </remarks>
    private const string LatestStable = "latest";

    /// <summary>The language <c>--language</c> resolves to when nobody names one.</summary>
    private const string DefaultLanguage = "cs";

    [CommandOption("--manifest <PATH>")]
    [Description("Generate every catalogue declared in a manifest. Paths inside it are relative to the manifest.")]
    public string? Manifest { get; init; }

    [CommandOption("--package <ID>")]
    [Description("The NuGet package whose analyzers to read.")]
    public string? Package { get; init; }

    [CommandOption("--package-version <VERSION>")]
    [Description("Which release of --package to read: an exact version, 'latest' (latest stable) or 'latest-any'.")]
    [DefaultValue(LatestStable)]
    public string PackageVersion { get; init; } = LatestStable;

    [CommandOption("--source <NAME-OR-URL>")]
    [Description("Which configured feed to read --package from. Defaults to every enabled source in NuGet.config.")]
    public string? Source { get; init; }

    [CommandOption("--nupkg <PATH>")]
    [Description("A .nupkg already on disk. Its .nuspec names the source unless you say otherwise.")]
    public string? Nupkg { get; init; }

    [CommandOption("--project <PATH>")]
    [Description("A project that produces analyzers, already built. Repeat to read several together.")]
    public string[] Projects { get; init; } = [];

    [CommandOption("--solution <PATH>")]
    [Description("A solution; reads the projects in it that declare ProducesDiagnosticRules. Already built.")]
    public string? Solution { get; init; }

    [CommandOption("--configuration <NAME>")]
    [Description("Which configuration of --project to read. Defaults to Release.")]
    public string? Configuration { get; init; }

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
    [Description("Which language's analyzers to read out of a package. Only cs can be read today.")]
    [DefaultValue(DefaultLanguage)]
    public string Language { get; init; } = DefaultLanguage;

    [CommandOption("--source-name <NAME>")]
    [Description("What to record as the source. Defaults to the package's id, the project's assembly name, or the first assembly's.")]
    public string? SourceName { get; init; }

    [CommandOption("--source-version <VERSION>")]
    [Description("What to record as the source's release. Defaults to the package's version, the project's, or the assembly's.")]
    public string? SourceVersion { get; init; }

    [CommandOption("--summary <PATH>")]
    [Description("Write a Markdown report of what changed, for a pull request body to carry.")]
    public string? Summary { get; init; }

    /// <summary>
    /// Refuses a command line that names no source, no destination, or two sources.
    /// </summary>
    public override ValidationResult Validate()
    {
        List<string> named = NamedSources();

        if (Manifest is not null)
        {
            // A manifest carries every source AND every destination itself, so anything naming one
            // alongside it is a command line that contradicts its own input.
            //
            // Both halves, and the second is the one that cost something: this branch used to
            // return after checking the sources, so --source, --namespace, --container, --output,
            // --configuration and the rest were accepted and then read from nowhere. A run meant
            // for a private feed resolved against nuget.org and said nothing about it — the exact
            // shape SwitchesMatchTheSource refuses further down, on the reasoning that a caller who
            // passed one believes it took effect.
            List<string> ignored = [.. named, .. SwitchesTheManifestCarries()];

            return ignored.Count > 0
                ? ValidationResult.Error(
                    $"--manifest already carries its sources and destinations; drop {string.Join(" and ", ignored)}.")
                : ValidationResult.Success();
        }

        ValidationResult source = OneSourceAndSomewhereToWriteIt(named);

        return source.Successful ? SwitchesMatchTheSource() : source;
    }

    /// <summary>
    /// The switches a manifest already states, named as the caller wrote them.
    /// </summary>
    /// <remarks>
    /// <c>--package-version</c> and <c>--language</c> are compared with their defaults rather than
    /// tested for presence, and they have to be: they carry one, so a caller who omitted them and a
    /// caller who typed the default are the same command line by the time this runs. Refusing them
    /// by presence would refuse every manifest run there is. What is left is a value the caller can
    /// only have typed.
    /// <para>
    /// <c>--summary</c> and <c>--date</c> are absent from this list on purpose. They say what the
    /// RUN does — where its report goes, what date it stamps — rather than what a catalogue is, so
    /// a manifest states neither and both are meaningful beside one.
    /// </para>
    /// </remarks>
    private List<string> SwitchesTheManifestCarries()
    {
        List<string> carried = [];
        if (PackageVersion != LatestStable) carried.Add("--package-version");
        if (Source is not null) carried.Add("--source");
        if (Namespace is not null) carried.Add("--namespace");
        if (Container is not null) carried.Add("--container");
        if (Output is not null) carried.Add("--output");
        if (Language != DefaultLanguage) carried.Add("--language");
        if (SourceName is not null) carried.Add("--source-name");
        if (SourceVersion is not null) carried.Add("--source-version");
        if (Configuration is not null) carried.Add("--configuration");

        return carried;
    }

    /// <summary>The source switches this command line carries, named as the caller wrote them.</summary>
    private List<string> NamedSources()
    {
        List<string> named = [];
        if (Package is not null) named.Add("--package");
        if (Nupkg is not null) named.Add("--nupkg");
        if (Projects.Length > 0) named.Add("--project");
        if (Solution is not null) named.Add("--solution");
        if (Assemblies.Length > 0) named.Add("--assembly");

        return named;
    }

    /// <summary>Exactly one source, and the three switches that say where the result goes.</summary>
    private ValidationResult OneSourceAndSomewhereToWriteIt(List<string> named)
    {
        // Refused rather than resolved by precedence: each names a source, and picking one silently
        // would generate a catalogue from something the caller did not ask for.
        if (named.Count > 1)
        {
            return ValidationResult.Error($"{string.Join(" and ", named)} name different sources; give one.");
        }

        if (named.Count == 0)
        {
            return ValidationResult.Error(
                "nothing to read: give --package, --nupkg, --project, --solution, --assembly or --manifest.");
        }

        // Checked apart from the source so the message says which half is missing.
        List<string> missing = [];
        if (Namespace is null) missing.Add("--namespace");
        if (Container is null) missing.Add("--container");
        if (Output is null) missing.Add("--output");

        return missing.Count > 0
            ? ValidationResult.Error($"nowhere to write the catalogue: {string.Join(", ", missing)} missing.")
            : ValidationResult.Success();
    }

    /// <summary>
    /// The switches that only mean something against one kind of source, refused rather than
    /// ignored when they are passed against another.
    /// </summary>
    private ValidationResult SwitchesMatchTheSource()
    {
        // Reported rather than ignored: a caller who passed one of these with --package believes it
        // took effect, and a catalogue whose recorded source silently came from somewhere else is
        // exactly the drift a recorded source exists to prevent. A .nupkg on disk accepts them —
        // its .nuspec is only a default there, and a file can have been renamed or rebuilt.
        if (Package is not null && (SourceName is not null || SourceVersion is not null))
        {
            return ValidationResult.Error(
                "--source-name and --source-version describe a source on disk; a feed states its own.");
        }

        // Same reason, the other way round: a caller who names a feed for a source that is not a
        // feed believes it took effect. Nothing would be read from it, and nothing would say so.
        if (Source is not null && Package is null)
        {
            return ValidationResult.Error("--source selects a feed for --package; it means nothing for the others.");
        }

        // Refused here rather than discovered at the end. A language this tool cannot read used to be
        // accepted, resolve a package, download it, read hundreds of descriptors and only then refuse
        // on the analyzers that would not load — a promise kept right up to the point of breaking it.
        if (!CatalogLanguages.CanRead(Language))
        {
            return ValidationResult.Error(CatalogLanguages.Refusal(Language));
        }

        // And again: a configuration selects among a project's build outputs. Against an assembly
        // path it would be silently discarded, having been passed precisely by someone who believed
        // it selected which build was read.
        if (Configuration is not null && Projects.Length == 0 && Solution is null)
        {
            return ValidationResult.Error(
                "--configuration selects a build of --project or --solution; it means nothing for the others.");
        }

        return ValidationResult.Success();
    }
}
