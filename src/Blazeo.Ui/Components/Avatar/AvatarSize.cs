using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Size of an <see cref="BzAvatar"/> - drives the style sheet's scale via <c>data-size</c>.</summary>
public enum AvatarSize
{
    /// <summary>Standard (size-8).</summary>
    [Description("default")]
    Default,

    /// <summary>Small (size-6).</summary>
    [Description("sm")]
    Sm,

    /// <summary>Large (size-10).</summary>
    [Description("lg")]
    Lg,
}
