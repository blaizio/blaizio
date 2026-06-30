namespace Blaizio;

/// <summary>
/// State a <see cref="BaseSwitch"/> cascades to its <see cref="BaseSwitchThumb"/> so the thumb can
/// mirror the switch's <c>data-state</c>/<c>data-disabled</c> without the consumer wiring it up - the
/// switch's shared context.
/// </summary>
/// <param name="Checked">Whether the switch is on.</param>
/// <param name="Disabled">Whether the switch is disabled.</param>
public sealed record SwitchContext(bool Checked, bool Disabled);
