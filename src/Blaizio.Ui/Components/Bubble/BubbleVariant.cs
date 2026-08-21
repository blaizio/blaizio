using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>Visual style of a <see cref="BzBubble"/>.</summary>
public enum BubbleVariant
{
    /// <summary>Strong primary fill - the current user's outgoing messages.</summary>
    [Description("default")]
    Default,

    /// <summary>Standard neutral fill for incoming messages.</summary>
    [Description("secondary")]
    Secondary,

    /// <summary>Lower-emphasis fill for quiet content.</summary>
    [Description("muted")]
    Muted,

    /// <summary>Subtle primary-tinted fill - a softer take on the sender bubble.</summary>
    [Description("tinted")]
    Tinted,

    /// <summary>Bordered bubble on the page background.</summary>
    [Description("outline")]
    Outline,

    /// <summary>Unframed content at full width - assistant prose, rich blocks.</summary>
    [Description("ghost")]
    Ghost,

    /// <summary>Error treatment for failed sends or destructive results.</summary>
    [Description("destructive")]
    Destructive,
}

/// <summary>Inline alignment of a <see cref="BzBubble"/> - the sender's side of the thread.</summary>
public enum BubbleAlign
{
    /// <summary>Aligned to the inline start - incoming messages (the default).</summary>
    [Description("start")]
    Start,

    /// <summary>Aligned to the inline end - the current user's messages.</summary>
    [Description("end")]
    End,
}

/// <summary>Speech-tail of a <see cref="BzBubble"/> - which outer corner the pointer grows from.</summary>
public enum BubbleTail
{
    /// <summary>No tail (the default).</summary>
    [Description("none")]
    None,

    /// <summary>On the bottom outer corner; put it on the LAST bubble of a run.</summary>
    [Description("bottom")]
    Bottom,

    /// <summary>On the top outer corner, pointing at the avatar; put it on the FIRST bubble of a run.</summary>
    [Description("top")]
    Top,
}

/// <summary>Outline of a <see cref="BzBubble"/>'s tail. The fill always follows the variant.</summary>
public enum BubbleTailShape
{
    /// <summary>A flat right triangle tucked against the corner (the default).</summary>
    [Description("triangle")]
    Triangle,

    /// <summary>A taller wedge with a rounded tip that sweeps away from the corner.</summary>
    [Description("curved")]
    Curved,
}

/// <summary>Block side a <see cref="BzBubbleReactions"/> row anchors to.</summary>
public enum BubbleReactionAnchor
{
    /// <summary>Overlapping the bubble's bottom edge (the default).</summary>
    [Description("bottom")]
    Bottom,

    /// <summary>Overlapping the bubble's top edge.</summary>
    [Description("top")]
    Top,
}
