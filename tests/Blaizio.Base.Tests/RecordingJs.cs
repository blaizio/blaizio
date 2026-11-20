using Microsoft.JSInterop;

namespace Blaizio.Base.Tests;

/// <summary>
/// A script module that records the calls made on it and answers with what the test put in
/// <see cref="Results"/>, or fails with what it put in <see cref="Failures"/>. For the services
/// that talk to a module directly (no component, so no bUnit renderer in between).
/// </summary>
internal sealed class RecordingJsModule : IJSObjectReference
{
    public List<(string Identifier, object?[] Arguments)> Calls { get; } = [];
    public Dictionary<string, object?> Results { get; } = [];
    public Dictionary<string, Exception> Failures { get; } = [];
    public bool Disposed { get; private set; }

    public IEnumerable<string> Identifiers => Calls.Select(c => c.Identifier);

    public object?[] ArgumentsOf(string identifier) => Calls.Single(c => c.Identifier == identifier).Arguments;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        Calls.Add((identifier, args ?? []));
        if (Failures.TryGetValue(identifier, out var failure)) return ValueTask.FromException<TValue>(failure);
        return ValueTask.FromResult(Results.TryGetValue(identifier, out var result) && result is TValue typed ? typed : default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>A runtime that only imports modules: each import is recorded and answered by <see cref="OnImport"/>.</summary>
internal sealed class RecordingJsRuntime : IJSRuntime
{
    public RecordingJsRuntime(RecordingJsModule module) => OnImport = _ => Task.FromResult<IJSObjectReference>(module);

    public RecordingJsRuntime(Func<string, Task<IJSObjectReference>> onImport) => OnImport = onImport;

    public List<string> Imports { get; } = [];
    public Func<string, Task<IJSObjectReference>> OnImport { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (identifier != "import")
            throw new InvalidOperationException($"Only module imports are expected on the runtime, not '{identifier}'.");
        var path = (string)args![0]!;
        Imports.Add(path);
        return new ValueTask<TValue>(Cast<TValue>(OnImport(path)));
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        InvokeAsync<TValue>(identifier, args);

    private static async Task<TValue> Cast<TValue>(Task<IJSObjectReference> task) => (TValue)await task;
}
