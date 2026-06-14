namespace Blazeo;

/// <summary>
/// Selection mode of a <see cref="BzToggleGroup"/>
/// (<c>type="single" | "multiple"</c>).
/// </summary>
public enum ToggleGroupType
{
    /// <summary>At most one item is on; pressing the on item turns it back off (the default).</summary>
    Single,

    /// <summary>Any number of items can be on at once.</summary>
    Multiple,
}
