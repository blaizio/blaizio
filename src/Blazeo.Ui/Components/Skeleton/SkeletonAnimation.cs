using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Loading animation of a <see cref="Skeleton"/>.</summary>
public enum SkeletonAnimation
{
    /// <summary>A highlight sweeping left to right.</summary>
    [Description("shimmer")]
    Shimmer,

    /// <summary>Opacity pulse (the default).</summary>
    [Description("pulse")]
    Pulse,

    /// <summary>Static block, no animation.</summary>
    [Description("none")]
    None,
}
