/**
 * Keyboard navigation + the virtual highlight for a Combobox, attached to its
 * [data-bz-combobox-list] listbox. The sibling of ts/command.ts - same combobox interaction pattern
 * (focus stays in the text input, the "highlighted" option is virtual: aria-activedescendant on the
 * input + data-highlighted on the option, never DOM focus) - but for an autocomplete popup that opens
 * and closes, so it adds two behaviours command.ts does not need:
 *
 * Note the split from command.ts: there, the active option is marked data-selected. A combobox option
 * has a SECOND state - whether its value is chosen (the check indicator), which C# owns via
 * aria-selected/data-selected. So this module marks the active option data-highlighted instead, leaving
 * the selected/aria-selected attributes entirely to C#. The two can coexist on one option (the chosen
 * value, currently highlighted).
 *
 *  - the open seed is value-driven: command.ts always seeds + resyncs the highlight to the first match
 *    (the palette is always showing a "current" command). A combobox instead seeds from the SELECTION -
 *    opening highlights the selected option (scrolled into view, like a Select), or nothing when nothing
 *    is selected. It is never an unconditional first item. autoHighlight (OFF by default, Base UI
 *    semantics) governs only FILTERING: with it on, a query that is narrowing the list keeps the first
 *    match highlighted so Enter picks it; off, a refilter that drops the active option clears the
 *    highlight rather than jumping to the first match.
 *  - the list unmounts when the popup closes, so dispose() clears the input's aria-activedescendant -
 *    otherwise the (still-mounted) input would point at an option id that no longer exists.
 *
 * Selection flows through each option's own Blazor onclick, exactly like command.ts: Enter synthesizes
 * a click on the active option, and a mouse click is the same path. C# owns the query text and which
 * options render; this module owns the highlight, the scrolling, and (when autoHighlight is on) snapping
 * the highlight back to the first match after each refilter.
 */

export interface ComboboxOptions {
  /** Id of the combobox input this list belongs to (drives its aria-activedescendant). */
  inputId: string;
  /** Arrow navigation wraps past the ends. */
  loop: boolean;
  /** While a query filters the list, keep the first match highlighted so Enter picks it; off otherwise. */
  autoHighlight: boolean;
}

const ITEM_SELECTOR = '[data-bz-combobox-item]';

/** Visible to the user (not `hidden`, not in a hidden group). checkVisibility where supported. */
function isVisible(el: HTMLElement): boolean {
  const withCheck = el as HTMLElement & { checkVisibility?: () => boolean };
  return typeof withCheck.checkVisibility === 'function' ? withCheck.checkVisibility() : el.offsetParent !== null;
}

function isDisabled(el: HTMLElement): boolean {
  return el.hasAttribute('data-disabled') || el.getAttribute('aria-disabled') === 'true';
}

/** A navigable option: rendered, matching the search, and enabled. */
function isSelectable(el: HTMLElement): boolean {
  return isVisible(el) && !isDisabled(el);
}

class Combobox {
  private active: HTMLElement | null = null;
  // Whether the current highlight came from the mouse (so it should clear when the pointer leaves) vs
  // the keyboard / open-seed / autoHighlight filter (which must survive an incidental pointer leave).
  private pointerActive = false;
  private readonly observer: MutationObserver;

  constructor(
    private readonly list: HTMLElement,
    private readonly input: HTMLElement,
    private readonly options: ComboboxOptions,
  ) {
    this.input.addEventListener('keydown', this.onKeyDown);
    this.list.addEventListener('pointermove', this.onPointerMove);
    this.list.addEventListener('pointerleave', this.onPointerLeave);

    // Watch for the list re-rendering (items added/removed) and for the `hidden` / `data-disabled`
    // toggles C# uses to filter - then re-validate the highlight. The attributes WE set
    // (data-selected, aria-selected) are intentionally absent from attributeFilter so this never
    // re-fires on its own highlight changes.
    this.observer = new MutationObserver(() => this.resync());
    this.observer.observe(this.list, {
      childList: true,
      subtree: true,
      attributes: true,
      attributeFilter: ['hidden', 'data-disabled'],
    });

    // Seed the highlight from the current selection, so opening LANDS on the chosen value (scrolled
    // into view) like a Select. With nothing selected the popup opens unhighlighted until the user
    // types or arrows - autoHighlight seeds the first match only once a query is filtering (resync),
    // never on a bare open. The highlight is always value- or query-driven, never an unconditional
    // first item.
    this.setActive(this.fallback(), true);
  }

  /** The selected option (C# marks it aria-selected / data-selected), if it is currently navigable. */
  private selected(): HTMLElement | null {
    return (
      this.items().find(
        (el) => el.getAttribute('aria-selected') === 'true' || el.hasAttribute('data-selected'),
      ) ?? null
    );
  }

  /** Whether a query is narrowing the list - C# has hidden the non-matching options. */
  private filtering(): boolean {
    return this.list.querySelector(`${ITEM_SELECTOR}[hidden]`) !== null;
  }

  /**
   * Where the highlight settles when no option is actively chosen (on open, or after a refilter drops
   * the active one): the selected option while the full list shows - so opening lands on the current
   * value - or the first match while a query filters and autoHighlight is on. Never an unconditional
   * first item; with nothing selected and no filter, nothing is highlighted.
   */
  private fallback(): HTMLElement | null {
    if (this.filtering()) return this.options.autoHighlight ? this.items()[0] ?? null : null;
    return this.selected();
  }

  /** Every navigable option, in DOM order. */
  private items(): HTMLElement[] {
    return Array.from(this.list.querySelectorAll<HTMLElement>(ITEM_SELECTOR)).filter(isSelectable);
  }

  /** Move the highlight onto an option (or clear it), and point the input's activedescendant at it. */
  private setActive(el: HTMLElement | null, scroll = true, pointer = false): void {
    this.list
      .querySelectorAll<HTMLElement>(`${ITEM_SELECTOR}[data-highlighted]`)
      .forEach((node) => node.removeAttribute('data-highlighted'));

    this.active = el && this.list.contains(el) ? el : null;
    // Remember whether the mouse owns this highlight: only a hover clears when the pointer leaves.
    this.pointerActive = this.active !== null && pointer;

    if (this.active) {
      this.active.setAttribute('data-highlighted', 'true');
      this.input.setAttribute('aria-activedescendant', this.active.id);
      if (scroll) this.active.scrollIntoView({ block: 'nearest' });
    } else {
      this.input.removeAttribute('aria-activedescendant');
    }
  }

  /** Step the highlight by `delta`, wrapping at the ends when loop is on, else stopping there. */
  private move(delta: number): void {
    const items = this.items();
    if (items.length === 0) {
      this.setActive(null);
      return;
    }

    const index = this.active ? items.indexOf(this.active) : -1;
    let next: number;
    if (index === -1) next = delta > 0 ? 0 : items.length - 1;
    else if (this.options.loop) next = (index + delta + items.length) % items.length;
    else next = Math.min(Math.max(index + delta, 0), items.length - 1);

    this.setActive(items[next] ?? null);
  }

  private onKeyDown = (event: KeyboardEvent): void => {
    if (event.isComposing || event.altKey || event.ctrlKey || event.metaKey) return;

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.move(1);
        return;
      case 'ArrowUp':
        event.preventDefault();
        this.move(-1);
        return;
      case 'Home': {
        event.preventDefault();
        this.setActive(this.items()[0] ?? null);
        return;
      }
      case 'End': {
        event.preventDefault();
        const items = this.items();
        this.setActive(items[items.length - 1] ?? null);
        return;
      }
      case 'Enter':
        if (this.active && isSelectable(this.active)) {
          event.preventDefault();
          this.active.click();
        }
        return;
      default:
        return;
    }
  };

  /**
   * The mouse over an option highlights it (without scrolling - the pointer is already there); moving
   * off every option (onto the list's padding) drops a hover highlight, so it does not linger.
   */
  private onPointerMove = (event: PointerEvent): void => {
    if (event.pointerType !== 'mouse') return;
    const item = (event.target as HTMLElement | null)?.closest<HTMLElement>(ITEM_SELECTOR);
    if (item && this.list.contains(item) && isSelectable(item)) {
      if (item === this.active) this.pointerActive = true;
      else this.setActive(item, false, true);
    } else if (this.pointerActive) {
      this.setActive(null);
    }
  };

  /** The pointer leaving the list drops a hover highlight (a keyboard / seeded one is left alone). */
  private onPointerLeave = (event: PointerEvent): void => {
    if (event.pointerType !== 'mouse') return;
    if (this.pointerActive) this.setActive(null);
  };

  /**
   * After a refilter (or any list change), keep the highlight on a real option. If the active option
   * is still selectable, leave it; otherwise fall back: the first match while a query filters (when
   * autoHighlight is on), else the selected option if the full list shows, else nothing.
   */
  private resync(): void {
    if (this.active && this.list.contains(this.active) && isSelectable(this.active)) return;
    this.setActive(this.fallback());
  }

  dispose(): void {
    this.observer.disconnect();
    this.input.removeEventListener('keydown', this.onKeyDown);
    this.list.removeEventListener('pointermove', this.onPointerMove);
    this.list.removeEventListener('pointerleave', this.onPointerLeave);
    // The input outlives this list (it is the anchor, mounted across opens): leave no dangling
    // aria-activedescendant pointing at an option that just unmounted with the closing popup.
    this.input.removeAttribute('aria-activedescendant');
  }
}

const noop = { dispose() {} };

/**
 * Attaches combobox navigation to a <code>[data-bz-combobox-list]</code> listbox. The input is
 * resolved from <code>options.inputId</code>. Returns a handle whose <code>dispose()</code> detaches
 * everything; a no-op handle if the list isn't an element or the input can't be found.
 */
export function createCombobox(list: HTMLElement, options: ComboboxOptions): { dispose(): void } {
  if (!(list instanceof HTMLElement)) return noop;
  const input = document.getElementById(options.inputId);
  if (!input) return noop;
  return new Combobox(list, input, options);
}

/**
 * Moves focus to the combobox input by id (and selects its text, so the next keystroke replaces a
 * stale value). Used by Clear / a chip's remove button to hand focus back so typing can continue.
 */
export function focusInput(id: string): void {
  const input = document.getElementById(id);
  if (input instanceof HTMLInputElement) {
    input.focus();
    input.select();
  } else {
    input?.focus();
  }
}
