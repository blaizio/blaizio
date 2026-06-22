/**
 * Toast - the stacking / animation / gesture engine for BzToaster, ported from sonner's index.tsx in
 * the same spirit as slider.ts and rovingFocus: wholly DOM-side, reading state live from the markup so
 * the C# side only owns the toast DATA (add / update / remove). Blazor renders the <ol>/<li> skeleton
 * with the static data contract; this module takes over everything that moves.
 *
 *  - It never owns the list. C# adds a <li data-bz-toast> (no inline transform, no data-mounted) and
 *    calls `sync()` after each render. The engine measures each toast, lays out the stack via CSS custom
 *    properties, runs the enter animation, drives hover-to-expand and swipe-to-dismiss, and runs the
 *    auto-close timers. When a toast has finished animating out it calls C# `Remove(id, autoClose)`,
 *    which is the ONLY path that drops a toast from the list - so the exit animation is never cut off.
 *  - A programmatic dismiss (C# `Store.Dismiss`) sets `data-bz-toast-dismiss` on the <li>; `sync()` picks
 *    it up and starts the same exit. Swipe and timer dismissals start here in the engine.
 *  - Reading direction, position, duration and dismissibility are read off attributes each time, so a
 *    runtime change needs no re-init.
 */

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

const TOAST_SELECTOR = '[data-bz-toast]';
const LIST_SELECTOR = '[data-bz-toaster]';

const DEFAULT_DURATION = 4000;
const VISIBLE_TOASTS = 3;
const GAP = 14;
// Equal to the exit-animation duration in the stylesheet.
const TIME_BEFORE_UNMOUNT = 200;
// Past this (px) or a fast flick, a swipe dismisses instead of springing back.
const SWIPE_THRESHOLD = 45;

type SwipeSide = 'top' | 'right' | 'bottom' | 'left';

interface ToastEntry {
  id: string;
  el: HTMLElement;
  height: number;
  mounted: boolean;
  removing: boolean;
  // Auto-close timing (mirrors sonner's remaining-time bookkeeping so hover pauses are exact).
  duration: number; // ms, or Infinity for persistent
  remaining: number;
  timer: number | null;
  closeStart: number;
  lastCloseStart: number;
  // Swipe gesture, per toast.
  pointerStart: { x: number; y: number } | null;
  swipeDirection: 'x' | 'y' | null;
  dragStart: number;
}

const now = (): number => Date.now();

/** One stack of toasts anchored to a corner - a single <ol data-bz-toaster>. */
class Stack {
  private entries = new Map<string, ToastEntry>();
  private expanded = false;
  private interacting = false;
  private visibleToasts: number;

  constructor(
    private readonly ol: HTMLElement,
    private readonly ref: DotNetObjectReference,
    private readonly isDocumentHidden: () => boolean,
  ) {
    this.visibleToasts = this.numAttr(ol, 'data-bz-visible-toasts', VISIBLE_TOASTS);
    ol.addEventListener('mouseenter', this.onExpand);
    ol.addEventListener('mousemove', this.onExpand);
    ol.addEventListener('mouseleave', this.onCollapse);
    ol.addEventListener('pointerdown', this.onListPointerDown);
    ol.addEventListener('pointerup', this.onListPointerUp);
  }

  private numAttr(el: Element, name: string, fallback: number): number {
    const value = parseFloat(el.getAttribute(name) ?? '');
    return Number.isFinite(value) ? value : fallback;
  }

  /** Reconcile the engine's entries with the <li>s Blazor currently has rendered. */
  sync(): void {
    this.visibleToasts = this.numAttr(this.ol, 'data-bz-visible-toasts', VISIBLE_TOASTS);
    const lis = Array.from(this.ol.querySelectorAll<HTMLElement>(TOAST_SELECTOR));
    const seen = new Set<string>();

    for (const el of lis) {
      const id = el.getAttribute('data-bz-toast-id');
      if (!id) continue;
      seen.add(id);

      let entry = this.entries.get(id);
      if (!entry) {
        entry = this.register(el);
        this.entries.set(id, entry);
      } else if (entry.el !== el) {
        // Blazor re-keyed the node (rare) - re-bind to the live element.
        entry.el = el;
      }

      // Pick up content / duration / type changes (e.g. a promise toast resolving).
      this.remeasure(entry);
      this.refreshDuration(entry);

      // A C#-requested dismissal arrives as this attribute; start the exit once.
      if (el.getAttribute('data-bz-toast-dismiss') === 'true' && !entry.removing) {
        this.dismiss(entry, false);
      }
    }

    // Drop engine state for toasts C# has already removed from the DOM.
    for (const [id, entry] of this.entries) {
      if (!seen.has(id)) {
        if (entry.timer !== null) clearTimeout(entry.timer);
        this.entries.delete(id);
      }
    }

    this.layout();
    this.evaluateTimers();
  }

  private register(el: HTMLElement): ToastEntry {
    const duration = this.durationOf(el);
    const entry: ToastEntry = {
      id: el.getAttribute('data-bz-toast-id') ?? '',
      el,
      height: 0,
      mounted: false,
      removing: false,
      duration,
      remaining: duration,
      timer: null,
      closeStart: 0,
      lastCloseStart: 0,
      pointerStart: null,
      swipeDirection: null,
      dragStart: 0,
    };

    el.addEventListener('pointerdown', (e) => this.onTilePointerDown(e, entry));
    el.addEventListener('pointermove', (e) => this.onTilePointerMove(e, entry));
    el.addEventListener('pointerup', () => this.onTilePointerUp(entry));

    entry.height = this.measure(el);

    // Enter animation: the <li> starts in the unmounted CSS state (opacity 0, translated off-edge);
    // flip data-mounted on the next frame so the transition runs. A setTimeout backstop flips it even
    // when rAF is starved (a backgrounded tab never paints), so the toast can't get stuck invisible.
    const flip = (): void => {
      if (entry.mounted) return;
      entry.mounted = true;
      el.setAttribute('data-mounted', 'true');
    };
    requestAnimationFrame(() => requestAnimationFrame(flip));
    window.setTimeout(flip, 100);

    return entry;
  }

  private durationOf(el: HTMLElement): number {
    const raw = el.getAttribute('data-bz-toast-duration');
    if (raw === 'infinity' || raw === 'persistent') return Infinity;
    const value = parseFloat(raw ?? '');
    return Number.isFinite(value) && value > 0 ? value : DEFAULT_DURATION;
  }

  // Re-read the duration; if a persistent toast (e.g. loading) became finite, arm its timer afresh.
  private refreshDuration(entry: ToastEntry): void {
    const duration = this.durationOf(entry.el);
    if (duration === entry.duration) return;
    entry.duration = duration;
    entry.remaining = duration;
    if (entry.timer !== null) {
      clearTimeout(entry.timer);
      entry.timer = null;
    }
  }

  private measure(el: HTMLElement): number {
    const original = el.style.height;
    el.style.height = 'auto';
    const height = el.getBoundingClientRect().height;
    el.style.height = original;
    return height;
  }

  private remeasure(entry: ToastEntry): void {
    if (entry.removing) return;
    const height = this.measure(entry.el);
    if (height > 0) entry.height = height;
  }

  // --- layout: assign each toast its stack index, offset and z-index via CSS custom properties ---

  private layout(): void {
    const lis = Array.from(this.ol.querySelectorAll<HTMLElement>(TOAST_SELECTOR));
    const live = lis
      .map((el) => this.entries.get(el.getAttribute('data-bz-toast-id') ?? ''))
      .filter((e): e is ToastEntry => !!e && !e.removing);

    let cumulative = 0;
    live.forEach((entry, index) => {
      const el = entry.el;
      el.style.setProperty('--index', String(index));
      el.style.setProperty('--toasts-before', String(index));
      el.style.setProperty('--z-index', String(live.length - index));
      el.style.setProperty('--offset', `${index * GAP + cumulative}px`);
      el.style.setProperty('--initial-height', `${entry.height}px`);
      el.setAttribute('data-front', String(index === 0));
      el.setAttribute('data-index', String(index));
      el.setAttribute('data-visible', String(index < this.visibleToasts));
      el.setAttribute('data-expanded', String(this.expanded));
      cumulative += entry.height;
    });

    const front = live[0];
    this.ol.style.setProperty('--front-toast-height', `${front ? front.height : 0}px`);
    this.ol.setAttribute('data-lifted', String(this.expanded && live.length > 1));
  }

  // --- hover / interaction state ---

  private onExpand = (): void => {
    if (this.expanded) return;
    this.expanded = true;
    this.layout();
    this.evaluateTimers();
  };

  private onCollapse = (): void => {
    // Don't collapse mid-swipe, or the lifted layout would snap away under the pointer.
    if (this.interacting) return;
    if (!this.expanded) return;
    this.expanded = false;
    this.layout();
    this.evaluateTimers();
  };

  private onListPointerDown = (event: PointerEvent): void => {
    const target = event.target as HTMLElement | null;
    if (target?.dataset.dismissible === 'false') return;
    this.interacting = true;
  };

  private onListPointerUp = (): void => {
    this.interacting = false;
  };

  // --- auto-close timers (paused while expanded, interacting, or the tab is hidden) ---

  evaluateTimers(): void {
    const paused = this.expanded || this.interacting || this.isDocumentHidden();
    for (const entry of this.entries.values()) {
      if (entry.removing || entry.duration === Infinity) continue;
      if (paused) this.pauseTimer(entry);
      else this.startTimer(entry);
    }
  }

  private pauseTimer(entry: ToastEntry): void {
    if (entry.lastCloseStart < entry.closeStart) {
      const elapsed = now() - entry.closeStart;
      entry.remaining = entry.remaining - elapsed;
    }
    entry.lastCloseStart = now();
    if (entry.timer !== null) {
      clearTimeout(entry.timer);
      entry.timer = null;
    }
  }

  private startTimer(entry: ToastEntry): void {
    if (entry.timer !== null || entry.remaining === Infinity) return;
    entry.closeStart = now();
    entry.timer = window.setTimeout(() => {
      entry.timer = null;
      this.dismiss(entry, true);
    }, Math.max(0, entry.remaining));
  }

  // --- dismissal ---

  private dismiss(entry: ToastEntry, autoClose: boolean): void {
    if (entry.removing) return;
    entry.removing = true;
    if (entry.timer !== null) {
      clearTimeout(entry.timer);
      entry.timer = null;
    }
    entry.el.setAttribute('data-removed', 'true');
    // Re-flow the remaining toasts now (this one is excluded), then unmount after the exit plays.
    this.layout();
    window.setTimeout(() => {
      void this.ref.invokeMethodAsync('Remove', entry.id, autoClose);
    }, TIME_BEFORE_UNMOUNT);
  }

  // --- swipe gesture ---

  private swipeSidesFor(el: HTMLElement): SwipeSide[] {
    const y = el.getAttribute('data-y-position');
    const x = el.getAttribute('data-x-position');
    const sides: SwipeSide[] = [];
    if (y === 'top' || y === 'bottom') sides.push(y);
    if (x === 'left' || x === 'right') sides.push(x);
    return sides.length ? sides : ['right'];
  }

  private onTilePointerDown = (event: PointerEvent, entry: ToastEntry): void => {
    if (event.button === 2) return; // ignore right-click
    if (entry.removing || entry.el.getAttribute('data-dismissible') === 'false') return;
    // A click on a button (action / cancel / close) is handled by Blazor - never a swipe.
    if ((event.target as HTMLElement | null)?.closest('button')) return;

    entry.dragStart = now();
    entry.pointerStart = { x: event.clientX, y: event.clientY };
    entry.swipeDirection = null;
    entry.el.setAttribute('data-swiping', 'true');
    try {
      (event.target as HTMLElement).setPointerCapture(event.pointerId);
    } catch {
      /* nothing to capture - the document-level move/up still drive the swipe */
    }
  };

  private onTilePointerMove = (event: PointerEvent, entry: ToastEntry): void => {
    if (!entry.pointerStart) return;
    if ((window.getSelection()?.toString().length ?? 0) > 0) return;

    const dx = event.clientX - entry.pointerStart.x;
    const dy = event.clientY - entry.pointerStart.y;
    const sides = this.swipeSidesFor(entry.el);

    if (!entry.swipeDirection && (Math.abs(dx) > 1 || Math.abs(dy) > 1)) {
      entry.swipeDirection = Math.abs(dx) > Math.abs(dy) ? 'x' : 'y';
    }

    const dampen = (delta: number): number => 1 / (1.5 + Math.abs(delta) / 20);
    let amountX = 0;
    let amountY = 0;

    if (entry.swipeDirection === 'y' && (sides.includes('top') || sides.includes('bottom'))) {
      const allowed = (sides.includes('top') && dy < 0) || (sides.includes('bottom') && dy > 0);
      amountY = allowed ? dy : this.clampDampened(dy, dampen(dy));
    } else if (entry.swipeDirection === 'x' && (sides.includes('left') || sides.includes('right'))) {
      const allowed = (sides.includes('left') && dx < 0) || (sides.includes('right') && dx > 0);
      amountX = allowed ? dx : this.clampDampened(dx, dampen(dx));
    }

    if (Math.abs(amountX) > 0 || Math.abs(amountY) > 0) entry.el.setAttribute('data-swiped', 'true');
    entry.el.style.setProperty('--swipe-amount-x', `${amountX}px`);
    entry.el.style.setProperty('--swipe-amount-y', `${amountY}px`);
  };

  // Resist movement against the toast's anchored edge without jumping when the dampening kicks in.
  private clampDampened(delta: number, factor: number): number {
    const dampened = delta * factor;
    return Math.abs(dampened) < Math.abs(delta) ? dampened : delta;
  }

  private onTilePointerUp = (entry: ToastEntry): void => {
    if (!entry.pointerStart || entry.removing) return;
    const el = entry.el;
    const px = (name: string): number =>
      Number(el.style.getPropertyValue(name).replace('px', '') || 0);
    const amountX = px('--swipe-amount-x');
    const amountY = px('--swipe-amount-y');
    const amount = entry.swipeDirection === 'x' ? amountX : amountY;
    const velocity = Math.abs(amount) / Math.max(1, now() - entry.dragStart);

    entry.pointerStart = null;
    el.removeAttribute('data-swiping');

    if (Math.abs(amount) >= SWIPE_THRESHOLD || velocity > 0.11) {
      const direction =
        entry.swipeDirection === 'x' ? (amountX > 0 ? 'right' : 'left') : amountY > 0 ? 'down' : 'up';
      el.setAttribute('data-swipe-direction', direction);
      el.setAttribute('data-swipe-out', 'true');
      this.dismiss(entry, false);
    } else {
      el.style.setProperty('--swipe-amount-x', '0px');
      el.style.setProperty('--swipe-amount-y', '0px');
      el.removeAttribute('data-swiped');
    }
    entry.swipeDirection = null;
  };

  dispose(): void {
    for (const entry of this.entries.values()) {
      if (entry.timer !== null) clearTimeout(entry.timer);
    }
    this.entries.clear();
    this.ol.removeEventListener('mouseenter', this.onExpand);
    this.ol.removeEventListener('mousemove', this.onExpand);
    this.ol.removeEventListener('mouseleave', this.onCollapse);
    this.ol.removeEventListener('pointerdown', this.onListPointerDown);
    this.ol.removeEventListener('pointerup', this.onListPointerUp);
  }
}

class Toaster {
  private stacks = new Map<HTMLElement, Stack>();
  private documentHidden = false;

  constructor(
    private readonly root: HTMLElement,
    private readonly ref: DotNetObjectReference,
  ) {
    document.addEventListener('visibilitychange', this.onVisibilityChange);
  }

  private onVisibilityChange = (): void => {
    this.documentHidden = document.hidden;
    for (const stack of this.stacks.values()) stack.evaluateTimers();
  };

  /** Called by C# after each render: reconcile every stack with the live <ol>/<li> markup. */
  sync(): void {
    const ols = Array.from(this.root.querySelectorAll<HTMLElement>(LIST_SELECTOR));
    const seen = new Set<HTMLElement>();

    for (const ol of ols) {
      seen.add(ol);
      let stack = this.stacks.get(ol);
      if (!stack) {
        stack = new Stack(ol, this.ref, () => this.documentHidden);
        this.stacks.set(ol, stack);
      }
      stack.sync();
    }

    for (const [ol, stack] of this.stacks) {
      if (!seen.has(ol)) {
        stack.dispose();
        this.stacks.delete(ol);
      }
    }
  }

  dispose(): void {
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
    for (const stack of this.stacks.values()) stack.dispose();
    this.stacks.clear();
  }
}

/** Attaches the toast stacking / animation / swipe engine to a BzToaster root element. */
export const createToaster = (root: HTMLElement, ref: DotNetObjectReference): Toaster =>
  new Toaster(root, ref);
