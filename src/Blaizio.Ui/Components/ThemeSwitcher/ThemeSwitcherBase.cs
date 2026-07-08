using Blaizio;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blaizio.Ui;

/// <summary>
/// Shared plumbing for the theme switcher components (<c>BzThemeSwitcher</c>, <c>BzThemeMenu</c>,
/// <c>BzThemeToggle</c>): binds the scoped <see cref="IThemeService"/> (localStorage persistence;
/// the pre-paint counterpart is Blaizio.Base's <c>dist/boot.js</c>) behind two rendered states.
/// Every switcher on the page subscribes to the service's change notifications, so a theme set
/// anywhere - another switcher, <see cref="IThemeService.SetThemeAsync"/> from app code, or the OS
/// flipping while in system mode - re-renders them all in step.
/// </summary>
public abstract class ThemeSwitcherBase : ComponentBase, IDisposable
{
    [Inject] protected IThemeService ThemeService { get; set; } = default!;

    /// <summary>The persisted preference: <c>"light"</c>, <c>"dark"</c> or <c>"system"</c> (the default).</summary>
    protected string Theme { get; private set; } = "system";

    /// <summary>What is on screen right now: <c>"light"</c> or <c>"dark"</c> (system resolved against the OS).</summary>
    protected string Resolved { get; private set; } = "light";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        ThemeService.ThemeChanged += OnThemeChanged;
        try
        {
            Theme = await ThemeService.GetThemeAsync();
            Resolved = await ThemeService.GetResolvedThemeAsync();
            StateHasChanged();
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone before the service was wired.
        }
    }

    /// <summary>Applies and persists a preference. State updates arrive back through the change event.</summary>
    protected async Task SetThemeAsync(string theme)
    {
        try
        {
            await ThemeService.SetThemeAsync(theme);
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone - nothing to apply to.
        }
    }

    // Invoked by IThemeService on every theme change (this switcher's or anyone else's).
    private void OnThemeChanged(ThemeChangedEventArgs args)
    {
        Theme = args.Theme;
        Resolved = args.Resolved;
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose() => ThemeService.ThemeChanged -= OnThemeChanged;
}
