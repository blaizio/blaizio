using Microsoft.JSInterop;

namespace Blaizio;

/// <summary>
/// One script instance of an overlay surface (its presence tracker, its positioning, its
/// dismissable layer...), created at most once each time the surface appears.
/// </summary>
/// <remarks>
/// <see cref="JsInstance"/> closes the gap between creating an instance and disposing it. A
/// surface needs one thing more: <c>OnAfterRenderAsync</c> runs again on every render, and a
/// second pass must not create a second instance while the first is still on its way. So the
/// creation is claimed before the first await and later renders find it claimed.
/// <para>
/// The claim stands until the release, whatever the factory answered: a surface whose anchor is
/// not in the page is not asked again on every render.
/// </para>
/// </remarks>
internal sealed class SurfaceScript
{
    private readonly JsInstance _instance = new();
    private Task<bool>? _creation;

    /// <summary>True from the claim until the release.</summary>
    public bool Claimed => _creation is not null;

    /// <summary>
    /// Imports <paramref name="modulePath"/> and calls <paramref name="factory"/>, unless this
    /// appearance of the surface already did. Check <see cref="Claimed"/> first when building the
    /// arguments costs something.
    /// </summary>
    public void Claim(IJSRuntime js, string modulePath, string factory, params object?[] args) =>
        _creation ??= _instance.CreateAsync(js, modulePath, factory, args);

    /// <summary>
    /// Calls a function on the instance once its creation has landed. Does nothing when there is
    /// no instance: never claimed, or released in the meantime.
    /// </summary>
    public async ValueTask InvokeVoidAsync(string identifier, params object?[] args)
    {
        if (_creation is { } creation) await creation;
        await _instance.InvokeVoidAsync(identifier, args);
    }

    /// <summary>
    /// The surface is gone but its component stays: releases the instance and gives the claim
    /// up, so the next appearance creates a fresh one.
    /// </summary>
    /// <remarks>
    /// A creation still on its way is waited for. It releases what it made when it lands, and the
    /// caller goes on only after that: a portaled element is back at its place before the render
    /// that removes it.
    /// </remarks>
    public ValueTask ReleaseAsync() => ReleaseAllAsync([this]);

    /// <summary>
    /// Releases several instances, in the order given. Every claim is given up before the first
    /// await, so a render that comes in while the releases are still running finds none of them
    /// claimed and creates all of them again, not just the ones already released.
    /// </summary>
    public static async ValueTask ReleaseAllAsync(SurfaceScript[] scripts)
    {
        var creations = new Task<bool>?[scripts.Length];
        var releases = new ValueTask[scripts.Length];
        for (var i = 0; i < scripts.Length; i++)
        {
            creations[i] = scripts[i]._creation;
            scripts[i]._creation = null;
            releases[i] = scripts[i]._instance.ReleaseAsync();
        }

        for (var i = 0; i < scripts.Length; i++)
        {
            await releases[i];
            if (creations[i] is { } creation) await creation;
        }
    }

    /// <summary>
    /// The component is going away: releases the instance for good. A creation still on its way
    /// is not waited for, it releases what it made when it lands.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _creation = null;
        return _instance.DisposeAsync();
    }
}
