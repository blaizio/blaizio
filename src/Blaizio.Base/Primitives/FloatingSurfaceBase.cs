using Microsoft.JSInterop;

namespace Blaizio;

/// <summary>
/// Shared lifecycle of a surface that floats next to an anchor: a popover, a tooltip, a hover
/// card, a menu, a listbox. Adds anchored positioning (<c>ts/positioning.ts</c>, which also moves
/// the surface to the document body) and, where the surface asks for it, dismissal by a press
/// outside (<c>ts/dismissableLayer.ts</c>) to the presence lifecycle of
/// <see cref="PresenceSurfaceBase"/>.
/// </summary>
/// <remarks>
/// The script reports the side and alignment the surface really got (it flips to stay in view)
/// through <see cref="OnPlaced"/>, so <c>data-side</c> and <c>data-align</c> stay attributes that
/// Blazor renders, from <see cref="ResolvedSide"/> and <see cref="ResolvedAlign"/>.
/// </remarks>
public abstract class FloatingSurfaceBase : PresenceSurfaceBase
{
    private const string PositioningModule = "./_content/blaizio.base/dist/positioning.js";
    private const string DismissModule = "./_content/blaizio.base/dist/dismissableLayer.js";

    // Only the surfaces of this assembly build on the lifecycle.
    private protected FloatingSurfaceBase()
    {
    }

    /// <summary>CSS selector for the trigger this surface anchors + positions against.</summary>
    protected abstract string AnchorSelector { get; }

    /// <summary>Preferred side of the anchor. Flips to the opposite side to stay in view.</summary>
    protected abstract Side PreferredSide { get; }

    /// <summary>Alignment along the anchor's edge.</summary>
    protected abstract Align PreferredAlign { get; }

    /// <summary>Gap in pixels between the anchor and the surface.</summary>
    protected virtual double PreferredSideOffset => 4;

    /// <summary>Offset in pixels along the alignment axis.</summary>
    protected virtual double PreferredAlignOffset => 0;

    /// <summary>
    /// Render the surface in place instead of portaling it to the document body while open (the
    /// ts/positioning.js <c>inline</c> option).
    /// </summary>
    protected virtual bool RenderInline => false;

    /// <summary>Attach an outside-pointer-down dismissal layer.</summary>
    protected virtual bool Dismissable => false;

    /// <summary>Closes the owning root. Invoked for the dismissal the layer reports.</summary>
    protected virtual Task RequestCloseAsync() => Task.CompletedTask;

    /// <summary>
    /// Whether the layer also takes Escape. Off for a surface that closes on other keys as well
    /// and handles them in its own key handler.
    /// </summary>
    private protected virtual bool DismissOnEscape => false;

    /// <summary>The factory of <c>ts/positioning.ts</c> that places this surface.</summary>
    private protected virtual string PositioningFactory => "createPositioning";

    /// <summary>
    /// Whether <c>data-side</c> and <c>data-align</c> start from what was asked for each time the
    /// surface opens, instead of from what they held last.
    /// </summary>
    private protected virtual bool StartsFromRequestedPlacement => false;

    /// <summary>What <c>ts/dismissableLayer.ts</c> is created with.</summary>
    private protected virtual object DismissLayerOptions => new
    {
        // A press on the anchor is not "outside": the anchor toggles the surface itself.
        anchorSelector = AnchorSelector,
        dismissOnEscape = DismissOnEscape,
    };

    // Fields, as the menu surfaces declared them before this class existed.

    /// <summary>The resolved placement side reported back by the positioning module (Blazor owns the attribute).</summary>
    protected string ResolvedSide = "bottom";

    /// <summary>The resolved alignment reported back by the positioning module.</summary>
    protected string ResolvedAlign = "start";

    /// <summary>The instance of <c>ts/positioning.ts</c> that keeps the surface on its anchor.</summary>
    private protected readonly SurfaceScript Positioning = new();

    /// <summary>The surface's place in the stack of <c>ts/dismissableLayer.ts</c>.</summary>
    private protected readonly SurfaceScript DismissLayer = new();

    private protected override IDisposable CreateSelfReference() => DotNetObjectReference.Create(this);

    // The layer goes first: the document listeners stop before the positioning loop does, and
    // positioning puts a portaled element back before the presence tracker lets go of it.
    private protected override SurfaceScript[] Scripts => [DismissLayer, Positioning, Presence];

    private protected override void OnOpening(bool reopened)
    {
        if (!StartsFromRequestedPlacement) return;
        // Start from the side that was asked for: the first frame (the entry slide, the arrow)
        // is painted before the positioning script reports where the surface really landed.
        ResolvedSide = PreferredSide.ToAttribute();
        ResolvedAlign = PreferredAlign.ToAttribute();
    }

    private protected override void ClaimScripts()
    {
        ClaimPresence();
        ClaimPositioning();
        ClaimDismissLayer();
    }

    /// <summary>Anchors the element to <see cref="AnchorSelector"/>.</summary>
    private protected void ClaimPositioning()
    {
        if (Positioning.Claimed) return;
        var options = new
        {
            side = PreferredSide.ToAttribute(),
            align = PreferredAlign.ToAttribute(),
            sideOffset = PreferredSideOffset,
            alignOffset = PreferredAlignOffset,
            collisionPadding = 8,
            inline = RenderInline,
        };
        Positioning.Claim(Js, PositioningModule, PositioningFactory, AnchorSelector, Element, options, SelfReference);
    }

    /// <summary>Puts the element on the stack of dismissable surfaces, when it is <see cref="Dismissable"/>.</summary>
    private protected void ClaimDismissLayer()
    {
        if (!Dismissable || DismissLayer.Claimed) return;
        DismissLayer.Claim(Js, DismissModule, "createDismissableLayer", Element, SelfReference, DismissLayerOptions);
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

    /// <summary>Called by the dismissable layer when the surface is to be dismissed.</summary>
    [JSInvokable]
    public virtual Task OnDismissRequested() => RequestCloseAsync();
}
