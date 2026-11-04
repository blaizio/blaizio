namespace Blaizio;

/// <summary>The 19 languages every app has, in the order <see cref="SyntaxText.Languages"/> lists them.</summary>
internal static class BuiltInLanguages
{
    public static readonly SyntaxLanguage[] All =
    [
        CSharpSyntax.Instance,
        RazorSyntax.Instance,
        JsonSyntax.Instance,
        ShellSyntax.Instance,
        ConsoleSyntax.Instance,
        PowerShellSyntax.Instance,
        ScriptSyntax.TypeScript,
        ScriptSyntax.JavaScript,
        MarkupSyntax.Html,
        MarkupSyntax.Xml,
        CssSyntax.Instance,
        SqlSyntax.Instance,
        YamlSyntax.Instance,
        DiffSyntax.Instance,
        HttpSyntax.Instance,
        DockerfileSyntax.Instance,
        IniSyntax.Ini,
        IniSyntax.Toml,
        MarkdownSyntax.Instance,
    ];
}

/// <summary>
/// One immutable set of languages, found by name or alias (any case). <see cref="SyntaxOptions"/>
/// swaps in a new set on every change, and a <see cref="SyntaxDocument"/> keeps the set it was
/// created with, so the languages a document hosts never change under it.
/// </summary>
/// <remarks>
/// A name always wins over an alias. Between aliases, one set with <see cref="SyntaxOptions.Alias"/>
/// wins over one a language declares, a language added later wins over one added before it, and the
/// aliases of a replaced language keep finding its replacement unless something else claims them.
/// </remarks>
internal sealed class SyntaxRegistry
{
    public static readonly SyntaxRegistry BuiltIn = new(BuiltInLanguages.All, [], []);

    private readonly Dictionary<string, SyntaxLanguage> _lookup;

    private SyntaxRegistry(SyntaxLanguage[] languages, (string Alias, string Language)[] inherited, (string Alias, string Language)[] aliases)
    {
        Languages = languages;
        InheritedAliases = inherited;
        ExplicitAliases = aliases;
        Names = [.. languages.Select(l => l.Name)];

        var byName = new Dictionary<string, SyntaxLanguage>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in languages) byName[language.Name] = language;

        // Weakest first: what a replaced language answered to, what each language declares, what
        // the app set with Alias - and a name over all of them.
        _lookup = new Dictionary<string, SyntaxLanguage>(StringComparer.OrdinalIgnoreCase);
        foreach (var (alias, name) in inherited) _lookup[alias] = byName[name];
        foreach (var language in languages)
        foreach (var alias in language.Aliases)
            if (!string.IsNullOrWhiteSpace(alias)) _lookup[alias.Trim()] = language;
        foreach (var (alias, name) in aliases) _lookup[alias] = byName[name];
        foreach (var language in languages) _lookup[language.Name] = language;
    }

    /// <summary>The languages, built-ins first, then in the order they were added.</summary>
    public SyntaxLanguage[] Languages { get; }

    /// <summary>Their canonical names, in the same order.</summary>
    public string[] Names { get; }

    // The aliases of languages that were replaced: `cs` still finds a replacement for csharp.
    private (string Alias, string Language)[] InheritedAliases { get; }

    private (string Alias, string Language)[] ExplicitAliases { get; }

    public SyntaxLanguage? Find(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : _lookup.GetValueOrDefault(name.Trim());

    /// <summary>This set with <paramref name="added"/>: each replaces the language of the same name, or joins the end.</summary>
    public SyntaxRegistry With(IEnumerable<SyntaxLanguage> added)
    {
        var languages = Languages.ToList();
        var inherited = InheritedAliases.ToList();
        foreach (var language in added)
        {
            var index = languages.FindIndex(l => string.Equals(l.Name, language.Name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                languages.Add(language);
                continue;
            }
            var replaced = languages[index];
            inherited.AddRange(replaced.Aliases.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => (a.Trim(), replaced.Name)));
            languages[index] = language;
        }
        // Aliases set earlier point at their language by name, so they follow a replacement too.
        return new SyntaxRegistry([.. languages], [.. inherited], ExplicitAliases);
    }

    /// <summary>This set with <paramref name="alias"/> naming the language called <paramref name="name"/>.</summary>
    public SyntaxRegistry WithAlias(string alias, string name) =>
        new(Languages, InheritedAliases, [.. ExplicitAliases.Where(a => !string.Equals(a.Alias, alias, StringComparison.OrdinalIgnoreCase)), (alias, name)]);

    /// <summary>Whether <paramref name="name"/> is a language's canonical name in this set.</summary>
    public bool IsName(string name) => Names.Contains(name, StringComparer.OrdinalIgnoreCase);
}
