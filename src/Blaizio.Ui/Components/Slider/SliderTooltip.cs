namespace Blaizio.Ui;

/// <summary>When a <see cref="BzSlider"/> shows the value bubble that floats on each thumb.</summary>
public enum SliderTooltip
{
    /// <summary>No bubble (the default).</summary>
    None,

    /// <summary>Show the bubble only while a thumb is hovered, focused, or being dragged.</summary>
    OnInteraction,

    /// <summary>Always show the bubble.</summary>
    Always,
}
