/**
 * RovingFocus - the ARIA roving-tabindex keyboard pattern for composite widgets (radio groups,
 * toggle groups, toolbars, tabs). Arrow keys move focus between items; only one item is in the tab
 * sequence at a time.
 *
 * Ported and modernised from ArcBlazor's roverFocus.ts:
 *  - No .NET interop. The original round-tripped every focus move to C# (`UpdateCurrentIndex`); here
 *    the behaviour is wholly DOM-side. Selection/activation still flows through the items' own Blazor
 *    click handlers, so C# never needs a focus callback.
 *  - Items are found by the `[data-bz-roving-item]` marker, not a hard-coded `button` tag, so any
 *    element type (radio, tab, toggle) participates; disabled items are skipped.
 *  - C# never renders `tabindex` on items - this class owns it entirely (avoiding a Blazor-diff vs
 *    imperative-DOM tug-of-war). A `focusin` listener keeps the roving point in sync with mouse/Tab
 *    focus too, not just arrow keys. Strict TypeScript, no `any`.
 *  - Reading direction is read LIVE from the DOM at keydown (not cached at construction), so a
 *    runtime language / `dir` switch flips Left/Right immediately - no re-init, no interop.
 */

export interface RovingFocusOptions {
  /** Which arrow keys navigate. "both" accepts all four. */
  orientation: 'horizontal' | 'vertical' | 'both';
  /**
   * Reading-direction fallback (RTL swaps Left/Right). The live direction is read from the DOM
   * (`getComputedStyle(container).direction`) at keydown so a runtime switch needs no re-init; this
   * construction-time value is used only while the container is detached.
   */
  dir: 'ltr' | 'rtl';
  /** Wrap around at the ends. */
  loop: boolean;
  /**
   * Activate (click) an item the moment arrow-navigation focuses it - the ARIA radio pattern
   * ("selection follows focus"). Only the arrow path triggers it, so Tab/click focus never auto-selects.
   * Radio groups opt in; toggle groups / tabs / toolbars leave it false.
   */
  selectOnFocus: boolean;
}

type FocusIntent = 'first' | 'last' | 'prev' | 'next';

const ITEM_SELECTOR = '[data-bz-roving-item]';

export class RovingFocus {
  constructor(
    private readonly container: HTMLElement,
    private readonly options: RovingFocusOptions,
  ) {
    if (!container) return;
    this.seedTabStops();
    this.container.addEventListener('keydown', this.onKeyDown);
    this.container.addEventListener('focusin', this.onFocusIn);
  }

  /** Make exactly one item tabbable: the `[data-roving-active]` one if present, else the first. */
  private seedTabStops(): void {
    const items = this.getItems();
    const active = items.find((item) => item.hasAttribute('data-roving-active')) ?? items[0];
    for (const item of items) item.setAttribute('tabindex', item === active ? '0' : '-1');
  }

  /** Whenever an item receives focus (arrow, Tab, or click), it becomes the sole tab stop. */
  private onFocusIn = (event: FocusEvent): void => {
    const item = (event.target as HTMLElement | null)?.closest<HTMLElement>(ITEM_SELECTOR);
    if (!item || !this.container.contains(item)) return;
    for (const candidate of this.getItems()) {
      candidate.setAttribute('tabindex', candidate === item ? '0' : '-1');
    }
  };

  private onKeyDown = (event: KeyboardEvent): void => {
    if (event.metaKey || event.ctrlKey || event.altKey || event.shiftKey) return;

    const intent = this.getFocusIntent(event.key);
    if (intent === undefined) return;
    event.preventDefault();

    let candidates = this.getItems();
    if (intent === 'last') {
      candidates.reverse();
    } else if (intent === 'prev' || intent === 'next') {
      if (intent === 'prev') candidates.reverse();
      const current = (event.target as HTMLElement | null)?.closest<HTMLElement>(ITEM_SELECTOR);
      const index = current ? candidates.indexOf(current) : -1;
      candidates = this.options.loop ? wrap(candidates, index + 1) : candidates.slice(index + 1);
    }

    const focused = focusFirst(candidates);
    // Selection-follows-focus: only on the arrow path, and only when focus actually moved.
    if (focused && this.options.selectOnFocus) focused.click();
  };

  private getFocusIntent(rawKey: string): FocusIntent | undefined {
    const key = this.directionAwareKey(rawKey);
    const { orientation } = this.options;

    if (orientation === 'vertical' && (key === 'ArrowLeft' || key === 'ArrowRight')) return undefined;
    if (orientation === 'horizontal' && (key === 'ArrowUp' || key === 'ArrowDown')) return undefined;

    switch (key) {
      case 'ArrowLeft':
      case 'ArrowUp':
        return 'prev';
      case 'ArrowRight':
      case 'ArrowDown':
        return 'next';
      case 'Home':
      case 'PageUp':
        return 'first';
      case 'End':
      case 'PageDown':
        return 'last';
      default:
        return undefined;
    }
  }

  /**
   * Current reading direction, read live from the DOM (the inherited `dir` attribute / CSS
   * `direction`) so a runtime language switch flips arrow-key behaviour with no re-init. Falls back
   * to the construction-time hint only when the container is detached (no computed style to read).
   */
  private get dir(): 'ltr' | 'rtl' {
    if (!this.container.isConnected) return this.options.dir;
    return getComputedStyle(this.container).direction === 'rtl' ? 'rtl' : 'ltr';
  }

  /** In RTL, Left/Right swap meaning; other keys pass through. */
  private directionAwareKey(key: string): string {
    if (this.dir !== 'rtl') return key;
    if (key === 'ArrowLeft') return 'ArrowRight';
    if (key === 'ArrowRight') return 'ArrowLeft';
    return key;
  }

  /** Enabled items in DOM order. */
  private getItems(): HTMLElement[] {
    return Array.from(this.container.querySelectorAll<HTMLElement>(ITEM_SELECTOR)).filter(
      (el) =>
        !(el as HTMLElement & { disabled?: boolean }).disabled &&
        !el.hasAttribute('data-disabled') &&
        el.getAttribute('aria-disabled') !== 'true',
    );
  }

  dispose = (): void => {
    this.container.removeEventListener('keydown', this.onKeyDown);
    this.container.removeEventListener('focusin', this.onFocusIn);
  };
}

/** Focus the first candidate that actually takes focus; returns it (or null if none moved focus). */
function focusFirst(candidates: HTMLElement[]): HTMLElement | null {
  const previous = document.activeElement;
  for (const candidate of candidates) {
    if (candidate === previous) return null;
    candidate.focus();
    if (document.activeElement !== previous) return candidate;
  }
  return null;
}

/** Rotate an array so iteration starts at `start` and wraps around. */
function wrap<T>(array: T[], start: number): T[] {
  return array.map((_, index) => array[(start + index) % array.length] as T);
}

export const createRovingFocus = (container: HTMLElement, options: RovingFocusOptions): RovingFocus =>
  new RovingFocus(container, options);
