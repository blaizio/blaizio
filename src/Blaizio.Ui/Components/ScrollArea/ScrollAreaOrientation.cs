namespace Blaizio.Ui;

/// <summary>Which scrollbars a <see cref="BzScrollArea"/> renders.</summary>
public enum ScrollAreaOrientation
{
    /// <summary>A vertical scrollbar only (the default).</summary>
    Vertical,

    /// <summary>A horizontal scrollbar only.</summary>
    Horizontal,

    /// <summary>Both scrollbars, with a corner where they meet.</summary>
    Both,
}
