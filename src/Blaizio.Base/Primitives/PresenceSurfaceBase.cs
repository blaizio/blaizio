using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blaizio;

/// <summary>
/// Shared lifecycle of an overlay surface that plays an exit animation before it leaves the
/// render tree: a dialog window, its backdrop, and through <see cref="FloatingSurfaceBase"/> every
/// anchored surface (popover, tooltip, hover card, menu, listbox).
/// </summary>
/// <remarks>
/// The surface follows the open state of its root in three steps. Open: it is rendered with
/// <c>data-state="open"</c>. Closing: it stays rendered with <c>data-state="closed"</c> while the
/// style's exit animation plays. Closed: <c>ts/presence.ts</c> reports the end of that animation
/// (at once when the style has none) through <see cref="OnCloseFinished"/>, and the surface is
/// removed. Opening again while it is closing keeps the same element and the same scripts.
/// <para>
/// Every script instance bound to the surface is held by a <see cref="JsInstance"/>: created
/// once each time the surface appears, released when it has closed, and released for good with
/// the component, also when one of them is still on its way at that moment.
/// </para>
/// <para>
/// A derived component supplies <see cref="IsOpen"/>, renders its element while
/// <see cref="Present"/> is true with <c>data-state</c> taken from <see cref="Closing"/>, and
/// hands the element to <see cref="CaptureElement"/>.
/// </para>
/// </remarks>
public abstract class PresenceSurfaceBase : BzComponentBase, IAsyncDisposable
{
    private const string PresenceModule = "./_content/blaizio.base/dist/presence.js";

    // Only the surfaces of this assembly build on the lifecycle.
    private protected PresenceSurfaceBase()
    {
    }

    [Inject] private protected IJSRuntime Js { get; set; } = default!;

    /// <summary>Whether the root this surface belongs to is open.</summary>
    protected abstract bool IsOpen { get; }

    /// <summary>Hook for the concrete surface to react after the exit animation has fully removed it.</summary>
    protected virtual ValueTask OnClosedAsync() => ValueTask.CompletedTask;

    // The three below are fields, as the menu surfaces declared them before this class existed.

    /// <summary><see langword="true"/> while the surface should be in the DOM (open, or animating closed).</summary>
    protected bool Present;

    /// <summary><see langword="true"/> while the exit animation is playing (renders <c>data-state="closed"</c>).</summary>
    protected bool Closing;

    /// <summary>The surface element, captured by the concrete markup for the JS modules to drive.</summary>
    protected ElementReference Element;

    /// <summary>Captures the surface element reference (passed to <c>ElementRefCaptured</c> in the markup).</summary>
    protected void CaptureElement(ElementReference reference) => Element = reference;

    /// <summary>The instance of <c>ts/presence.ts</c> that reports the end of the exit animation.</summary>
    private protected readonly SurfaceScript Presence = new();

    private IDisposable? _selfReference;
    private bool _releasePending;

    /// <summary>True once the component was disposed.</summary>
    private protected bool Disposed { get; private set; }

    /// <summary>The reference the scripts call back through, created on first use.</summary>
    private protected object SelfReference => _selfReference ??= CreateSelfReference();

    /// <summary>
    /// Creates <see cref="SelfReference"/>. A class that adds a script callback of its own
    /// overrides this with the same body, so the reference is typed with the class that declares
    /// the callback and trimming keeps it.
    /// </summary>
    private protected virtual IDisposable CreateSelfReference() => DotNetObjectReference.Create(this);

    /// <summary>
    /// Whether the surface element is in the render tree, so scripts can attach to it. Differs
    /// from <see cref="Present"/> only where the markup has a condition of its own.
    /// </summary>
    private protected virtual bool HasElement => Present;

    /// <summary>The script instances of the surface, in the order they are torn down.</summary>
    private protected virtual SurfaceScript[] Scripts => [Presence];

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var open = IsOpen;

        if (open && (!Present || Closing))
        {
            var reopened = Present;
            Present = true;
            Closing = false;
            OnOpening(reopened);
        }
        else if (!open && Present && !Closing)
        {
            // Stays rendered with data-state="closed" while the exit animation plays;
            // OnCloseFinished then removes it.
            Closing = true;
        }
    }

    /// <summary>
    /// The surface starts to open. <paramref name="reopened"/> is true when it was still on its
    /// way out: the element and its scripts are the ones from before.
    /// </summary>
    private protected virtual void OnOpening(bool reopened)
    {
    }

    /// <summary>
    /// Takes the surface out of the render tree in the render that is about to run: no exit
    /// animation, no report from the script. For a surface inside another one that is closing.
    /// The scripts bound to the element are released after that render.
    /// </summary>
    private protected void DropWithoutAnimation()
    {
        if (!Present) return;
        Present = false;
        Closing = false;
        _releasePending = true;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_releasePending)
        {
            // The element went with the last render and the component stays. The instances were
            // bound to that element: kept, the next opening would render a new element that no
            // script ever touched.
            _releasePending = false;
            await SurfaceScript.ReleaseAllAsync(Scripts);
        }

        if (!HasElement) return;

        // Claimed before any await: this method runs again on every render, and a slow module
        // import must not let a second pass create an instance twice.
        ClaimScripts();

        await OnSurfaceRenderedAsync();

        if (Closing)
        {
            try
            {
                // A style with no exit animation reports back at once; otherwise animationend does.
                await Presence.InvokeVoidAsync("onClosing");
            }
            catch (JSDisconnectedException)
            {
                // Circuit already gone.
            }
            catch (ObjectDisposedException)
            {
                // The runtime was torn down under the call.
            }
        }
    }

    /// <summary>
    /// Creates the script instances of the surface, each through <see cref="SurfaceScript.Claim"/>.
    /// Runs after every render while the element is there, and must not await: the order of the
    /// claims is the order the scripts are created in.
    /// </summary>
    private protected virtual void ClaimScripts() => ClaimPresence();

    /// <summary>Creates the presence tracker for the element.</summary>
    private protected void ClaimPresence()
    {
        if (Presence.Claimed) return;
        Presence.Claim(Js, PresenceModule, "createPresence", Element, SelfReference);
    }

    /// <summary>
    /// Runs after every render while the element is there, once the scripts are claimed and
    /// before a closing surface asks the script about its exit animation.
    /// </summary>
    private protected virtual Task OnSurfaceRenderedAsync() => Task.CompletedTask;

    /// <summary>Called by the presence module once the exit animation has finished.</summary>
    [JSInvokable]
    public async Task OnCloseFinished()
    {
        // A report that arrives after the surface was opened again is not news.
        if (!Closing) return;
        Present = false;
        Closing = false;
        await FinishCloseAsync();
        await OnClosedAsync();
    }

    /// <summary>
    /// Releases the scripts and renders the surface away. The scripts go first: one of them may
    /// have moved the element to the document body, and it has to be back at its place before
    /// the render that removes it.
    /// </summary>
    private protected virtual async Task FinishCloseAsync()
    {
        foreach (var script in Scripts) await script.ReleaseAsync();
        StateHasChanged();
    }

    /// <summary>Releases every script instance for good. Runs when the component is disposed.</summary>
    private protected virtual async ValueTask DisposeScriptsAsync()
    {
        foreach (var script in Scripts) await script.DisposeAsync();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Disposed = true;
        // Instances only: the modules are shared through JsModules and outlive this component.
        await DisposeScriptsAsync();
        _selfReference?.Dispose();
        GC.SuppressFinalize(this);
    }
}
