/**
 * Marquee - hover-to-reveal for truncated text. This module finds elements marked
 * `data-bz-marquee` under a root, measures whether their text actually overflows, and stamps
 * `data-truncated` on the ones that do (the stylesheet swaps their ellipsis for `clip` on
 * :hover). The slide itself is driven HERE, not by a CSS transition: Chromium fails to repaint
 * glyphs for interpolated `text-indent` transition frames on an ellipsised line box - the
 * computed value and layout advance while the pixels stay frozen - so the module steps the
 * inline `text-indent` itself in a rAF loop, where every frame is a discrete style write that
 * invalidates paint. Labels that fit are left untouched, so only ellipsised text ever moves.
 *
 * Re-measures on resize (the root AND each label), on subtree changes (new nodes - expanded tree
 * branches, virtualized rows), and once webfonts land, coalesced into one rAF pass.
 */

const SELECTOR = '[data-bz-marquee]';

/**
 * Slide speed: ms per overflowing pixel, clamped so short and long tails both read well. Kept
 * brisk on purpose - a reveal is a response to a hover, and anything slower than about a second
 * reads as nothing happening at all, because the first frames of a linear slide are imperceptible.
 */
const MS_PER_PX = 5;
const MIN_MS = 300;
const MAX_MS = 1600;

/** Per-element slide state, attached expando-style so Blazor node swaps drop it naturally. */
interface SlideState {
  raf: number;
  enter: () => void;
  leave: () => void;
}

const states = new WeakMap<HTMLElement, SlideState>();

const prefersReducedMotion = (): boolean =>
  window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;

/**
 * Animate the label's inline `text-indent` from wherever it currently is to `to` (px, negative
 * = tail revealed). Duration is proportional to the remaining travel so an interrupted slide
 * reverses at the same speed instead of crawling. Reduced motion jumps straight to the target.
 */
function slide(el: HTMLElement, state: SlideState, to: number): void {
  cancelAnimationFrame(state.raf);
  const from = parseFloat(getComputedStyle(el).textIndent) || 0;
  const travel = Math.abs(to - from);
  if (travel < 1 || prefersReducedMotion()) {
    finish(el, to);
    return;
  }
  const duration = Math.min(MAX_MS, Math.max(MIN_MS, travel * MS_PER_PX));
  const start = performance.now();
  const step = (now: number): void => {
    const p = Math.min(1, (now - start) / duration);
    if (p < 1) {
      el.style.textIndent = `${from + (to - from) * p}px`;
      state.raf = requestAnimationFrame(step);
    } else {
      finish(el, to);
    }
  };
  state.raf = requestAnimationFrame(step);
}

/** Land exactly on the target; at rest (0) clear the inline override so CSS owns the ellipsis. */
function finish(el: HTMLElement, to: number): void {
  if (to === 0) el.style.removeProperty('text-indent');
  else el.style.textIndent = `${to}px`;
}

class Marquee {
  private readonly resizeObserver: ResizeObserver;
  private readonly mutationObserver: MutationObserver;
  private pending = false;

  constructor(private readonly root: HTMLElement) {
    if (!root) {
      this.resizeObserver = undefined!;
      this.mutationObserver = undefined!;
      return;
    }

    this.resizeObserver = new ResizeObserver(() => this.schedule());
    this.mutationObserver = new MutationObserver(() => this.schedule());
    this.mutationObserver.observe(root, { childList: true, subtree: true, characterData: true });
    this.resizeObserver.observe(root);

    // Webfonts are the reason a first pass cannot be trusted: with fallback metrics a label often
    // FITS, so nothing is stamped, and the swap to the real face changes text width without
    // touching the DOM or the root's size - no observer would ever fire. Without this the labels
    // stay unarmed until some unrelated mutation happens to re-trigger a pass, and a hover in the
    // meantime does nothing.
    document.fonts?.ready.then(() => this.schedule());
    document.fonts?.addEventListener('loadingdone', this.onFontsLoaded);

    this.schedule();
  }

  private onFontsLoaded = (): void => this.schedule();

  private schedule(): void {
    if (this.pending) return;
    this.pending = true;
    const run = () => {
      if (!this.pending) return;
      this.pending = false;
      this.measureAll();
    };
    // rAF coalesces measurement with the next paint, but a hidden or background tab never ticks
    // it - the timeout fallback guarantees the pass still lands (whichever fires first wins).
    requestAnimationFrame(run);
    setTimeout(run, 100);
  }

  private measureAll(): void {
    for (const el of this.root.querySelectorAll<HTMLElement>(SELECTOR)) {
      // Per-label observation, not just the root: a label narrows when a sibling column grows or
      // an ancestor reflows without the root itself changing size. Observing an element already
      // observed is a no-op, so this is safe to redo every pass.
      this.resizeObserver.observe(el);

      // A hovered label that is already stamped is mid-slide (text-indent shifts its scroll box) -
      // skip it; its stamped values are still valid, and the next mutation/resize pass will catch
      // any real change. An UNSTAMPED hovered label must still be measured: the cursor can be
      // parked on it before the first pass lands (cold circuit, fonts still loading), and skipping
      // it here would leave it unarmed - hover doing nothing - until the pointer leaves.
      if (el.matches(':hover') && el.hasAttribute('data-truncated')) continue;

      const distance = el.scrollWidth - el.clientWidth;
      if (distance > 1) {
        el.setAttribute('data-truncated', '');
        this.arm(el);
        // The cursor can already be sitting on the label the moment it becomes armed (parked
        // there through a page load) - there is no pointerenter still to come, so start the
        // reveal here or it never happens.
        if (el.matches(':hover')) states.get(el)?.enter();
      } else if (el.hasAttribute('data-truncated')) {
        el.removeAttribute('data-truncated');
        this.disarm(el);
      }
    }
  }

  /** Attach the hover listeners (idempotent). The slide distance is measured on each enter. */
  private arm(el: HTMLElement): void {
    const existing = states.get(el);
    if (existing) return; // listeners live; distance is re-read from layout on each enter

    const state: SlideState = {
      raf: 0,
      enter: () => {
        // Measure at enter time, not stamp time: the layout may have changed since. At rest the
        // inline indent is cleared, so scrollWidth is the label's true unshifted overflow.
        const d = el.scrollWidth - el.clientWidth;
        if (d > 1) slide(el, state, -d);
      },
      leave: () => slide(el, state, 0),
    };
    states.set(el, state);
    el.addEventListener('pointerenter', state.enter);
    el.addEventListener('pointerleave', state.leave);
  }

  /** Detach hover listeners and stop any in-flight slide; the label fits again. */
  private disarm(el: HTMLElement): void {
    const state = states.get(el);
    if (!state) return;
    cancelAnimationFrame(state.raf);
    el.removeEventListener('pointerenter', state.enter);
    el.removeEventListener('pointerleave', state.leave);
    el.style.removeProperty('text-indent');
    states.delete(el);
  }

  dispose = (): void => {
    this.pending = false;
    for (const el of this.root?.querySelectorAll<HTMLElement>(SELECTOR) ?? []) this.disarm(el);
    this.resizeObserver?.disconnect();
    this.mutationObserver?.disconnect();
    document.fonts?.removeEventListener('loadingdone', this.onFontsLoaded);
  };
}

/** Watch a root's `[data-bz-marquee]` descendants and stamp their overflow state. */
export const createMarquee = (root: HTMLElement): Marquee => new Marquee(root);
