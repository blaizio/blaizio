namespace Blaizio.Ui;

/// <summary>
/// Where a <see cref="BzCodeBlockCopyButton"/> is after a click - what its
/// <see cref="BzCodeBlockCopyButton.ChildContent"/> receives, so the content can change with it.
/// </summary>
public enum CodeBlockCopyState
{
    /// <summary>Nothing copied lately (the default).</summary>
    Idle,

    /// <summary>The text reached the clipboard. Lasts two seconds, then the button is idle again.</summary>
    Copied,

    /// <summary>The clipboard refused the write. Lasts two seconds, then the button is idle again.</summary>
    Failed,
}
