using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Blaizio;

/// <summary>
/// The languages an app highlights: the 19 built into <see cref="SyntaxText"/> plus the ones it
/// adds, configured once in <c>AddBlaizio</c>:
/// <code>
/// builder.Services.AddBlaizio(options => options.Syntax
///     .Add(SyntaxLanguages.Python, SyntaxLanguages.Go)
///     .Alias("py3", "python"));
/// </code>
/// The Code Block reads them from there; elsewhere, pass them to a <see cref="SyntaxDocument"/>.
/// Every lookup is per app: two apps in one process never see each other's languages.
/// </summary>
public sealed class SyntaxOptions
{
    private readonly Lock _gate = new();
    private SyntaxRegistry _registry = SyntaxRegistry.BuiltIn;

    // Only BlaizioOptions.Syntax creates one: configuration has one home, AddBlaizio.
    internal SyntaxOptions()
    {
    }

    internal SyntaxRegistry Registry => Volatile.Read(ref _registry);

    /// <summary>
    /// The canonical names of every language here: the built-in ones (<see cref="SyntaxText.Languages"/>),
    /// then the added ones in the order they were added.
    /// </summary>
    public IReadOnlyList<string> Languages => Registry.Names;

    /// <summary>
    /// Adds <paramref name="languages"/> - from Blaizio.Syntax.Languages, a <see cref="SyntaxGrammar"/>
    /// of your own, or any <see cref="SyntaxLanguage"/>. One with the name of a language already here
    /// (a built-in included) replaces it, everywhere: by name, by its aliases, and inside the languages
    /// that host others (a Markdown fence, an HTML script element). Its own aliases come with it,
    /// except one that is another language's name.
    /// </summary>
    /// <exception cref="ArgumentException">A language has no name, or a name with spaces in it.</exception>
    public SyntaxOptions Add(params IEnumerable<SyntaxLanguage> languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        var list = languages.ToList();
        foreach (var language in list)
        {
            ArgumentNullException.ThrowIfNull(language, nameof(languages));
            var name = language.Name;
            if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsWhiteSpace))
                throw new ArgumentException($"A language needs a name without spaces; '{name}' is not one.", nameof(languages));
            language.Seal();
        }
        lock (_gate) Volatile.Write(ref _registry, _registry.With(list));
        return this;
    }

    /// <summary>
    /// Makes <paramref name="alias"/> another name for <paramref name="language"/> (a name or alias
    /// already here): <c>Alias("fsi", "fsharp")</c>. Overrides an alias a language declares; add the
    /// language first.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="language"/> is not here, or <paramref name="alias"/> is a language's name.</exception>
    public SyntaxOptions Alias(string alias, string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        alias = alias.Trim();
        lock (_gate)
        {
            var target = _registry.Find(language)
                ?? throw new ArgumentException($"'{language}' is not a language here; add it before aliasing it.", nameof(language));
            if (_registry.IsName(alias))
                throw new ArgumentException($"'{alias}' is already a language's name, so it cannot be an alias.", nameof(alias));
            Volatile.Write(ref _registry, _registry.WithAlias(alias, target.Name));
        }
        return this;
    }

    /// <summary>Whether <paramref name="language"/> (a name or alias, any case) is here.</summary>
    public bool IsSupported([NotNullWhen(true)] string? language) => Registry.Find(language) is not null;

    /// <summary>The canonical name for <paramref name="language"/>, or <see langword="null"/> when it is not here.</summary>
    public string? GetLanguageName(string? language) => Registry.Find(language)?.Name;

    /// <summary>The language <paramref name="language"/> names, or <see langword="null"/> when it is not here.</summary>
    public SyntaxLanguage? Find(string? language) => Registry.Find(language);

    /// <summary>
    /// Tokenizes <paramref name="code"/> as <paramref name="language"/>, one <see cref="SyntaxLine"/>
    /// per line: <see cref="SyntaxText.Tokenize"/> with this app's languages.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="language"/> is not here.</exception>
    public IReadOnlyList<SyntaxLine> Tokenize(string code, string language)
    {
        ArgumentNullException.ThrowIfNull(code);
        return new SyntaxDocument(language, this).Update(code);
    }
}

/// <summary>Adds <c>Syntax</c> to <see cref="BlaizioOptions"/>, where an app configures its languages.</summary>
public static class BlaizioOptionsSyntaxExtensions
{
    // Blaizio.Base knows nothing of this package, so the languages ride beside the options instead
    // of inside them: one set per BlaizioOptions instance (per app), gone with it.
    private static readonly ConditionalWeakTable<BlaizioOptions, SyntaxOptions> s_syntax = new();

    extension(BlaizioOptions options)
    {
        /// <summary>
        /// The languages the app highlights: the built-in ones plus the ones added here, in
        /// <c>AddBlaizio(options => options.Syntax.Add(...))</c>. Read by the Code Block.
        /// </summary>
        public SyntaxOptions Syntax => s_syntax.GetValue(options, static _ => new SyntaxOptions());
    }
}
