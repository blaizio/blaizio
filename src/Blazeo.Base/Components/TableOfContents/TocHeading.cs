namespace Blazeo;

/// <summary>
/// One entry of a <see cref="BaseTableOfContents"/>: a heading discovered in the page content
/// by ts/toc.ts.
/// </summary>
/// <param name="Id">The heading element's id (assigned from the text when it had none).</param>
/// <param name="Text">The heading's text content.</param>
/// <param name="Level">The heading level (2 for h2, 3 for h3, ...) - drives indentation.</param>
public sealed record TocHeading(string Id, string Text, int Level);
