// Edge autoscroll for pointer drags (the tree and the sortable list): while a dragged item is held
// near an edge of its scroll container, the container scrolls so the drop target can be reached.
// The two callers own their own frame loop and their own reaction to the scroll; what they share
// is which element scrolls and how far one frame moves it.

/** Distance (px) from a scrollable edge inside which the container scrolls. */
const SCROLL_EDGE = 40;
/** How far (px) one frame scrolls. */
const SCROLL_SPEED = 12;

export interface DragPoint {
  x: number;
  y: number;
}

/** The nearest ancestor of `start` (itself included) that can scroll, else the page's scroller. */
export function scrollableAncestor(start: HTMLElement | null): Element | null {
  let node = start;
  while (node) {
    const style = getComputedStyle(node);
    const scrollableY = /(auto|scroll)/.test(style.overflowY) && node.scrollHeight > node.clientHeight;
    const scrollableX = /(auto|scroll)/.test(style.overflowX) && node.scrollWidth > node.clientWidth;
    if (scrollableY || scrollableX) return node;
    node = node.parentElement;
  }
  return document.scrollingElement;
}

/** One frame's scroll for a pointer at `p`: non-zero on an axis whose edge the pointer is near. */
export function edgeScrollStep(p: DragPoint, scroller: Element): { dx: number; dy: number } {
  const r =
    scroller === document.scrollingElement
      ? { left: 0, top: 0, right: window.innerWidth, bottom: window.innerHeight }
      : scroller.getBoundingClientRect();
  let dx = 0;
  let dy = 0;
  if (p.y - r.top < SCROLL_EDGE) dy = -SCROLL_SPEED;
  else if (r.bottom - p.y < SCROLL_EDGE) dy = SCROLL_SPEED;
  if (p.x - r.left < SCROLL_EDGE) dx = -SCROLL_SPEED;
  else if (r.right - p.x < SCROLL_EDGE) dx = SCROLL_SPEED;
  return { dx, dy };
}
