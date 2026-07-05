namespace Blaizio;

/// <summary>How a <see cref="BaseTree{TItem}"/> lets the user select nodes.</summary>
public enum TreeSelectionMode
{
    /// <summary>Nodes cannot be selected (the tree is navigation/expansion only).</summary>
    None,

    /// <summary>At most one node is selected at a time.</summary>
    Single,

    /// <summary>Any number of nodes can be selected (Ctrl toggles, Shift extends a range).</summary>
    Multiple,
}
