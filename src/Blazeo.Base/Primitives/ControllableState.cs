using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// The C# analogue of Radix's <c>useControllableState</c>: lets a component be either <i>controlled</i>
/// (the consumer owns the value via a bound parameter + change callback) or <i>uncontrolled</i> (the
/// component keeps its own state, seeded once from a default), behind a single <see cref="Value"/> and
/// one <see cref="SetAsync"/> setter — so the component body never branches on which mode it's in.
/// </summary>
/// <remarks>
/// Drive it from the lifecycle: call <see cref="Sync"/> in <c>OnParametersSet</c> to reconcile with the
/// latest parameters, and <see cref="SetAsync"/> from event handlers to commit a new value.
///
/// <para><b>Controlled vs uncontrolled</b> is decided by the caller (it knows the shape of its
/// "unset" sentinel — typically a <see cref="System.Nullable{T}"/> being <c>null</c>) and passed to
/// <see cref="Sync"/> as <c>controlled</c>. When controlled, <see cref="SetAsync"/> does not mutate
/// internal state — it only fires the change callback and lets the parent flow a new value back in on
/// the next render, exactly as Radix does.</para>
/// </remarks>
/// <typeparam name="T">The state value type (e.g. <see cref="bool"/> for a toggle's pressed state).</typeparam>
public sealed class ControllableState<T>
{
    private T _uncontrolled = default!;
    private bool _seeded;

    /// <summary><see langword="true"/> when the consumer owns the value (a controlled prop was supplied).</summary>
    public bool IsControlled { get; private set; }

    /// <summary>The effective current value — the controlled prop when controlled, else the internal state.</summary>
    public T Value { get; private set; } = default!;

    /// <summary>
    /// Reconcile with the current render's parameters. Call from <c>OnParametersSet</c>.
    /// </summary>
    /// <param name="controlled">Whether the consumer is driving the value this render.</param>
    /// <param name="controlledValue">The controlled value (ignored when <paramref name="controlled"/> is false).</param>
    /// <param name="defaultValue">The uncontrolled seed — applied once, on the first sync only.</param>
    public void Sync(bool controlled, T controlledValue, T defaultValue)
    {
        if (!_seeded)
        {
            _uncontrolled = defaultValue;
            _seeded = true;
        }

        IsControlled = controlled;
        Value = controlled ? controlledValue : _uncontrolled;
    }

    /// <summary>
    /// Commit <paramref name="next"/>: when uncontrolled, stores it internally and reflects it in
    /// <see cref="Value"/> immediately; always raises <paramref name="onChange"/> so a controlled
    /// parent (or a <c>@bind</c>) can react. The parent's new value, if any, lands on the next sync.
    /// </summary>
    public async Task SetAsync(T next, EventCallback<T> onChange)
    {
        if (!IsControlled)
        {
            _uncontrolled = next;
            Value = next;
        }

        await onChange.InvokeAsync(next);
    }
}
