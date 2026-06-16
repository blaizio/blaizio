/**
 * Menu navigation for a single menu level (the WAI-ARIA menu keyboard pattern), attached to a
 * [data-bz-menu-content] surface. Vertical Arrow movement, Home/End, typeahead, and Enter/Space
 * activation are owned here - wholly DOM-side, no per-keystroke interop (the sibling of
 * rovingFocus.ts). Selection still flows through each item's own Blazor onclick: Enter/Space
 * synthesizes a click on the focused item, exactly like rovingFocus.ts's selectOnFocus.
 *
 * Deliberately NOT handled here, so they reach the items' / surface's own C# handlers:
 *  - ArrowLeft / ArrowRight: open / close submenus (direction-aware, handled per item in C#).
 *  - Escape / Tab: dismissal (handled on the surface in C#, like BzPopoverContent).
 *
 * Levels nest: a submenu's surface is a DOM descendant of its parent's, so a keydown bubbles
 * through both listeners. Each instance acts only when focus is within ITS level (nearest menu
 * surface === its container) and stops propagation once it handles a key, so the parent never
 * double-handles. Reading direction never matters here - menus navigate on the vertical axis only.
 */

export interface MenuOptions {
  /** Where to place focus the instant the surface mounts (decided by how the menu was opened). */
  initialFocus: 'none' | 'first' | 'last';
  /** Milliseconds of inactivity before the typeahead buffer resets. */
  typeaheadTimeout: number;
}

const ITEM_SELECTOR = '[data-bz-menu-item]';
const SURFACE_SELECTOR = '[data-bz-menu-content]';

class Menu {
  private search = '';
  private searchTimer?: number;

  constructor(
    private readonly container: HTMLElement,
    private readonly options: MenuOptions,
  ) {
    this.container.addEventListener('keydown', this.onKeyDown);
    this.container.addEventListener('pointermove', this.onPointerMove);

    if (this.options.initialFocus === 'first') this.focusEdge('first');
    else if (this.options.initialFocus === 'last') this.focusEdge('last');
  }

  /** Enabled items that belong to THIS level - excludes those nested in a child submenu surface. */
  private getItems(): HTMLElement[] {
    return Array.from(this.container.querySelectorAll<HTMLElement>(ITEM_SELECTOR)).filter(
      (el) =>
        el.closest(SURFACE_SELECTOR) === this.container &&
        !(el as HTMLElement & { disabled?: boolean }).disabled &&
        !el.hasAttribute('data-disabled') &&
        el.getAttribute('aria-disabled') !== 'true',
    );
  }

  /** The focused item, but only when it sits at this level (not in a nested submenu). */
  private currentItem(): HTMLElement | null {
    const item = (document.activeElement as HTMLElement | null)?.closest<HTMLElement>(ITEM_SELECTOR);
    return item && item.closest(SURFACE_SELECTOR) === this.container ? item : null;
  }

  /** Whether keyboard focus currently rests on this surface (the container itself or one of its items). */
  private get focusIsHere(): boolean {
    return (document.activeElement as HTMLElement | null)?.closest(SURFACE_SELECTOR) === this.container;
  }

  private onKeyDown = (event: KeyboardEvent): void => {
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    // A nested submenu (a descendant surface) owns its own keys; ignore unless focus is at our level.
    if (!this.focusIsHere) return;

    switch (event.key) {
      case 'ArrowDown':
        this.consume(event);
        this.move(1);
        return;
      case 'ArrowUp':
        this.consume(event);
        this.move(-1);
        return;
      case 'Home':
        this.consume(event);
        this.focusEdge('first');
        return;
      case 'End':
        this.consume(event);
        this.focusEdge('last');
        return;
      case 'Enter':
      case ' ': {
        const current = this.currentItem();
        if (current) {
          this.consume(event);
          current.click();
        }
        return;
      }
      default:
        // Single printable character (and not a modifier-only key) drives typeahead.
        if (event.key.length === 1 && /\S/.test(event.key)) {
          event.stopPropagation();
          this.typeahead(event.key);
        }
    }
  };

  /** Hovering an item at this level focuses it (the highlight follows the pointer). */
  private onPointerMove = (event: PointerEvent): void => {
    const item = (event.target as HTMLElement | null)?.closest<HTMLElement>(ITEM_SELECTOR);
    if (!item || item.closest(SURFACE_SELECTOR) !== this.container) return;
    if (this.isDisabled(item) || item === document.activeElement) return;
    item.focus({ preventScroll: true });
  };

  private isDisabled(item: HTMLElement): boolean {
    return (
      (item as HTMLElement & { disabled?: boolean }).disabled === true ||
      item.hasAttribute('data-disabled') ||
      item.getAttribute('aria-disabled') === 'true'
    );
  }

  /** Move the focused item by `delta`, wrapping at the ends (menus loop). */
  private move(delta: number): void {
    const items = this.getItems();
    if (items.length === 0) return;
    const current = this.currentItem();
    const index = current ? items.indexOf(current) : delta > 0 ? -1 : 0;
    const next = (index + delta + items.length) % items.length;
    items[next]?.focus({ preventScroll: true });
  }

  private focusEdge(edge: 'first' | 'last'): void {
    const items = this.getItems();
    const target = edge === 'first' ? items[0] : items[items.length - 1];
    target?.focus({ preventScroll: true });
  }

  private typeahead(char: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = window.setTimeout(() => (this.search = ''), this.options.typeaheadTimeout);
    this.search += char.toLowerCase();

    const items = this.getItems();
    const labels = items.map((item) => (item.textContent ?? '').trim().toLowerCase());
    // Anchor the search just after the focused item so repeated presses cycle through matches.
    const current = this.currentItem();
    const start = current ? items.indexOf(current) : -1;
    // A run of the same key ("p","p","p") cycles same-initial items rather than demanding "ppp".
    const allSame = this.search.length > 1 && this.search.split('').every((c) => c === this.search[0]);
    const needle = allSame ? this.search[0]! : this.search;
    const offset = allSame ? 1 : 0;

    for (let i = 0; i < items.length; i++) {
      const probe = (start + offset + i) % items.length;
      if (labels[probe]!.startsWith(needle)) {
        items[probe]!.focus({ preventScroll: true });
        return;
      }
    }
  }

  private consume(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
  }

  dispose(): void {
    clearTimeout(this.searchTimer);
    this.container.removeEventListener('keydown', this.onKeyDown);
    this.container.removeEventListener('pointermove', this.onPointerMove);
  }
}

const noop = { dispose() {} };

/**
 * Attaches menu navigation to a <code>[data-bz-menu-content]</code> surface. Returns a handle whose
 * <code>dispose()</code> detaches the listeners; a no-op handle if <code>container</code> isn't an element.
 */
export function createMenu(container: HTMLElement, options: MenuOptions): { dispose(): void } {
  if (!(container instanceof HTMLElement)) return noop;
  return new Menu(container, options);
}
