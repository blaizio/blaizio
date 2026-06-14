using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// State a <see cref="BzTabs"/> cascades to its list, triggers, and contents: the selected
/// value, the id scheme that wires triggers to panels for accessibility, the layout/activation
/// settings, and the callback a trigger invokes to select itself.
/// </summary>
/// <param name="BaseId">Unique prefix for this tabs instance's trigger/content ids.</param>
/// <param name="Value">The selected tab value (<see langword="null"/> when none).</param>
/// <param name="Orientation">The navigation/layout axis.</param>
/// <param name="ActivationMode">Whether arrow-navigation also activates.</param>
/// <param name="Select">Invoked by a trigger (with its value) to become the selection.</param>
public sealed record TabsContext(
    string BaseId,
    string? Value,
    Orientation Orientation,
    TabsActivationMode ActivationMode,
    EventCallback<string> Select)
{
    /// <summary>Whether <paramref name="tabValue"/> is the selected tab.</summary>
    public bool IsSelected(string tabValue) => Value == tabValue;

    /// <summary>The trigger element id for <paramref name="tabValue"/> (the panel's aria-labelledby).</summary>
    public string TriggerId(string tabValue) => $"{BaseId}-trigger-{tabValue}";

    /// <summary>The content element id for <paramref name="tabValue"/> (the trigger's aria-controls).</summary>
    public string ContentId(string tabValue) => $"{BaseId}-content-{tabValue}";
}
