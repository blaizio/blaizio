using Microsoft.JSInterop;
using Xunit;

namespace Blaizio.Base.Tests;

/// <summary>
/// JsInstance owns a script-side instance across the gap between creating it and disposing it.
/// A component creates its instance over two awaits and can be disposed during either; these
/// prove a late arrival is released instead of leaking, and that every call is safe at any point.
/// </summary>
public class JsInstanceTests
{
    // A script instance: records the calls made on it.
    private sealed class FakeInstance : IJSObjectReference
    {
        public List<string> Calls { get; } = [];
        public bool Disposed { get; private set; }
        public Exception? Throw { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (Throw is not null) throw Throw;
            Calls.Add(identifier);
            return ValueTask.FromResult(identifier == "answer" ? (TValue)(object)42 : default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    // A module whose factory call completes only when the test says so.
    private sealed class FakeModule : IJSObjectReference
    {
        private readonly Queue<TaskCompletionSource<IJSObjectReference>> _pending = new();
        public int FactoryCalls { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            FactoryCalls++;
            var source = new TaskCompletionSource<IJSObjectReference>();
            _pending.Enqueue(source);
            return new ValueTask<TValue>(Cast<TValue>(source.Task));
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        public FakeInstance Land()
        {
            var instance = new FakeInstance();
            _pending.Dequeue().SetResult(instance);
            return instance;
        }

        public void Fail(Exception exception) => _pending.Dequeue().SetException(exception);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static async Task<TValue> Cast<TValue>(Task<IJSObjectReference> task) => (TValue)await task;
    }

    [Fact]
    public async Task A_created_instance_takes_calls_and_is_released_on_dispose()
    {
        var module = new FakeModule();
        var handle = new JsInstance();

        var creating = handle.CreateAsync(module, "create");
        var instance = module.Land();
        Assert.True(await creating);
        Assert.True(handle.IsCreated);

        await handle.InvokeVoidAsync("sync");
        Assert.Equal(42, await handle.InvokeAsync<int>("answer"));
        await handle.DisposeAsync();

        Assert.Equal(["sync", "answer", "dispose"], instance.Calls);
        Assert.True(instance.Disposed);
        Assert.False(handle.IsCreated);
    }

    [Fact]
    public async Task An_instance_that_lands_after_dispose_is_released_not_kept()
    {
        var module = new FakeModule();
        var handle = new JsInstance();

        var creating = handle.CreateAsync(module, "create");
        await handle.DisposeAsync(); // the component went away while the factory call was in flight
        var instance = module.Land();

        Assert.False(await creating);
        Assert.False(handle.IsCreated);
        Assert.Equal(["dispose"], instance.Calls);
        Assert.True(instance.Disposed);
    }

    [Fact]
    public async Task A_disposed_handle_never_calls_the_factory()
    {
        var module = new FakeModule();
        var handle = new JsInstance();
        await handle.DisposeAsync();

        Assert.False(await handle.CreateAsync(module, "create"));
        Assert.Equal(0, module.FactoryCalls);
    }

    [Fact]
    public async Task Calls_before_creation_and_after_disposal_do_nothing()
    {
        var handle = new JsInstance();
        await handle.InvokeVoidAsync("sync");
        Assert.Equal(0, await handle.InvokeAsync<int>("answer"));

        await handle.DisposeAsync();
        await handle.DisposeAsync(); // twice is fine
        await handle.InvokeVoidAsync("sync");
        Assert.True(handle.IsDisposed);
    }

    [Fact]
    public async Task Release_drops_a_creation_in_flight_and_allows_a_fresh_one()
    {
        var module = new FakeModule();
        var handle = new JsInstance();

        var first = handle.CreateAsync(module, "create");
        await handle.ReleaseAsync(); // e.g. a panel left the render tree mid-creation
        var stale = module.Land();
        Assert.False(await first);
        Assert.True(stale.Disposed);

        var second = handle.CreateAsync(module, "create");
        var fresh = module.Land();
        Assert.True(await second);
        Assert.True(handle.IsCreated);
        Assert.False(fresh.Disposed);
    }

    [Fact]
    public async Task Two_overlapping_creations_keep_one_instance()
    {
        var module = new FakeModule();
        var handle = new JsInstance();

        var first = handle.CreateAsync(module, "create");
        var second = handle.CreateAsync(module, "create"); // a second render before the first landed
        var kept = module.Land();
        var extra = module.Land();

        Assert.True(await first);
        Assert.False(await second);
        Assert.False(kept.Disposed);
        Assert.True(extra.Disposed);
    }

    [Fact]
    public async Task A_gone_circuit_is_not_an_error()
    {
        var module = new FakeModule();
        var handle = new JsInstance();

        var creating = handle.CreateAsync(module, "create");
        module.Fail(new JSDisconnectedException("gone"));
        Assert.False(await creating);

        var live = new JsInstance();
        var landing = live.CreateAsync(module, "create");
        var instance = module.Land();
        await landing;
        instance.Throw = new JSDisconnectedException("gone");
        await live.DisposeAsync(); // releasing over a dead circuit must not throw
    }

    [Fact]
    public async Task An_argument_disposed_under_the_call_is_swallowed_only_after_disposal()
    {
        var module = new FakeModule();
        var disposed = new JsInstance();
        var creating = disposed.CreateAsync(module, "create");
        await disposed.DisposeAsync();
        module.Fail(new ObjectDisposedException("selfRef"));
        Assert.False(await creating);

        var live = new JsInstance();
        var failing = live.CreateAsync(module, "create");
        module.Fail(new ObjectDisposedException("selfRef"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => failing);
    }
}
