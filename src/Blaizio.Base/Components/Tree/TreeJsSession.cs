using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blaizio;

/// <summary>
/// The JS side of a <see cref="BaseTree{TItem}"/>: attaches ts/tree.ts (pointer drag-and-drop) on
/// first render, the scroll observer while virtualized, and the marquee overflow watcher while
/// MarqueeLabels is on - then keeps each in step with the tree's parameters render over render and
/// disposes the instances (never the shared modules) at the end. The
/// <see cref="DotNetObjectReference{TValue}"/> handed to JS is the TREE, so every
/// [JSInvokable] callback stays on the component and ts/tree.ts needs no knowledge of this split.
/// </summary>
internal sealed class TreeJsSession<TItem>(BaseTree<TItem> tree, IJSRuntime js)
{
    private const string TreeModule = "./_content/blaizio.base/dist/tree.js";

    private readonly JsInstance _instance = new();
    private readonly JsInstance _scrollObserver = new();
    private readonly JsInstance _marqueeInstance = new();
    private DotNetObjectReference<BaseTree<TItem>>? _selfRef;
    private bool _attached;
    private bool _disabled;
    private bool _draggable;
    private bool _virtualized;
    private bool _marquee;

    /// <summary>
    /// Called from the tree's OnAfterRenderAsync on every render: first call wires everything up,
    /// later calls diff the flags (Disabled/Draggable/Virtualize/MarqueeLabels) and sync JS.
    /// Swallows <see cref="JSDisconnectedException"/> - a gone circuit has nothing to keep in step.
    /// </summary>
    public async Task SyncAsync(ElementReference element)
    {
        try
        {
            if (!_attached)
            {
                _attached = true;
                _selfRef ??= DotNetObjectReference.Create(tree);
                var module = await JsModules.ImportAsync(js, TreeModule);
                _disabled = tree.Disabled;
                _draggable = tree.Draggable;
                await _instance.CreateAsync(module, "createTree", element, _selfRef, new
                {
                    id = tree.ResolvedId,
                    group = tree.Group,
                    disabled = tree.Disabled,
                    dragEnabled = tree.Draggable,
                    delayMs = tree.DragDelayMs,
                    hoverExpandMs = tree.HoverExpandDelayMs,
                    transferIn = tree.AllowTransferIn,
                    transferOut = tree.AllowTransferOut,
                    indicatorClass = tree.DropIndicatorClass,
                });

                _virtualized = tree.Virtualize;
                if (tree.Virtualize)
                    await _scrollObserver.CreateAsync(module, "observeScroll", element, _selfRef);

                await SyncMarqueeAsync(element);
                return;
            }

            if (!_instance.IsCreated) return;

            if (tree.Virtualize != _virtualized)
            {
                // Virtualization was toggled after first render: attach or detach the scroll observer.
                _virtualized = tree.Virtualize;
                if (tree.Virtualize)
                {
                    // Import and create in one call: the handle then knows about a release that
                    // arrives while the module is still loading.
                    await _scrollObserver.CreateAsync(js, TreeModule, "observeScroll", element, _selfRef);
                }
                else
                {
                    await _scrollObserver.ReleaseAsync();
                }
            }
            else if (tree.Disabled != _disabled || tree.Draggable != _draggable)
            {
                _disabled = tree.Disabled;
                _draggable = tree.Draggable;
                await _instance.InvokeVoidAsync("update", new { disabled = tree.Disabled, dragEnabled = tree.Draggable });
            }
            else if (tree.MarqueeLabels != _marquee)
            {
                await SyncMarqueeAsync(element);
            }
        }
        catch (JSDisconnectedException)
        {
            // Circuit gone.
        }
    }

    // Attach or detach the marquee overflow watcher (ts/marquee.ts) to match MarqueeLabels.
    private async Task SyncMarqueeAsync(ElementReference element)
    {
        _marquee = tree.MarqueeLabels;
        if (tree.MarqueeLabels && !_marqueeInstance.IsCreated)
        {
            await _marqueeInstance.CreateAsync(js, "./_content/blaizio.base/dist/marquee.js", "createMarquee", element);
        }
        else if (!tree.MarqueeLabels)
        {
            await _marqueeInstance.ReleaseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Instances only - the modules are shared via JsModules and outlive this component.
        await _scrollObserver.DisposeAsync();
        await _instance.DisposeAsync();
        await _marqueeInstance.DisposeAsync();
        _selfRef?.Dispose();
    }
}
