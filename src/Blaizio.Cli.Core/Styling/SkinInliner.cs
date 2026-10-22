namespace Blaizio.Cli.Core.Styling;

/// <summary>Obsolete: use <see cref="StyleInliner"/>. "Skin" was renamed to "style"; this type
/// forwards every member to a wrapped <see cref="StyleInliner"/>.</summary>
[Obsolete("Use StyleInliner. \"Skin\" was renamed to \"style\".")]
public sealed class SkinInliner
{
    private SkinInliner(StyleInliner inner) => Inner = inner;

    /// <summary>The <see cref="StyleInliner"/> this forwarder wraps.</summary>
    public StyleInliner Inner { get; }

    /// <inheritdoc cref="StyleInliner.Tokens"/>
    public IReadOnlyList<string> Tokens => Inner.Tokens;

    /// <inheritdoc cref="StyleInliner.Resolve"/>
    public string? Resolve(string token) => Inner.Resolve(token);

    /// <inheritdoc cref="StyleInliner.Create"/>
    public static SkinInliner Create(string sharedCss, string skinCss) =>
        new(StyleInliner.Create(sharedCss, skinCss));

    /// <inheritdoc cref="StyleInliner.Inline"/>
    public string Inline(string source) => Inner.Inline(source);
}
