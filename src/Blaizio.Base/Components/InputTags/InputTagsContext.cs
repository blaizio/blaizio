using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// State a <see cref="BaseInputTags"/> cascades to its chips, chip-remove buttons, and input. The root
/// owns the committed tags (<see cref="Values"/>) and the in-progress text (<see cref="Search"/>); the
/// input raises <see cref="SearchChanged"/> as the user types and <see cref="Commit"/> when a boundary key
/// (Enter or a delimiter) lands, and the chips remove themselves through <see cref="RemoveAt"/>.
/// </summary>
/// <param name="Values">The committed tags, in entry order.</param>
/// <param name="Search">The in-progress text in the input (the tag being typed).</param>
/// <param name="SearchChanged">Raised by the input with the new text on every keystroke (the root splits out any pasted delimiters).</param>
/// <param name="Commit">Turns the current query into a tag (Enter or a delimiter key). Trims; empty text is dropped quietly.</param>
/// <param name="RemoveAt">Removes the tag at the given index (a chip's remove button).</param>
/// <param name="RemoveLast">Removes the newest tag (Backspace on an empty query).</param>
/// <param name="FocusInput">Moves focus back to the input - used after removing a chip so typing can continue.</param>
/// <param name="Delimiters">Keys that commit the query in addition to Enter (also split out of pasted text).</param>
/// <param name="AtMax">Whether <see cref="BaseInputTags.MaxTags"/> is reached - commits are ignored until a tag is removed.</param>
/// <param name="InputId">Id of the input element (the focus target).</param>
/// <param name="Label">Accessible label for the input, or <see langword="null"/>.</param>
/// <param name="Disabled">Whether the whole field is disabled.</param>
/// <param name="ReadOnly">Whether the tags show but cannot be added to or removed.</param>
public sealed record InputTagsContext(
    IReadOnlyList<string> Values,
    string Search,
    EventCallback<string?> SearchChanged,
    EventCallback Commit,
    EventCallback<int> RemoveAt,
    EventCallback RemoveLast,
    EventCallback FocusInput,
    IReadOnlyList<string> Delimiters,
    bool AtMax,
    string InputId,
    string? Label,
    bool Disabled,
    bool ReadOnly)
{
    /// <summary>Whether any tag is committed at all.</summary>
    public bool HasValues => Values.Count > 0;
}

/// <summary>
/// Cascaded by a <see cref="BaseInputTagsChip"/> to the <see cref="BaseInputTagsChipRemove"/> nested inside
/// it, so the remove button knows which tag to drop.
/// </summary>
/// <param name="Index">The tag's position in <see cref="InputTagsContext.Values"/>.</param>
/// <param name="Value">The tag's text.</param>
public sealed record InputTagsChipContext(int Index, string Value);
