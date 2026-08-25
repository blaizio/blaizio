/**
 * Toolbar overflow state - the JS half of a Scroll or Expand toolbar. Watches the viewport and
 * publishes on the toolbar root:
 *
 * - `data-overflowing` ("true" / "false"): whether the controls outgrow the viewport at all. Both
 *   modes hide their extra buttons (chevrons, expand toggle) until this is true, so a bar that
 *   fits carries no dead chrome. Hiding the buttons widens the viewport and showing them narrows
 *   it, but the check always runs against the CURRENT layout, so the band where either answer
 *   would hold keeps whatever state it is in - hysteresis by construction, no flicker.
 * - `data-can-scroll-prev` / `data-can-scroll-next` (Scroll only): whether paging in that
 *   logical direction has anywhere to go, so an exhausted chevron can dim itself in pure CSS.
 * - `data-reveal-open` (Expand only): whether a control marked `data-reveal="expand"` currently
 *   sits on a clipped row. The bar's CSS holds the clip open while it is true - a contextual
 *   control that appears mid-session is seen, and the hold releases itself when the control
 *   leaves or fits the first row. Not a state change: the expand toggle's own state is untouched.
 *
 * `scroll` pages the viewport by most of its own width in LOGICAL direction (prev/next), so RTL
 * needs no special-casing at the call site.
 *
 * Keyboard needs none of this: arrow keys move focus through the roving items and the browser
 * scrolls the focused item into view on its own - the buttons are a pointer affordance.
 */

export interface ToolbarScroller {
  scroll(direction: 'prev' | 'next'): void;
  dispose(): void;
}

export type ToolbarOverflowMode = 'scroll' | 'expand';

/** Resolve the toolbar root from the viewport, so C# only threads one element reference. */
export function createScrollerFromViewport(
  viewport: HTMLElement,
  mode: ToolbarOverflowMode = 'scroll',
): ToolbarScroller {
  return createScroller(viewport, viewport.closest<HTMLElement>('[role="toolbar"]') ?? viewport, mode);
}

export function createScroller(
  viewport: HTMLElement,
  root: HTMLElement,
  mode: ToolbarOverflowMode = 'scroll',
): ToolbarScroller {
  const update = (): void => {
    if (mode === 'expand') {
      const rowBottom = firstRowBottom(viewport);
      root.setAttribute('data-overflowing', String(wrapsRows(viewport, rowBottom)));
      root.setAttribute('data-reveal-open', String(hasClippedReveal(viewport, rowBottom)));
      return;
    }
    root.setAttribute('data-overflowing', String(overflowsWidth(viewport)));

    // scrollLeft is negative in RTL (0 at the logical start, -max at the logical end); the
    // absolute value is the logical distance travelled either way.
    const max = viewport.scrollWidth - viewport.clientWidth;
    const position = Math.abs(viewport.scrollLeft);
    root.setAttribute('data-can-scroll-prev', String(position > 1));
    root.setAttribute('data-can-scroll-next', String(position < max - 1));
  };

  // The viewport resizing AND its content resizing both move the thresholds - observe both (new
  // items rendered by Blazor change the content size without any viewport resize). A MutationObserver
  // picks up children Blazor adds or removes later, so they get observed too - subtree, because a
  // reveal control can appear inside a group without the viewport's child list changing.
  const observer = new ResizeObserver(update);
  observer.observe(viewport);
  const observeChildren = (): void => {
    for (const child of viewport.children) observer.observe(child);
  };
  observeChildren();
  const mutations = new MutationObserver(() => {
    observeChildren();
    update();
  });
  mutations.observe(viewport, { childList: true, subtree: true });
  viewport.addEventListener('scroll', update, { passive: true });
  update();

  return {
    scroll(direction) {
      const logical = direction === 'next' ? 1 : -1;
      const physical = getComputedStyle(viewport).direction === 'rtl' ? -logical : logical;
      viewport.scrollBy({
        left: viewport.clientWidth * 0.8 * physical,
        behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth',
      });
    },
    dispose() {
      observer.disconnect();
      mutations.disconnect();
      viewport.removeEventListener('scroll', update);
    },
  };
}

/** Scroll: the single row is wider than the viewport shows. */
function overflowsWidth(viewport: HTMLElement): boolean {
  return viewport.scrollWidth - viewport.clientWidth > 1;
}

/**
 * Whether an element takes part in the row layout at all. A closed select's content sits in the
 * viewport as display:none position:fixed - offsetTop 0, height 0 - and taking it as the topmost
 * child would put the "first row" at the top of the page, making every real control read as
 * wrapped. offsetParent is null for display:none AND position:fixed; absolute is out of flow too.
 */
function inFlow(el: HTMLElement): boolean {
  return el.offsetParent !== null && getComputedStyle(el).position !== 'absolute';
}

/**
 * The bottom edge of the first visual row: the bottom of the topmost IN-FLOW child (by offsetTop,
 * not DOM order - a pinned control can reorder the row). Measured by position rather than by
 * scrollHeight, so the answer is the same whether the bar is currently clipped or expanded (an
 * expanded bar has no hidden overflow, yet still has rows to collapse back to). A control on a
 * later row starts below this edge; controls sharing the row never do, even when items-center
 * staggers their tops. Infinity when nothing is in flow, so nothing ever reads as wrapped.
 */
function firstRowBottom(viewport: HTMLElement): number {
  let top = Infinity;
  let bottom = Infinity;
  for (const child of viewport.children) {
    const el = child as HTMLElement;
    if (inFlow(el) && el.offsetTop < top) {
      top = el.offsetTop;
      bottom = el.offsetTop + el.offsetHeight;
    }
  }
  return bottom;
}

/** Expand: the controls wrap onto more than one row. */
function wrapsRows(viewport: HTMLElement, rowBottom: number): boolean {
  for (const child of viewport.children) {
    const el = child as HTMLElement;
    if (inFlow(el) && el.offsetTop >= rowBottom) return true;
  }
  return false;
}

/**
 * Whether any control marked `data-reveal="expand"` sits below the first row. Descendants count
 * too (a control inside a group), and offsetTop is comparable across them because nothing in the
 * bar establishes its own offset context.
 */
function hasClippedReveal(viewport: HTMLElement, rowBottom: number): boolean {
  for (const el of viewport.querySelectorAll<HTMLElement>('[data-reveal="expand"]')) {
    if (inFlow(el) && el.offsetTop >= rowBottom) return true;
  }
  return false;
}
