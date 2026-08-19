using Microsoft.AspNetCore.Components;

namespace Blaizio.Ui;

/// <summary>
/// State a <see cref="BzPanel"/> cascades to its parts: the open state and the setter the close
/// button invokes. A fresh instance is cascaded whenever the state changes, so parts re-render.
/// </summary>
/// <param name="Open">Whether the panel is expanded.</param>
/// <param name="SetOpen">Sets the open state.</param>
public sealed record PanelContext(bool Open, EventCallback<bool> SetOpen);
