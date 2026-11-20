using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blaizio;

/// <summary>
/// Shared base for an expandable panel that plays an exit animation before it leaves the render
/// tree. The panel stays rendered with <c>data-state="closed"</c> while its closing animation runs; a
/// small JS module (<c>dist/collapse.js</c>) measures the panel's natural height into a CSS variable
/// the keyframes animate to and reports back when the animation has finished so the element can be
/// removed (when the active style declares no animation it reports immediately, so closing is
/// instant). A derived panel can instead keep the closed element rendered and hidden (see
/// <see cref="KeepRenderedWhenClosed"/>).
/// </summary>
/// <remarks>
/// Both <see cref="BaseCollapsibleContent"/> and the accordion's panel build on this, so the
/// height-measurement and close/animation-end handshake live in one place. A derived component
/// supplies <see cref="IsOpen"/> (where the open state comes from) and
/// <see cref="BuildPanelAttributes"/> (the element's ARIA/id/<c>data-state</c>) and wires the
/// markup's element reference to <see cref="CaptureElement"/>. The methods the script calls back
/// (<see cref="OnCloseFinished"/>, <see cref="OnBeforeMatch"/>) are declared here, on the type the
/// object reference is created with: that is what keeps them in a trimmed app. A callback declared
/// only on a derived panel would be removed there, since nothing in C# calls it.
/// </remarks>
public abstract class CollapsiblePanelBase : BzComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    /// <summary>Panel content.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private bool _present;
    private bool _closing;
    private bool _hidden;
    private bool _justOpened;
    private ElementReference _element;
    private readonly JsInstance _instance = new();
    private DotNetObjectReference<CollapsiblePanelBase>? _selfRef;

    /// <summary>
    /// Whether the panel is in the render tree: open, closed but still animating out, or closed and
    /// kept rendered (<see cref="Hidden"/>).
    /// </summary>
    protected bool Present => _present;

    /// <summary>Whether the panel is animating closed - drives <c>data-state</c>.</summary>
    protected bool Closing => _closing;

    /// <summary>
    /// Whether the panel is closed and at rest but kept in the render tree - render it hidden. Only
    /// ever true when <see cref="KeepRenderedWhenClosed"/> is.
    /// </summary>
    protected bool Hidden => _hidden;

    /// <summary>The open state this panel follows (the collapsible's, or the accordion item's).</summary>
    protected abstract bool IsOpen { get; }

    /// <summary>
    /// Keep the closed panel in the render tree, hidden, instead of removing it once its exit
    /// animation has played. The components inside then keep their state between openings, and a
    /// panel that starts closed is rendered (hidden) from the start. Defaults to <see langword="false"/>.
    /// </summary>
    protected virtual bool KeepRenderedWhenClosed => false;

    /// <summary>The merged attribute dictionary for the rendered panel element.</summary>
    protected abstract IReadOnlyDictionary<string, object> BuildPanelAttributes();

    /// <summary>Captures the rendered panel element (measured for its height). Wire to <c>ElementRefCaptured</c>.</summary>
    protected void CaptureElement(ElementReference reference) => _element = reference;

    protected override void OnParametersSet()
    {
        var open = IsOpen;

        if (open && (!_present || _closing || _hidden))
        {
            _present = true;
            _closing = false;
            _hidden = false;
            _justOpened = true;
        }
        else if (!open && _present && !_closing && !_hidden)
        {
            // Keep the panel rendered with data-state="closed" while the exit animation plays;
            // OnCloseFinished then removes it (or hides it, when it is kept rendered).
            _closing = true;
        }
        else if (!open && !_present && KeepRenderedWhenClosed)
        {
            // Closed from the start (or just switched to kept-rendered): render it hidden right away,
            // so its content is in the page for find-in-page and its components exist to hold state.
            _present = true;
            _hidden = true;
        }
        else if (!open && _hidden && !KeepRenderedWhenClosed)
        {
            // No longer kept rendered: the hidden panel leaves the render tree (no animation - it
            // is not visible). OnAfterRenderAsync releases the JS instance.
            _present = false;
            _hidden = false;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_present)
        {
            await DisposeInstanceAsync();
            return;
        }

        if (!_instance.IsCreated)
        {
            _selfRef ??= DotNetObjectReference.Create(this);
            // createCollapse measures the (open) panel, setting the height variable. Import and
            // create in one call: the handle then knows about a release that arrives while the
            // module is still loading.
            await _instance.CreateAsync(Js, "./_content/blaizio.base/dist/collapse.js", "createCollapse", _element, _selfRef);
            _justOpened = false;
        }
        else if (_justOpened)
        {
            _justOpened = false;
            await _instance.InvokeVoidAsync("measure");
        }

        if (_closing)
        {
            // No-animation styles report back immediately; otherwise animationend does.
            await _instance.InvokeVoidAsync("onClosing");
        }
    }

    /// <summary>Invoked by dist/collapse.js once the close animation has played.</summary>
    [JSInvokable] public Task OnCloseFinished() => OnCloseFinishedCoreAsync();

    /// <summary>
    /// Invoked by dist/collapse.js when find-in-page matches text inside a panel that is kept
    /// rendered while closed (the <c>beforematch</c> event).
    /// </summary>
    [JSInvokable] public Task OnBeforeMatch() => OnBeforeMatchCoreAsync();

    /// <summary>
    /// What <see cref="OnBeforeMatch"/> does. The browser has already revealed the element; a panel
    /// that can open itself does so here. Does nothing by default.
    /// </summary>
    protected virtual Task OnBeforeMatchCoreAsync() => Task.CompletedTask;

    /// <summary>
    /// The body of <see cref="OnCloseFinished"/>: invoked once the close animation has played, it
    /// removes the panel from the render tree - or, when the panel is kept rendered, leaves it
    /// hidden.
    /// </summary>
    protected async Task OnCloseFinishedCoreAsync()
    {
        if (!_closing) return;
        _closing = false;
        if (KeepRenderedWhenClosed)
        {
            // The element stays, so its JS instance does too - the next opening only re-measures.
            _hidden = true;
        }
        else
        {
            _present = false;
            await DisposeInstanceAsync();
        }
        StateHasChanged();
    }

    // The panel left the render tree but the component lives on: drop the instance (and any
    // creation still in flight) so the next opening attaches a fresh one to the new element. The
    // close-finish callback and DisposeAsync can both get here (a navigation disposes the
    // component mid close-animation); the handle hands the reference to one of them only.
    private ValueTask DisposeInstanceAsync() => _instance.ReleaseAsync();

    public async ValueTask DisposeAsync()
    {
        // Instances only - the modules are shared via JsModules and outlive this component.
        await _instance.DisposeAsync();
        _selfRef?.Dispose();
        GC.SuppressFinalize(this);
    }
}
