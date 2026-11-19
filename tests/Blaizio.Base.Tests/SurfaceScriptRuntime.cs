using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blaizio.Base.Tests;

/// <summary>
/// A script runtime for the overlay surfaces: it writes down every call in order, keeps a record
/// of every instance a factory made, and lets a test hold an import, a factory call or a call on
/// an instance back until it says so. That is what the lifecycle tests need to stand a component
/// in the middle of an interop round trip, which bUnit's own interop answers at once.
/// </summary>
internal sealed class SurfaceScriptRuntime : IJSRuntime
{
    private readonly HashSet<string> _heldFactories = [];
    private readonly Dictionary<string, Queue<(TaskCompletionSource<IJSObjectReference?> Source, string Label)>> _pendingFactories = [];
    private readonly Dictionary<string, TaskCompletionSource<IJSObjectReference>> _heldImports = [];
    private readonly Dictionary<string, int> _ordinals = [];

    /// <summary>Every call, in the order it was made: <c>createPresence#1.onClosing</c>, <c>createPresence#1 released</c>...</summary>
    public List<string> Log { get; } = [];

    /// <summary>Every instance a factory was asked for, landed or not.</summary>
    public List<ScriptInstance> Instances { get; } = [];

    /// <summary>The instances that landed and were not released.</summary>
    public IEnumerable<ScriptInstance> Live => Instances.Where(i => i.Landed && !i.Released);

    public IReadOnlyList<ScriptInstance> Of(string factory) => Instances.Where(i => i.Factory == factory).ToList();

    public int Count(string entry) => Log.Count(line => line == entry);

    /// <summary>Calls to <paramref name="factory"/> stay unanswered until <see cref="Land"/>.</summary>
    public void HoldFactory(string factory) => _heldFactories.Add(factory);

    public void ReleaseFactory(string factory) => _heldFactories.Remove(factory);

    /// <summary>Answers the oldest held call to <paramref name="factory"/> with its instance.</summary>
    public ScriptInstance Land(string factory)
    {
        var (source, label) = _pendingFactories[factory].Dequeue();
        var instance = Instances.Single(i => i.Label == label);
        instance.Landed = true;
        source.SetResult(instance);
        return instance;
    }

    /// <summary>Answers the oldest held call to <paramref name="factory"/> with nothing.</summary>
    public void LandNothing(string factory) => _pendingFactories[factory].Dequeue().Source.SetResult(null);

    /// <summary>The import of <paramref name="module"/> (<c>presence.js</c>) stays unanswered until <see cref="LandImport"/>.</summary>
    public void HoldImport(string module) => _heldImports[module] = new TaskCompletionSource<IJSObjectReference>();

    public void LandImport(string module)
    {
        var source = _heldImports[module];
        _heldImports.Remove(module);
        source.SetResult(new Module(this, module));
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (identifier != "import")
        {
            Log.Add($"runtime.{identifier}");
            return new ValueTask<TValue>(default(TValue)!);
        }

        var module = ((string)args![0]!).Split('/')[^1];
        Log.Add($"import {module}");
        return _heldImports.TryGetValue(module, out var held)
            ? new ValueTask<TValue>(Cast<TValue>(held.Task))
            : new ValueTask<TValue>((TValue)(object)new Module(this, module));
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);

    private static async Task<TValue> Cast<TValue>(Task<IJSObjectReference> task) => (TValue)await task;

    private static async Task<TValue> Answer<TValue>(Task<IJSObjectReference?> task) => (TValue)(await task)!;

    private sealed class Module(SurfaceScriptRuntime runtime, string name) : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (typeof(TValue) != typeof(IJSObjectReference))
            {
                runtime.Log.Add($"{name}::{identifier}");
                return new ValueTask<TValue>(default(TValue)!);
            }

            runtime._ordinals[identifier] = runtime._ordinals.GetValueOrDefault(identifier) + 1;
            var instance = new ScriptInstance(runtime, identifier, $"{identifier}#{runtime._ordinals[identifier]}", args ?? []);
            runtime.Instances.Add(instance);
            runtime.Log.Add(identifier);

            if (!runtime._heldFactories.Contains(identifier))
            {
                instance.Landed = true;
                return new ValueTask<TValue>((TValue)(object)instance);
            }

            var source = new TaskCompletionSource<IJSObjectReference?>();
            if (!runtime._pendingFactories.TryGetValue(identifier, out var queue))
                runtime._pendingFactories[identifier] = queue = new();
            queue.Enqueue((source, instance.Label));
            return new ValueTask<TValue>(Answer<TValue>(source.Task));
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync()
        {
            runtime.Log.Add($"{name} MODULE RELEASED"); // modules are shared: nothing may release one
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>One instance a factory made: what was called on it, and whether it was given back.</summary>
internal sealed class ScriptInstance(SurfaceScriptRuntime runtime, string factory, string label, object?[] arguments) : IJSObjectReference
{
    private readonly Dictionary<string, TaskCompletionSource> _held = [];

    public string Factory { get; } = factory;
    public string Label { get; } = label;

    /// <summary>What the factory was called with.</summary>
    public object?[] Arguments { get; } = arguments;

    /// <summary>The element the factory was given, when it was given one.</summary>
    public ElementReference? Element => Arguments.OfType<ElementReference>().Cast<ElementReference?>().FirstOrDefault();

    public List<string> Calls { get; } = [];
    public bool Landed { get; set; }
    public bool Released { get; private set; }
    public bool ReleasedTwice { get; private set; }
    public bool UsedAfterRelease { get; private set; }

    /// <summary>The next call to <paramref name="identifier"/> stays unanswered until <see cref="Complete"/>.</summary>
    public void Hold(string identifier) => _held[identifier] = new TaskCompletionSource();

    public void Complete(string identifier)
    {
        var source = _held[identifier];
        _held.Remove(identifier);
        source.SetResult();
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (Released) UsedAfterRelease = true;
        Calls.Add(identifier);
        runtime.Log.Add($"{Label}.{identifier}");
        return _held.TryGetValue(identifier, out var held)
            ? new ValueTask<TValue>(After<TValue>(held.Task))
            : new ValueTask<TValue>(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);

    public ValueTask DisposeAsync()
    {
        if (Released) ReleasedTwice = true;
        Released = true;
        runtime.Log.Add($"{Label} released");
        return ValueTask.CompletedTask;
    }

    private static async Task<TValue> After<TValue>(Task task)
    {
        await task;
        return default!;
    }
}
