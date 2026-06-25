using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazeo;

/// <summary>
/// Shared machinery for a dropdown-menu surface - the root <see cref="BaseDropdownMenuContent"/> and
/// each submenu's <see cref="BaseDropdownMenuSubContent"/>. It owns the JS lifecycle: presence-managed
/// mount/unmount with an exit animation (ts/presence.js), @floating-ui anchoring (ts/positioning.js),
/// menu keyboard + pointer navigation (ts/menu.js), and - for the root only - outside-pointer-down
/// dismissal (ts/dismissableLayer.js). Focus is moved in by a wrapping <see cref="BaseFocusScope"/>
/// that the concrete <c>.razor</c> renders; this base owns only the JS plumbing and the
/// <c>data-state</c>/<c>-side</c>/<c>-align</c> contract. The non-blocking floating model matches
/// <see cref="BasePopoverContent"/>: no overlay, no scroll lock, presses pass through.
/// </summary>
public abstract class MenuContentBase : BzComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>Whether the owning menu (root or submenu) is currently open.</summary>
    protected abstract bool IsOpen { get; }

    /// <summary>Where to place focus the moment the surface mounts.</summary>
    protected abstract MenuFocusIntent FocusIntent { get; }

    /// <summary>
    /// Arrow navigation wraps at the ends of the menu (past the last item returns to the first).
    /// Defaults to <see langword="true"/>; a concrete surface overrides it from its own <c>Loop</c> parameter.
    /// </summary>
    protected virtual bool MenuLoop => true;

    /// <summary>CSS selector for the trigger this surface anchors + positions against.</summary>
    protected abstract string AnchorSelector { get; }

    /// <summary>Attach an outside-pointer-down dismissal layer (root menu only; submenus dismiss with the root).</summary>
    protected abstract bool Dismissable { get; }

    /// <summary>
    /// <see langword="true"/> for a submenu surface. ts/menu.js then lets this level close itself on
    /// the inline-start arrow and on focus leaving it (reached back through <see cref="OnSubCloseRequested"/>),
    /// and the close restores focus to the trigger only if it was stranded - never yanking it off a
    /// sibling the pointer moved to.
    /// </summary>
    protected virtual bool IsSubmenu => false;

    /// <summary>
    /// <see langword="true"/> when an ancestor surface is being dismissed and will unmount this one
    /// with it (a submenu when the whole menu closes). The surface then drops SYNCHRONOUSLY, skipping
    /// its own exit animation - it is a <c>position:fixed</c> descendant of the closing ancestor, and
    /// that ancestor's exit-animation <c>transform</c> would otherwise become its containing block and
    /// shift it across the screen for the animation's duration. The ancestor keeps its own animation.
    /// </summary>
    protected virtual bool AncestorClosing => false;

    /// <summary>
    /// On close, restore focus to the trigger only when it was stranded (focus orphaned to
    /// <c>&lt;body&gt;</c>), instead of always. Submenus do this so a mouse-out close never yanks the
    /// highlight off a hovered sibling; a menubar menu does it so switching to another menu doesn't
    /// pull focus back to the old trigger. Defaults to <see cref="IsSubmenu"/>.
    /// </summary>
    protected virtual bool RestoreFocusOnlyIfStranded => IsSubmenu;

    /// <summary>Preferred side of the anchor. Flips to the opposite side to stay in view.</summary>
    protected abstract Side PreferredSide { get; }

    /// <summary>Alignment along the anchor's edge.</summary>
    protected abstract Align PreferredAlign { get; }

    /// <summary>Gap in pixels between the anchor and the surface.</summary>
    protected virtual double PreferredSideOffset => 4;

    /// <summary>Offset in pixels along the alignment axis.</summary>
    protected virtual double PreferredAlignOffset => 0;

    /// <summary>Closes the owning menu - invoked by the base only for the outside-dismiss callback.</summary>
    protected abstract Task RequestCloseAsync();

    /// <summary>Hook for the concrete surface to react after the exit animation has fully unmounted it.</summary>
    protected virtual ValueTask OnClosedAsync() => ValueTask.CompletedTask;

    /// <summary><see langword="true"/> while the surface should be in the DOM (open, or animating closed).</summary>
    protected bool Present;

    /// <summary><see langword="true"/> while the exit animation is playing (renders <c>data-state="closed"</c>).</summary>
    protected bool Closing;

    /// <summary>The resolved placement side reported back by the positioning module (Blazor owns the attribute).</summary>
    protected string ResolvedSide = "bottom";

    /// <summary>The resolved alignment reported back by the positioning module.</summary>
    protected string ResolvedAlign = "start";

    /// <summary>The surface element, captured by the concrete markup for the JS modules to drive.</summary>
    protected ElementReference Element;

    private IJSObjectReference? _presenceModule;
    private IJSObjectReference? _positioningModule;
    private IJSObjectReference? _dismissModule;
    private IJSObjectReference? _menuModule;
    private Task<IJSObjectReference>? _presenceTask;
    private Task<IJSObjectReference>? _positioningTask;
    private Task<IJSObjectReference>? _dismissTask;
    private Task<IJSObjectReference>? _menuTask;
    private DotNetObjectReference<MenuContentBase>? _selfRef;
    private bool _dismissedByPointer;

    /// <summary>Captures the surface element reference (passed to <c>ElementRefCaptured</c> in the markup).</summary>
    protected void CaptureElement(ElementReference reference) => Element = reference;

    /// <summary>
    /// Suppresses the wrapping <see cref="BaseFocusScope"/>'s own auto-focus, on mount and unmount
    /// alike (wired to both <c>OnMountAutoFocus</c> and <c>OnUnmountAutoFocus</c>). Menus own their
    /// focus end to end: <c>ts/menu.js</c> places it when the surface opens, and
    /// <see cref="OnCloseFinished"/> returns it to the trigger on close. The focus scope is kept only
    /// for its nested-scope stack management - its mount auto-focus would race <c>ts/menu.js</c> (a
    /// cold-open flicker, an un-highlighted first item), and its unmount restore targets an element
    /// captured at mount that the same race can leave pointing at a now-unmounted item.
    /// </summary>
    protected static void SuppressAutoFocus(FocusScopeEvent e) => e.PreventDefault();

    /// <summary>The <c>initialFocus</c> option ts/menu.js expects for this open.</summary>
    private string FocusIntentToken => FocusIntent switch
    {
        MenuFocusIntent.First => "first",
        MenuFocusIntent.Last => "last",
        _ => "none",
    };

    protected override void OnParametersSet()
    {
        // The whole menu is being dismissed and the root will unmount us. Leave the DOM now, in the
        // same render that turns the root to data-state="closed", so the root's exit-animation
        // transform never has a fixed-positioned submenu descendant to reparent (the close "shift").
        if (AncestorClosing)
        {
            if (Present)
            {
                Present = false;
                Closing = false;
            }
            return;
        }

        var open = IsOpen;

        if (open && (!Present || Closing))
        {
            Present = true;
            Closing = false;
        }
        else if (!open && Present && !Closing)
        {
            Closing = true;
        }
    }

    [ExcludeFromCodeCoverage] // JS-interop seam: a no-op under bUnit's loose interop, verified in-browser.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Present) return;

        // Guards claimed synchronously before any await: OnAfterRenderAsync re-enters on every render,
        // and a slow module import must not let a second pass create twice.
        _presenceTask ??= CreatePresenceAsync();
        _positioningTask ??= CreatePositioningAsync();
        _menuTask ??= CreateMenuAsync();
        if (Dismissable) _dismissTask ??= CreateDismissableLayerAsync();

        if (Closing)
        {
            // No-animation skins report back immediately; otherwise animationend does.
            var presence = await _presenceTask;
            await presence.InvokeVoidAsync("onClosing");
        }
    }

    [ExcludeFromCodeCoverage]
    private async Task<IJSObjectReference> CreatePresenceAsync()
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        _presenceModule ??= await Js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Blazeo.Base/dist/presence.js");
        return await _presenceModule.InvokeAsync<IJSObjectReference>("createPresence", Element, _selfRef);
    }

    [ExcludeFromCodeCoverage]
    private async Task<IJSObjectReference> CreatePositioningAsync()
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        _positioningModule ??= await Js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Blazeo.Base/dist/positioning.js");

        var options = new
        {
            side = PreferredSide.ToAttribute(),
            align = PreferredAlign.ToAttribute(),
            sideOffset = PreferredSideOffset,
            alignOffset = PreferredAlignOffset,
            collisionPadding = 8,
        };

        return await _positioningModule.InvokeAsync<IJSObjectReference>(
            "createPositioning", AnchorSelector, Element, options, _selfRef);
    }

    [ExcludeFromCodeCoverage]
    private async Task<IJSObjectReference> CreateMenuAsync()
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        _menuModule ??= await Js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Blazeo.Base/dist/menu.js");

        var options = new
        {
            initialFocus = FocusIntentToken,
            typeaheadTimeout = 1000,
            loop = MenuLoop,
            isSubmenu = IsSubmenu,
            triggerSelector = AnchorSelector,
        };

        // The ref lets a submenu request its own close (inline-start arrow / focus-out); the root never does.
        return await _menuModule.InvokeAsync<IJSObjectReference>("createMenu", Element, options, _selfRef);
    }

    [ExcludeFromCodeCoverage]
    private async Task<IJSObjectReference> CreateDismissableLayerAsync()
    {
        _selfRef ??= DotNetObjectReference.Create(this);
        _dismissModule ??= await Js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Blazeo.Base/dist/dismissableLayer.js");

        var options = new
        {
            // A press on the trigger is not "outside" - the trigger toggles itself (no double-close).
            anchorSelector = AnchorSelector,
            dismissOnEscape = false, // Escape is handled in C# OnKeyDown, like BaseDialogContent / BasePopoverContent.
        };
        return await _dismissModule.InvokeAsync<IJSObjectReference>(
            "createDismissableLayer", Element, _selfRef, options);
    }

    /// <summary>Called by the positioning module when the resolved placement flips.</summary>
    [JSInvokable]
    public void OnPlaced(string side, string align)
    {
        if (ResolvedSide == side && ResolvedAlign == align) return;
        ResolvedSide = side;
        ResolvedAlign = align;
        StateHasChanged();
    }

    /// <summary>Called by the dismissable layer on an outside pointer-down (root menu only).</summary>
    [JSInvokable]
    public Task OnDismissRequested()
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

    /// <summary>Called by the presence module once the exit animation has finished.</summary>
    [JSInvokable]
    public async Task OnCloseFinished()
    {
        if (!Closing) return;
        Present = false;
        Closing = false;
        // Unmount the surface, then return focus to the trigger ourselves. We do this rather than lean
        // on BaseFocusScope's previouslyFocused (suppressed via SuppressAutoFocus): that target is
        // captured at mount and races ts/menu.js's opening focus, so it can latch onto an item which,
        // when it unmounts here, drops focus to <body> - the submenu-close-loses-parent-item bug.
        // focusTrigger is a no-op when the trigger is already gone (whole menu torn down), so a parent
        // menu's own restore wins. Then tear down the JS: pointer-down listener, positioning loop, menu
        // + presence listeners.
        StateHasChanged();
        await RestoreFocusAsync();
        await DisposeDismissAsync();
        await DisposePositioningAsync();
        await DisposeMenuAsync();
        await DisposePresenceAsync();
        await OnClosedAsync();
    }

    [ExcludeFromCodeCoverage] // JS-interop seam: verified in-browser.
    private async Task RestoreFocusAsync()
    {
        var byPointer = _dismissedByPointer;
        _dismissedByPointer = false;
        if (_menuModule is null) return;
        // A stranded-restore surface (submenu / menubar menu) dismissed by an outside PRESS leaves
        // focus where the press landed: returning it to the trigger would re-highlight a bar the user
        // just clicked away from. A keyboard dismiss (Escape) never sets the flag, so it still restores
        // and the bar stays navigable.
        if (byPointer && RestoreFocusOnlyIfStranded) return;
        try
        {
            // A submenu only restores focus if it was stranded - ts/menu.js owns submenu close-focus
            // (trigger on ArrowLeft, the hovered sibling on mouse-out), so we must not yank it back.
            await _menuModule.InvokeVoidAsync("focusTrigger", AnchorSelector, RestoreFocusOnlyIfStranded);
        }
        catch (JSDisconnectedException) { }
    }

    /// <summary>
    /// Synchronously return focus to the trigger now, ahead of the close animation. A keyboard dismiss
    /// (Escape) on a surface whose trigger shows an "open" highlight uses this so that highlight runs
    /// straight into the focus highlight: otherwise the open highlight drops the instant the state
    /// flips and the focus highlight only returns once the animation has finished - a visible flicker.
    /// No-op until the menu module has loaded.
    /// </summary>
    [ExcludeFromCodeCoverage] // JS-interop seam: verified in-browser.
    protected async Task RefocusTriggerAsync()
    {
        if (_menuModule is null) return;
        try { await _menuModule.InvokeVoidAsync("focusTrigger", AnchorSelector, false); }
        catch (JSDisconnectedException) { }
    }

    [ExcludeFromCodeCoverage]
    private async Task DisposeDismissAsync()
    {
        if (_dismissTask is null) return;
        try
        {
            var dismiss = await _dismissTask;
            await dismiss.InvokeVoidAsync("dispose");
            await dismiss.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        _dismissTask = null;
    }

    [ExcludeFromCodeCoverage]
    private async Task DisposePositioningAsync()
    {
        if (_positioningTask is null) return;
        try
        {
            var positioning = await _positioningTask;
            await positioning.InvokeVoidAsync("dispose");
            await positioning.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        _positioningTask = null;
    }

    [ExcludeFromCodeCoverage]
    private async Task DisposeMenuAsync()
    {
        if (_menuTask is null) return;
        try
        {
            var menu = await _menuTask;
            await menu.InvokeVoidAsync("dispose");
            await menu.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        _menuTask = null;
    }

    [ExcludeFromCodeCoverage]
    private async Task DisposePresenceAsync()
    {
        if (_presenceTask is null) return;
        try
        {
            var presence = await _presenceTask;
            await presence.InvokeVoidAsync("dispose");
            await presence.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        _presenceTask = null;
    }

    [ExcludeFromCodeCoverage]
    public async ValueTask DisposeAsync()
    {
        try
        {
            await DisposeDismissAsync();
            await DisposePositioningAsync();
            await DisposeMenuAsync();
            await DisposePresenceAsync();
            if (_dismissModule is not null) await _dismissModule.DisposeAsync();
            if (_positioningModule is not null) await _positioningModule.DisposeAsync();
            if (_menuModule is not null) await _menuModule.DisposeAsync();
            if (_presenceModule is not null) await _presenceModule.DisposeAsync();
        }
        catch (JSDisconnectedException) { }

        _selfRef?.Dispose();
        GC.SuppressFinalize(this);
    }
}
