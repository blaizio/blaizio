using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazeo;

/// <summary>
/// Shared base for an expandable panel that plays an exit animation before it unmounts. The panel
/// stays mounted with <c>data-state="closed"</c> while its closing animation runs; a small JS module
/// (<c>dist/collapse.js</c>) measures the panel's natural height into a CSS variable the keyframes
/// animate to and reports back when the animation has finished so the element can unmount (when the
/// active skin declares no animation it reports immediately, so closing is instant).
/// </summary>
/// <remarks>
/// Both <see cref="BaseCollapsibleContent"/> and the accordion's panel build on this, so the
/// height-measurement and close/animation-end handshake live in one place. A derived component
/// supplies <see cref="IsOpen"/> (where the open state comes from) and
/// <see cref="BuildPanelAttributes"/> (the element's ARIA/id/<c>data-state</c>), wires the markup's
/// element reference to <see cref="CaptureElement"/>, and forwards a <c>[JSInvokable]</c> method to
/// <see cref="OnCloseFinishedCoreAsync"/> (the attribute must sit on the concrete type for JS to
/// resolve it).
/// </remarks>
public abstract class CollapsiblePanelBase : BzComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>Panel content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private bool _present;
    private bool _closing;
    private bool _justOpened;
    private ElementReference _element;
    private IJSObjectReference? _module;
    private IJSObjectReference? _instance;
    private DotNetObjectReference<CollapsiblePanelBase>? _selfRef;

    /// <summary>Whether the panel is in the render tree (open, or closed-but-still-animating-out).</summary>
    protected bool Present => _present;

    /// <summary>Whether the panel is animating closed - drives <c>data-state</c>.</summary>
    protected bool Closing => _closing;

    /// <summary>The open state this panel follows (the collapsible's, or the accordion item's).</summary>
    protected abstract bool IsOpen { get; }

    /// <summary>The merged attribute dictionary for the rendered panel element.</summary>
    protected abstract IReadOnlyDictionary<string, object> BuildPanelAttributes();

    /// <summary>Captures the rendered panel element (measured for its height). Wire to <c>ElementRefCaptured</c>.</summary>
    protected void CaptureElement(ElementReference reference) => _element = reference;

    protected override void OnParametersSet()
    {
        var open = IsOpen;

        if (open && (!_present || _closing))
        {
            _present = true;
            _closing = false;
            _justOpened = true;
        }
        else if (!open && _present && !_closing)
        {
            // Keep the panel mounted with data-state="closed" while the exit animation plays;
            // OnCloseFinished unmounts it.
            _closing = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_present) return;

        if (_instance is null)
        {
            _selfRef ??= DotNetObjectReference.Create(this);
            _module ??= await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Blazeo.Base/dist/collapse.js");
            // createCollapse measures the (open) panel, setting the height variable.
            _instance = await _module.InvokeAsync<IJSObjectReference>("createCollapse", _element, _selfRef);
            _justOpened = false;
        }
        else if (_justOpened)
        {
            _justOpened = false;
            await _instance.InvokeVoidAsync("measure");
        }

        if (_closing)
        {
            // No-animation skins report back immediately; otherwise animationend does.
            await _instance.InvokeVoidAsync("onClosing");
        }
    }

    /// <summary>
    /// The body of the panel's <c>[JSInvokable]</c> close callback: invoked once the close animation
    /// has played, it unmounts the panel. A derived component exposes the <c>[JSInvokable]</c>
    /// forwarder so JS can resolve the method on the concrete type.
    /// </summary>
    protected async Task OnCloseFinishedCoreAsync()
    {
        if (!_closing) return;
        _present = false;
        _closing = false;
        await DisposeInstanceAsync();
        StateHasChanged();
    }

    private async Task DisposeInstanceAsync()
    {
        if (_instance is null) return;
        try
        {
            await _instance.InvokeVoidAsync("dispose");
            await _instance.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone.
        }
        _instance = null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DisposeInstanceAsync();
            if (_module is not null) await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone.
        }

        _selfRef?.Dispose();
        GC.SuppressFinalize(this);
    }
}
