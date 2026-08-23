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
    root.setAttribute('data-overflowing', String(mode === 'expand' ? wrapsRows(viewport) : overflowsWidth(viewport)));
    if (mode !== 'scroll') return;

    // scrollLeft is negative in RTL (0 at the logical start, -max at the logical end); the
    // absolute value is the logical distance travelled either way.
    const max = viewport.scrollWidth - viewport.clientWidth;
    const position = Math.abs(viewport.scrollLeft);
    root.setAttribute('data-can-scroll-prev', String(position > 1));
    root.setAttribute('data-can-scroll-next', String(position < max - 1));
  };

  // The viewport resizing AND its content resizing both move the thresholds - observe both (new
  // items rendered by Blazor change the content size without any viewport resize). A MutationObserver
  // picks up children Blazor adds or removes later, so they get observed too.
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
  mutations.observe(viewport, { childList: true });
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
 * Expand: the controls wrap onto more than one row. Measured by position rather than by
 * scrollHeight, so the answer is the same whether the bar is currently clipped or expanded (an
 * expanded bar has no hidden overflow, yet still has rows to collapse back to). A control on a
 * later row starts below the first control's bottom edge; controls sharing its row never do,
 * even when items-center staggers their tops.
 */
function wrapsRows(viewport: HTMLElement): boolean {
  const first = viewport.firstElementChild as HTMLElement | null;
  if (first === null) return false;
  const firstBottom = first.offsetTop + first.offsetHeight;
  for (const child of viewport.children) {
    if ((child as HTMLElement).offsetTop >= firstBottom) return true;
  }
  return false;
}
