using Blaizio;

namespace Blaizio.Ui;

/// <summary>
/// Messages for <see cref="IToastService.Track{T}"/>: a single toast that starts as a loading spinner
/// and resolves in place to a success or error toast when the awaited work settles.
/// </summary>
/// <typeparam name="T">The result type of the awaited operation.</typeparam>
public sealed record ToastTrackOptions<T>
{
    /// <summary>The message shown while the operation is in flight.</summary>
    public required string Loading { get; init; }

    /// <summary>Builds the success message from the result. Defaults to "Success".</summary>
    public Func<T, string>? Success { get; init; }

    /// <summary>Builds the error message from the thrown exception. Defaults to "Something went wrong".</summary>
    public Func<Exception, string>? Error { get; init; }

    /// <summary>Override the toast provider's position for this toast.</summary>
    public ToastPosition? Position { get; init; }
}
