using Blaizio.Docs.Services;
using Microsoft.JSInterop;

namespace Blaizio.Docs.Shared;

/// <summary>
/// The state behind a docs copy button. At rest the button shows its own glyph and label; for a
/// moment after a press it shows whether the clipboard really took the text
/// (<see cref="IDocsJs.TryCopyAsync"/>): a check, or a struck-out copy glyph when the browser
/// refused the write (insecure context, denied permission) - never an assumed success. A button
/// renders <see cref="Announcement"/> in an always-present <c>role="status"</c> span, so screen
/// readers hear the outcome either way. Toast-based copies use <see cref="TryAsync"/> alone.
/// </summary>
public sealed class CopyFeedback
{
    private static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(1500);

    /// <summary>Null at rest; after a press (for <see cref="Hold"/>), whether the write landed.</summary>
    public bool? Outcome { get; private set; }

    /// <summary>The check or the struck-out copy glyph, else <paramref name="rest"/>.</summary>
    public Icon Glyph(Icon rest) => Outcome switch
    {
        true => Tabler.Outline.Check,
        false => Tabler.Outline.CopyOff,
        null => rest,
    };

    /// <summary>The glyph's color for the outcome, else <paramref name="rest"/>.</summary>
    public string Tone(string rest = "") => Outcome switch
    {
        true => "text-success",
        false => "text-destructive",
        null => rest,
    };

    /// <summary>The tooltip (or visible) label for the outcome, else <paramref name="rest"/>.</summary>
    public string Label(string rest) => Outcome switch
    {
        true => "Copied",
        false => "Copy failed",
        null => rest,
    };

    /// <summary>What the status region says: empty at rest, so only an outcome is announced.</summary>
    public string Announcement => Outcome switch
    {
        true => "Copied to clipboard",
        false => "Copy failed: select the text and copy it by hand",
        null => "",
    };

    /// <summary>
    /// Copy <paramref name="text"/> and show the outcome until the hold runs out.
    /// <paramref name="render"/> flushes the outcome in (intermediate awaits don't re-render);
    /// the event handler's own completion renders the return to rest.
    /// </summary>
    public async Task CopyAsync(IDocsJs js, string text, Action render)
    {
        Outcome = await TryAsync(js, text);
        render();
        await Task.Delay(Hold);
        Outcome = null;
    }

    /// <summary>Whether <paramref name="text"/> reached the clipboard. A module that fails to
    /// answer (a stale docs.js mid-rebuild) counts as a refused write, not an error page.</summary>
    public static async Task<bool> TryAsync(IDocsJs js, string text)
    {
        try { return await js.TryCopyAsync(text); }
        catch (JSException) { return false; }
    }
}
