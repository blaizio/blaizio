namespace Blaizio;

/// <summary>A <see cref="BasePanel"/>'s registration in its group: its constraints (all percentages of
/// the group) and its current size. The panel owns the instance and mutates its constraints when its
/// parameters change; the group owns <see cref="Size"/>.</summary>
public sealed class PanelInfo
{
    /// <summary>Stable id for the panel.</summary>
    public required string Id { get; init; }

    /// <summary>The size the panel starts at, or <see langword="null"/> to share what's left equally.</summary>
    public double? DefaultSize { get; set; }

    /// <summary>Smallest size the panel can be dragged to (before collapsing, if collapsible).</summary>
    public double MinSize { get; set; } = 10;

    /// <summary>Largest size the panel can be dragged to.</summary>
    public double MaxSize { get; set; } = 100;

    /// <summary>Whether dragging past <see cref="MinSize"/> snaps the panel to <see cref="CollapsedSize"/>.</summary>
    public bool Collapsible { get; set; }

    /// <summary>The size a collapsible panel snaps to when collapsed (default 0).</summary>
    public double CollapsedSize { get; set; }

    /// <summary>The panel's current size (percentage of the group), owned by the group.</summary>
    public double Size { get; set; }

    /// <summary>Whether the panel is currently collapsed.</summary>
    public bool IsCollapsed => Collapsible && Size <= CollapsedSize + 0.01;
}
