namespace DiagnosticCatalog.Cli;

/// <summary>
/// What <c>dcat validate</c> accepts — the same source and destination as <c>generate</c>, and
/// nothing more.
/// </summary>
/// <remarks>
/// It carries no <c>--date</c>, and the absence is the point: validation compares the rules a
/// catalogue publishes against the ones its source declares, and the generation date is precisely
/// the field that moves without any rule moving. Accepting a switch that could not affect the
/// answer would suggest it could.
/// </remarks>
internal sealed class ValidateSettings : CatalogueSettings
{
}
