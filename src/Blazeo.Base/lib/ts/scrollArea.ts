// Custom overlay scrollbars for the ScrollArea. The native scrollbar is hidden (the viewport carries
// `scrollbar-none`); over it we draw a thumb sized and positioned from the live scroll metrics -
// radix/vaul-style. The thumb is draggable, and each scrollbar is shown/hidden per the root's
// `data-type` (auto | always | scroll | hover). Pure DOM: C# renders the structure and wires this on
// first render; there are no .NET round-trips.
//
// Expected structure under the root element:
//   [data-slot=scroll-area]                         <- the root passed to createScrollArea
//     [data-slot=scroll-area-viewport]              <- the scroller (overflow + scrollbar-none)
//       [data-scroll-area-content]                  <- single measurable wrapper around the content
//     [data-slot=scroll-area-scrollbar][data-orientation=vertical|horizontal][data-state]
//       [data-slot=scroll-area-thumb]
//     ... (optional second scrollbar, corner)

const MIN_THUMB = 18; // px - the thumb never shrinks below this so it stays grabbable
const HIDE_DELAY = 600; // ms - how long a scrollbar lingers after scrolling stops (type=scroll/hover)

interface Bar {
  el: HTMLElement;
  thumb: HTMLElement | null;
  horizontal: boolean;
  dragging: boolean;
  pointerStart: number; // client X/Y where the drag began
  scrollStart: number; // viewport scroll offset where the drag began
  pointerId: number;
  down: (e: PointerEvent) => void;
}

class ScrollAreaController {
  private readonly viewport: HTMLElement | null;
  private readonly bars: Bar[];
  private readonly type: string;
  private hovering = false;
  private scrolling = false;
  private hideTimer = 0;
  private readonly ro: ResizeObserver;

  constructor(private readonly root: HTMLElement) {
    this.viewport = root.querySelector<HTMLElement>('[data-slot="scroll-area-viewport"]');
    this.type = root.getAttribute('data-type') || 'hover';

    this.bars = Array.from(
      root.querySelectorAll<HTMLElement>('[data-slot="scroll-area-scrollbar"]'),
    ).map((el) => {
      const bar: Bar = {
        el,
        thumb: el.querySelector<HTMLElement>('[data-slot="scroll-area-thumb"]'),
        horizontal: el.getAttribute('data-orientation') === 'horizontal',
        dragging: false,
        pointerStart: 0,
        scrollStart: 0,
        pointerId: -1,
        down: (e) => this.onThumbDown(e, bar),
      };
      bar.thumb?.addEventListener('pointerdown', bar.down);
      return bar;
    });

    this.viewport?.addEventListener('scroll', this.onScroll, { passive: true });
    root.addEventListener('pointerenter', this.onPointerEnter);
    root.addEventListener('pointerleave', this.onPointerLeave);

    this.ro = new ResizeObserver(() => this.update());
    if (this.viewport) {
      this.ro.observe(this.viewport);
      const content = this.viewport.querySelector<HTMLElement>('[data-scroll-area-content]');
      if (content) this.ro.observe(content);
    }

    this.update();
  }

  private onScroll = (): void => {
    this.update();
    if (this.type === 'scroll' || this.type === 'hover') {
      this.scrolling = true;
      clearTimeout(this.hideTimer);
      this.hideTimer = window.setTimeout(() => {
        this.scrolling = false;
        this.updateVisibility();
      }, HIDE_DELAY);
      this.updateVisibility();
    }
  };

  private onPointerEnter = (): void => {
    this.hovering = true;
    this.updateVisibility();
  };

  private onPointerLeave = (): void => {
    this.hovering = false;
    this.updateVisibility();
  };

  // Re-measure every thumb's size + offset from the current scroll metrics.
  private update(): void {
    const vp = this.viewport;
    if (!vp) return;
    for (const bar of this.bars) {
      if (!bar.thumb) continue;
      const track = bar.horizontal ? bar.el.clientWidth : bar.el.clientHeight;
      const view = bar.horizontal ? vp.clientWidth : vp.clientHeight;
      const total = bar.horizontal ? vp.scrollWidth : vp.scrollHeight;
      const maxScroll = total - view;
      if (maxScroll <= 1 || track <= 0) {
        // No overflow on this axis - collapse the thumb; the bar is hidden by updateVisibility.
        bar.thumb.style[bar.horizontal ? 'width' : 'height'] = '0px';
        continue;
      }
      const size = Math.max(MIN_THUMB, (view / total) * track);
      const maxOffset = track - size;
      const offset = (bar.horizontal ? vp.scrollLeft : vp.scrollTop) / maxScroll * maxOffset;
      if (bar.horizontal) {
        bar.thumb.style.width = `${size}px`;
        bar.thumb.style.transform = `translate3d(${offset}px, 0, 0)`;
      } else {
        bar.thumb.style.height = `${size}px`;
        bar.thumb.style.transform = `translate3d(0, ${offset}px, 0)`;
      }
    }
    this.updateVisibility();
  }

  private hasOverflow(bar: Bar): boolean {
    const vp = this.viewport;
    if (!vp) return false;
    return bar.horizontal
      ? vp.scrollWidth - vp.clientWidth > 1
      : vp.scrollHeight - vp.clientHeight > 1;
  }

  private updateVisibility(): void {
    for (const bar of this.bars) {
      let visible = this.hasOverflow(bar);
      if (visible) {
        if (this.type === 'hover') visible = this.hovering || this.scrolling || bar.dragging;
        else if (this.type === 'scroll') visible = this.scrolling || bar.dragging;
        // auto / always: visible whenever there is overflow
      }
      bar.el.setAttribute('data-state', visible ? 'visible' : 'hidden');
    }
  }

  private onThumbDown(e: PointerEvent, bar: Bar): void {
    const vp = this.viewport;
    if (e.button !== 0 || !vp || !bar.thumb) return;
    e.preventDefault();
    e.stopPropagation();
    bar.dragging = true;
    bar.pointerId = e.pointerId;
    bar.pointerStart = bar.horizontal ? e.clientX : e.clientY;
    bar.scrollStart = bar.horizontal ? vp.scrollLeft : vp.scrollTop;
    bar.thumb.setPointerCapture(e.pointerId);
    bar.thumb.addEventListener('pointermove', this.onThumbMove);
    bar.thumb.addEventListener('pointerup', this.onThumbUp);
    bar.thumb.addEventListener('pointercancel', this.onThumbUp);
    (bar.thumb as unknown as { _bar: Bar })._bar = bar;
    this.updateVisibility();
  }

  private onThumbMove = (e: PointerEvent): void => {
    const bar = (e.currentTarget as unknown as { _bar?: Bar })._bar;
    const vp = this.viewport;
    if (!bar?.dragging || !vp || !bar.thumb) return;
    const track = bar.horizontal ? bar.el.clientWidth : bar.el.clientHeight;
    const thumbSize = bar.horizontal ? bar.thumb.offsetWidth : bar.thumb.offsetHeight;
    const maxOffset = track - thumbSize;
    const maxScroll = bar.horizontal
      ? vp.scrollWidth - vp.clientWidth
      : vp.scrollHeight - vp.clientHeight;
    const delta = (bar.horizontal ? e.clientX : e.clientY) - bar.pointerStart;
    const target = bar.scrollStart + (maxOffset > 0 ? (delta / maxOffset) * maxScroll : 0);
    if (bar.horizontal) vp.scrollLeft = target;
    else vp.scrollTop = target;
  };

  private onThumbUp = (e: PointerEvent): void => {
    const bar = (e.currentTarget as unknown as { _bar?: Bar })._bar;
    if (!bar) return;
    bar.dragging = false;
    try {
      bar.thumb?.releasePointerCapture(bar.pointerId);
    } catch {
      // pointer already released
    }
    bar.thumb?.removeEventListener('pointermove', this.onThumbMove);
    bar.thumb?.removeEventListener('pointerup', this.onThumbUp);
    bar.thumb?.removeEventListener('pointercancel', this.onThumbUp);
    this.updateVisibility();
  };

  dispose(): void {
    clearTimeout(this.hideTimer);
    this.viewport?.removeEventListener('scroll', this.onScroll);
    this.root.removeEventListener('pointerenter', this.onPointerEnter);
    this.root.removeEventListener('pointerleave', this.onPointerLeave);
    for (const bar of this.bars) bar.thumb?.removeEventListener('pointerdown', bar.down);
    this.ro.disconnect();
  }
}

/** Wires custom overlay scrollbars onto a scroll-area root. Returns a handle whose dispose() detaches. */
export function createScrollArea(root: HTMLElement): { dispose(): void } {
  if (!(root instanceof HTMLElement)) return { dispose() {} };
  return new ScrollAreaController(root);
}
