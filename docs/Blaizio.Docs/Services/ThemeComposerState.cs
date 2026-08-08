using Blaizio.Cli.Core.Styling;

namespace Blaizio.Docs.Services;

/// <summary>
/// The theme composer's state machine: the current selection, the session-local knob locks, and
/// the bounded undo/redo history - pure state, no JS interop and no navigation. CreatePage owns
/// applying a selection to the document (theme.ts) and mirroring it into <c>?preset=</c>; every
/// mutation goes through here, so history and the URL can never record different selections.
/// Scoped: one composer session per circuit.
/// </summary>
public sealed class ThemeComposerState
{
    private const int HistoryLimit = 50;

    private readonly Stack<PresetSelection> _undo = new();
    private readonly Stack<PresetSelection> _redo = new();

    /// <summary>The skin, e.g. <c>ember</c>.</summary>
    public string Style { get; set; } = "ember";

    /// <summary>The color preset, e.g. <c>nova</c>.</summary>
    public string Preset { get; set; } = "nova";

    /// <summary>The chart palette name.</summary>
    public string Chart { get; set; } = "default";

    /// <summary>The heading face name.</summary>
    public string Heading { get; set; } = "default";

    /// <summary>The body face name.</summary>
    public string Font { get; set; } = "default";

    /// <summary>The radius scale name.</summary>
    public string Radius { get; set; } = "default";

    /// <summary>Whether the previewed direction is RTL (mirrors the header knob).</summary>
    public bool Rtl { get; set; }

    /// <summary>Directly-edited theme tokens, keyed by (token, mode). Ordered into
    /// <see cref="Selection"/> canonically (token order, light before dark) so equal edit sets
    /// always produce the same code.</summary>
    public Dictionary<(string Token, bool Dark), OklchColor> TokenOverrides { get; } = [];

    /// <summary>Set or replace one token edit.</summary>
    public void SetToken(string token, bool dark, OklchColor color) =>
        TokenOverrides[(token, dark)] = color;

    /// <summary>Remove one token edit (revert to the theme's own value).</summary>
    public void ClearToken(string token, bool dark) => TokenOverrides.Remove((token, dark));

    private IReadOnlyList<TokenOverride> OrderedOverrides =>
        [.. TokenOverrides
            .OrderBy(kv => Array.IndexOf(ThemeTokens.All, kv.Key.Token))
            .ThenBy(kv => kv.Key.Dark)
            .Select(kv => new TokenOverride(kv.Key.Token, kv.Key.Dark, kv.Value))];

    /// <summary>
    /// Locked knobs: Shuffle skips them and a theme pick won't overwrite their pairing targets.
    /// Session-local by design - locks are a working gesture, not part of the shareable selection.
    /// </summary>
    public HashSet<string> Locks { get; } = [];

    /// <summary>
    /// True once the initial selection (deep link or defaults) has been applied. History and URL
    /// writes are suppressed until then, so early renders can't clobber a shared link.
    /// </summary>
    public bool Seeded { get; set; }

    /// <summary>The current selection as one value (what history, the URL and dialogs exchange).</summary>
    public PresetSelection Selection =>
        new(Style, Preset, Rtl, Chart, Heading, Font, Radius) { Overrides = OrderedOverrides };

    /// <summary>Whether Undo has anywhere to go.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Whether Redo has anywhere to go.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Copy a whole selection into the knobs (undo/redo, open-preset, deep link, reset).</summary>
    public void Load(PresetSelection s)
    {
        (Style, Preset, Rtl, Chart, Heading, Font, Radius) =
            (s.Style, s.Preset, s.Rtl, s.Chart, s.Heading, s.Font, s.Radius);
        TokenOverrides.Clear();
        foreach (var o in s.Overrides) TokenOverrides[(o.Token, o.Dark)] = o.Color;
    }

    /// <summary>Toggle a knob's lock.</summary>
    public void ToggleLock(string knob)
    {
        if (!Locks.Add(knob)) Locks.Remove(knob);
    }

    /// <summary>
    /// Record the CURRENT selection as an undo step (call before applying a user action) and drop
    /// any redo tail. Bounded to the newest <see cref="HistoryLimit"/> steps. No-op until seeded.
    /// </summary>
    public void RecordHistory()
    {
        if (!Seeded) return;
        _undo.Push(Selection);
        _redo.Clear();
        if (_undo.Count > HistoryLimit)
        {
            // Trim the oldest by rebuilding (Stack has no bounded push).
            var kept = _undo.Take(HistoryLimit).Reverse().ToArray();
            _undo.Clear();
            foreach (var s in kept) _undo.Push(s);
        }
    }

    /// <summary>Step back: current selection moves to redo, the popped one comes out to apply.</summary>
    public bool TryUndo(out PresetSelection previous)
    {
        previous = default!;
        if (_undo.Count == 0) return false;
        _redo.Push(Selection);
        previous = _undo.Pop();
        return true;
    }

    /// <summary>Step forward: current selection moves to undo, the popped one comes out to apply.</summary>
    public bool TryRedo(out PresetSelection next)
    {
        next = default!;
        if (_redo.Count == 0) return false;
        _undo.Push(Selection);
        next = _redo.Pop();
        return true;
    }
}
