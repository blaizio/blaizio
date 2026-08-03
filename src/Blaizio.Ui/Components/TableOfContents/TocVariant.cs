using System.ComponentModel;

namespace Blaizio.Ui;

/// <summary>How a <see cref="BzTableOfContents"/> presents its headings.</summary>
public enum TocVariant
{
    /// <summary>A plain list of links, indented by heading level (the default).</summary>
    [Description("list")] List,

    /// <summary>
    /// The same list against a hairline rail, with one indicator bar that slides from section to
    /// section as you scroll instead of restyling each link.
    /// </summary>
    [Description("rail")] Rail,

    /// <summary>
    /// A column of marks, one per heading, sized by level - the labels stay hidden until the panel
    /// is hovered or focused, then slide in. Reads as a minimap of the page.
    /// </summary>
    [Description("marks")] Marks,

    /// <summary>
    /// The rail with a progress track: the fill follows how far the tracked region has scrolled,
    /// and the indicator still marks the section in view.
    /// </summary>
    [Description("progress")] Progress,

    /// <summary>
    /// Marks with nothing else: no panel, no labels, just the ticks - and hovering ONE of them
    /// pops that section's label beside it rather than opening the whole list. The quietest way to
    /// carry a table of contents, and the only variant whose reveal is per-heading.
    /// </summary>
    [Description("peek")] Peek,
}
