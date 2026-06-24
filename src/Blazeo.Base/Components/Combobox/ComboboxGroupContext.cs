namespace Blazeo;

/// <summary>
/// Cascaded by a <see cref="BzComboboxGroup"/> to the items nested inside it, so each item registers
/// under the right group and the root can hide a group whose every item is filtered out.
/// </summary>
/// <param name="GroupId">The group's stable id, matched against item registrations.</param>
public sealed record ComboboxGroupContext(string GroupId);
