/**
 * Keyboard navigation + the virtual highlight for a Command palette, attached to its
 * [data-bz-command-list] listbox. The sibling of menu.ts, but for the combobox pattern: focus stays
 * in the text input the whole time, so the "highlighted" option is virtual - tracked with
 * aria-activedescendant on the input and data-selected on the option, never with DOM focus. Wholly
 * DOM-side, no per-keystroke interop: C# owns the search text and which items render, this module owns
 * the highlight, the scrolling, and snapping the highlight back to the first match after each refilter.
 *
 * Selection flows through each option's own Blazor onclick, exactly like menu.ts: Enter synthesizes a
 * click on the active option, and a mouse click is the same path.
 *
 * The active option is re-derived from the DOM, never cached as state C# must stay in sync with: when
 * the user types, C# re-renders the list (toggling each option's `hidden` attribute), a MutationObserver
 * notices, and we move the highlight to the first still-visible option. We deliberately do NOT observe
 * the data-selected / aria-selected attributes we set ourselves, so applying the highlight can't loop.
 */

export interface CommandOptions {
  /** Id of the combobox input this list belongs to (drives its aria-activedescendant). */
  inputId: string;
  /** Arrow navigation wraps past the ends. */
  loop: boolean;
}

const ITEM_SELECTOR = '[data-bz-command-item]';

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

class Command {
  private active: HTMLElement | null = null;
  private readonly observer: MutationObserver;

  constructor(
    private readonly list: HTMLElement,
    private readonly input: HTMLElement,
    private readonly options: CommandOptions,
  ) {
    this.input.addEventListener('keydown', this.onKeyDown);
    this.list.addEventListener('pointermove', this.onPointerMove);

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

    this.setActive(this.items()[0] ?? null);
  }

  /** Every navigable option, in DOM order. */
  private items(): HTMLElement[] {
    return Array.from(this.list.querySelectorAll<HTMLElement>(ITEM_SELECTOR)).filter(isSelectable);
  }

  /** Move the highlight onto an option (or clear it). Mirrors the option's state into the input's aria. */
  private setActive(el: HTMLElement | null, scroll = true): void {
    this.list
      .querySelectorAll<HTMLElement>(`${ITEM_SELECTOR}[data-selected]`)
      .forEach((node) => {
        node.removeAttribute('data-selected');
        node.removeAttribute('aria-selected');
      });

    this.active = el && this.list.contains(el) ? el : null;

    if (this.active) {
      this.active.setAttribute('data-selected', 'true');
      this.active.setAttribute('aria-selected', 'true');
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

  /** The mouse moving over an option highlights it (without scrolling - the pointer is already there). */
  private onPointerMove = (event: PointerEvent): void => {
    if (event.pointerType !== 'mouse') return;
    const item = (event.target as HTMLElement | null)?.closest<HTMLElement>(ITEM_SELECTOR);
    if (item && this.list.contains(item) && isSelectable(item) && item !== this.active) {
      this.setActive(item, false);
    }
  };

  /** After a refilter (or any list change), keep the highlight on a real option - the first match. */
  private resync(): void {
    if (this.active && this.list.contains(this.active) && isSelectable(this.active)) return;
    this.setActive(this.items()[0] ?? null);
  }

  dispose(): void {
    this.observer.disconnect();
    this.input.removeEventListener('keydown', this.onKeyDown);
    this.list.removeEventListener('pointermove', this.onPointerMove);
  }
}

const noop = { dispose() {} };

/**
 * Attaches command-palette navigation to a <code>[data-bz-command-list]</code> listbox. The input is
 * resolved from <code>options.inputId</code>. Returns a handle whose <code>dispose()</code> detaches
 * everything; a no-op handle if the list isn't an element or the input can't be found.
 */
export function createCommand(list: HTMLElement, options: CommandOptions): { dispose(): void } {
  if (!(list instanceof HTMLElement)) return noop;
  const input = document.getElementById(options.inputId);
  if (!input) return noop;
  return new Command(list, input, options);
}
