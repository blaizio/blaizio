using Microsoft.AspNetCore.Components;

namespace Blaizio.Ui;

/// <summary>
/// State a <see cref="BzSidebarProvider"/> cascades to every descendant sidebar part: the open and
/// collapsed state, the mobile state and its separate open flag, and the setters/toggle the trigger
/// and rail invoke. A fresh instance is cascaded whenever any of these change, so consumers (the
/// sidebar shell, menu buttons, …) re-render and re-resolve their <c>data-state</c>.
/// </summary>
/// <param name="Open">Whether the sidebar is expanded (desktop).</param>
/// <param name="OpenMobile">Whether the mobile sheet is open.</param>
/// <param name="IsMobile">Whether the viewport is below the mobile breakpoint.</param>
/// <param name="SetOpen">Sets the desktop open state.</param>
/// <param name="SetOpenMobile">Sets the mobile sheet open state.</param>
/// <param name="Toggle">Toggles the sidebar - the mobile sheet on mobile, the desktop state otherwise.</param>
public sealed record SidebarContext(
    bool Open,
    bool OpenMobile,
    bool IsMobile,
    EventCallback<bool> SetOpen,
    EventCallback<bool> SetOpenMobile,
    EventCallback Toggle)
{
    /// <summary><c>"expanded"</c> or <c>"collapsed"</c> - the value emitted as <c>data-state</c> for the skins to target.</summary>
    public string State => Open ? "expanded" : "collapsed";
}
