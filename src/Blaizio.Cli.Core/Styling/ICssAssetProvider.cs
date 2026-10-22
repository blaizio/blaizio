namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// Supplies the raw CSS assets <see cref="TailwindSetup"/> writes into a consumer project: the
/// theme token block, the Blaizio base contract, and one style. The CLI backs this with files it
/// embeds from Blaizio.Ui so setup works fully offline and always matches the shipped components.
/// </summary>
/// <remarks>
/// The style members (<see cref="GetSharedStyleCss"/>, <see cref="GetStyleCss"/>,
/// <see cref="AvailableStyles"/>) replace the obsolete skin-named ones. Both sets carry default
/// implementations that forward to each other, so an implementer overrides ONE set: new code
/// implements the style members; an implementer written against the old names keeps compiling.
/// Implementing neither set throws <see cref="NotImplementedException"/> on first use.
/// </remarks>
public interface ICssAssetProvider
{
    /// <summary>The theme tokens + Tailwind token map (<c>:root</c>/<c>.dark</c> + <c>@theme</c>).</summary>
    string GetThemeCss();

    /// <summary>
    /// The vendored tw-animate-css (the enter/exit animation utilities the overlay styles use).
    /// Vendored rather than imported by package name so every pipeline - including the Node-free
    /// standalone binary, which cannot resolve <c>node_modules</c> - has it locally.
    /// </summary>
    string GetAnimateCss();

    /// <summary>The base contract: <c>data-*</c> custom variants, keyframes, shared utilities.</summary>
    string GetBaseCss();

    /// <summary>The shared style layer: the component classes every style agrees on (unscoped defaults).</summary>
    string GetSharedStyleCss() =>
        CssAssetForwarding.Guard(nameof(GetSharedStyleCss), () =>
#pragma warning disable CS0618 // forwarding to the obsolete member is the point
            GetSharedSkinCss());
#pragma warning restore CS0618

    /// <summary>One style's differences from the shared layer, e.g. <c>style-ember.css</c>.</summary>
    /// <param name="style">Style name without the <c>style-</c> prefix or extension.</param>
    string GetStyleCss(string style) =>
        CssAssetForwarding.Guard(nameof(GetStyleCss), () =>
#pragma warning disable CS0618
            GetSkinCss(style));
#pragma warning restore CS0618

    /// <summary>Names of every available style (without the <c>style-</c> prefix).</summary>
    IReadOnlyList<string> AvailableStyles =>
        CssAssetForwarding.Guard(nameof(AvailableStyles), () =>
#pragma warning disable CS0618
            AvailableSkins);
#pragma warning restore CS0618

    /// <summary>Obsolete: use <see cref="GetSharedStyleCss"/>.</summary>
    [Obsolete("Use GetSharedStyleCss. \"Skin\" was renamed to \"style\".")]
    string GetSharedSkinCss() =>
        CssAssetForwarding.Guard(nameof(GetSharedSkinCss), GetSharedStyleCss);

    /// <summary>Obsolete: use <see cref="GetStyleCss"/>.</summary>
    /// <param name="skin">Style name without the <c>style-</c> prefix or extension.</param>
    [Obsolete("Use GetStyleCss. \"Skin\" was renamed to \"style\".")]
    string GetSkinCss(string skin) =>
        CssAssetForwarding.Guard(nameof(GetSkinCss), () => GetStyleCss(skin));

    /// <summary>Obsolete: use <see cref="AvailableStyles"/>.</summary>
    [Obsolete("Use AvailableStyles. \"Skin\" was renamed to \"style\".")]
    IReadOnlyList<string> AvailableSkins =>
        CssAssetForwarding.Guard(nameof(AvailableSkins), () => AvailableStyles);

    /// <summary>
    /// One color preset's token sheet, e.g. <c>preset-comet.css</c> (scoped under
    /// <c>.preset-&lt;name&gt;</c>; the default <c>nova</c> palette lives in the theme css and has
    /// no preset file).
    /// </summary>
    /// <param name="preset">Preset name without the <c>preset-</c> prefix or extension.</param>
    string GetPresetCss(string preset);

    /// <summary>Names of every available color preset (excluding the built-in <c>nova</c> default).</summary>
    IReadOnlyList<string> AvailablePresets { get; }
}

/// <summary>
/// Breaks the mutual forwarding between <see cref="ICssAssetProvider"/>'s style members and their
/// obsolete skin-named twins: an implementer that overrides neither set gets a clear
/// <see cref="NotImplementedException"/> instead of a stack overflow.
/// </summary>
internal static class CssAssetForwarding
{
    [ThreadStatic]
    private static HashSet<string>? _active;

    public static T Guard<T>(string member, Func<T> forward)
    {
        _active ??= [];
        if (!_active.Add(member))
            throw new NotImplementedException(
                $"{nameof(ICssAssetProvider)} implementations must implement the style members " +
                $"({nameof(ICssAssetProvider.GetSharedStyleCss)}, {nameof(ICssAssetProvider.GetStyleCss)}, " +
                $"{nameof(ICssAssetProvider.AvailableStyles)}).");
        try
        {
            return forward();
        }
        finally
        {
            _active.Remove(member);
        }
    }
}
