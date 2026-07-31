using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every <c>--option</c> the documentation shows is one <c>dcat</c> actually accepts.
/// </summary>
/// <remarks>
/// <para>
/// A documented flag that the tool does not have is worse than an undocumented one. The reader types
/// it, the command fails with a parse error, and the natural conclusion is that they got something
/// else wrong — the documentation is the last thing anyone suspects. Renaming an option is exactly
/// the change that produces it, and nothing in a build reads prose.
/// </para>
/// <para>
/// The truth is read off the compiled settings types rather than off the source, because that is
/// what the tool parses arguments with. ADR-0009 sets the same standard for catalogue content: the
/// descriptors are what the analyzer reports with, so they are what a claim about them is checked
/// against.
/// </para>
/// <para>
/// Read through <see cref="CustomAttributeData"/> and by assembly name, so this test needs neither
/// an internals grant from the CLI — every settings type there is <c>internal</c>, and widening that
/// for a documentation check would be the check changing the code — nor a compile-time dependency on
/// the version of Spectre.Console.Cli that happens to be pinned.
/// </para>
/// <para>
/// The converse is checked too, and it needs somewhere to land: an obligation spread across every
/// document that mentions the tool is one no document can discharge. <c>doc/guide/dcat-reference</c>
/// carries it, in both languages, so an option added without documentation fails rather than waiting
/// for a user to ask what it does.
/// </para>
/// </remarks>
public sealed class CliOptionTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Long options only. A short one is a single letter and would match ordinary prose — an
    /// em-dashed aside, a hyphenated word at a line break — turning a check into noise.
    /// </summary>
    private const string OptionPattern = "(?<!\\w)--(?<name>[a-z][a-z0-9-]{2,})";

    /// <summary>
    /// Flags that belong to other tools and appear in the documentation because a reader runs them
    /// beside <c>dcat</c>, or because a contributor document quotes a build or shell command. They
    /// are named here rather than filtered by a pattern, so that adding one is a decision somebody
    /// wrote down.
    /// </summary>
    private static readonly HashSet<string> Foreign = new(StringComparer.Ordinal)
    {
        // dotnet CLI
        "global", "framework", "no-restore", "no-build", "interactive", "verbosity", "runtime",
        "logger", "collect", "settings", "results-directory", "no-logo", "info", "list-sdks",
        // git
        "no-verify", "force-with-lease", "oneline", "amend", "autosquash", "allow-empty", "stat",
        "grep", "no-merges", "first-parent",
        // shell tooling quoted in the contributor documents
        "proto", "proto-redir", "check", "strict", "no-run-if-empty", "color", "version-sort",
        "quiet", "silent", "recursive",
    };

    /// <summary>
    /// Options the command tree declares rather than a settings type: Spectre wires <c>--help</c>
    /// and <c>--version</c> from the configuration, so they carry no <c>[CommandOption]</c> anywhere
    /// and reflection cannot see them. <c>--version</c> is the one the documentation leans on
    /// hardest — it is why the upstream release is <c>--package-version</c> — so it is named here
    /// rather than filtered away as somebody else's flag.
    /// </summary>
    private static readonly string[] DeclaredByTheApplication = ["help", "version"];

    public static TheoryData<string> DocumentsMentioningTheTool()
    {
        TheoryData<string> paths = new();
        foreach (MarkdownDocument document in Repository.Documents)
        {
            if (document.Text.Contains("dcat ", StringComparison.Ordinal))
            {
                paths.Add(document.Path);
            }
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(DocumentsMentioningTheTool))]
    public void Every_documented_option_exists_on_the_tool(string path)
    {
        MarkdownDocument document = Repository.Require(path);
        IReadOnlyCollection<string> declared = DeclaredOptions();

        foreach (Match option in Regex.Matches(Addressable(document), OptionPattern, RegexOptions.None, MatchTimeout))
        {
            string name = option.Groups["name"].Value;
            if (Foreign.Contains(name)) continue;
            if (declared.Contains(name)) continue;

            Assert.Fail(
                $"{path} documents --{name}, which no dcat settings type declares. A reader who " +
                "types it gets a parse error and blames themselves. Declared options: " +
                $"{string.Join(", ", declared.Select(declaredName => "--" + declaredName))}.");
        }
    }

    /// <summary>
    /// Guards the theory above against accepting anything at all. Were the CLI assembly to stop
    /// being reachable, every documented option would be "not declared" — or, if the set were read
    /// as empty and the theory written the other way round, every one would pass. Either way the
    /// check has to say out loud that it found the tool.
    /// </summary>
    public static TheoryData<string> ReferenceLanguages() => new("en", "fr");

    /// <summary>
    /// Every option the tool exposes appears in the reference page. The direction that catches the
    /// commoner mistake: a flag shipped and never written down is met by a reader who cannot know it
    /// exists, and the only signal is nobody using it.
    /// </summary>
    [Theory]
    [MemberData(nameof(ReferenceLanguages))]
    public void Every_option_the_tool_exposes_is_documented(string language)
    {
        MarkdownDocument reference = Repository.Require($"doc/guide/dcat-reference.{language}.md");

        foreach (string option in DeclaredOptions())
        {
            Assert.True(
                Regex.IsMatch(
                    Addressable(reference),
                    "(?<!\\w)--" + Regex.Escape(option) + "(?![\\w-])",
                    RegexOptions.None,
                    MatchTimeout),
                $"dcat accepts --{option} and doc/guide/dcat-reference.{language}.md never mentions " +
                "it. A flag nobody wrote down is a flag nobody can know exists.");
        }
    }

    [Fact]
    public void The_tool_options_are_discovered()
    {
        IReadOnlyCollection<string> declared = DeclaredOptions();

        Assert.True(
            declared.Count >= 10,
            $"Only {declared.Count} options were read off the dcat settings types. Check that " +
            "DiagnosticCatalog.Cli is still referenced by this test project and lands beside it.");

        Assert.Contains("manifest", declared);
        Assert.Contains("package-version", declared);
    }

    /// <summary>
    /// The long name of every <c>[CommandOption]</c> the CLI assembly declares. The attribute's sole
    /// constructor argument is Spectre's own template — <c>"-s|--source &lt;NAME&gt;"</c> — so the
    /// long names are the tokens in it that begin with two hyphens.
    /// </summary>
    private static IReadOnlyCollection<string> DeclaredOptions()
    {
        SortedSet<string> options = new(StringComparer.Ordinal);
        foreach (string option in DeclaredByTheApplication)
        {
            options.Add(option);
        }

        // `dcat`, not `DiagnosticCatalog.Cli`: the project is named for what it is and the assembly
        // for what a user types, which is the tool command name the package installs (ADR-0017).
        Assembly cli = Assembly.Load(new AssemblyName("dcat"));

        foreach (Type type in cli.GetTypes())
        {
            foreach (PropertyInfo property in type.GetProperties(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                foreach (string name in LongNamesOf(property))
                {
                    options.Add(name);
                }
            }
        }

        return options;
    }

    /// <summary>
    /// The document with its link targets removed, which is what an option may be looked for in.
    /// </summary>
    /// <remarks>
    /// A heading that names an option produces an anchor that begins with it —
    /// <c>#--solution-and-why-it-needs-a-declaration</c> for "`--solution`, and why it needs a
    /// declaration" — and a scanner reading raw text takes the whole slug for a flag. Stripping the
    /// targets is the fix rather than renaming the headings: the collision belongs to every heading
    /// that will ever name an option, and only one of the two places can be made not to recur.
    /// </remarks>
    private static string Addressable(MarkdownDocument document) =>
        Regex.Replace(
            Regex.Replace(document.Text, "\\]\\([^)]*\\)", "]()", RegexOptions.None, MatchTimeout),
            "href=\"[^\"]*\"",
            "href=\"\"",
            RegexOptions.None,
            MatchTimeout);

    /// <summary>
    /// The long option names a property declares, or nothing when it declares no option. The
    /// attribute's sole constructor argument is Spectre's own template — <c>"-s|--source
    /// &lt;NAME&gt;"</c> — so the long names are the tokens in it that begin with two hyphens.
    /// </summary>
    private static IEnumerable<string> LongNamesOf(PropertyInfo property)
    {
        foreach (CustomAttributeData attribute in property.GetCustomAttributesData())
        {
            if (!string.Equals(attribute.AttributeType.Name, "CommandOptionAttribute", StringComparison.Ordinal))
            {
                continue;
            }

            if (attribute.ConstructorArguments.Count == 0) continue;
            if (attribute.ConstructorArguments[0].Value is not string template) continue;

            foreach (Match name in Regex.Matches(template, OptionPattern, RegexOptions.None, MatchTimeout))
            {
                yield return name.Groups["name"].Value;
            }
        }
    }

}
