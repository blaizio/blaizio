// Height measurement + close-animation presence for accordion/collapsible content.
//
// The styled layer animates height between 0 and a CSS variable (the height variables ts/collapse.ts
// publishes: --bz-accordion-content-height / --bz-collapsible-content-height, which the skins
// reference). This module owns the variable: it measures the content's natural height
// when it opens, keeps it fresh while the panel stays open (content rendered on demand, async
// loads, window resizes), and during close it tells C# when the exit animation finished so the
// element can unmount (the exit-animation presence equivalent). Callback interop, like FocusScope.

import { invokeDotNet } from './core';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

class Collapse {
  private fallback?: number;
  private frame?: number;
  private timer?: number;
  private readonly observer: MutationObserver;

  constructor(
    private readonly el: HTMLElement,
    private readonly ref: DotNetObjectReference,
  ) {
    this.measure();
    el.addEventListener('animationend', this.onAnimationEnd);
    el.addEventListener('animationcancel', this.onAnimationEnd);

    // A stale height variable is a visible bug: the close animation starts FROM it, so content
    // that grew after opening (a thread loading in, a composer expanding) makes the panel snap
    // to the old height before animating - the "close blink". Re-measure whenever the panel's
    // subtree changes or the window resizes (text re-wraps inside the pinned inner wrapper
    // without any DOM mutation). The style attribute writes in measure() are change-guarded, so
    // the observer can't feed back into itself.
    this.observer = new MutationObserver(this.scheduleMeasure);
    this.observer.observe(el, { childList: true, subtree: true, characterData: true, attributes: true });
    window.addEventListener('resize', this.scheduleMeasure);
  }

  /** Sets the height variables from the content's natural height. Call while fully open. */
  measure() {
    // The inner wrapper is pinned to the PREVIOUS measurement (a height class referencing the
    // variable), so measuring through it just reports the stale value back - a fixed-height flex
    // wrapper even shrinks freshly-added children into itself. Lift the pin for the measurement,
    // then restore it.
    const child = this.el.firstElementChild as HTMLElement | null;
    const pinned = child?.style.height ?? '';
    if (child) child.style.height = 'auto';
    // scrollHeight rounds the fractional layout height down (fractional line heights, 125%/150%
    // display scaling), and the inner wrapper is pinned to the variable - a floor would clip
    // the content's last pixel. Take the fractional child rect too and round the larger one up.
    const natural = Math.max(this.el.scrollHeight, child ? child.getBoundingClientRect().height : 0);
    if (child) child.style.height = pinned;
    const height = `${Math.ceil(natural)}px`;
    if (this.el.style.getPropertyValue('--bz-collapsible-content-height') === height) return;
    this.el.style.setProperty('--bz-accordion-content-height', height);
    this.el.style.setProperty('--bz-collapsible-content-height', height);
  }

  /** Throttled re-measure; skipped while the close animation owns the height. rAF-aligned when
   * visible; hidden pages never fire rAF, so they fall back to a macrotask (same reasoning as
   * onClosing's timer fallback) - the variable must be fresh by the time the page is shown. */
  private scheduleMeasure = (): void => {
    if (this.frame !== undefined || this.timer !== undefined) return;
    const run = () => {
      this.frame = undefined;
      this.timer = undefined;
      if (this.el.dataset.state !== 'closed') this.measure();
    };
    if (document.hidden) this.timer = window.setTimeout(run, 0);
    else this.frame = requestAnimationFrame(run);
  };

  /**
   * Called after the element re-rendered with data-state="closed". When no exit animation is
   * configured (e.g. a skin without accordion animations), reports "done" immediately so C#
   * unmounts at once; otherwise animationend does it - backed by a timer for the longest
   * declared animation, because hidden pages (background tab, headless window) get no rendering
   * updates and never dispatch animation events. Same pattern as presence.ts.
   */
  onClosing() {
    const style = getComputedStyle(this.el);
    if (!style.animationName || style.animationName === 'none') {
      this.finish();
      return;
    }

    const longest = (list: string) => Math.max(...list.split(',').map((v) => parseFloat(v) || 0));
    const total = longest(style.animationDuration) + longest(style.animationDelay);
    this.fallback = window.setTimeout(this.finish, total * 1000 + 100);
  }

  private onAnimationEnd = (e: AnimationEvent) => {
    if (e.target === this.el && this.el.dataset.state === 'closed') this.finish();
  };

  /** Idempotent per close - C# ignores the report unless it is actually closing. */
  private finish = (): void => {
    clearTimeout(this.fallback);
    void invokeDotNet(this.ref, 'OnCloseFinished');
  };

  dispose() {
    clearTimeout(this.fallback);
    clearTimeout(this.timer);
    if (this.frame !== undefined) cancelAnimationFrame(this.frame);
    this.observer.disconnect();
    window.removeEventListener('resize', this.scheduleMeasure);
    this.el.removeEventListener('animationend', this.onAnimationEnd);
    this.el.removeEventListener('animationcancel', this.onAnimationEnd);
  }
}

/** Attaches collapse behaviour to an accordion/collapsible content element. */
export function createCollapse(el: HTMLElement, ref: DotNetObjectReference): Collapse {
  return new Collapse(el, ref);
}
