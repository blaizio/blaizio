// Exit-animation presence for overlay surfaces (dialog, sheet, popover) - the generic sibling of
// collapse.ts (which also measures heights). The element stays mounted with data-state="closed"
// while the skin's exit animation plays; this module tells C# when it finished so the element can
// unmount (the exit-animation presence equivalent). Callback interop, like FocusScope.

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

class Presence {
  private fallback?: number;

  constructor(
    private readonly el: HTMLElement,
    private readonly ref: DotNetObjectReference,
  ) {
    el.addEventListener('animationend', this.onAnimationEnd);
    el.addEventListener('animationcancel', this.onAnimationEnd);
  }

  /**
   * Called after the element re-rendered with data-state="closed". When no exit animation is
   * configured (a skin without overlay animations, or prefers-reduced-motion), reports "done"
   * immediately so C# unmounts at once; otherwise animationend does it - backed by a timer for
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
    this.fallback = window.setTimeout(this.finish, total * 1000 + 100);
  }

  private onAnimationEnd = (e: AnimationEvent) => {
    if (e.target === this.el && this.el.dataset.state === 'closed') this.finish();
  };

  /** Idempotent per close - C# ignores the report unless it is actually closing. */
  private finish = (): void => {
    clearTimeout(this.fallback);
    void this.ref.invokeMethodAsync('OnCloseFinished');
  };

  dispose() {
    clearTimeout(this.fallback);
    this.el.removeEventListener('animationend', this.onAnimationEnd);
    this.el.removeEventListener('animationcancel', this.onAnimationEnd);
  }
}

/** Attaches presence tracking to a presence-managed element. */
export function createPresence(el: HTMLElement, ref: DotNetObjectReference): Presence {
  return new Presence(el, ref);
}
