using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// BaseResizeHandle's first-render init awaits the module import and then createResizeHandle. A fast
/// navigation can dispose the handle while either call is still in flight; the init must neither hand
/// JS its disposed DotNetObjectReference (ObjectDisposedException, the Blazor error banner) nor leak a
/// JS instance that resolves after disposal.
/// </summary>
public class ResizeHandleDisposeTests : BunitContext
{
    // bUnit refuses to plan IJSObjectReference results, so the handle gets a fake runtime instead.
    private void UseRuntime(Func<string, Task<object?>?> handler) =>
        Services.AddSingleton<IJSRuntime>(new FakeJsObject { Handler = handler });

    private static Func<string, Task<object?>?> Imports(Task<object?> module) =>
        id => id == "import" ? module : null;

    [Fact]
    public async Task Disposed_during_module_import_never_passes_the_disposed_reference_to_js()
    {
        var import = new TaskCompletionSource<object?>();
        var module = new FakeJsObject();
        UseRuntime(Imports(import.Task));

        Render<BaseResizeHandle>(p => p.Add(x => x.AriaLabel, "Resize"));
        await DisposeComponentsAsync();
        import.SetResult(module);

        await Settle();
        Assert.Empty(module.Calls);
        Assert.Null(module.SerializationError);
    }

    [Fact]
    public async Task Disposed_during_create_disposes_the_late_instance()
    {
        var instance = new FakeJsObject();
        var create = new TaskCompletionSource<object?>();
        var module = new FakeJsObject { Handler = id => id == "createResizeHandle" ? create.Task : null };
        UseRuntime(Imports(Task.FromResult<object?>(module)));

        Render<BaseResizeHandle>(p => p.Add(x => x.AriaLabel, "Resize"));
        await WaitUntil(() => module.Calls.Contains("createResizeHandle"));
        await DisposeComponentsAsync();
        create.SetResult(instance);

        await WaitUntil(() => instance.Calls.Contains("DisposeAsync"));
        Assert.Equal(["dispose", "DisposeAsync"], instance.Calls);
        Assert.Null(module.SerializationError);
    }

    [Fact]
    public async Task Disposed_after_init_disposes_the_instance()
    {
        var instance = new FakeJsObject();
        var module = new FakeJsObject { Handler = id => id == "createResizeHandle" ? Task.FromResult<object?>(instance) : null };
        UseRuntime(Imports(Task.FromResult<object?>(module)));

        Render<BaseResizeHandle>(p => p.Add(x => x.AriaLabel, "Resize"));
        await WaitUntil(() => module.Calls.Contains("createResizeHandle"));
        await DisposeComponentsAsync();

        await WaitUntil(() => instance.Calls.Contains("DisposeAsync"));
        Assert.Equal(["dispose", "DisposeAsync"], instance.Calls);
    }

    // Continuations hop through the renderer's dispatcher; give them a few turns to run.
    private static async Task Settle()
    {
        for (var i = 0; i < 20; i++) await Task.Delay(5);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++) await Task.Delay(5);
        Assert.True(condition(), "Timed out waiting for the JS call.");
    }

    /// <summary>
    /// Stands in for the runtime and the JS objects it returns. Records calls and, like the real interop serializer, reads every DotNetObjectReference
    /// argument (its Value getter throws ObjectDisposedException once the reference is disposed).
    /// </summary>
    private sealed class FakeJsObject : IJSObjectReference, IJSRuntime
    {
        public List<string> Calls { get; } = [];
        public ObjectDisposedException? SerializationError { get; private set; }
        public Func<string, Task<object?>?>? Handler { get; init; }

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            foreach (var arg in args ?? [])
            {
                if (arg is DotNetObjectReference<BaseResizeHandle> r)
                {
                    try { _ = r.Value; }
                    catch (ObjectDisposedException e) { SerializationError = e; throw; }
                }
            }
            Calls.Add(identifier);
            var task = Handler?.Invoke(identifier);
            return task is null ? default! : (TValue)(await task)!;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync()
        {
            Calls.Add("DisposeAsync");
            return ValueTask.CompletedTask;
        }
    }
}
