using Microsoft.JSInterop;

namespace Blaizio;

/// <summary>
/// Owns one script-side instance created from a module factory (<c>createSlider</c>,
/// <c>createCarousel</c>, ...) and closes the gap between creating it and disposing it.
/// </summary>
/// <remarks>
/// A component creates its instance in <c>OnAfterRenderAsync</c>, across two awaits (the module
/// import, then the factory call). The component can be disposed during either one. A plain
/// <see cref="IJSObjectReference"/> field is still null at that moment, so <c>DisposeAsync</c>
/// finds nothing to release, and the instance that arrives afterwards keeps its listeners and
/// observers on the page for the rest of the circuit. This handle remembers that it was disposed
/// and releases a late arrival itself.
/// <para>
/// Every call is safe at any point of the lifecycle: before the instance exists, and after it was
/// released, <see cref="InvokeVoidAsync"/> does nothing and <see cref="InvokeAsync{TValue}"/>
/// returns the default value. The script instance must expose a <c>dispose()</c> function; it is
/// called before the reference is released.
/// </para>
/// <para>
/// Public on purpose: the styled components are distributed as source into consumer projects,
/// where they compile in the consumer's assembly.
/// </para>
/// </remarks>
public sealed class JsInstance : IAsyncDisposable
{
    private IJSObjectReference? _reference;
    private bool _disposed;
    // Bumped by every release. A creation that started before the bump belongs to a lifetime that
    // has ended, so it releases what it made instead of installing it.
    private int _epoch;

    /// <summary>True while a script instance is attached.</summary>
    public bool IsCreated => _reference is not null;

    /// <summary>True once <see cref="DisposeAsync"/> ran. A disposed handle never creates again.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Calls <paramref name="factory"/> on <paramref name="module"/> and keeps the instance it
    /// returns. Returns false, keeping nothing, when the factory returned null, when the handle
    /// was disposed or released in the meantime, when an instance is already attached, or when the
    /// circuit is gone.
    /// </summary>
    public async Task<bool> CreateAsync(IJSObjectReference module, string factory, params object?[] args)
    {
        if (_disposed || _reference is not null) return false;
        var epoch = _epoch;
        IJSObjectReference? reference;
        try
        {
            reference = await module.InvokeAsync<IJSObjectReference?>(factory, args);
        }
        catch (JSDisconnectedException)
        {
            return false; // circuit gone before the instance landed
        }
        catch (ObjectDisposedException) when (_disposed || epoch != _epoch)
        {
            return false; // the owner disposed an argument (its DotNetObjectReference) under the call
        }

        // A factory may answer null when it has nothing to attach to yet (its element is not in
        // the DOM): nothing was created, and the caller may try again.
        if (reference is null) return false;

        if (_disposed || epoch != _epoch || _reference is not null)
        {
            await ReleaseAsync(reference);
            return false;
        }

        _reference = reference;
        return true;
    }

    /// <summary>
    /// Imports <paramref name="modulePath"/> through <see cref="JsModules"/>, then creates the
    /// instance as <see cref="CreateAsync(IJSObjectReference, string, object?[])"/> does.
    /// </summary>
    public async Task<bool> CreateAsync(IJSRuntime js, string modulePath, string factory, params object?[] args)
    {
        if (_disposed || _reference is not null) return false;
        var epoch = _epoch;
        IJSObjectReference module;
        try
        {
            module = await JsModules.ImportAsync(js, modulePath);
        }
        catch (JSDisconnectedException)
        {
            return false;
        }

        if (_disposed || epoch != _epoch) return false;
        return await CreateAsync(module, factory, args);
    }

    /// <summary>Calls a function on the instance. Does nothing when no instance is attached.</summary>
    public ValueTask InvokeVoidAsync(string identifier, params object?[] args) =>
        _reference is { } reference ? reference.InvokeVoidAsync(identifier, args) : ValueTask.CompletedTask;

    /// <summary>
    /// Calls a function on the instance and returns its result, or the default value of
    /// <typeparamref name="TValue"/> when no instance is attached.
    /// </summary>
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, params object?[] args) =>
        _reference is { } reference ? reference.InvokeAsync<TValue>(identifier, args) : ValueTask.FromResult(default(TValue)!);

    /// <summary>
    /// Releases the current instance, and any creation still in flight, but leaves the handle
    /// usable: a later <see cref="CreateAsync(IJSObjectReference, string, object?[])"/> attaches a
    /// fresh one. For a component that drops its instance while it stays on the page.
    /// </summary>
    public ValueTask ReleaseAsync()
    {
        _epoch++;
        var reference = _reference;
        _reference = null;
        return ReleaseAsync(reference);
    }

    /// <summary>Releases the instance for good. Safe to call more than once.</summary>
    public ValueTask DisposeAsync()
    {
        _disposed = true;
        return ReleaseAsync();
    }

    private static async ValueTask ReleaseAsync(IJSObjectReference? reference)
    {
        if (reference is null) return;
        try
        {
            await reference.InvokeVoidAsync("dispose");
            await reference.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone (navigation / refresh) - the page took the instance with it.
        }
        catch (ObjectDisposedException)
        {
            // The runtime itself was torn down first.
        }
    }
}
