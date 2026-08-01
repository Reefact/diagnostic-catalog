using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every command <c>dcat</c> registers appears in <c>doc/guide/dcat-reference</c>, and every command
/// the documentation shows is one the tool registers.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CliOptionTests"/> covers the flags and stops there, so the command tree — the first
/// thing a reader of the reference meets, and the coarsest thing the tool publishes — was the one
/// part of the CLI a change could move without any check noticing. A fifth command could ship with
/// the reference's table untouched and every test in this project stay green.
/// </para>
/// <para>
/// The names are read from the registrations in <c>CliApplication.Configure</c>, because that is the
/// only place they exist: Spectre takes the name as a string argument and records it in no metadata
/// reflection can reach. Reading the source is not the compromise it would be for an option — a
/// registration is the code that runs, not prose describing it — but a regex over source is brittle
/// in a way compiled metadata is not, so it is tied back to the assembly: the number of names found
/// must equal the number of concrete Spectre command types the tool compiles. A rewrite of
/// <c>Configure</c> that defeats the pattern makes the two disagree and says so, rather than quietly
/// checking nothing.
/// </para>
/// <para>
/// The Spectre base class is matched by name rather than referenced, exactly as
/// <see cref="CliOptionTests"/> reads its attributes through <see cref="CustomAttributeData"/>: a
/// documentation check should not carry a compile-time dependency on whichever version of
/// Spectre.Console.Cli happens to be pinned.
/// </para>
/// </remarks>
public sealed class CliCommandTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    private const string Reference = "doc/guide/dcat-reference.{0}.md";

    private static readonly Lazy<SortedSet<string>> Registered = new(ReadRegistrations);

    public static TheoryData<string, string> RegisteredByLanguage()
    {
        TheoryData<string, string> data = [];
        foreach (string command in Registered.Value)
        {
            data.Add(command, "en");
            data.Add(command, "fr");
        }

        return data;
    }

    public static TheoryData<string> DocumentsMentioningTheTool()
    {
        TheoryData<string> paths = [];
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
    [MemberData(nameof(RegisteredByLanguage))]
    public void Every_command_the_tool_registers_is_documented(string command, string language)
    {
        MarkdownDocument reference = Repository.Require(string.Format(Reference, language));

        Assert.True(
            Regex.IsMatch(
                reference.Text,
                "dcat\\s+" + Regex.Escape(command) + "(?![a-z0-9-])",
                RegexOptions.None,
                MatchTimeout),
            $"dcat registers the '{command}' command and {reference.Path} never shows it. A command " +
            "nobody wrote down is a command nobody can know exists, and the only signal is nobody " +
            "using it.");
    }

    [Theory]
    [MemberData(nameof(DocumentsMentioningTheTool))]
    public void Every_command_a_document_shows_is_registered(string path)
    {
        MarkdownDocument document = Repository.Require(path);
        SortedSet<string> registered = Registered.Value;

        foreach (string shown in CommandsShownIn(document))
        {
            Assert.True(
                registered.Contains(shown),
                $"{path} shows 'dcat {shown}', which the command tree does not register. A reader " +
                "who types it is told there is no such command and assumes they mistyped it. " +
                $"Registered: {string.Join(", ", registered)}.");
        }
    }

    [Fact]
    public void The_commands_are_discovered()
    {
        SortedSet<string> registered = Registered.Value;

        Assert.True(
            registered.Count >= 4,
            $"Only {registered.Count} commands were read from CliApplication. Check that the " +
            "registrations still read AddCommand<T>(\"name\").");

        Assert.Contains("generate", registered);

        SortedSet<string> compiled = CompiledCommandTypes();

        // Not equality: registering one command type under a second name is an alias, which is a
        // legitimate thing to do and would make a strict count wrong. The direction that matters is
        // the other one — fewer names than types means the pattern missed a registration, and a
        // command the pattern cannot see is a command nothing checks against the documentation.
        Assert.True(
            registered.Count >= compiled.Count,
            $"Only {registered.Count} command registrations were read from the source, and the " +
            $"assembly compiles {compiled.Count} concrete Spectre command types " +
            $"({string.Join(", ", compiled)}). The pattern has missed a registration, so at least one " +
            "command is going unchecked against the documentation.");
    }

    /// <summary>
    /// The command names a document shows, under the two spellings a command is ever written in: a
    /// backticked span, and a line of a fenced block.
    /// </summary>
    /// <remarks>
    /// A looser pattern is wrong, and provably so — the navigation footers read "← The dcat tool"
    /// and "← The dcat reference", so anything matching <c>dcat</c> followed by a word reports two
    /// commands the tool has never had. The backtick and the start of the line are what separate a
    /// command from a sentence that happens to mention the tool.
    /// </remarks>
    private static SortedSet<string> CommandsShownIn(MarkdownDocument document)
    {
        SortedSet<string> shown = new(StringComparer.Ordinal);

        foreach (string pattern in new[] { "`dcat\\s+(?<name>[a-z][a-z0-9-]*)", "^dcat\\s+(?<name>[a-z][a-z0-9-]*)" })
        {
            foreach (Match command in Regex.Matches(
                         document.Text,
                         pattern,
                         RegexOptions.Multiline,
                         MatchTimeout))
            {
                shown.Add(command.Groups["name"].Value);
            }
        }

        return shown;
    }

    private static SortedSet<string> ReadRegistrations()
    {
        SortedSet<string> commands = new(StringComparer.Ordinal);

        string path = Path.Combine(Repository.Root, "src", "DiagnosticCatalog.Cli", "CliApplication.cs");
        if (!File.Exists(path)) return commands;

        foreach (Match registration in Regex.Matches(
                     File.ReadAllText(path),
                     "AddCommand<[A-Za-z0-9_]+>\\(\\s*\"(?<name>[a-z][a-z0-9-]*)\"\\s*\\)",
                     RegexOptions.None,
                     MatchTimeout))
        {
            commands.Add(registration.Groups["name"].Value);
        }

        return commands;
    }

    /// <summary>
    /// The concrete command types the tool compiles — what the registrations above are counted
    /// against.
    /// </summary>
    private static SortedSet<string> CompiledCommandTypes()
    {
        SortedSet<string> commands = new(StringComparer.Ordinal);

        // `dcat`, not `DiagnosticCatalog.Cli`: the assembly is named for what a user types (ADR-0017).
        Assembly cli = Assembly.Load(new AssemblyName("dcat"));

        foreach (Type type in cli.GetTypes())
        {
            // An abstract command cannot be registered, so counting it would make the two sides
            // disagree about a type that was never a command.
            if (type.IsAbstract) continue;
            if (!DerivesFromSpectreCommand(type)) continue;

            commands.Add(type.Name);
        }

        return commands;
    }

    private static bool DerivesFromSpectreCommand(Type type)
    {
        for (Type? current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (!string.Equals(current.Namespace, "Spectre.Console.Cli", StringComparison.Ordinal)) continue;

            if (current.Name is "Command" or "AsyncCommand" or "Command`1" or "AsyncCommand`1")
            {
                return true;
            }
        }

        return false;
    }

}
