namespace Blaizio;

/// <summary>What happened to a tree node, as a <see cref="TreeAnnouncement"/> reports it.</summary>
public enum TreeAnnouncementKind
{
    /// <summary>Ctrl+Space picked the node up for a keyboard move.</summary>
    Grabbed,

    /// <summary>The node moved: an arrow key while it is held, or a pointer drop.</summary>
    Moved,

    /// <summary>Ctrl+Space or Enter put the held node down.</summary>
    Dropped,

    /// <summary>Escape ended the keyboard move.</summary>
    Cancelled,

    /// <summary>A rename was committed with new text.</summary>
    Renamed,

    /// <summary><c>LoadChildren</c> failed for the branch being opened, which stays closed.</summary>
    LoadFailed,
}

/// <summary>
/// What a tree's <c>Announcement</c> receives: a node that was picked up, moved, put down, renamed
/// or could not load its children, for what screen readers hear in the reader's language.
/// </summary>
public sealed class TreeAnnouncement
{
    internal TreeAnnouncement(TreeAnnouncementKind kind, string node)
    {
        Kind = kind;
        Node = node;
    }

    /// <summary>What happened to the node.</summary>
    public TreeAnnouncementKind Kind { get; }

    /// <summary>
    /// The node's text (its value when the tree no longer has it), and for
    /// <see cref="TreeAnnouncementKind.Renamed"/> the new text.
    /// </summary>
    public string Node { get; }

    /// <summary>The English announcement, for example <c>Moved Reports.</c></summary>
    public string Message => Kind switch
    {
        TreeAnnouncementKind.Grabbed =>
            $"Grabbed {Node}. Use the arrow keys to move, Control Space or Enter to drop, Escape to cancel.",
        TreeAnnouncementKind.Moved => $"Moved {Node}.",
        TreeAnnouncementKind.Dropped => "Dropped.",
        TreeAnnouncementKind.Cancelled => "Move cancelled.",
        TreeAnnouncementKind.Renamed => $"Renamed to {Node}.",
        _ => $"Could not load {Node}.",
    };
}
