namespace Blazeo;

/// <summary>
/// The outcome of an imperatively shown dialog (see <see cref="IDialogStore"/>): either a value the
/// dialog resolved with, or a cancellation (Escape, the backdrop, or teardown).
/// </summary>
public sealed record DialogResult
{
    /// <summary><see langword="true"/> when the dialog was dismissed without a result (Escape, backdrop, or disposal).</summary>
    public bool Cancelled { get; init; }

    /// <summary>The value the dialog resolved with, if any.</summary>
    public object? Value { get; init; }

    /// <summary>A resolved result carrying an optional <paramref name="value"/>.</summary>
    public static DialogResult Ok(object? value = null) => new() { Value = value };

    /// <summary>A cancelled result (no value).</summary>
    public static DialogResult Cancel() => new() { Cancelled = true };

    /// <summary>The <see cref="Value"/> cast to <typeparamref name="T"/>, or <see langword="default"/> if absent or another type.</summary>
    public T? ValueAs<T>() => Value is T value ? value : default;
}
