using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazeo;

/// <summary>
/// Shared machinery for a dropdown-menu surface - the root <see cref="BzDropdownMenuContent"/> and
/// each submenu's <see cref="BzDropdownMenuSubContent"/>. It owns the JS lifecycle: presence-managed
/// mount/unmount with an exit animation (ts/presence.js), @floating-ui anchoring (ts/positioning.js),
/// menu keyboard + pointer navigation (ts/menu.js), and - for the root only - outside-pointer-down
/// dismissal (ts/dismissableLayer.js). Focus is moved in by a wrapping <see cref="BzFocusScope"/>
/// that the concrete <c>.razor</c> renders; this base owns only the JS plumbing and the
/// <c>data-state</c>/<c>-side</c>/<c>-align</c> contract. The non-blocking floating model matches
/// <see cref="BzPopoverContent"/>: no overlay, no scroll lock, presses pass through.
/// </summary>
public abstract class MenuContentBase : BzComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>Whether the owning menu (root or submenu) is currently open.</summary>
    protected abstract bool IsOpen { get; }

    /// <summary>Where to place focus the moment the surface mounts.</summary>
    protected abstract MenuFocusIntent FocusIntent { get; }

    /// <summary>CSS selector for the trigger this surface anchors + positions against.</summary>
    protected abstract string AnchorSelector { get; }

    /// <summary>Attach an outside-pointer-down dismissal layer (root menu only; submenus dismiss with the root).</summary>
    protected abstract bool Dismissable { get; }

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

    /// <summary>Captures the surface element reference (passed to <c>ElementRefCaptured</c> in the markup).</summary>
    protected void CaptureElement(ElementReference reference) => Element = reference;

    /// <summary>
    /// Suppresses the wrapping <see cref="BzFocusScope"/>'s own auto-focus, on mount and unmount
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
        _menuModule ??= await Js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Blazeo.Base/dist/menu.js");
        return await _menuModule.InvokeAsync<IJSObjectReference>(
            "createMenu", Element, new { initialFocus = FocusIntentToken, typeaheadTimeout = 1000 });
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
            dismissOnEscape = false, // Escape is handled in C# OnKeyDown, like BzDialogContent / BzPopoverContent.
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
    public Task OnDismissRequested() => RequestCloseAsync();

    /// <summary>Called by the presence module once the exit animation has finished.</summary>
    [JSInvokable]
    public async Task OnCloseFinished()
    {
        if (!Closing) return;
        Present = false;
        Closing = false;
        // Unmount the surface, then return focus to the trigger ourselves. We do this rather than lean
        // on BzFocusScope's previouslyFocused (suppressed via SuppressAutoFocus): that target is
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
        if (_menuModule is null) return;
        try
        {
            await _menuModule.InvokeVoidAsync("focusTrigger", AnchorSelector);
        }
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
