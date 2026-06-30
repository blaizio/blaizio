namespace Blaizio;

/// <summary>
/// Keeps at most one hover card open at a time within a document. Scoped, so it is per-circuit on
/// Blazor Server and per-app on WebAssembly - never shared across users.
/// <para>
/// A hover card is pointer/focus-driven, so only one can ever be <em>actively</em> triggered at once;
/// the only way two end up visible is the close delay leaving the previous one up while you move onto
/// the next (they then overlap, the later one painting over the earlier). When a card opens it asks the
/// coordinator to dismiss whichever was open before it, so the lingering one closes at once instead of
/// stacking. Controlled cards (<c>@bind-Open</c>) opt out - the page owns their state.
/// </para>
/// </summary>
public sealed class HoverCardCoordinator
{
    private IHoverCardController? _current;

    /// <summary>
    /// Registers <paramref name="card"/> as the open one and dismisses the card that was open before
    /// it, if any. Returns once the previous card has been told to close.
    /// </summary>
    public Task OpenedAsync(IHoverCardController card)
    {
        var previous = _current;
        _current = card;
        return previous is null || ReferenceEquals(previous, card)
            ? Task.CompletedTask
            : previous.DismissFromCoordinatorAsync();
    }

    /// <summary>Clears the registration when <paramref name="card"/> closes (a no-op if another card is current).</summary>
    public void Closed(IHoverCardController card)
    {
        if (ReferenceEquals(_current, card)) _current = null;
    }
}

/// <summary>A hover card the <see cref="HoverCardCoordinator"/> can dismiss when a sibling opens.</summary>
public interface IHoverCardController
{
    /// <summary>Closes this card at once, cancelling any pending open/close delay.</summary>
    Task DismissFromCoordinatorAsync();
}
