using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>Which edge of an <see cref="BzInputGroup"/> an <see cref="BzInputGroupAddon"/> sits on.</summary>
public enum InputGroupAddonAlign
{
    /// <summary>Leading edge, inline before the control.</summary>
    [Description("inline-start")]
    InlineStart,

    /// <summary>Trailing edge, inline after the control.</summary>
    [Description("inline-end")]
    InlineEnd,

    /// <summary>Full-width row stacked above the control.</summary>
    [Description("block-start")]
    BlockStart,

    /// <summary>Full-width row stacked below the control.</summary>
    [Description("block-end")]
    BlockEnd,
}

/// <summary>Size of an <see cref="BzInputGroupButton"/>. The <c>Icon*</c> sizes are square.</summary>
public enum InputGroupButtonSize
{
    /// <summary>Extra small (h-6).</summary>
    [Description("xs")]
    Xs,

    /// <summary>Small.</summary>
    [Description("sm")]
    Sm,

    /// <summary>Extra-small square icon button.</summary>
    [Description("icon-xs")]
    IconXs,

    /// <summary>Small square icon button.</summary>
    [Description("icon-sm")]
    IconSm,
}
