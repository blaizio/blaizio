using Microsoft.AspNetCore.Components;

namespace Blazeo;

/// <summary>
/// State a <see cref="BaseMenubar"/> cascades to each <see cref="BaseMenubarMenu"/> (and, through it, the
/// triggers): which menu in the bar is currently open and how focus should land when one opens, plus
/// the open/close requests. Only one menu in a bar is open at a time - the single <see cref="Value"/>
/// slot enforces it, so opening another menu closes the previous one.
/// </summary>
/// <param name="Value">Id of the open menu, or <see langword="null"/> when the bar is closed.</param>
/// <param name="FocusIntent">Where the opening menu's content should place focus (Arrow keys vs pointer).</param>
/// <param name="Open">Opens a menu by id with the given focus intent (which closes any other).</param>
/// <param name="Close">Closes the menu with the given id (a no-op if it is not the open one).</param>
public sealed record MenubarContext(
    string? Value,
    MenuFocusIntent FocusIntent,
    EventCallback<(string Value, MenuFocusIntent Intent)> Open,
    EventCallback<string> Close);
