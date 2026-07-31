using System.ComponentModel;
using Spectre.Console.Cli;

namespace DiagnosticCatalog.Cli;

/// <summary>
/// What <c>dcat generate</c> accepts: a source, a destination, and the one option that only
/// matters when something is written.
/// </summary>
internal sealed class GenerateSettings : CatalogueSettings
{
    [CommandOption("--date <yyyy-MM-dd>")]
    [Description("The generation date to stamp. Pin it to make regenerating the same inputs byte-identical.")]
    public string? Date { get; init; }
}
