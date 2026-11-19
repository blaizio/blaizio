using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blaizio;

/// <summary>
/// Shared machinery for a dropdown-menu surface - the root <see cref="BaseDropdownMenuContent"/> and
/// each submenu's <see cref="BaseDropdownMenuSubContent"/>. The lifecycle is the one every floating
/// surface shares (<see cref="FloatingSurfaceBase"/>: presence-managed render / removal with an exit
/// animation, @floating-ui anchoring, outside-pointer-down dismissal for the root); this class adds
/// what makes the surface a menu: keyboard + pointer navigation (ts/menu.js), focus that the menu
/// places and returns itself, and a submenu that leaves with its root. Focus is moved in by a
/// wrapping <see cref="BaseFocusScope"/> that the concrete <c>.razor</c> renders; this base owns
/// only the JS plumbing and the <c>data-state</c>/<c>-side</c>/<c>-align</c> contract. The
/// non-blocking floating model matches <see cref="BasePopoverContent"/>: no overlay, no scroll lock,
/// presses pass through.
/// </summary>
public abstract class MenuContentBase : FloatingSurfaceBase
{
    private const string MenuModule = "./_content/blaizio.base/dist/menu.js";

    /// <summary>Where to place focus the moment the surface renders.</summary>
    protected abstract MenuFocusIntent FocusIntent { get; }

    /// <summary>
    /// Arrow navigation wraps at the ends of the menu (past the last item returns to the first).
    /// Defaults to <see langword="true"/>; a concrete surface overrides it from its own <c>Loop</c> parameter.
    /// </summary>
    protected virtual bool MenuLoop => true;

    /// <summary>Attach an outside-pointer-down dismissal layer (root menu only; submenus dismiss with the root).</summary>
    protected abstract override bool Dismissable { get; }

    /// <summary>
    /// Dismiss when anything outside the surface scrolls. Off by default - element-anchored menus
    /// reposition with their trigger; the point-anchored context menu overrides this to close instead.
    /// </summary>
    protected virtual bool DismissOnScroll => false;

    /// <summary>
    /// <see langword="true"/> for a submenu surface. ts/menu.js then lets this level close itself on
    /// the inline-start arrow and on focus leaving it (reached back through <see cref="OnSubCloseRequested"/>),
    /// and the close restores focus to the trigger only if it was stranded - never yanking it off a
    /// sibling the pointer moved to.
    /// </summary>
    protected virtual bool IsSubmenu => false;

    /// <summary>
    /// <see langword="true"/> when an ancestor surface is being dismissed and will remove this one
    /// with it (a submenu when the whole menu closes). The surface then drops SYNCHRONOUSLY, skipping
    /// its own exit animation - it is a <c>position:fixed</c> descendant of the closing ancestor, and
    /// that ancestor's exit-animation <c>transform</c> would otherwise become its containing block and
    /// shift it across the screen for the animation's duration. The ancestor keeps its own animation.
    /// </summary>
    protected virtual bool AncestorClosing => false;

    /// <summary>
    /// Whether a pointer dismissal (an outside press) may restore focus to the trigger at all.
    /// On by default: the restore is stranded-only and invisible to pointer users (programmatic
    /// focus never matches <c>:focus-visible</c>), and it keeps the keyboard's Tab position at
    /// the menu instead of the top of the document. Set <see langword="false"/> when the trigger
    /// sits inside a host that reacts to any focus landing in it (an editor lighting up on
    /// focus-in) - a click-away then leaves focus where the click put it. Keyboard closes
    /// (Escape, selecting an item) always restore. Surfaces that are already stranded-only on
    /// pointer dismissal (submenus, menubar menus) ignore it.
    /// </summary>
    [Parameter] public bool RestoreFocusOnPointerDismiss { get; set; } = true;

    /// <summary>
    /// On close, restore focus to the trigger only when it was stranded (focus orphaned to
    /// <c>&lt;body&gt;</c>), instead of always. Submenus do this so a mouse-out close never yanks the
    /// highlight off a hovered sibling; a menubar menu does it so switching to another menu doesn't
    /// pull focus back to the old trigger. Defaults to <see cref="IsSubmenu"/>.
    /// </summary>
    protected virtual bool RestoreFocusOnlyIfStranded => IsSubmenu;

    /// <summary>
    /// Render the surface in place instead of portaling it to the document body while open (the
    /// ts/positioning.js <c>inline</c> option). Defaults to <see cref="IsSubmenu"/>: a submenu must
    /// stay a DOM descendant of its (already portaled) parent surface so the root's outside-press
    /// detection and teardown keep containing it; root surfaces override this from their own
    /// <c>Inline</c> parameter.
    /// </summary>
    protected override bool RenderInline => IsSubmenu;

    /// <summary>Closes the owning menu - invoked by the base only for the outside-dismiss callback.</summary>
    protected abstract override Task RequestCloseAsync();

    // The instance of ts/menu.js: navigation, typeahead, and where focus goes on open.
    private readonly SurfaceScript _menu = new();
    private bool _dismissedByPointer;
    private bool _refocusPending;

    /// <summary>
    /// Suppresses the wrapping <see cref="BaseFocusScope"/>'s own auto-focus, on first render and on removal
    /// alike (wired to both <c>OnMountAutoFocus</c> and <c>OnUnmountAutoFocus</c>). Menus own their
    /// focus end to end: <c>ts/menu.js</c> places it when the surface opens, and
    /// <see cref="PresenceSurfaceBase.OnCloseFinished"/> returns it to the trigger on close. The focus scope is kept only
    /// for its nested-scope stack management - its first-render auto-focus would race <c>ts/menu.js</c> (a
    /// cold-open flicker, an un-highlighted first item), and its removal restore targets an element
    /// captured at first render that the same race can leave pointing at a now-removed item.
    /// </summary>
    protected static void SuppressAutoFocus(FocusScopeEvent e) => e.PreventDefault();

    /// <summary>The <c>initialFocus</c> option ts/menu.js expects for this open.</summary>
    private string FocusIntentToken => FocusIntent switch
    {
        MenuFocusIntent.First => "first",
        MenuFocusIntent.Last => "last",
        _ => "none",
    };

    private protected override IDisposable CreateSelfReference() => DotNetObjectReference.Create(this);

    private protected override SurfaceScript[] Scripts => [DismissLayer, Positioning, _menu, Presence];

    private protected override object DismissLayerOptions => new
    {
        // A press on the trigger is not "outside" - the trigger toggles itself (no double-close).
        anchorSelector = AnchorSelector,
        dismissOnEscape = false, // Escape and Tab both close a menu: its own OnKeyDown handles the pair.
        dismissOnScroll = DismissOnScroll,
    };

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The whole menu is being dismissed and the root will remove us. Leave the DOM now, in the
        // same render that turns the root to data-state="closed", so the root's exit-animation
        // transform never has a fixed-positioned submenu descendant to reparent (the close "shift").
        // The element goes with this render; the script instances bound to it follow it.
        if (AncestorClosing)
        {
            DropWithoutAnimation();
            return;
        }

        base.OnParametersSet();
    }

    private protected override void OnOpening(bool reopened)
    {
        base.OnOpening(reopened);
        // Reopened while closing: the surface and its menu instance stay, and the instance
        // places the opening focus once, so it has to be asked for again.
        if (reopened) _refocusPending = true;
    }

    private protected override void ClaimScripts()
    {
        ClaimPresence();
        ClaimPositioning();
        ClaimMenu();
        ClaimDismissLayer();
    }

    private void ClaimMenu()
    {
        if (_menu.Claimed) return;
        var options = new
        {
            initialFocus = FocusIntentToken,
            typeaheadTimeout = 1000,
            loop = MenuLoop,
            isSubmenu = IsSubmenu,
            triggerSelector = AnchorSelector,
        };

        // The ref lets a submenu request its own close (inline-start arrow / focus-out); the root never does.
        _menu.Claim(Js, MenuModule, "createMenu", Element, options, SelfReference);
    }

    private protected override async Task OnSurfaceRenderedAsync()
    {
        if (!_refocusPending) return;
        _refocusPending = false;
        try
        {
            // The press that closed the menu moved focus to the page. Without this the
            // reopened menu had no focus, and Escape, the arrows and typing reached nothing.
            await _menu.InvokeVoidAsync("refocus");
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
    }

    /// <summary>Called by the dismissable layer on an outside pointer-down (root menu only).</summary>
    [JSInvokable]
    public override Task OnDismissRequested()
    {
        // Record that this close came from a press OUTSIDE the surface, so RestoreFocusAsync can leave
        // focus where the user clicked instead of pulling it back to the trigger - a menubar clicked
        // away from goes idle, with no trigger left re-highlighted.
        _dismissedByPointer = true;
        return RequestCloseAsync();
    }

    /// <summary>
    /// Called by ts/menu.js when a submenu surface closes itself: the inline-start arrow, or focus
    /// leaving it for a sibling. Routes to the same close the rest of the level uses; ts/menu.js has
    /// already placed focus (on the trigger or the hovered sibling), so the close won't move it.
    /// </summary>
    [JSInvokable]
    public Task OnSubCloseRequested() => RequestCloseAsync();

    private protected override async Task FinishCloseAsync()
    {
        // Positioning goes down FIRST: its dispose moves a portaled surface back to its placeholder,
        // and that must precede the removal render so Blazor removes the node from the tree it still
        // owns. Then remove it and return focus to the trigger ourselves. We do this rather than lean
        // on BaseFocusScope's previouslyFocused (suppressed via SuppressAutoFocus): that target is
        // captured at first render and races ts/menu.js's opening focus, so it can latch onto an item which,
        // when it is removed here, drops focus to <body> - the submenu-close-loses-parent-item bug.
        // focusTrigger is a no-op when the trigger is already gone (whole menu torn down), so a parent
        // menu's own restore wins. Then tear down the rest of the JS: pointer-down listener, menu
        // + presence listeners.
        await Positioning.ReleaseAsync();
        StateHasChanged();
        await RestoreFocusAsync();
        await DismissLayer.ReleaseAsync();
        await _menu.ReleaseAsync();
        await Presence.ReleaseAsync();
    }

    private async Task RestoreFocusAsync()
    {
        var byPointer = _dismissedByPointer;
        _dismissedByPointer = false;
        if (!_menu.Claimed) return;
        // A stranded-restore surface (submenu / menubar menu) dismissed by an outside PRESS leaves
        // focus where the press landed: returning it to the trigger would re-highlight a bar the user
        // just clicked away from. A keyboard dismiss (Escape) never sets the flag, so it still restores
        // and the bar stays navigable. RestoreFocusOnPointerDismiss=false opts a root surface into
        // the same rule.
        if (byPointer && (RestoreFocusOnlyIfStranded || !RestoreFocusOnPointerDismiss)) return;
        try
        {
            // Always stranded-only, scoped to our own (closing) surface: a keyboard or item-select
            // close restores the trigger (focus is inside the surface or fell to <body> with it),
            // but a dismissing click that already focused something else - a text input - keeps
            // the user's focus. Submenus additionally skip entirely on pointer dismissal (above):
            // ts/menu.js owns their close-focus (trigger on ArrowLeft, hovered sibling on mouse-out).
            var module = await JsModules.ImportAsync(Js, MenuModule);
            await module.InvokeVoidAsync("focusTrigger", AnchorSelector, true, Element);
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { } // DisposeAsync raced this close and took the module down.
    }

    /// <summary>
    /// Synchronously return focus to the trigger now, ahead of the close animation. A keyboard dismiss
    /// (Escape) on a surface whose trigger shows an "open" highlight uses this so that highlight runs
    /// straight into the focus highlight: otherwise the open highlight drops the instant the state
    /// flips and the focus highlight only returns once the animation has finished - a visible flicker.
    /// No-op until the menu module has loaded.
    /// </summary>
    protected async Task RefocusTriggerAsync()
    {
        if (!_menu.Claimed) return;
        try
        {
            var module = await JsModules.ImportAsync(Js, MenuModule);
            await module.InvokeVoidAsync("focusTrigger", AnchorSelector, false);
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { } // DisposeAsync raced this close and took the module down.
    }
}
