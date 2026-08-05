using System.Globalization;
using System.Text;

namespace CatalogGen;

// Extracted from Program.cs so it can be exercised by tests: a static local function in a
// top-level-statements program is unreachable from another assembly. Static local functions
// cannot capture, so the move is a relocation and cannot alter behaviour.

internal static class CatalogEmitter
{
    /// <param name="writeChanges">
    /// False to compute everything and touch nothing. It is what separates asking "is this
    /// catalogue still true?" from making it true: the comparison is the same work either way, and
    /// a check that had to write in order to answer could not be run against a clean tree.
    /// </param>
    internal static GenerateResult Emit(
        Job job, string packageId, string version,
        SortedDictionary<string, RuleInfo>? upstream, Previous? previous, string? dateOverride,
        bool writeChanges = true)
    {
        SortedDictionary<string, RuleInfo> accepted = new(upstream!, StringComparer.Ordinal);
        List<string> retired = CarryForwardRetired(accepted, previous);
        Catalogue catalogue = new(job, packageId, version, accepted);

        int liveCount = accepted.Count(r => !r.Value.Retired);
        CategoryLayout categories = LayOutCategories(job.Container, accepted, previous);
        Changes changes = DescribeChanges(accepted, previous, version, retired);

        // The date only moves when something else did. Bumping it on every run would make the
        // scheduled job open a pull request every night whose only content was a new date.
        if (previous is not null && !changes.VersionChanged && !changes.Any)
        {
            Console.WriteLine($"unchanged: {packageId} {version}, {liveCount} rules — " +
                              (writeChanges ? "file left untouched" : "the catalogue is current"));
            return new GenerateResult(Changed: false, Summary: string.Empty);
        }

        string date = dateOverride ?? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (writeChanges)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(job.Output)!);
            File.WriteAllText(job.Output, RenderSource(catalogue, date, categories), new UTF8Encoding(false));
            Console.WriteLine($"wrote {liveCount} live rules " +
                              $"({accepted.Count - liveCount} retired) to {job.Output}");

            UpdateMirrorBanners(job, catalogue, previous, date, liveCount, categories.Ordered.Count);
        }
        else
        {
            Console.WriteLine($"OUT OF DATE: {job.Output} — {liveCount} live rules upstream " +
                              $"({accepted.Count - liveCount} retired)");
        }

        string summary = RenderSummary(catalogue, previous, changes, liveCount, categories.Ordered.Count);

        return new GenerateResult(Changed: true, Summary: summary);
    }

    // --- the mirrored release, restated wherever a consumer reads -----------------
    //
    // Which upstream release a catalogue reflects is the first thing a consumer needs, and a
    // hand-written banner saying so goes stale the first night regeneration moves it — silently,
    // because nothing compiles a README. So the generator writes it, in the files a consumer
    // actually opens, and DocumentedMirrorTests asserts that what they say matches the catalogue's
    // own CatalogSource attribute. Between the two, the statement cannot drift: the generator keeps
    // it current, and the test fails the build if anything else moves it.
    //
    // A README is a PAIR (ADR-0034), and both halves are written here for the reason the banner is
    // written at all: a translation nothing refreshes states last month's release in the language
    // its reader cannot check against anything. Only the prose differs — the mirrored release is a
    // package id and a version, which are not translated.
    //
    // The nightly job commits the whole of src/, so the refreshed banners travel in the same pull
    // request as the rules that moved, with no change to the workflow.

    private const string MirrorBegin = "<!-- mirror:begin -->";
    private const string MirrorEnd = "<!-- mirror:end -->";

    // What a README is called where the banner has to land. Here it is a pair, because a catalogue's
    // page is maintained in both languages (ADR-0034); in a repository `dcat` was pointed at it is
    // whatever single file that repository keeps, which is the spelling this generator wrote into
    // before the pair existed and must keep writing into.
    private static readonly string[] EnglishReadmes = ["README.en.md", "README.md"];
    private const string FrenchReadme = "README.fr.md";

    private static void UpdateMirrorBanners(
        Job job, Catalogue catalogue, Previous? previous, string date, int liveCount, int categoryCount)
    {
        string dir = Path.GetDirectoryName(job.Output)!;
        string mirrored = $"`{catalogue.PackageId} {catalogue.Version}`";

        // The banner carries only what the generator knows. Whatever a catalogue has to explain
        // about its upstream — a vendor mirrored on its prerelease line, analyzers that ship inside
        // the SDK — is prose belonging to that catalogue and is written OUTSIDE the markers, where
        // rewriting the block cannot destroy it.
        //
        // Only the spellings that are actually there are written to, and a missing one is not
        // reported: a repository that keeps a single README has not lost a translation, and a
        // repository that keeps a pair has not lost the single file. What IS worth saying is a
        // catalogue with no README at all, and that is said once, below.
        // Singular when the catalogue declares one category. DiagnosticCatalog.PublicApi is the
        // first that does, and this banner is rendered on nuget.org: "1 categories" is a wart on a
        // package page, not an internal string. Rules are never fewer than one, so only the
        // category noun needs the agreement.
        string categoryNoun = categoryCount == 1 ? "category" : "categories";
        string categoryNounFr = categoryCount == 1 ? "catégorie" : "catégories";

        WriteReadmeBlocks(dir,
            $"> ## 🪞 Mirrors {mirrored}\n" +
            ">\n" +
            $"> **{liveCount} rules, {categoryCount} {categoryNoun}**, every identifier and category read\n" +
            $"> from that release's own analyzers. Regenerated {date}.",
            $"> ## 🪞 Reflète {mirrored}\n" +
            ">\n" +
            $"> **{liveCount} règles, {categoryCount} {categoryNounFr}**, chaque identifiant et chaque\n" +
            $"> catégorie lus dans les analyseurs de cette version. Régénéré le {date}.");

        // In the changelog the banner sits under Unreleased, so a release promotes it into that
        // version's section along with everything else — which is what makes every published entry
        // state the release it mirrored, including the ones where nothing upstream moved.
        string moved = previous is not null
                       && !string.Equals(previous.SourceVersion, catalogue.Version, StringComparison.Ordinal)
            ? $" — upstream moved from `{previous.SourceVersion}`"
            : " — unchanged upstream";
        WriteBlock(Path.Combine(dir, "CHANGELOG.md"), $"**Mirrors {mirrored}**{moved}.");
    }

    // Each half where it belongs, into the README spellings the catalogue's folder actually has.
    // The mirrored release itself is a package id and a version, which are the same sentence in
    // either language; only the prose around them is translated.
    private static void WriteReadmeBlocks(string dir, string english, string french)
    {
        List<string> written = [];

        foreach (string name in EnglishReadmes)
        {
            if (!File.Exists(Path.Combine(dir, name))) continue;

            WriteBlock(Path.Combine(dir, name), english);
            written.Add(name);
        }

        if (File.Exists(Path.Combine(dir, FrenchReadme)))
        {
            WriteBlock(Path.Combine(dir, FrenchReadme), french);
            written.Add(FrenchReadme);
        }

        if (written.Count == 0)
        {
            Console.WriteLine("  note: no README found beside the catalogue — no banner written");
        }
    }

    // Replaces what sits between the markers, and reports rather than repairs when they are absent:
    // where a banner belongs in a document is an editorial choice this tool cannot make, and a
    // generator that guessed would eventually insert one in the wrong place, silently.
    private static void WriteBlock(string path, string body)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"  note: {Path.GetFileName(path)} not found beside the catalogue — no banner written");
            return;
        }

        string text = File.ReadAllText(path);
        int start = text.IndexOf(MirrorBegin, StringComparison.Ordinal);
        int end = text.IndexOf(MirrorEnd, StringComparison.Ordinal);
        string newline = LineEndingOf(text);
        if (start < 0 || end < start)
        {
            // A note rather than a warning, and it says only what is true anywhere. The markers are
            // how a document ASKS for a banner, so a document without them has asked for nothing and
            // nothing is wrong. This line ships inside `dcat`: it is read in repositories that never
            // adopted the convention, where announcing a fault — still worse, naming tests that only
            // exist here — would report a problem its reader does not have and cannot act on.
            //
            // What makes reporting enough for THIS repository is not said here but done next door:
            // DocumentedMirrorTests fails the build when a shipped document stops agreeing with its
            // catalogue. That check belongs to the repository, so it is the repository that states it.
            Console.WriteLine($"  note: no {MirrorBegin} … {MirrorEnd} block in {Path.GetFileName(path)} " +
                              "— no banner written");
            return;
        }

        // Spelled with the document's own line ending rather than with "\n". The banner is written
        // into somebody else's file, in place, and the rest of that file keeps whatever endings it
        // had — so a hard "\n" leaves a handful of lone LF lines in the middle of a CRLF document.
        // That is a diff on lines nobody edited, in the file a consumer opens first, and it repeats
        // on every checkout that converts them back.
        string block = (newline + body + newline).ReplaceLineEndings(newline);

        string updated = text[..(start + MirrorBegin.Length)] + block + text[end..];
        if (string.Equals(updated, text, StringComparison.Ordinal)) return;

        File.WriteAllText(path, updated, new UTF8Encoding(false));
        Console.WriteLine($"  updated the mirrored release stated in {Path.GetFileName(path)}");
    }

    /// <summary>The line ending the document already uses, read from its first one.</summary>
    /// <remarks>
    /// From the FIRST ending rather than from whether one appears anywhere: a document with mixed
    /// endings — a generated header pasted onto hand-written prose — would otherwise be rewritten
    /// wholesale in whichever kind happened to occur. A document with no ending at all has no layout
    /// to preserve, and LF is what this tool writes everywhere else.
    /// </remarks>
    private static string LineEndingOf(string text)
    {
        int newline = text.IndexOf('\n');

        if (newline < 0) return "\n";

        return newline > 0 && text[newline - 1] == '\r' ? "\r\n" : "\n";
    }

    // §23.1: a constant is never deleted. Consumers inline const values at their own
    // compile time, so removing one breaks their recompilation. A rule that upstream has
    // retired is carried forward and marked [Obsolete] instead — a warning they can act
    // on, rather than a missing member they cannot.
    //
    // Echoes the carried-forward rules into <paramref name="accepted"/> and returns the ids
    // retired by THIS run: one an earlier run already carried forward is not news, and reporting
    // it again would have the scheduled job open the same pull request every night.
    private static List<string> CarryForwardRetired(
        SortedDictionary<string, RuleInfo> accepted, Previous? previous)
    {
        List<string> retired = [];
        if (previous is null) return retired;

        foreach ((string id, RuleInfo info) in previous.Rules)
        {
            if (accepted.ContainsKey(id)) continue;
            accepted[id] = info with { Retired = true };
            if (!info.Retired) retired.Add(id);
        }

        return retired;
    }

    // A catalogue repeats very few distinct categories across very many rules — Sonar spends
    // 456 declarations on 13 values. Declare each once and have the rules refer to it: a
    // const initialised from another const is still a compile-time constant, so the rules
    // stay usable as attribute arguments and still fold to the literal in metadata.
    private static CategoryLayout LayOutCategories(
        string container, SortedDictionary<string, RuleInfo> accepted, Previous? previous)
    {
        List<string> ordered = accepted.Values.Select(v => v.Category).Distinct()
            .OrderBy(c => c, StringComparer.Ordinal).ToList();
        Console.WriteLine($"distinct categories ({ordered.Count}): {string.Join(", ", ordered)}");

        string containerName = container.EndsWith("Rule", StringComparison.Ordinal)
            ? container[..^"Rule".Length] + "Category"
            : container + "Category";

        Dictionary<string, string> names = new(StringComparer.Ordinal);
        HashSet<string> used = new(StringComparer.Ordinal);
        ReservePublishedNames(ordered, previous, names, used);

        foreach (string c in ordered)
        {
            if (names.ContainsKey(c)) continue;   // reserved above
            string baseName = Naming.ToIdentifier(c);
            string name = baseName;
            // Deterministic disambiguation: two categories differing only in punctuation would
            // otherwise silently collapse onto one constant.
            int suffix = 2;
            while (!used.Add(name))
            {
                name = baseName + suffix.ToString(CultureInfo.InvariantCulture);
                suffix++;
            }

            if (name != baseName) Console.WriteLine($"  note: category '{c}' named {name} to avoid a collision");
            names[c] = name;
        }

        return new CategoryLayout(ordered, names, containerName);
    }

    // ADR-0012: a category constant is a member consumers reference by hand, so its name is part of
    // the published contract and must never move under them. Names are otherwise assigned in ordinal
    // order, which makes that fragile: the day upstream adds a category that flattens to the same
    // identifier as an existing one AND sorts before it, the newcomer would take the base name and
    // push the EXISTING constant onto a numbered suffix. Every project referencing it would stop
    // compiling, from an unattended nightly run.
    //
    // So already-published names are RESERVED here, before any new category is fitted around them.
    // Stability wins over prettiness: a category published as MajorCodeSmell2 keeps that name even
    // once whatever collided with it is gone, because renaming it back would break exactly the
    // consumers this pass exists to protect.
    private static void ReservePublishedNames(
        List<string> ordered, Previous? previous, Dictionary<string, string> names, HashSet<string> used)
    {
        if (previous is null) return;

        foreach (string c in ordered)
        {
            if (previous.CategoryNames.TryGetValue(c, out string? published) && used.Add(published))
                names[c] = published;
        }
    }

    // --- what actually changed -------------------------------------------------
    private static Changes DescribeChanges(
        SortedDictionary<string, RuleInfo> accepted, Previous? previous, string version, List<string> retired)
    {
        List<string> added = accepted.Keys.Where(id => previous is null || !previous.Rules.ContainsKey(id)).ToList();
        List<(string Id, string From, string To)> recategorised = previous is null
            ? []
            : accepted.Where(r => previous.Rules.TryGetValue(r.Key, out RuleInfo? old)
                                  && !string.Equals(old.Category, r.Value.Category, StringComparison.Ordinal))
                      .Select(r => (Id: r.Key, From: previous.Rules[r.Key].Category, To: r.Value.Category))
                      .ToList();

        // A reworded title is upstream content that this catalogue now publishes, so it has to be
        // able to move the file on its own. Without this, a release that only rewrote titles would
        // be reported as "no rule changes" and the catalogue would keep serving the old sentences.
        List<(string Id, string From, string To)> retitled = previous is null
            ? []
            : accepted.Where(r => previous.Rules.TryGetValue(r.Key, out RuleInfo? old)
                                  && !string.Equals(old.Title, r.Value.Title, StringComparison.Ordinal))
                      .Select(r => (Id: r.Key, From: previous.Rules[r.Key].Title, To: r.Value.Title))
                      .ToList();

        // A help link is published content too — the catalogue emits it as a constant a consumer can
        // reference — and it moves without anything else moving: a vendor fixing a broken docs URL
        // changes the link and nothing else. Compared for the reason the title is: what this file
        // states, this comparison has to be able to notice.
        List<(string Id, string From, string To)> relinked = previous is null
            ? []
            : accepted.Where(r => previous.Rules.TryGetValue(r.Key, out RuleInfo? old)
                                  && !string.Equals(old.HelpLinkUri, r.Value.HelpLinkUri, StringComparison.Ordinal))
                      .Select(r => (Id: r.Key, From: previous.Rules[r.Key].HelpLinkUri, To: r.Value.HelpLinkUri))
                      .ToList();

        // The retirement running backwards. `retired` above is what CarryForwardRetired found going
        // the other way — a rule upstream no longer declares — and it cannot see this one, because a
        // rule the vendor declares again is in `accepted` and is never carried forward at all.
        //
        // Missing it leaves the catalogue telling a consumer that a live rule is gone and that their
        // suppression should be removed. Nothing downstream contradicts that: the platform never
        // validates a suppression's category (§3.2), which is the whole reason this file exists.
        List<string> restored = previous is null
            ? []
            : accepted.Where(r => !r.Value.Retired
                                  && previous.Rules.TryGetValue(r.Key, out RuleInfo? old)
                                  && old.Retired)
                      .Select(r => r.Key)
                      .ToList();

        bool versionChanged = previous is null
                              || !string.Equals(previous.SourceVersion, version, StringComparison.Ordinal);

        return new Changes(added, recategorised, retitled, relinked, retired, restored, versionChanged);
    }

    // ---------------------------------------------------------------------------
    // Emit. Output is ordered deterministically so a regeneration produces a diff that
    // shows only genuine upstream change.
    // ---------------------------------------------------------------------------
    private static string RenderSource(Catalogue catalogue, string date, CategoryLayout categories)
    {
        Job job = catalogue.Job;
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//     Generated by eng/CatalogGen from the DiagnosticDescriptor instances declared by");
        sb.AppendLine($"//     {catalogue.PackageId} {catalogue.Version} (language: {job.Language}).");
        sb.AppendLine("//     Do not edit by hand: rerun the generator.");
        sb.AppendLine("//");
        sb.AppendLine("//     Only Id, Category, Title and HelpLinkUri are emitted, and only when the");
        sb.AppendLine("//     descriptor actually supplies them. A rule's title is reproduced verbatim as its");
        sb.AppendLine("//     documentation comment: it is the one sentence that says what the rule is about,");
        sb.AppendLine($"//     and it is {catalogue.PackageId}'s own wording. Rule descriptions and message");
        sb.AppendLine("//     formats are the vendor's documentation and are not redistributed here.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
        // System is needed for one reason only: [Obsolete] on a rule the vendor retired. Emitting it
        // unconditionally leaves an unused directive in every catalogue that has retired nothing,
        // which is all of them today — and it made the generator disagree with its own committed
        // output, so a regeneration would have produced a diff carrying no upstream change at all.
        if (catalogue.Rules.Values.Any(r => r.Retired)) sb.AppendLine("using System;");
        sb.AppendLine("using DiagnosticCatalog;");
        sb.AppendLine();
        sb.AppendLine("[assembly: CatalogSource(");
        sb.AppendLine($"    source:        \"{catalogue.PackageId}\",");
        sb.AppendLine($"    sourceVersion: \"{catalogue.Version}\",");
        sb.AppendLine($"    generatedOn:   \"{date}\")]");
        sb.AppendLine();
        sb.AppendLine($"namespace {job.Namespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// The diagnostic categories used by {catalogue.PackageId}, declared once each.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine("/// INTERNAL by design. A category is reachable only through the rule that carries it:");
        sb.AppendLine("/// write <c>SomeRule.Sxxxx.Category</c>, never the category constant directly. The two");
        sb.AppendLine("/// spellings fold to the same string today and stop agreeing the day the vendor moves the");
        sb.AppendLine("/// rule to another category -- the rule member follows, a category named on its own does");
        sb.AppendLine("/// not, and the suppression is left asserting a category the rule no longer carries.");
        sb.AppendLine("/// Nothing reports that: Roslyn ignores the category when it matches a suppression, so the");
        sb.AppendLine("/// mistake has no symptom at all. Keeping this class out of the public surface makes that");
        sb.AppendLine("/// decoupling unwritable rather than merely discouraged.");
        sb.AppendLine("/// The public <c>Category</c> constant on each rule is initialised from here and folds to");
        sb.AppendLine("/// the literal at compile time, so a consumer loses nothing.");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine("[DiagnosticCategory]");
        sb.AppendLine($"internal static class {categories.ContainerName}");
        sb.AppendLine("{");
        bool firstCategory = true;
        foreach (string c in categories.Ordered)
        {
            if (!firstCategory) sb.AppendLine();
            firstCategory = false;
            sb.AppendLine($"    /// <summary>The <c>{Naming.Escape(c)}</c> category.</summary>");
            sb.AppendLine($"    public const string {categories.Names[c]} = \"{Naming.Escape(c)}\";");
        }

        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// The {catalogue.PackageId} diagnostic rules, as declared by that package's analyzers.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {job.Container}");
        sb.AppendLine("{");

        bool first = true;
        foreach ((string id, RuleInfo info) in catalogue.Rules)
        {
            if (!first) sb.AppendLine();
            first = false;
            AppendRule(sb, catalogue, id, info, categories);
        }

        sb.AppendLine("}");

        return sb.ToString().ReplaceLineEndings("\n");
    }

    private static void AppendRule(
        StringBuilder sb, Catalogue catalogue, string id, RuleInfo info, CategoryLayout categories)
    {
        bool hasHelp = !string.IsNullOrWhiteSpace(info.HelpLinkUri);
        sb.AppendLine($"    /// <summary>{SummaryOf(id, info)}</summary>");

        // Everything that is not the rule's own sentence goes on one further line, so that hovering
        // a rule yields the sentence first and the file stays diffable. A regeneration is reviewed
        // as a diff (ADR-0009), and a line carrying a title AND a help link reaches 282 characters
        // on the .NET analyzers, which is where a diff stops being read.
        List<string> notes = [];
        if (info.Retired)
            notes.Add($"No longer declared by {catalogue.PackageId} as of {catalogue.Version}.");
        if (hasHelp) notes.Add($"See <see href=\"{Naming.EscapeXml(info.HelpLinkUri)}\"/>.");
        if (notes.Count > 0) sb.AppendLine($"    /// <remarks>{string.Join(" ", notes)}</remarks>");

        if (info.Retired)
            sb.AppendLine($"    [Obsolete(\"{Naming.Escape(id)} is no longer declared by {catalogue.PackageId} " +
                          $"as of {catalogue.Version}. " +
                          "Kept so that removing it does not break recompilation; remove your suppression.\")]");
        sb.AppendLine("    [DiagnosticRule]");
        sb.AppendLine($"    public static class {id}");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>The canonical identifier of this diagnostic.</summary>");
        sb.AppendLine($"        public const string Id = nameof({id});");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>The category declared by the analyzer's DiagnosticDescriptor.</summary>");
        sb.AppendLine($"        public const string Category = {categories.ContainerName}.{categories.Names[info.Category]};");
        if (hasHelp)
        {
            sb.AppendLine();
            sb.AppendLine("        /// <summary>The help link declared by the analyzer's DiagnosticDescriptor.</summary>");
            sb.AppendLine($"        public const string HelpLinkUri = \"{Naming.Escape(info.HelpLinkUri)}\";");
        }

        sb.AppendLine("    }");
    }

    // The sentence a rule's documentation comment carries. The vendor's own title when the
    // descriptor declares one — it says what the rule is about, which an identifier cannot, and it
    // is the value a consumer is hovering the constant to learn.
    private static string SummaryOf(string id, RuleInfo info) =>
        info.Title.Length > 0
            ? Naming.EscapeXml(Naming.Sentence(info.Title))
            : SummaryWithoutTitle(id, info.Category);

    // What a rule says when no title is known: what every rule said before titles were emitted.
    // The parser next door reproduces this to tell a rule that has no title from one whose title
    // happens to be this sentence, so the two must agree exactly — hence one method, not two.
    internal static string SummaryWithoutTitle(string id, string category) =>
        $"Rule <c>{id}</c>, category <c>{Naming.EscapeXml(category)}</c>.";

    // --- human-readable summary for the pull request ---------------------------
    private static string RenderSummary(
        Catalogue catalogue, Previous? previous, Changes changes, int liveCount, int categoryCount)
    {
        StringBuilder md = new();
        string fromTo = previous is not null && changes.VersionChanged
            ? $"{previous.SourceVersion} → {catalogue.Version}"
            : catalogue.Version;
        md.AppendLine($"#### {catalogue.Job.Namespace} — {catalogue.PackageId} {fromTo}");
        md.AppendLine();
        if (!changes.Any)
        {
            md.AppendLine("No rule changes. Only the mirrored upstream version moved.");
        }
        else
        {
            AppendAdded(md, changes.Added, catalogue.Rules);
            AppendRecategorised(md, changes.Recategorised);
            AppendRetitled(md, changes.Retitled);
            AppendRelinked(md, changes.Relinked);
            AppendRetired(md, changes.Retired);
            AppendRestored(md, changes.Restored);
        }

        md.AppendLine($"{liveCount} live rules, {categoryCount} categories.");

        return md.ToString().TrimEnd() + "\n";
    }

    private static void AppendAdded(
        StringBuilder md, List<string> added, SortedDictionary<string, RuleInfo> rules)
    {
        if (added.Count == 0) return;

        md.AppendLine($"**Added ({added.Count}):**");
        foreach (string id in added.Take(50))
            md.AppendLine($"- `{id}` — {rules[id].Category}");
        if (added.Count > 50) md.AppendLine($"- …and {added.Count - 50} more");
        md.AppendLine();
    }

    private static void AppendRecategorised(
        StringBuilder md, List<(string Id, string From, string To)> recategorised)
    {
        if (recategorised.Count == 0) return;

        md.AppendLine($"**Recategorised ({recategorised.Count}):**");
        foreach ((string Id, string From, string To) r in recategorised)
            md.AppendLine($"- `{r.Id}` — {r.From} → {r.To}");
        md.AppendLine();
    }

    private static void AppendRetitled(
        StringBuilder md, List<(string Id, string From, string To)> retitled)
    {
        if (retitled.Count == 0) return;

        md.AppendLine($"**Retitled upstream ({retitled.Count}):**");
        // Capped for the same reason as the added list: the run that first emits titles reports
        // every rule in the catalogue, and a pull request body is not the place to read 456 of them.
        foreach ((string Id, string From, string To) r in retitled.Take(50))
            md.AppendLine($"- `{r.Id}` — {Quoted(r.From)} → {Quoted(r.To)}");
        if (retitled.Count > 50) md.AppendLine($"- …and {retitled.Count - 50} more");
        md.AppendLine();

        static string Quoted(string title) => title.Length == 0 ? "*(none)*" : $"\"{title}\"";
    }

    private static void AppendRelinked(StringBuilder md, List<(string Id, string From, string To)> relinked)
    {
        if (relinked.Count == 0) return;

        md.AppendLine($"**Help link moved ({relinked.Count}):**");
        // Capped like the added and retitled lists: the run that first emits links reports every
        // rule that has one, and a pull request body is not the place to read hundreds of URLs.
        foreach ((string Id, string From, string To) r in relinked.Take(50))
            md.AppendLine($"- `{r.Id}` — {Shown(r.From)} → {Shown(r.To)}");
        if (relinked.Count > 50) md.AppendLine($"- …and {relinked.Count - 50} more");
        md.AppendLine();

        static string Shown(string link) => link.Length == 0 ? "*(none)*" : $"<{link}>";
    }

    private static void AppendRestored(StringBuilder md, List<string> restored)
    {
        if (restored.Count == 0) return;

        md.AppendLine($"**Declared again upstream ({restored.Count}) — `[Obsolete]` removed:**");
        foreach (string id in restored)
            md.AppendLine($"- `{id}`");
        md.AppendLine();
        md.AppendLine("> The catalogue had been telling consumers to remove their suppression for " +
                      "these. The vendor declares them again, so the marker goes.");
        md.AppendLine();
    }

    private static void AppendRetired(StringBuilder md, List<string> retired)
    {
        if (retired.Count == 0) return;

        md.AppendLine($"**Retired upstream ({retired.Count}) — kept and marked `[Obsolete]`:**");
        foreach (string id in retired)
            md.AppendLine($"- `{id}`");
        md.AppendLine();
        md.AppendLine("> Constants are never deleted: consumers inline them, so removing one breaks " +
                      "their recompilation. Deleting these is a major version (§23.1).");
        md.AppendLine();
    }

    /// <summary>
    /// One catalogue as this run resolved it. <see cref="Version"/> is the upstream release
    /// actually mirrored, which is not <c>Job.Version</c> once "latest" has been resolved, and
    /// <see cref="Rules"/> is what will be written — rules carried forward as retired included.
    /// </summary>
    private sealed record Catalogue(
        Job Job, string PackageId, string Version, SortedDictionary<string, RuleInfo> Rules);

    /// <summary>
    /// The category constants a catalogue declares: the distinct categories in emission order, the
    /// identifier chosen for each, and the name of the class that holds them.
    /// </summary>
    private sealed record CategoryLayout(
        List<string> Ordered, Dictionary<string, string> Names, string ContainerName);

    /// <summary>
    /// What this run found to have moved since the previous one. <see cref="Any"/> is what decides
    /// whether the file is rewritten at all, so the three lists and the version are read together.
    /// </summary>
    private sealed record Changes(
        List<string> Added,
        List<(string Id, string From, string To)> Recategorised,
        List<(string Id, string From, string To)> Retitled,
        List<(string Id, string From, string To)> Relinked,
        List<string> Retired,
        List<string> Restored,
        bool VersionChanged)
    {
        internal bool Any =>
            Added.Count > 0 || Retired.Count > 0 || Restored.Count > 0
            || Recategorised.Count > 0 || Retitled.Count > 0 || Relinked.Count > 0;
    }
}
