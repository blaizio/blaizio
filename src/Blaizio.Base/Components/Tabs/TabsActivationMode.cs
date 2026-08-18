namespace Blaizio;

/// <summary>
/// How a <see cref="BaseTabs"/> trigger activates during keyboard navigation (the
/// <c>activationMode</c> setting).
/// </summary>
public enum TabsActivationMode
{
    /// <summary>Arrowing to a tab also activates it - selection follows focus.</summary>
    Automatic,

    /// <summary>Arrowing only moves focus; Enter/Space activates (the default).</summary>
    Manual,
}
