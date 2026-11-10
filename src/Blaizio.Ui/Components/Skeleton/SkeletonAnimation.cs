using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Loading animation of a <see cref="BzSkeleton"/>.</summary>
public enum SkeletonAnimation
{
    /// <summary>A highlight sweeping left to right (the default).</summary>
    [Description("shimmer")]
    Shimmer,

    /// <summary>Opacity pulse.</summary>
    [Description("pulse")]
    Pulse,

    /// <summary>Static block, no animation.</summary>
    [Description("none")]
    None,
}
