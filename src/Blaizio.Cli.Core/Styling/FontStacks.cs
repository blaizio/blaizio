namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// The CSS font-family stacks behind the /create "Heading" and "Font" knobs (see
/// <see cref="PresetCode.Fonts"/>). Thin façade over <see cref="FontCatalog"/>, the canonical list.
/// </summary>
public static class FontStacks
{
    /// <summary>
    /// The CSS font-family stack for a <see cref="PresetCode.Fonts"/> value, or <c>null</c> for
    /// <c>"default"</c> (or any unknown value) — the built-in stack, which needs no overlay.
    /// </summary>
    public static string? Stack(string name) => FontCatalog.Stack(name);
}
