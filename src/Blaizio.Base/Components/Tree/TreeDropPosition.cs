namespace Blaizio;

/// <summary>Where a dragged node lands relative to the drop target node.</summary>
public enum TreeDropPosition
{
    /// <summary>As the target's previous sibling.</summary>
    Before,

    /// <summary>As the target's next sibling.</summary>
    After,

    /// <summary>As a child of the target branch (appended to its children).</summary>
    Inside,
}
