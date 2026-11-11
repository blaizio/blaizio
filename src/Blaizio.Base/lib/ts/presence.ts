// Exit-animation presence for overlay surfaces (dialog, sheet, popover) - the generic sibling of
// collapse.ts (which also measures heights). The element stays rendered with data-state="closed"
// while the style's exit animation plays; this module tells C# when it finished so the element can
// be removed (the exit-animation presence equivalent). Callback interop, like FocusScope.

import { invokeDotNet } from './core';
import type { DotNetObjectReference } from './core';

class Presence {
  private fallback?: number;
  private hidden = false;
  private readonly reopened: MutationObserver;

  constructor(
    private readonly el: HTMLElement,
    private readonly ref: DotNetObjectReference,
  ) {
    el.addEventListener('animationend', this.onAnimationEnd);
    el.addEventListener('animationcancel', this.onAnimationEnd);

    // A surface can be reopened while its close is still in flight: finish() has hidden it and
    // reported "done", but the open reached C# first, so C# ignores the report and keeps the SAME
    // element, now data-state="open". Nothing re-renders the inline style, so without this the
    // surface would stay invisible while trapping focus. Undo our own hide (and only ours) the
    // moment the state leaves "closed".
    this.reopened = new MutationObserver(() => {
      if (this.el.dataset.state === 'closed') return;
      clearTimeout(this.fallback);
      if (this.hidden) {
        this.hidden = false;
        this.el.style.removeProperty('visibility');
      }
    });
    this.reopened.observe(el, { attributes: true, attributeFilter: ['data-state'] });
  }

  /**
   * Called after the element re-rendered with data-state="closed". When no exit animation is
   * configured (a style without overlay animations, or prefers-reduced-motion), reports "done"
   * immediately so C# removes it at once; otherwise animationend does it - backed by a timer for
   * the longest declared animation, because hidden pages (background tab, headless window) get
   * no rendering updates and never dispatch animation events.
   */
  onClosing() {
    const style = getComputedStyle(this.el);
    if (!style.animationName || style.animationName === 'none') {
      this.finish();
      return;
    }

    const longest = (list: string) => Math.max(...list.split(',').map((v) => parseFloat(v) || 0));
    const total = longest(style.animationDuration) + longest(style.animationDelay);
    // Called on every render while closing: keep one timer, or a stale one from an interrupted
    // close would cut a later exit animation short.
    clearTimeout(this.fallback);
    this.fallback = window.setTimeout(this.finish, total * 1000 + 100);
  }

  // Only the animation the element runs NOW counts. Closing while the entry animation is still
  // playing cancels that one, and its animationcancel arrives with the state already "closed":
  // taken for the end of the exit, it removed the surface before the exit animation had played.
  private onAnimationEnd = (e: AnimationEvent) => {
    if (e.target !== this.el || this.el.dataset.state !== 'closed') return;
    const running = getComputedStyle(this.el).animationName.split(',').map((name) => name.trim());
    if (running.includes(e.animationName)) this.finish();
  };

  /** Idempotent per close - C# ignores the report unless it is actually closing. */
  private finish = (): void => {
    clearTimeout(this.fallback);
    // Hide NOW, before the interop roundtrip. The exit animation has fill-mode none, so once it
    // finishes the surface snaps back to its fully-visible resting styles; on Blazor Server the
    // removal only lands a network roundtrip later, and that gap reads as a close-blink. The next
    // open renders a fresh element; a reopen that reuses this one is handled by the observer above.
    if (this.el.dataset.state === 'closed') {
      this.el.style.visibility = 'hidden';
      this.hidden = true;
    }
    void invokeDotNet(this.ref, 'OnCloseFinished');
  };

  dispose() {
    clearTimeout(this.fallback);
    this.reopened.disconnect();
    this.el.removeEventListener('animationend', this.onAnimationEnd);
    this.el.removeEventListener('animationcancel', this.onAnimationEnd);
  }
}

/** Attaches presence tracking to a presence-managed element. */
export function createPresence(el: HTMLElement, ref: DotNetObjectReference): Presence {
  return new Presence(el, ref);
}
