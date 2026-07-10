namespace Blaizio.Cli.Core.Styling;

/// <summary>
/// How much of a project's Blaizio wiring an <c>init</c> run should (re)apply. Used by the docs
/// /create "Get Code" dialog's "Existing Project" apply cards.
/// </summary>
public enum StyleScope
{
    /// <summary>Theme + fonts + host/packages/components — the normal <c>init</c> run.</summary>
    Full,

    /// <summary>Re-apply the skin + preset CSS tokens only (no host/packages/components).</summary>
    Theme,

    /// <summary>Write the font overlay tokens only.</summary>
    Fonts,
}
