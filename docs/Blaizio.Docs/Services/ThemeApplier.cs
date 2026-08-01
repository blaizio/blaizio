using Blaizio.Cli.Core.Styling;
namespace Blaizio.Docs.Services;

/// <summary>
/// Applies theme knobs to the document: persists each pick into <see cref="ThemeComposerState"/>
/// and writes it through <see cref="IDocsJs"/> (the same html classes / localStorage theme.ts
/// uses), loading webfonts on demand. Also owns hover PREVIEW - canvas-only writes that never
/// touch the state. History, URL sync and page-shell coordination stay in CreatePage; this
/// service is only "make the document look like the selection".
/// </summary>
public sealed class ThemeApplier(IDocsJs js, ThemeComposerState state)
{
    /// <summary>Raised after a direction write so the page can sync the site shell.</summary>
    public Action<bool>? DirectionApplied { get; set; }

    // ---- Low-level writers (persist + apply one knob; no history, no url sync) ----

    public async Task ApplyStyleAsync(string v) { state.Style = v; await js.SetStyleAsync(v); }
    public async Task ApplyPresetAsync(string v) { state.Preset = v; await js.SetPresetAsync(v); }
    public async Task ApplyChartAsync(string v) { state.Chart = v; await js.SetChartAsync(v); }
    public async Task ApplyHeadingAsync(string v) { state.Heading = v; await js.SetHeadingAsync(v); await LoadWebFontAsync(v); }
    public async Task ApplyFontAsync(string v) { state.Font = v; await js.SetFontAsync(v); await LoadWebFontAsync(v); }
    public async Task ApplyRadiusAsync(string v) { state.Radius = v; await js.SetRadiusAsync(v); }

    public async Task ApplyDirAsync(bool rtl)
    {
        if (state.Rtl == rtl) return;
        state.Rtl = rtl;
        await js.SetDirAsync(rtl ? "rtl" : "ltr");
        DirectionApplied?.Invoke(rtl);
    }

    /// <summary>Write a whole selection (undo/redo, open-preset, deep link, reset). Never records.</summary>
    public async Task WriteAllAsync(PresetSelection s)
    {
        await ApplyStyleAsync(s.Style);
        await ApplyPresetAsync(s.Preset);
        await ApplyChartAsync(s.Chart);
        await ApplyHeadingAsync(s.Heading);
        await ApplyFontAsync(s.Font);
        await ApplyRadiusAsync(s.Radius);
        await ApplyDirAsync(s.Rtl);
    }

    // Hover preview from the rail: apply the hovered value to the canvas without recording it;
    // (knob, null) means the pointer left the menu - re-apply the recorded selection.
    public async Task PreviewAsync((string Knob, string? Value) p)
    {
        switch (p.Knob)
        {
            case "style": await js.SetStyleAsync(p.Value ?? state.Style); break;
            case "preset":
                // A theme preview previews the WHOLE look: its paired faces and chart series ride
                // along (into unlocked knobs), exactly like a real pick would apply them.
                if (p.Value is { } hovered && Presets.Find(hovered) is { } entry)
                {
                    await js.SetPresetAsync(hovered);
                    if (!state.Locks.Contains("heading"))
                    {
                        await js.SetHeadingAsync(entry.PairedHeading);
                        await LoadWebFontAsync(entry.PairedHeading);
                    }
                    if (!state.Locks.Contains("font"))
                    {
                        await js.SetFontAsync(entry.PairedFont);
                        await LoadWebFontAsync(entry.PairedFont);
                    }
                    if (!state.Locks.Contains("chart")) await js.SetChartAsync(entry.PairedChart);
                }
                else
                {
                    await js.SetPresetAsync(state.Preset);
                    await js.SetHeadingAsync(state.Heading);
                    await js.SetFontAsync(state.Font);
                    await js.SetChartAsync(state.Chart);
                }
                break;
            case "chart": await js.SetChartAsync(p.Value ?? state.Chart); break;
            case "heading":
                await js.SetHeadingAsync(p.Value ?? state.Heading);
                await LoadWebFontAsync(p.Value ?? state.Heading);
                break;
            case "font":
                await js.SetFontAsync(p.Value ?? state.Font);
                await LoadWebFontAsync(p.Value ?? state.Font);
                break;
            case "radius": await js.SetRadiusAsync(p.Value ?? state.Radius); break;
        }
    }

    /// <summary>The overlay class only names the family - a webfont pick must also load it.</summary>
    public async Task LoadWebFontAsync(string name)
    {
        if (FontCatalog.CssUrl(name) is { } url)
            await js.LoadWebFontAsync(url);
    }
}
