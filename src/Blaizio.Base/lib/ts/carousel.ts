// Headless carousel engine. A carousel "viewport" is a flex + scroll-snap scroll container whose direct
// children (data-slot=carousel-item) are the slides. The engine never sizes or styles anything - it
// measures the scroll position, derives the selected slide + whether either edge is reached, drives
// programmatic scrolling (prev/next/to), adds mouse drag-to-scroll (touch/trackpad scroll natively),
// and optional autoplay. All state is pushed to C# via OnScrollState(index, canPrev, canNext, count)
// and OnPlayingChanged(playing).
//
//   scrollPrev() / scrollNext() / scrollTo(i)              programmatic moves (smooth, snap-aligned)
//   play() / pause()                                       rotation control (autoplay carousels)
//   OnScrollState(index, canPrev, canNext, count)          reported on init / scroll / resize
//   OnPlayingChanged(playing)                              reported on init and every rotation change
//
// Rotation contract (WCAG 2.2.2 / APG carousel): prefers-reduced-motion blocks auto-start; keyboard
// focus entering the carousel or a drag stops rotation PERMANENTLY (only an explicit play() call
// restarts it); hover merely suspends the timer while the pointer is over the viewport.

import { invokeDotNet } from './interop';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

interface CarouselOptions {
  horizontal: boolean;
  loop: boolean;
  autoplayMs: number;
}

class Carousel {
  private readonly resizeObs: ResizeObserver;
  private reportTimer = 0;
  private autoplayTimer = 0;
  private playing = false;

  // mouse drag-to-scroll state
  private dragging = false;
  private moved = false;
  private startX = 0;
  private startY = 0;
  private startScrollLeft = 0;
  private startScrollTop = 0;

  constructor(
    private readonly vp: HTMLElement,
    private readonly ref: DotNetObjectReference,
    private readonly opts: CarouselOptions,
  ) {
    vp.addEventListener('scroll', this.scheduleReport, { passive: true });
    vp.addEventListener('pointerdown', this.onPointerDown);
    vp.addEventListener('click', this.onClickCapture, true);
    this.resizeObs = new ResizeObserver(this.scheduleReport);
    this.resizeObs.observe(vp);
    this.report(); // initial state, synchronously (don't wait on a frame that a background tab won't paint)
    this.initAutoplay();
  }

  private get rtl(): boolean {
    return this.opts.horizontal && getComputedStyle(this.vp).direction === 'rtl';
  }

  private items(): HTMLElement[] {
    return Array.from(this.vp.children).filter(
      (c): c is HTMLElement => c instanceof HTMLElement && c.getAttribute('data-slot') === 'carousel-item',
    );
  }

  // Signed distance of a slide's logical-start edge from the viewport's logical-start edge. Using rects
  // (not scrollLeft) keeps the math identical in LTR, RTL and vertical.
  private startDelta(el: HTMLElement): number {
    const vr = this.vp.getBoundingClientRect();
    const ir = el.getBoundingClientRect();
    if (!this.opts.horizontal) return ir.top - vr.top;
    return this.rtl ? ir.right - vr.right : ir.left - vr.left;
  }

  private selected(): number {
    let best = 0;
    let bestD = Infinity;
    this.items().forEach((el, i) => {
      const d = Math.abs(this.startDelta(el));
      if (d < bestD - 0.5) {
        bestD = d;
        best = i;
      }
    });
    return best;
  }

  private maxScroll(): number {
    return this.opts.horizontal
      ? this.vp.scrollWidth - this.vp.clientWidth
      : this.vp.scrollHeight - this.vp.clientHeight;
  }

  private pos(): number {
    return Math.abs(this.opts.horizontal ? this.vp.scrollLeft : this.vp.scrollTop);
  }

  private report = (): void => {
    const count = this.items().length;
    const max = this.maxScroll();
    const p = this.pos();
    const atStart = p <= 1;
    const atEnd = p >= max - 1;
    const canPrev = this.opts.loop ? count > 1 : !atStart;
    const canNext = this.opts.loop ? count > 1 : !atEnd;
    void invokeDotNet(this.ref, 'OnScrollState', this.selected(), canPrev, canNext, count);
  };

  // Coalesce bursts of scroll/resize events into one report. setTimeout (not requestAnimationFrame) so a
  // backgrounded tab - where rAF is paused - still reports.
  private scheduleReport = (): void => {
    if (this.reportTimer) return;
    this.reportTimer = window.setTimeout(() => {
      this.reportTimer = 0;
      this.report();
    }, 60);
  };

  scrollTo(index: number): void {
    const items = this.items();
    if (!items.length) return;
    const i = Math.max(0, Math.min(index, items.length - 1));
    const delta = this.startDelta(items[i]);
    this.vp.scrollBy({
      left: this.opts.horizontal ? delta : 0,
      top: this.opts.horizontal ? 0 : delta,
      behavior: 'smooth',
    });
  }

  scrollNext(): void {
    const items = this.items();
    if (this.selected() >= items.length - 1 || this.pos() >= this.maxScroll() - 1) {
      if (this.opts.loop) this.scrollTo(0);
      return;
    }
    this.scrollTo(this.selected() + 1);
  }

  scrollPrev(): void {
    if (this.selected() <= 0 || this.pos() <= 1) {
      if (this.opts.loop) this.scrollTo(this.items().length - 1);
      return;
    }
    this.scrollTo(this.selected() - 1);
  }

  // ---- mouse drag-to-scroll ----
  private onPointerDown = (e: PointerEvent): void => {
    if (e.pointerType !== 'mouse' || e.button !== 0) return;
    this.dragging = true;
    this.moved = false;
    this.startX = e.clientX;
    this.startY = e.clientY;
    this.startScrollLeft = this.vp.scrollLeft;
    this.startScrollTop = this.vp.scrollTop;
    this.vp.style.scrollSnapType = 'none'; // let the drag move freely; snap restored on release
    // The viewport carries scroll-behavior:smooth (for the prev/next/dot moves). Left on, every direct
    // scrollLeft assignment below would be ANIMATED, so the drag lags a frame behind the pointer and
    // stutters. Force instant scrolling for the duration of the drag; restored on release.
    this.vp.style.scrollBehavior = 'auto';
    try { this.vp.setPointerCapture(e.pointerId); } catch { /* no capture */ }
    this.vp.addEventListener('pointermove', this.onPointerMove);
    this.vp.addEventListener('pointerup', this.onPointerUp);
    // Taking hold of the viewport is the user claiming control: stop rotation for good.
    this.pause();
  };

  private onPointerMove = (e: PointerEvent): void => {
    if (!this.dragging) return;
    const dx = e.clientX - this.startX;
    const dy = e.clientY - this.startY;
    if (Math.abs(dx) > 3 || Math.abs(dy) > 3) this.moved = true;
    if (this.opts.horizontal) this.vp.scrollLeft = this.startScrollLeft - dx;
    else this.vp.scrollTop = this.startScrollTop - dy;
  };

  private onPointerUp = (e: PointerEvent): void => {
    if (!this.dragging) return;
    this.dragging = false;
    try { this.vp.releasePointerCapture(e.pointerId); } catch { /* already released */ }
    this.vp.removeEventListener('pointermove', this.onPointerMove);
    this.vp.removeEventListener('pointerup', this.onPointerUp);
    this.vp.style.scrollSnapType = ''; // restore snap, then settle onto the nearest slide
    this.vp.style.scrollBehavior = ''; // hand smooth scrolling back to the CSS (scrollTo asks for smooth explicitly)
    this.scrollTo(this.selected());
    // No re-arm: a drag stopped rotation permanently; only an explicit play() restarts it.
  };

  // Swallow the click that ends a drag so a slide's link/button isn't triggered by the release.
  private onClickCapture = (e: MouseEvent): void => {
    if (this.moved) {
      e.preventDefault();
      e.stopPropagation();
      this.moved = false;
    }
  };

  // ---- rotation control ----
  // `playing` is the user-intended rotation state; the timer additionally suspends while the pointer
  // hovers the viewport (transient - rotation resumes on leave while still playing). Focus or a drag
  // pauses for real, and nothing implicit ever calls play() again.

  private arm(): void {
    if (this.autoplayTimer) return;
    this.autoplayTimer = window.setInterval(() => {
      if (this.pos() >= this.maxScroll() - 1) this.scrollTo(0);
      else this.scrollNext();
    }, this.opts.autoplayMs);
  }

  private disarm(): void {
    if (this.autoplayTimer) {
      clearInterval(this.autoplayTimer);
      this.autoplayTimer = 0;
    }
  }

  /** Start (or restart) rotation. An explicit user request, so it overrides reduced-motion's auto-start block. */
  play(): void {
    if (this.opts.autoplayMs <= 0 || this.playing) return;
    this.playing = true;
    this.arm();
    this.reportPlaying();
  }

  /** Stop rotation until play() is called. */
  pause(): void {
    this.disarm();
    if (!this.playing) return;
    this.playing = false;
    this.reportPlaying();
  }

  private suspend = (): void => this.disarm();

  private resume = (): void => {
    if (this.playing) this.arm();
  };

  private hardPause = (): void => this.pause();

  private reportPlaying(): void {
    void invokeDotNet(this.ref, 'OnPlayingChanged', this.playing);
  }

  private initAutoplay(): void {
    if (this.opts.autoplayMs <= 0) return;
    this.vp.addEventListener('pointerenter', this.suspend);
    this.vp.addEventListener('pointerleave', this.resume);
    this.vp.addEventListener('focusin', this.hardPause);
    // Auto-start only when the user hasn't asked the platform for less motion; an explicit play()
    // still works either way.
    if (!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) this.play();
    else this.reportPlaying();
  }

  dispose(): void {
    this.disarm();
    if (this.reportTimer) clearTimeout(this.reportTimer);
    this.resizeObs.disconnect();
    this.vp.removeEventListener('scroll', this.scheduleReport);
    this.vp.removeEventListener('pointerdown', this.onPointerDown);
    this.vp.removeEventListener('click', this.onClickCapture, true);
    this.vp.removeEventListener('pointermove', this.onPointerMove);
    this.vp.removeEventListener('pointerup', this.onPointerUp);
    this.vp.removeEventListener('pointerenter', this.suspend);
    this.vp.removeEventListener('pointerleave', this.resume);
    this.vp.removeEventListener('focusin', this.hardPause);
  }
}

/** Wires the scroll-snap engine onto a carousel viewport; returns a handle with dispose(). */
export function createCarousel(
  viewport: HTMLElement,
  ref: DotNetObjectReference,
  options: CarouselOptions,
): Carousel {
  return new Carousel(viewport, ref, options);
}
