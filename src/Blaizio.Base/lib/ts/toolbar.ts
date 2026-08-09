/**
 * Toolbar overflow scrolling - the state half of a scrollable toolbar viewport. Watches the
 * viewport and publishes `data-can-scroll-prev` / `data-can-scroll-next` on the toolbar root, so
 * the chevron buttons can dim themselves in pure CSS; `scroll` pages the viewport by most of its
 * own width in LOGICAL direction (prev/next), so RTL needs no special-casing at the call site.
 *
 * Keyboard needs none of this: arrow keys move focus through the roving items and the browser
 * scrolls the focused item into view on its own - the chevrons are a pointer affordance.
 */

export interface ToolbarScroller {
  scroll(direction: 'prev' | 'next'): void;
  dispose(): void;
}

/** Resolve the toolbar root from the viewport, so C# only threads one element reference. */
export function createScrollerFromViewport(viewport: HTMLElement): ToolbarScroller {
  return createScroller(viewport, viewport.closest<HTMLElement>('[role="toolbar"]') ?? viewport);
}

export function createScroller(viewport: HTMLElement, root: HTMLElement): ToolbarScroller {
  // scrollLeft is negative in RTL (0 at the logical start, -max at the logical end); the absolute
  // value is the logical distance travelled either way.
  const update = (): void => {
    const max = viewport.scrollWidth - viewport.clientWidth;
    const position = Math.abs(viewport.scrollLeft);
    root.setAttribute('data-can-scroll-prev', String(position > 1));
    root.setAttribute('data-can-scroll-next', String(position < max - 1));
  };

  // The viewport resizing AND its content resizing both move the thresholds - observe both (new
  // items rendered by Blazor change scrollWidth without any viewport resize).
  const observer = new ResizeObserver(update);
  observer.observe(viewport);
  for (const child of viewport.children) observer.observe(child);
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
      viewport.removeEventListener('scroll', update);
    },
  };
}
