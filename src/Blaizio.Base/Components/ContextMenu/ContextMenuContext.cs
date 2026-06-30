using Microsoft.AspNetCore.Components;

namespace Blaizio;

/// <summary>
/// State a <see cref="BaseContextMenu"/> cascades to its <see cref="BaseContextMenuTrigger"/> so a
/// right-click can open the menu at the pointer. The menu's open state, ARIA ids, and close request
/// flow through a separate <see cref="DropdownMenuContext"/> (also cascaded by the root), which lets
/// every <c>BaseDropdownMenu*</c> item / checkbox / radio / submenu be reused inside a context menu
/// unchanged.
/// </summary>
/// <param name="OpenAtPoint">Opens the menu anchored at the given viewport point.</param>
public sealed record ContextMenuContext(EventCallback<MenuPoint> OpenAtPoint);
