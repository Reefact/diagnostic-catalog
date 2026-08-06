using System.ComponentModel;
using CatalogGen;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DiagnosticCatalog.Cli;

/// <summary>
/// What <c>dcat list</c> and <c>dcat explain</c> read: one compiled catalogue.
/// </summary>
internal class CatalogueFileSettings : CommandSettings
{
    [CommandArgument(0, "<CATALOGUE>")]
    [Description("The compiled catalogue assembly to read, for example a package's DiagnosticCatalog.Sonar.dll.")]
    public string Catalogue { get; init; } = "";

    public override ValidationResult Validate()
        => string.IsNullOrWhiteSpace(Catalogue)
               ? ValidationResult.Error("name the catalogue assembly to read.")
               : ValidationResult.Success();
}

/// <summary>
/// What <c>dcat explain</c> reads, plus the rule it is asked about.
/// </summary>
internal sealed class ExplainSettings : CatalogueFileSettings
{
    [CommandArgument(1, "<RULE-ID>")]
    [Description("The rule to explain, for example SA1000.")]
    public string RuleId { get; init; } = "";

    public override ValidationResult Validate()
    {
        ValidationResult catalogue = base.Validate();
        if (!catalogue.Successful) return catalogue;

        return string.IsNullOrWhiteSpace(RuleId)
                   ? ValidationResult.Error("name the rule to explain.")
                   : ValidationResult.Success();
    }
}

/// <summary>
/// <c>dcat list</c> — what is in this catalogue.
/// </summary>
internal sealed class ListCommand : Command<CatalogueFileSettings>
{
    protected override int Execute(
        CommandContext context, CatalogueFileSettings settings, CancellationToken cancellationToken)
    {
        CatalogueContents? contents = CatalogueInspector.Read(settings.Catalogue);
        if (contents is null) return ExitCodes.Failure;

        InspectOutput.WriteProvenance(contents);

        foreach (CataloguedRule rule in contents.Rules)
            Console.WriteLine($"{rule.Id,-12} {rule.Category}{(rule.Retired ? "  [retired]" : "")}");

        Console.WriteLine();
        Console.WriteLine($"{contents.Rules.Count} rule(s), " +
                          $"{contents.Rules.Select(r => r.Category).Distinct(StringComparer.Ordinal).Count()} category(ies)");

        return ExitCodes.Success;
    }
}

/// <summary>
/// <c>dcat explain</c> — what this one rule is, and what to write to suppress it.
/// </summary>
internal sealed class ExplainCommand : Command<ExplainSettings>
{
    protected override int Execute(
        CommandContext context, ExplainSettings settings, CancellationToken cancellationToken)
    {
        CatalogueContents? contents = CatalogueInspector.Read(settings.Catalogue);
        if (contents is null) return ExitCodes.Failure;

        CataloguedRule? rule = contents.Rules.FirstOrDefault(
            r => string.Equals(r.Id, settings.RuleId, StringComparison.OrdinalIgnoreCase));
        if (rule is null)
        {
            // Naming the catalogue as well as the rule, because the likeliest mistake is asking the
            // right question of the wrong catalogue.
            Console.Error.WriteLine(
                $"{settings.RuleId} is not in {Path.GetFileName(Path.GetFullPath(settings.Catalogue))}");

            return ExitCodes.Failure;
        }

        InspectOutput.WriteProvenance(contents);
        Console.WriteLine($"id        {rule.Id}");
        Console.WriteLine($"category  {rule.Category}");
        if (rule.HelpLinkUri.Length > 0) Console.WriteLine($"help      {rule.HelpLinkUri}");
        if (rule.Retired) Console.WriteLine("state     retired upstream — kept as [Obsolete]");

        // The point of the catalogue, spelled out: this is the line the reader came for, and it is
        // the one that is worth copying rather than retyping from memory.
        //
        // Written so that it depends on NOTHING already being in the reader's file. Every name is
        // fully qualified from the global namespace — the attribute included, which lives in
        // System.Diagnostics.CodeAnalysis and is imported in far fewer files than the ones that
        // suppress something. A fragment needing two using directives to build is one that fails on
        // arrival for most of the people who copy it, and it fails as a name that "does not exist"
        // rather than as anything pointing back here.
        //
        // The rule's own segment is its TYPE name, not its identifier. They are the same string for
        // almost every rule, which is why writing the identifier read as correct — but §8.2's
        // blessed exception, an identifier C# will not accept as a type name, leaves the two apart:
        // "MeridianRule.MRD-0100.Category" is not C#, and this is the one line this command exists
        // to produce. CataloguedRule.Reference carries the whole spelling, escaping included.
        Console.WriteLine();
        Console.WriteLine("[global::System.Diagnostics.CodeAnalysis.SuppressMessage(");
        Console.WriteLine($"    {rule.Reference}.Category,");
        Console.WriteLine($"    {rule.Reference}.Id,");
        Console.WriteLine("    Justification = \"…\")]");

        return ExitCodes.Success;
    }
}

internal static class InspectOutput
{
    // A catalogue is a snapshot, and how old it is decides whether its answer can be trusted — so
    // it is stated before the answer rather than left to be looked up.
    internal static void WriteProvenance(CatalogueContents contents)
    {
        if (contents.Source is null) return;

        Console.WriteLine($"{contents.Source} {contents.SourceVersion}" +
                          (contents.GeneratedOn is null ? "" : $", generated {contents.GeneratedOn}"));
        Console.WriteLine();
    }
}
