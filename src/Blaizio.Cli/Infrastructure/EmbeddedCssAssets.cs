using System.Reflection;
using Blaizio.Cli.Core.Styling;

namespace Blaizio.Cli.Infrastructure;

/// <summary>
/// <see cref="ICssAssetProvider"/> backed by CSS files embedded in the CLI at build time (the theme
/// tokens plus Blaizio.Ui's base contract and styles). Ships the styling offline and in lockstep with
/// the tool version, so <c>init</c> needs no registry round-trip to wire Tailwind.
/// </summary>
public sealed class EmbeddedCssAssets : ICssAssetProvider
{
    private static readonly Assembly Owner = typeof(EmbeddedCssAssets).Assembly;

    /// <inheritdoc />
    public string GetThemeCss() => Read("theme.css");

    /// <inheritdoc />
    public string GetAnimateCss() => Read("animate.css");

    /// <inheritdoc />
    public string GetBaseCss() => Read("blaizio.css");

    /// <inheritdoc />
    public string GetSharedStyleCss() => Read("shared.css");

    /// <inheritdoc />
    public string GetStyleCss(string style)
    {
        var resource = $"style-{style}.css";
        if (!AvailableStyles.Contains(style, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Unknown style '{style}'. Available: {string.Join(", ", AvailableStyles)}.", nameof(style));
        return Read(resource);
    }

    /// <inheritdoc />
    public string GetPresetCss(string preset)
    {
        if (!AvailablePresets.Contains(preset, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Unknown preset '{preset}'. Available: nova, {string.Join(", ", AvailablePresets)}.", nameof(preset));
        return Read($"preset-{preset}.css");
    }

    /// <inheritdoc />
    public IReadOnlyList<string> AvailableStyles { get; } = Scan("style-");

    /// <inheritdoc />
    public IReadOnlyList<string> AvailablePresets { get; } = Scan("preset-");

    private static string[] Scan(string prefix) =>
    [
        .. Owner.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".css", StringComparison.Ordinal))
            .Select(n => n[prefix.Length..^".css".Length])
            .OrderBy(n => n, StringComparer.Ordinal),
    ];

    private static string Read(string logicalName)
    {
        using var stream = Owner.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded CSS asset '{logicalName}' is missing from the CLI build.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
