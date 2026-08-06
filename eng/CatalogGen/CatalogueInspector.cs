using System.Reflection;

namespace CatalogGen;

/// <summary>
/// One rule as a compiled catalogue publishes it.
/// </summary>
/// <param name="Container">
/// The name of the class the rule is nested in, or empty when it is declared at namespace level.
/// A fact about the shape of the catalogue; the spelling a use site writes is
/// <see cref="Reference"/>, which is not built from this.
/// </param>
/// <param name="Reference">
/// The rule type as C# has to be written to reach it from anywhere: <c>global::</c>, the namespace,
/// every enclosing type in order, and the type's own name, each segment escaped where C# would
/// otherwise read it as a keyword.
/// <para>
/// Carried whole rather than assembled by the caller, because every way of assembling it is a way
/// of getting it wrong and none of them fails loudly. A reference missing the namespace compiles in
/// the file the catalogue was written in and nowhere else; one carrying only the immediate declaring
/// type drops the levels above it; one built by concatenating a namespace that is empty starts with
/// a dot. What a reader copies out of <c>dcat explain</c> has to bind in THEIR file, which imports
/// nothing on this catalogue's account — so the reference depends on no <c>using</c> at all, and
/// <c>global::</c> is what makes that true even where a consumer has a namespace of their own that
/// shadows the first segment.
/// </para>
/// </param>
/// <param name="TypeName">
/// The rule TYPE's own name, which is what a use site writes — <c>ACME_0003</c>, never the
/// <c>ACME-0003</c> it declares.
/// <para>
/// Carried apart from <see cref="Id"/> because the two are only usually the same string. §8.2
/// recommends naming a rule type after its identifier and DCAT0005 reports the case where C# will
/// not allow it: an identifier carrying a character forbidden in a type name leaves the type as
/// that identifier legalised, and both spellings then exist. A reader holding only the identifier
/// cannot recover the name, and a reference written from it does not compile.
/// </para>
/// </param>
public sealed record CataloguedRule(
    string Id, string TypeName, string Container, string Reference, string Category, string HelpLinkUri,
    bool Retired);

/// <summary>
/// What a compiled catalogue assembly declares.
/// </summary>
public sealed record CatalogueContents(
    string? Source, string? SourceVersion, string? GeneratedOn, IReadOnlyList<CataloguedRule> Rules);

/// <summary>
/// Reads a catalogue back out of the assembly that ships it.
/// </summary>
/// <remarks>
/// <para>
/// Reflection-only, through a <see cref="MetadataLoadContext"/>: nothing in the assembly is
/// executed, no constructor runs, and no dependency of it has to be present for the parts that are
/// present to be read. That is the whole difference from the descriptor read next door — an
/// analyzer has to be CONSTRUCTED to say what it declares, which is why that happens in another
/// process; a catalogue declares everything in metadata, so reading it needs no such licence and
/// must not take one.
/// </para>
/// <para>
/// It reads the assembly rather than the generated source because the assembly is the artefact a
/// consumer actually has: the <c>.g.cs</c> lives in the repository that produced it, the package
/// travels everywhere else.
/// </para>
/// </remarks>
public static class CatalogueInspector
{
    private const string RuleMarker = "DiagnosticCatalog.DiagnosticRuleAttribute";
    private const string SourceMarker = "DiagnosticCatalog.CatalogSourceAttribute";
    private const string ObsoleteMarker = "System.ObsoleteAttribute";

    /// Null when the file cannot be read as an assembly, which is reported rather than thrown.
    public static CatalogueContents? Read(string assemblyPath)
    {
        string full = Path.GetFullPath(assemblyPath);
        if (!File.Exists(full))
        {
            Console.Error.WriteLine($"no such assembly: {assemblyPath}");

            return null;
        }

        try
        {
            using MetadataLoadContext context = new(new CatalogueResolver(Path.GetDirectoryName(full)!));
            Assembly assembly = context.LoadFromAssemblyPath(full);

            (string? source, string? version, string? generatedOn) = ReadProvenance(assembly);

            // GetTypes already returns nested types, so every rule is reached exactly once here.
            // Recursing as well as enumerating returned each of them twice, which showed up as a
            // catalogue of 394 rules where the file declares 197.
            List<CataloguedRule> rules = [];
            foreach (Type type in assembly.GetTypes())
                CollectRule(type, rules);

            return new CatalogueContents(source, version, generatedOn,
                                         [.. rules.OrderBy(r => r.Id, StringComparer.Ordinal)]);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or IOException)
        {
            Console.Error.WriteLine($"{assemblyPath} could not be read as an assembly ({ex.GetType().Name}: {ex.Message})");

            return null;
        }
    }

    // A rule is a nested static class carrying [DiagnosticRule], and its values are constants — so
    // they are read from metadata rather than from a running type. GetRawConstantValue is what
    // makes that true: it never triggers a static constructor.
    private static void CollectRule(Type type, List<CataloguedRule> rules)
    {
        if (!HasAttribute(type.GetCustomAttributesData(), RuleMarker)) return;

        // No title: a rule's title is a documentation comment, and a documentation comment is not
        // compiled into the assembly. Carrying a field that could never be filled would invite the
        // reader to wonder why it is always empty.
        rules.Add(new CataloguedRule(
            Constant(type, "Id") ?? type.Name,
            type.Name,
            type.DeclaringType?.Name ?? string.Empty,
            ReferenceTo(type),
            Constant(type, "Category") ?? string.Empty,
            Constant(type, "HelpLinkUri") ?? string.Empty,
            HasAttribute(type.GetCustomAttributesData(), ObsoleteMarker)));
    }

    /// <summary>
    /// The type, spelled as C# reaches it from a file that has imported nothing.
    /// </summary>
    /// <remarks>
    /// Built from the metadata rather than from <see cref="Type.FullName"/>, which spells a nested
    /// type <c>Outer+Inner</c> — a name the runtime accepts and the compiler does not — and would
    /// need unpicking anyway. <see cref="Type.Namespace"/> is the outermost enclosing type's for a
    /// nested one, which is exactly what a reference needs, and null for the global namespace, where
    /// there is no prefix at all.
    /// </remarks>
    private static string ReferenceTo(Type type)
    {
        List<string> nesting = [];
        for (Type? current = type; current is not null; current = current.DeclaringType)
            nesting.Insert(0, Writable(current.Name));

        string @namespace = type.Namespace ?? string.Empty;
        IEnumerable<string> segments = @namespace.Length == 0
            ? nesting
            : @namespace.Split('.').Select(Writable).Concat(nesting);

        return "global::" + string.Join(".", segments);
    }

    /// <summary>One identifier, as C# accepts it.</summary>
    /// <remarks>
    /// Metadata has no keywords, so a namespace called <c>class</c> or a type called <c>event</c> is
    /// an ordinary name to every reader here and unwritable in C# without the escape. Only RESERVED
    /// words are escaped: a contextual keyword — <c>var</c>, <c>record</c>, <c>value</c> — is a legal
    /// identifier already, and prefixing it would be noise in a line meant to be copied.
    /// </remarks>
    private static string Writable(string identifier) => Reserved.Contains(identifier) ? "@" + identifier : identifier;

    /// <summary>C#'s reserved words, which are a closed set fixed by the language.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class",
        "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event",
        "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
        "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
        "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static",
        "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
        "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    private static (string?, string?, string?) ReadProvenance(Assembly assembly)
    {
        CustomAttributeData? source = assembly.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == SourceMarker);
        if (source is null || source.ConstructorArguments.Count < 3) return (null, null, null);

        return (source.ConstructorArguments[0].Value as string,
                source.ConstructorArguments[1].Value as string,
                source.ConstructorArguments[2].Value as string);
    }

    private static bool HasAttribute(IEnumerable<CustomAttributeData> attributes, string fullName)
        => attributes.Any(a => a.AttributeType.FullName == fullName);

    private static string? Constant(Type type, string name)
        => type.GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() as string;

    /// <summary>
    /// Finds what a catalogue references, without requiring it to sit beside the catalogue.
    /// </summary>
    /// <remarks>
    /// A catalogue references the foundation for its attributes, and those attributes have to be
    /// RESOLVED before they can be recognised — reading them by name is not an option, which an
    /// earlier draft of this file assumed and a FileNotFoundException disproved. Where the
    /// foundation actually is depends on what the reader is pointing at: beside the catalogue in an
    /// application's output, and only in the package cache when the catalogue is a library's own
    /// build output, where a project reference is not copied.
    /// </remarks>
    private sealed class CatalogueResolver : MetadataAssemblyResolver
    {
        private readonly string[] _directories;
        private readonly string? _packageCache;

        internal CatalogueResolver(string besideTheCatalogue)
        {
            _directories =
            [
                besideTheCatalogue,
                Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            ];
            _packageCache = NuGetPackageCache();
        }

        public override Assembly? Resolve(MetadataLoadContext context, AssemblyName assemblyName)
        {
            string? name = assemblyName.Name;
            if (name is null) return null;

            foreach (string directory in _directories)
            {
                string candidate = Path.Combine(directory, name + ".dll");
                if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
            }

            // Searched last and by name only: the cache holds every version of everything, and which
            // one answers matters far less here than for a compilation. Nothing is executed, and the
            // only thing read out of the result is whether a type has a given full name.
            if (_packageCache is null) return null;

            string package = Path.Combine(_packageCache, name.ToLowerInvariant());
            if (!Directory.Exists(package)) return null;

            string? found = Directory.EnumerateFiles(package, name + ".dll", SearchOption.AllDirectories)
                                     .OrderBy(p => p, StringComparer.Ordinal)
                                     .LastOrDefault();

            return found is null ? null : context.LoadFromAssemblyPath(found);
        }

        private static string? NuGetPackageCache()
        {
            string? configured = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            if (!string.IsNullOrEmpty(configured) && Directory.Exists(configured)) return configured;

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home)) return null;

            string standard = Path.Combine(home, ".nuget", "packages");

            return Directory.Exists(standard) ? standard : null;
        }
    }
}
