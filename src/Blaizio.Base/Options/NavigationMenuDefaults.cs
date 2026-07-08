namespace Blaizio;

/// <summary>App-wide navigation menu timing defaults.</summary>
public sealed class NavigationMenuDefaults
{
    /// <summary>Hover delay (ms) before an item's content opens. Defaults to 200.</summary>
    public int OpenDelay { get; set; } = 200;

    /// <summary>Grace period (ms) after the pointer leaves before the content closes. Defaults to 150.</summary>
    public int CloseDelay { get; set; } = 150;
}
