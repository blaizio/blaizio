namespace Blaizio;

/// <summary>
/// Cascaded by a <see cref="BaseSelectGroup"/> so a nested <see cref="BaseSelectLabel"/> adopts the id
/// the group's aria-labelledby points at, naming the group after the label text.
/// </summary>
/// <param name="GroupId">The group's stable id; the label renders as <c>{GroupId}-label</c>.</param>
public sealed record SelectGroupContext(string GroupId);
