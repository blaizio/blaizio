/**
 * Calendar - the one DOM concern C# can't do alone: synchronously preventing the browser's default
 * action for the grid navigation keys (the arrows, Home, End, PageUp, PageDown would otherwise scroll
 * the page) before handing the keystroke to .NET.
 *
 * Everything else stays in C#: the date math, which day becomes focused, flipping the visible month,
 * and moving DOM focus (Blazor's ElementReference.FocusAsync). Enter/Space are NOT handled here - a
 * focused day is a native <button>, so the browser activates it (firing onclick) on its own.
 *
 * The listener is delegated on the root, so it keeps working as Blazor re-renders the day buttons.
 */

const NAV_KEYS = new Set([
  'ArrowLeft',
  'ArrowRight',
  'ArrowUp',
  'ArrowDown',
  'Home',
  'End',
  'PageUp',
  'PageDown',
]);

const DAY_SELECTOR = '[data-slot="calendar-day-button"]';

class Calendar {
  constructor(
    private readonly root: HTMLElement,
    private readonly ref: DotNetReference,
  ) {
    if (!root) return;
    this.root.addEventListener('keydown', this.onKeyDown);
  }

  private onKeyDown = (event: KeyboardEvent): void => {
    if (event.metaKey || event.ctrlKey || event.altKey) return;
    if (!NAV_KEYS.has(event.key)) return;

    // Only act when focus is on a day button inside this calendar.
    const target = event.target as HTMLElement | null;
    if (!target?.closest(DAY_SELECTOR) || !this.root.contains(target)) return;

    event.preventDefault();
    void this.ref.invokeMethodAsync('OnNavigate', event.key, event.shiftKey);
  };

  dispose = (): void => {
    this.root.removeEventListener('keydown', this.onKeyDown);
  };
}

/** Attaches grid-key handling to a calendar root, forwarding navigation keys to .NET. */
export const createCalendar = (root: HTMLElement, ref: DotNetReference): Calendar =>
  new Calendar(root, ref);
