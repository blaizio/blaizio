namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// The CSS font-family stacks behind the /create "Heading" and "Font" knobs (see
/// <see cref="PresetCode.Fonts"/>). Keep in sync with docs DocsThemes.Fonts / Styles/tokens.css.
/// </summary>
public static class FontStacks
{
    /// <summary>
    /// The CSS font-family stack for a <see cref="PresetCode.Fonts"/> value, or <c>null</c> for
    /// <c>"default"</c> (or any unknown value) — the built-in stack, which needs no overlay.
    /// </summary>
    public static string? Stack(string name) => name switch
    {
        "humanist" => "\"Segoe UI\", \"Helvetica Neue\", Helvetica, Arial, sans-serif",
        "classic" => "Georgia, Cambria, \"Times New Roman\", serif",
        "code" => "ui-monospace, \"Cascadia Code\", Consolas, \"SF Mono\", monospace",
        "soft" => "ui-rounded, \"SF Pro Rounded\", \"Segoe UI\", system-ui, sans-serif",
        _ => null,
    };
}
