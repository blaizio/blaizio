namespace Blazeo;

/// <summary>
/// How a <see cref="BlazeTabs"/> trigger activates during keyboard navigation. Mirrors Radix's
/// <c>activationMode</c>.
/// </summary>
public enum TabsActivationMode
{
    /// <summary>Arrowing to a tab also activates it — selection follows focus (the default).</summary>
    Automatic,

    /// <summary>Arrowing only moves focus; Enter/Space activates.</summary>
    Manual,
}
