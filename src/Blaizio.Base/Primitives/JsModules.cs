using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.JSInterop;

namespace Blaizio;

/// <summary>
/// Per-runtime cache of JS module imports. The browser caches module CODE, but every
/// <c>IJSRuntime.InvokeAsync("import", …)</c> still costs an interop round-trip and allocates a
/// new proxy - and floating surfaces import the same presence/positioning/dismiss modules once per
/// open instance. Sharing one <see cref="Task{IJSObjectReference}"/> per module path collapses that
/// to one import per circuit (keyed weakly by <see cref="IJSRuntime"/>, which is circuit-scoped, so
/// no DI registration and no per-consumer wiring).
/// </summary>
/// <remarks>
/// A cached module reference is SHARED: callers must never dispose what
/// <see cref="ImportAsync"/> returns (per-widget instances created FROM the module stay
/// caller-owned as before). The proxies live for the circuit and are released with it. A faulted
/// import (e.g. the circuit died mid-flight) is evicted so the next caller retries instead of
/// inheriting the failure.
/// <para>
/// Public on purpose: the styled components are distributed as SOURCE into consumer projects,
/// where they compile in the consumer's assembly - an internal cache would be unreachable from
/// the very components that need it.
/// </para>
/// </remarks>
public static class JsModules
{
    private static readonly ConditionalWeakTable<IJSRuntime, ConcurrentDictionary<string, Task<IJSObjectReference>>> s_caches = new();

    /// <summary>Import <paramref name="modulePath"/> once per runtime; later calls await the same task.</summary>
    public static Task<IJSObjectReference> ImportAsync(IJSRuntime js, string modulePath)
    {
        var cache = s_caches.GetOrCreateValue(js);
        return cache.GetOrAdd(modulePath, path => ImportCoreAsync(js, cache, path));
    }

    private static async Task<IJSObjectReference> ImportCoreAsync(
        IJSRuntime js,
        ConcurrentDictionary<string, Task<IJSObjectReference>> cache,
        string modulePath)
    {
        try
        {
            return await js.InvokeAsync<IJSObjectReference>("import", modulePath);
        }
        catch
        {
            // Never cache a failure - evict so the next open retries the import.
            cache.TryRemove(modulePath, out _);
            throw;
        }
    }
}
