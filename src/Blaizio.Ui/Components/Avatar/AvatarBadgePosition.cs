namespace Blaizio.Ui;

/// <summary>Corner an <see cref="BzAvatarBadge"/> is pinned to. Start/end follow the writing direction.</summary>
public enum AvatarBadgePosition
{
    /// <summary>Bottom trailing corner (bottom-right in LTR). The default.</summary>
    BottomEnd,

    /// <summary>Top trailing corner (top-right in LTR).</summary>
    TopEnd,

    /// <summary>Top leading corner (top-left in LTR).</summary>
    TopStart,

    /// <summary>Bottom leading corner (bottom-left in LTR).</summary>
    BottomStart,
}
