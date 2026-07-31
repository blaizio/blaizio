namespace Blaizio;

/// <summary>
/// Cascaded by <see cref="BaseDropdownMenuGroup"/> and <see cref="BaseDropdownMenuRadioGroup"/> so a
/// nested <see cref="BaseDropdownMenuLabel"/> adopts the id the group's aria-labelledby points at,
/// naming the group after the label text.
/// </summary>
/// <param name="GroupId">The group's stable id; the label renders as <c>{GroupId}-label</c>.</param>
public sealed record MenuGroupContext(string GroupId);
