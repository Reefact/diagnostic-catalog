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
/// The converse — every option the tool exposes is documented — is not checked yet. It needs a
/// single page that carries the obligation, and the <c>dcat</c> reference page does not exist. An
/// obligation spread across every document that happens to mention the tool is one no document can
/// discharge.
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

        foreach (Match option in Regex.Matches(document.Text, OptionPattern, RegexOptions.None, MatchTimeout))
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
