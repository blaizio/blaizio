// Height measurement + close-animation presence for accordion/collapsible content.
//
// The styled layer animates height between 0 and a CSS variable (the Radix convention:
// --radix-accordion-content-height / --radix-collapsible-content-height, which the ported skins
// reference verbatim). This module owns the variable: it measures the content's natural height
// when it opens, and during close it tells C# when the exit animation finished so the element can
// unmount (the Radix <Presence> equivalent). Callback interop, like FocusScope.

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

class Collapse {
  constructor(
    private readonly el: HTMLElement,
    private readonly ref: DotNetObjectReference,
  ) {
    this.measure();
    el.addEventListener('animationend', this.onAnimationEnd);
  }

  /** Sets the height variables from the content's natural height. Call while fully open. */
  measure() {
    const height = `${this.el.scrollHeight}px`;
    this.el.style.setProperty('--radix-accordion-content-height', height);
    this.el.style.setProperty('--radix-collapsible-content-height', height);
  }

  /**
   * Called after the element re-rendered with data-state="closed". When no exit animation is
   * configured (e.g. a skin without accordion animations), reports "done" immediately so C#
   * unmounts at once; otherwise animationend does it.
   */
  onClosing() {
    const name = getComputedStyle(this.el).animationName;
    if (!name || name === 'none') void this.ref.invokeMethodAsync('OnCloseFinished');
  }

  private onAnimationEnd = (e: AnimationEvent) => {
    if (e.target === this.el && this.el.dataset.state === 'closed')
      void this.ref.invokeMethodAsync('OnCloseFinished');
  };

  dispose() {
    this.el.removeEventListener('animationend', this.onAnimationEnd);
  }
}

/** Attaches collapse behaviour to an accordion/collapsible content element. */
export function createCollapse(el: HTMLElement, ref: DotNetObjectReference): Collapse {
  return new Collapse(el, ref);
}
