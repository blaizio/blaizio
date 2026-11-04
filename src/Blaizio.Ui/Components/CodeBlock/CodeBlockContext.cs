using Microsoft.AspNetCore.Components;

namespace Blaizio.Ui;

/// <summary>
/// What a <see cref="BzCodeBlock"/> shares with its parts: the marker its content carries so the
/// copy button can find the text, and the block-level wrap state its wrap button toggles.
/// </summary>
/// <param name="CopySource">The value of the content's <c>data-bz-copy-source</c> attribute.</param>
/// <param name="Wrap">Whether the block wraps long lines.</param>
/// <param name="ToggleWrap">Flips <paramref name="Wrap"/>.</param>
public sealed record CodeBlockContext(string CopySource, bool Wrap, EventCallback ToggleWrap)
{
    /// <summary>The selector the copy button reads the content's text through.</summary>
    public string CopySelector => $"[data-bz-copy-source=\"{CopySource}\"]";
}
