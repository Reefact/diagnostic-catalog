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
        Console.WriteLine();
        string qualified = rule.Container.Length > 0 ? $"{rule.Container}.{rule.Id}" : rule.Id;
        Console.WriteLine("[SuppressMessage(");
        Console.WriteLine($"    {qualified}.Category,");
        Console.WriteLine($"    {qualified}.Id,");
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
