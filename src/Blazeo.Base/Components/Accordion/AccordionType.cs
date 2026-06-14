namespace Blazeo;

/// <summary>Expansion mode of a <see cref="BlazeAccordion"/> (the <c>type</c> setting).</summary>
public enum AccordionType
{
    /// <summary>At most one item open (the default). Pair with <c>Collapsible</c> to allow closing it.</summary>
    Single,

    /// <summary>Any number of items can be open at once.</summary>
    Multiple,
}
