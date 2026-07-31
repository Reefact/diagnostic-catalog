namespace CatalogGen;

// Title defaults to empty because a rule can genuinely have none to state: one the vendor
// retired before this generator emitted titles at all is carried forward from a file that
// never recorded one, and no later run can recover it — the descriptor it came from is gone.
// The emitter falls back to the identifier and category for those, which is what every rule
// carried before.
internal sealed record RuleInfo(string Category, string HelpLinkUri, bool Retired, string Title = "");

// CategoryNames maps a category's LITERAL to the identifier it was published under — the
// direction the emitter needs to keep an already-published constant's name stable.
internal sealed record Previous(
    string SourceVersion,
    SortedDictionary<string, RuleInfo> Rules,
    SortedDictionary<string, string> CategoryNames);

internal sealed record GenerateResult(bool Changed, string Summary);
