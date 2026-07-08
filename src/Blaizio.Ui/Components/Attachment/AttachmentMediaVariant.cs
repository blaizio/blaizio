using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>What a <see cref="BzAttachmentMedia"/> slot holds, flowed to the style sheet via <c>data-variant</c>.</summary>
public enum AttachmentMediaVariant
{
    /// <summary>A file-type icon on a muted tile (the default).</summary>
    [Description("icon")]
    Icon,

    /// <summary>An image preview - the child <c>&lt;img&gt;</c> fills the tile.</summary>
    [Description("image")]
    Image,
}
