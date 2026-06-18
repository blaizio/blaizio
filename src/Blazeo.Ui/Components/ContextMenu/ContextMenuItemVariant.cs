using System.ComponentModel;

namespace Blazeo.Ui;

/// <summary>The visual intent of a <see cref="ContextMenuItem"/>, emitted as its <c>data-variant</c>.</summary>
public enum ContextMenuItemVariant
{
    /// <summary>The standard item.</summary>
    [Description("default")]
    Default,

    /// <summary>A destructive action (e.g. Delete), tinted with the destructive color.</summary>
    [Description("destructive")]
    Destructive,
}
