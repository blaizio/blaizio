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
  private focusTimer = 0;

  constructor(
    private readonly container: HTMLElement,
    private readonly options: MenuOptions,
  ) {
    this.container.addEventListener('keydown', this.onKeyDown);
    this.container.addEventListener('pointermove', this.onPointerMove);
    this.container.addEventListener('pointerleave', this.onPointerLeave);

    this.focusWhenReady();
  }

  /**
   * Place the opening focus once the surface can actually take it, then keep it there through a short
   * settle window. ts/positioning.js opens the surface visibility:hidden until its first frame lands,
   * and a hidden element silently rejects focus() - so a naive synchronous focus drops on a cold open,
   * leaving the first item un-highlighted (focus stranded on the trigger). We re-place focus while it
   * sits OUTSIDE this surface's whole subtree, and leave it the moment it's anywhere inside (the item
   * we placed, one the user navigated to, or a submenu they opened) - never yanking. 'first'/'last'
   * land on an item; 'none' (pointer-opened) lands on the surface so the first Arrow key has somewhere
   * to step from.
   *
   * The retry is a setTimeout, NOT requestAnimationFrame: rAF is paused in background tabs, so an rAF
   * retry would never run if the menu opened while the tab was hidden, stranding focus on the trigger.
   */
  private focusWhenReady = (attemptsLeft = 30): void => {
    if (!this.container.isConnected) return;

    if (!this.container.contains(document.activeElement)) {
      const target = this.initialTarget() ?? (attemptsLeft === 0 ? this.container : null);
      target?.focus({ preventScroll: true });
    }

    if (attemptsLeft > 0) {
      this.focusTimer = window.setTimeout(() => this.focusWhenReady(attemptsLeft - 1), 16);
    }
  };

  /** The element the opening focus should land on, or null while the items haven't rendered yet. */
  private initialTarget(): HTMLElement | null {
    if (this.options.initialFocus === 'none') return this.container;
    const items = this.getItems();
    const item = this.options.initialFocus === 'last' ? items[items.length - 1] : items[0];
    return item ?? null;
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

  /**
   * The pointer left this surface. The highlight is just :focus, and onPointerMove only ever moves
   * focus onto items - nothing took it back off, so the last-hovered item stayed lit after the
   * pointer was long gone. Pull focus back to the surface, clearing the highlight. We skip it when
   * the pointer is headed to another menu level (a submenu, or back to a parent) - that level claims
   * focus itself - and only act when focus is actually parked on one of OUR items.
   */
  private onPointerLeave = (event: PointerEvent): void => {
    const to = event.relatedTarget as HTMLElement | null;
    if (to?.closest(SURFACE_SELECTOR)) return;
    if (this.currentItem()) this.container.focus({ preventScroll: true });
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
    clearTimeout(this.focusTimer);
    this.container.removeEventListener('keydown', this.onKeyDown);
    this.container.removeEventListener('pointermove', this.onPointerMove);
    this.container.removeEventListener('pointerleave', this.onPointerLeave);
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

const TRIGGER_NAV_KEYS = new Set(['ArrowDown', 'ArrowUp']);

/**
 * Stop the page scrolling when ArrowDown / ArrowUp open a menu from its (closed) trigger. The
 * trigger's own Blazor onkeydown still opens the menu - this only suppresses the native scroll,
 * which a splatted Blazor handler can't preventDefault in time (it round-trips to .NET first). Pure
 * DOM, no callback. The listener only ever fires while the trigger itself holds focus (menu closed);
 * once open, focus is in the content and ts/menu.js owns the arrows. Returns a dispose() handle.
 */
export function guardTrigger(selector: string): { dispose(): void } {
  const trigger = document.querySelector<HTMLElement>(selector);
  if (!trigger) return noop;
  const onKeyDown = (event: KeyboardEvent): void => {
    if (TRIGGER_NAV_KEYS.has(event.key) && !event.altKey && !event.ctrlKey && !event.metaKey) {
      event.preventDefault();
    }
  };
  trigger.addEventListener('keydown', onKeyDown);
  return {
    dispose() {
      trigger.removeEventListener('keydown', onKeyDown);
    },
  };
}

/**
 * Return focus to a menu's trigger (matched by <code>selector</code>) when the menu closes. Used by
 * MenuContentBase instead of FocusScope's previouslyFocused, which is captured at mount and races
 * ts/menu.js's own opening focus - so it can latch onto an item that then unmounts, dropping focus
 * to &lt;body&gt;. A no-op if the trigger has left the DOM (the whole menu tore down), so a parent's
 * own restore wins.
 */
export function focusTrigger(selector: string): void {
  document.querySelector<HTMLElement>(selector)?.focus({ preventScroll: true });
}
