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

import { invokeDotNet } from './core';
import type { DotNetObjectReference } from './core';

interface CarouselOptions {
  horizontal: boolean;
  loop: boolean;
  autoplayMs: number;
}

// Where an arrow key already has a job of its own: the caret of a text field, a slider's value, the
// items of a menu, tab list, radio group, list box, tree or grid. A carousel leaves the key alone there.
const OWN_ARROW_KEYS =
  'input:not([type="checkbox"]):not([type="button"]):not([type="submit"]):not([type="reset"]), textarea, select, ' +
  '[contenteditable]:not([contenteditable="false"]), [role="textbox"], [role="combobox"], [role="spinbutton"], ' +
  '[role="slider"], [role="menu"], [role="menubar"], [role="tablist"], [role="radiogroup"], [role="listbox"], ' +
  '[role="tree"], [role="grid"], [role="toolbar"]';

class Carousel {
  private readonly resizeObs: ResizeObserver;
  private reportTimer = 0;
  private settleTimer = 0;
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
    // The carousel root (the region around the viewport and its controls); the viewport itself
    // when the caller has none to give.
    private readonly root: HTMLElement = vp,
  ) {
    root.addEventListener('keydown', this.onKeyDown);
    vp.addEventListener('scroll', this.scheduleReport, { passive: true });
    vp.addEventListener('scroll', this.scheduleSettle, { passive: true });
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

  private report = (settled = false): void => {
    const count = this.items().length;
    const max = this.maxScroll();
    const p = this.pos();
    const atStart = p <= 1;
    const atEnd = p >= max - 1;
    const canPrev = this.opts.loop ? count > 1 : !atStart;
    const canNext = this.opts.loop ? count > 1 : !atEnd;
    void invokeDotNet(this.ref, 'OnScrollState', this.selected(), canPrev, canNext, count, settled);
  };

  // "The viewport has come to rest": one report, flagged settled, once scroll events stop. C#
  // swallows the positions passed on the way to an index it asked for, and needs this to learn
  // that a scroll ended somewhere else (a drag, wheel or resize interrupted it) - otherwise it
  // would wait for an arrival that never comes and ignore every later scroll. Held back while
  // the mouse drags, since a still pointer is not the end of the gesture.
  private scheduleSettle = (): void => {
    clearTimeout(this.settleTimer);
    this.settleTimer = window.setTimeout(() => {
      this.settleTimer = 0;
      if (!this.dragging) this.report(true);
    }, 150);
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
    // A new scroll supersedes a rest that was about to be reported from the previous one.
    clearTimeout(this.settleTimer);
    this.settleTimer = 0;
    this.vp.scrollBy({
      left: this.opts.horizontal ? delta : 0,
      top: this.opts.horizontal ? 0 : delta,
      behavior: 'smooth',
    });
    // A target the viewport cannot move to (already at the end) fires no scroll event, so nothing
    // would ever report the rest and C# would keep waiting for an arrival. A real scroll re-arms
    // this on every event.
    this.scheduleSettle();
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

  // ---- arrow keys ----
  // Handled here and not by a Blazor handler on the root: that one cannot see where the key was
  // pressed, so an arrow typed in a slide's text field moved the caret and the slide together.
  private onKeyDown = (e: KeyboardEvent): void => {
    if (e.defaultPrevented || e.altKey || e.ctrlKey || e.metaKey) return;
    const prev = this.opts.horizontal ? 'ArrowLeft' : 'ArrowUp';
    const next = this.opts.horizontal ? 'ArrowRight' : 'ArrowDown';
    if (e.key !== prev && e.key !== next) return;
    const owner = e.target instanceof Element ? e.target.closest(OWN_ARROW_KEYS) : null;
    if (owner && owner !== this.root && this.root.contains(owner)) return;
    // Taken: the page (or the viewport) does not scroll along with the slide.
    e.preventDefault();
    if (e.key === prev) this.scrollPrev();
    else this.scrollNext();
  };

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
    // No pointer capture yet. A captured pointer sends its click to the viewport, so a press
    // that never becomes a drag would not reach the button, link or checkbox inside the slide.
    // The capture is taken once the pointer has really moved (onPointerMove). Until then the
    // listeners sit on the window, which also sees a release outside the viewport.
    window.addEventListener('pointermove', this.onPointerMove);
    window.addEventListener('pointerup', this.onPointerUp);
    window.addEventListener('pointercancel', this.onPointerUp);
    // Taking hold of the viewport is the user claiming control: stop rotation for good.
    this.pause();
  };

  private onPointerMove = (e: PointerEvent): void => {
    if (!this.dragging) return;
    // The button came up where no pointerup could reach us (outside the browser window, before
    // the capture): end the drag here instead of scrolling under a pointer that is only hovering.
    if (e.buttons === 0) { this.onPointerUp(e); return; }
    const dx = e.clientX - this.startX;
    const dy = e.clientY - this.startY;
    if (!this.moved && (Math.abs(dx) > 3 || Math.abs(dy) > 3)) {
      this.moved = true;
      // Now it is a drag: keep receiving the pointer when it leaves the viewport or the window.
      try { this.vp.setPointerCapture(e.pointerId); } catch { /* no capture */ }
    }
    if (this.opts.horizontal) this.vp.scrollLeft = this.startScrollLeft - dx;
    else this.vp.scrollTop = this.startScrollTop - dy;
  };

  private onPointerUp = (e: PointerEvent): void => {
    if (!this.dragging) return;
    this.dragging = false;
    try { this.vp.releasePointerCapture(e.pointerId); } catch { /* never captured, or already released */ }
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);
    // A cancelled drag is followed by no click, so the swallow would eat the next real one.
    if (e.type === 'pointercancel') this.moved = false;
    this.vp.style.scrollSnapType = ''; // restore snap, then settle onto the nearest slide
    this.vp.style.scrollBehavior = ''; // hand smooth scrolling back to the CSS (scrollTo asks for smooth explicitly)
    this.scrollTo(this.selected());
    this.scheduleSettle(); // a release that needs no snap scroll fires no scroll event
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
    clearTimeout(this.settleTimer);
    this.resizeObs.disconnect();
    this.root.removeEventListener('keydown', this.onKeyDown);
    this.vp.removeEventListener('scroll', this.scheduleReport);
    this.vp.removeEventListener('scroll', this.scheduleSettle);
    this.vp.removeEventListener('pointerdown', this.onPointerDown);
    this.vp.removeEventListener('click', this.onClickCapture, true);
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);
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
  root?: HTMLElement | null,
): Carousel {
  return new Carousel(viewport, ref, options, root instanceof HTMLElement ? root : viewport);
}
