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

/// <summary>Block side a <see cref="BzBubbleReactions"/> row anchors to.</summary>
public enum BubbleReactionsSide
{
    /// <summary>Overlapping the bubble's bottom edge (the default).</summary>
    [Description("bottom")]
    Bottom,

    /// <summary>Overlapping the bubble's top edge.</summary>
    [Description("top")]
    Top,
}
