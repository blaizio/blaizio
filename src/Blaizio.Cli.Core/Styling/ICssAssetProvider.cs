namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// Supplies the raw CSS assets <see cref="TailwindSetup"/> writes into a consumer project: the
/// theme token block, the Blaizio base contract, and one skin. The CLI backs this with files it
/// embeds from Blaizio.Ui so setup works fully offline and always matches the shipped components.
/// </summary>
public interface ICssAssetProvider
{
    /// <summary>The theme tokens + Tailwind token map (<c>:root</c>/<c>.dark</c> + <c>@theme</c>).</summary>
    string GetThemeCss();

    /// <summary>The base contract: <c>data-*</c> custom variants, keyframes, shared utilities.</summary>
    string GetBaseCss();

    /// <summary>One skin's component classes, e.g. <c>style-ember.css</c>.</summary>
    /// <param name="skin">Skin name without the <c>style-</c> prefix or extension.</param>
    string GetSkinCss(string skin);

    /// <summary>Names of every available skin (without the <c>style-</c> prefix).</summary>
    IReadOnlyList<string> AvailableSkins { get; }
}
