namespace CatalogGen;

// Title defaults to empty because a rule can genuinely have none to state: one the vendor
// retired before this generator emitted titles at all is carried forward from a file that
// never recorded one, and no later run can recover it — the descriptor it came from is gone.
// The emitter falls back to the identifier and category for those, which is what every rule
// carried before.
internal sealed record RuleInfo(string Category, string HelpLinkUri, bool Retired, string Title = "");

// CategoryNames maps a category's LITERAL to the identifier it was published under — the
// direction the emitter needs to keep an already-published constant's name stable.
//
// Published is the whole of what the previous run wrote, in the canonical form
// CatalogEmitter.Canonical defines, and it is what decides whether this run rewrites anything. The
// parsed fields above cannot answer that: a catalogue publishes its namespace, its container class,
// the source it mirrors and the language those analyzers were read for, none of which is a rule and
// every one of which a manifest can move. Comparing the text removes the question of which fields to
// compare — anything the emitter states, it states here.
//
// Empty means "not known", which is what a Previous assembled by hand rather than read off disk
// carries. It compares equal to nothing, so the run regenerates: the safe direction, since the cost
// is a rewrite that changes nothing and the alternative is a catalogue reported current while the
// file says something else.
internal sealed record Previous(
    string SourceVersion,
    SortedDictionary<string, RuleInfo> Rules,
    SortedDictionary<string, string> CategoryNames,
    string Published = "");

internal sealed record GenerateResult(bool Changed, string Summary);
