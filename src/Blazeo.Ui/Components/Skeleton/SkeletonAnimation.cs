namespace Blazeo.Ui;

/// <summary>Loading animation of a <see cref="Skeleton"/>.</summary>
public enum SkeletonAnimation
{
    /// <summary>A highlight sweeping left to right.</summary>
    Shimmer,

    /// <summary>Opacity pulse (the shadcn default).</summary>
    Pulse,

    /// <summary>Static block, no animation.</summary>
    None,
}
