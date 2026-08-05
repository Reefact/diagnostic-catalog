using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Xunit;

namespace DiagnosticCatalog.Documentation.UnitTests;

/// <summary>
/// Every rule a sample declares — in the XML documentation that ships inside the packages, or in a
/// Markdown code fence — satisfies §8.5, the requirement that <c>Category</c> reach a constant
/// declared in a <c>[DiagnosticCategory]</c> class, reported as <c>DCAT0011</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the second check whose absence had already cost something, and it cost it on the surface
/// where it reads worst. The <c>&lt;example&gt;</c> on <c>DiagnosticRuleAttribute</c> — the type
/// whose whole subject is the contract — declared <c>Category</c> from a string literal, which is
/// precisely what <c>DCAT0011</c> reports. So did every rule the specification showed outside §8.5
/// itself: the document that states the requirement spent twenty-two samples, across both
/// languages, not meeting it.
/// </para>
/// <para>
/// Neither went noticed because nothing could see them. <see cref="CatalogueSampleTests"/> reads
/// Markdown, but it asks whether a rule a sample NAMES exists, never what a sample DECLARES. And
/// the XML documentation is not Markdown at all: it is C#, it ships as <c>DiagnosticCatalog.xml</c>
/// inside the package, and an IDE renders it on hover without ever compiling it.
/// </para>
/// <para>
/// One check over both, because the requirement is one requirement and a reader copying a sample
/// does not care which file it came out of. A sample is judged only when its code declares a rule,
/// since <c>DCAT0011</c> only applies to the <c>Category</c> of a <c>[DiagnosticRule]</c> type: the
/// category class beside it declares its own constants from literals, which is where a literal
/// belongs and what makes it the single declaration.
/// </para>
/// </remarks>
public sealed class RuleSampleTests
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A <c>const string Category</c> initialiser, as written in a sample. The value runs to the
    /// semicolon so that an initialiser broken across lines is read whole rather than truncated
    /// into something that would pass.
    /// </summary>
    private const string CategoryInitialiser =
        @"const\s+string\s+Category\s*=\s*(?<initialiser>[^;]+);";

    /// <summary>
    /// What §8.5 accepts: a name. Resolution there is semantic, so every spelling that binds to the
    /// field satisfies it — qualified, aliased, imported by <c>using static</c>. What it refuses is
    /// an initialiser that is constant without being one field reference: a literal, a
    /// <c>nameof</c>, a concatenation. None of those is a name, so none of them matches this.
    /// </summary>
    private const string QualifiedName =
        @"^(global::)?@?[A-Za-z_][A-Za-z0-9_]*(\s*\.\s*@?[A-Za-z_][A-Za-z0-9_]*)*$";

    public static TheoryData<string> FilesShowingRules()
    {
        TheoryData<string> paths = [];
        foreach (string path in Searched())
        {
            if (RuleSamples(path).Count > 0)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    [Theory]
    [MemberData(nameof(FilesShowingRules))]
    public void Every_rule_a_sample_declares_reaches_a_declared_category(string path)
    {
        foreach (string initialiser in RuleSamples(path))
        {
            Assert.True(
                Regex.IsMatch(initialiser, QualifiedName, RegexOptions.None, MatchTimeout),
                $"{path} shows a rule whose Category is `{initialiser}`, which does not reach a " +
                "constant declared in a [DiagnosticCategory] class. A reader who copies the sample " +
                "gets DCAT0011 off the page that taught them the contract.\n" +
                "Declare the category once and refer to it:\n" +
                "  [DiagnosticCategory]\n" +
                "  internal static class JdCategory { public const string Usage = \"Usage\"; }\n" +
                "  ...\n" +
                "  public const string Category = JdCategory.Usage;\n" +
                "A document that declares the container in an earlier fence need not repeat it: " +
                "this reads one sample at a time and asks only what the initialiser is.");
        }
    }

    /// <summary>
    /// Guards the theory against passing on an empty world. It is parameterised by the files it
    /// found samples in, so a reader that stopped matching — a renamed folder, a fence written
    /// another way — would produce no cases at all and a green run that checked nothing. Both
    /// readers are named, because they fail independently and either one going quiet would leave
    /// the other looking like full coverage.
    /// </summary>
    [Fact]
    public void Both_readers_still_find_the_samples_they_are_there_for()
    {
        Assert.True(
            RuleSamples("src/DiagnosticCatalog/DiagnosticRuleAttribute.cs").Count > 0,
            "DiagnosticRuleAttribute no longer documents a rule with an <example>, or the XML "
            + "reader stopped seeing it. That example is the contract's shortest statement; if it "
            + "really went away, this test is what has to change with it.");

        Assert.True(
            RuleSamples("doc/specification.en.md").Count > 5,
            "The specification no longer shows rule declarations, or the Markdown reader stopped "
            + "seeing them. It carried twenty-two of them across both languages when this check "
            + "was written.");
    }

    /// <summary>Every file either reader looks at, whether or not it turns out to show a rule.</summary>
    private static List<string> Searched()
    {
        List<string> paths = [.. Repository.Sources];
        foreach (MarkdownDocument document in Repository.Documents)
        {
            paths.Add(document.Path);
        }

        return paths;
    }

    /// <summary>
    /// The <c>Category</c> initialisers of every rule declared in a sample carried by the file, in
    /// source order.
    /// </summary>
    private static List<string> RuleSamples(string path)
    {
        List<string> initialisers = [];

        foreach (string sample in Samples(path))
        {
            if (!sample.Contains("[DiagnosticRule]", StringComparison.Ordinal)) continue;

            foreach (Match declaration in Regex.Matches(
                         sample,
                         CategoryInitialiser,
                         RegexOptions.None,
                         MatchTimeout))
            {
                initialisers.Add(declaration.Groups["initialiser"].Value.Trim());
            }
        }

        return initialisers;
    }

    /// <summary>
    /// The C# samples a file carries: the <c>&lt;code&gt;</c> elements of a source file's XML
    /// documentation, or the <c>csharp</c> fences of a Markdown document.
    /// </summary>
    private static List<string> Samples(string path)
    {
        if (path.EndsWith(".md", StringComparison.Ordinal))
        {
            MarkdownDocument? document = Repository.Find(path);

            return document is null ? [] : Fenced(document.Text);
        }

        return Repository.Exists(path) ? CodeElements(Repository.ReadSource(path)) : [];
    }

    /// <summary>The bodies of a Markdown document's <c>csharp</c> fences.</summary>
    private static List<string> Fenced(string markdown) =>
        Bodies(markdown, "```csharp\\r?\\n(?<body>.*?)```", RegexOptions.Singleline);

    /// <summary>
    /// The bodies of the <c>&lt;code&gt;</c> elements in a source file's XML documentation.
    /// </summary>
    /// <remarks>
    /// The <c>///</c> prefixes are stripped first, so that a sample is read as the C# a reader sees
    /// rendered rather than as the comment that carries it. Deliberately not an XML parser: a doc
    /// comment is only well-formed XML once it is assembled per file and per member, and the two
    /// questions asked here — where a sample starts, and what a rule inside it declares — do not
    /// need the tree.
    /// </remarks>
    private static List<string> CodeElements(string source)
    {
        string documentation = Regex.Replace(
            source,
            @"^[ \t]*///[ \t]?",
            string.Empty,
            RegexOptions.Multiline,
            MatchTimeout);

        return Bodies(documentation, "<code>(?<body>.*?)</code>", RegexOptions.Singleline);
    }

    private static List<string> Bodies(string text, string pattern, RegexOptions options)
    {
        List<string> bodies = [];
        foreach (Match block in Regex.Matches(text, pattern, options, MatchTimeout))
        {
            bodies.Add(block.Groups["body"].Value);
        }

        return bodies;
    }
}
