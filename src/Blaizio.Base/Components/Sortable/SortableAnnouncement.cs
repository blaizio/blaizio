namespace Blaizio;

/// <summary>What a key press did to a sortable item, as a <see cref="SortableAnnouncement"/> reports it.</summary>
public enum SortableAnnouncementKind
{
    /// <summary>Space or Enter picked the item up.</summary>
    Grabbed,

    /// <summary>An arrow key, Home or End moved the held item to a new place.</summary>
    Moved,

    /// <summary>Space or Enter put the held item down.</summary>
    Dropped,

    /// <summary>Escape put the held item back where it was picked up.</summary>
    Cancelled,
}

/// <summary>
/// What a sortable list's <c>Announcement</c> receives: an item the keyboard just picked up, moved,
/// put down or put back, for what screen readers hear in the reader's language.
/// </summary>
public sealed class SortableAnnouncement
{
    internal SortableAnnouncement(SortableAnnouncementKind kind, int position, int count)
    {
        Kind = kind;
        Position = position;
        Count = count;
    }

    /// <summary>What happened to the item.</summary>
    public SortableAnnouncementKind Kind { get; }

    /// <summary>
    /// The item's place, counting from 1: where it was picked up, moved to or put down, or, for
    /// <see cref="SortableAnnouncementKind.Cancelled"/>, the place it went back to.
    /// </summary>
    public int Position { get; }

    /// <summary>How many items the list has.</summary>
    public int Count { get; }

    /// <summary>The English announcement, for example <c>Moved to position 2 of 5.</c></summary>
    public string Message => Kind switch
    {
        SortableAnnouncementKind.Grabbed =>
            $"Grabbed item {Position} of {Count}. Use the arrow keys to move, space to drop, escape to cancel.",
        SortableAnnouncementKind.Moved => $"Moved to position {Position} of {Count}.",
        SortableAnnouncementKind.Dropped => $"Dropped at position {Position} of {Count}.",
        _ => "Move cancelled.",
    };
}
