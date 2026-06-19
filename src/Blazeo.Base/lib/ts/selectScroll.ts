/**
 * Scroll buttons for a select listbox - the alternative to a scrollbar. An up chevron above a scrolling
 * viewport and a down chevron below it: each is HIDDEN when the viewport can't scroll further that way
 * (so the up button never shows at the top, the down button never at the bottom), and while the pointer
 * RESTS on a button the viewport auto-scrolls that way (an rAF loop) until it reaches the edge - at which
 * point the button hides and the scroll stops. Pure DOM; the buttons' visibility is the only thing this
 * owns - toggled via inline display (not the `hidden` attribute, which the buttons' `flex` class would
 * override). Blazor renders them with `display:none` and never re-renders them, so there is no fight.
 */

const SPEED = 5; // px per frame while a button is hovered

class SelectScroll {
  private raf = 0;
  private dir = 0;
  private readonly resize: ResizeObserver;

  constructor(
    private readonly viewport: HTMLElement,
    private readonly up: HTMLElement,
    private readonly down: HTMLElement,
  ) {
    this.resize = new ResizeObserver(this.update);
    this.resize.observe(this.viewport);
    this.viewport.addEventListener('scroll', this.update, { passive: true });
    this.up.addEventListener('pointerenter', this.onUpEnter);
    this.up.addEventListener('pointerleave', this.stop);
    this.down.addEventListener('pointerenter', this.onDownEnter);
    this.down.addEventListener('pointerleave', this.stop);
    this.update();
  }

  private get atTop(): boolean {
    return this.viewport.scrollTop <= 0;
  }

  private get atBottom(): boolean {
    return Math.ceil(this.viewport.scrollTop + this.viewport.clientHeight) >= this.viewport.scrollHeight;
  }

  /** Show each button only when there is room to scroll that way, and stop auto-scrolling at an edge. */
  private update = (): void => {
    // '' reverts to the button's `flex` class (visible); 'none' hides it - inline beats the class.
    this.up.style.display = this.atTop ? 'none' : '';
    this.down.style.display = this.atBottom ? 'none' : '';
    if ((this.dir < 0 && this.atTop) || (this.dir > 0 && this.atBottom)) this.stop();
  };

  private onUpEnter = (): void => this.start(-1);
  private onDownEnter = (): void => this.start(1);

  private start(dir: number): void {
    this.dir = dir;
    cancelAnimationFrame(this.raf);
    const step = (): void => {
      this.viewport.scrollTop += this.dir * SPEED;
      this.update();
      if (this.dir !== 0) this.raf = requestAnimationFrame(step);
    };
    this.raf = requestAnimationFrame(step);
  }

  private stop = (): void => {
    this.dir = 0;
    cancelAnimationFrame(this.raf);
  };

  dispose(): void {
    this.stop();
    this.resize.disconnect();
    this.viewport.removeEventListener('scroll', this.update);
    this.up.removeEventListener('pointerenter', this.onUpEnter);
    this.up.removeEventListener('pointerleave', this.stop);
    this.down.removeEventListener('pointerenter', this.onDownEnter);
    this.down.removeEventListener('pointerleave', this.stop);
  }
}

const noop = { dispose() {} };

/**
 * Wires the scroll buttons of a select listbox. Returns a handle whose <code>dispose()</code> detaches
 * everything; a no-op handle if any element is missing.
 */
export function createSelectScroll(
  viewport: HTMLElement,
  up: HTMLElement,
  down: HTMLElement,
): { dispose(): void } {
  if (!(viewport instanceof HTMLElement) || !(up instanceof HTMLElement) || !(down instanceof HTMLElement)) {
    return noop;
  }
  return new SelectScroll(viewport, up, down);
}
