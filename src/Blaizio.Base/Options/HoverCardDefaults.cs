namespace Blaizio;

/// <summary>App-wide hover card timing defaults.</summary>
public sealed class HoverCardDefaults
{
    /// <summary>Hover delay (ms) before a card opens. Defaults to 100.</summary>
    public int OpenDelay { get; set; } = 100;

    /// <summary>Grace period (ms) after the pointer leaves before the card closes. Defaults to 200.</summary>
    public int CloseDelay { get; set; } = 200;
}
