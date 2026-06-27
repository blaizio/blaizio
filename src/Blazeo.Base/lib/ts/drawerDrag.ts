// Drag-to-dismiss for the Drawer - the vaul-style gesture. The Drawer otherwise reuses the Dialog
// machinery (presence exit-animation, focus trap, scroll lock, Escape, overlay outside-click); this
// module adds only the pointer drag: the content follows the pointer toward its edge, the overlay
// fades with progress, and on release a past-threshold (or fast-flick) drag asks C# to close while a
// short one springs back. Instance + dispose, callback interop, modelled on slider.ts.
//
// The content keeps a `data-dragging` attribute while a gesture is live so the skin disables its
// enter/exit animation and transition (the JS transform owns the motion). On dismiss the attribute
// stays set through unmount, so presence.ts sees no exit animation and tears down immediately - the
// JS transform has already carried the panel off-screen.

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

type Direction = 'top' | 'bottom' | 'left' | 'right'; // physical, after resolving the logical edge
type Edge = 'top' | 'bottom' | 'start' | 'end'; // logical, as the component authors it

interface Options {
  direction: Edge;
  closeThreshold: number; // fraction of the panel size (0..1) past which release dismisses
  velocityThreshold: number; // px/ms toward the edge that dismisses regardless of distance
  dismissible: boolean; // false pins the panel - drag springs back, never closes
}

// Controls that own the pointer (or that the user clearly meant to press) never start a drag.
const INTERACTIVE = 'button, a, input, textarea, select, [contenteditable], [data-no-drag]';
const EASE = 'transform 0.3s cubic-bezier(0.32, 0.72, 0, 1)';

class DrawerDrag {
  private startX = 0;
  private startY = 0;
  private lastPos = 0;
  private lastTime = 0;
  private velocity = 0; // px/ms toward the dismiss edge
  private size = 0;
  private down = false;
  private dragging = false;
  private settleTimer?: number;
  // The PHYSICAL edge to drag toward. Left/Right arrive logical (inline start/end); under RTL the real
  // edge flips, so resolve against the content's computed direction.
  private readonly dir: Direction;

  constructor(
    private readonly content: HTMLElement,
    private readonly overlay: HTMLElement | null,
    private readonly ref: DotNetObjectReference,
    private readonly options: Options,
  ) {
    this.dir = resolvePhysical(content, options.direction);
    content.addEventListener('pointerdown', this.onPointerDown);
  }

  private get vertical(): boolean {
    return this.dir === 'top' || this.dir === 'bottom';
  }

  // +1 when the dismiss gesture increases the coordinate (bottom: drag down, right: drag right).
  private get sign(): number {
    return this.dir === 'bottom' || this.dir === 'right' ? 1 : -1;
  }

  // Signed displacement along the dismiss axis: >= 0 means dragging toward the closing edge.
  private toward(e: PointerEvent): number {
    const raw = this.vertical ? e.clientY - this.startY : e.clientX - this.startX;
    return raw * this.sign;
  }

  private onPointerDown = (e: PointerEvent): void => {
    if (!this.options.dismissible || e.button !== 0) return;
    const target = e.target as HTMLElement | null;
    if (!target) return;
    const onHandle = !!target.closest('[data-drawer-handle]');
    if (!onHandle && target.closest(INTERACTIVE)) return; // let real controls keep the click

    this.down = true;
    this.startX = e.clientX;
    this.startY = e.clientY;
    this.lastPos = this.vertical ? e.clientY : e.clientX;
    this.lastTime = e.timeStamp;
    this.velocity = 0;
    this.size = (this.vertical ? this.content.offsetHeight : this.content.offsetWidth) || 0;
    window.addEventListener('pointermove', this.onPointerMove, { passive: false });
    window.addEventListener('pointerup', this.onPointerUp);
    window.addEventListener('pointercancel', this.onPointerUp);
  };

  private onPointerMove = (e: PointerEvent): void => {
    if (!this.down) return;
    const along = this.toward(e);

    if (!this.dragging) {
      const perp = this.vertical ? Math.abs(e.clientX - this.startX) : Math.abs(e.clientY - this.startY);
      // Begin only on a deliberate move toward the edge that beats the cross-axis (so a vertical
      // scroll/swipe doesn't hijack a horizontal drawer, etc.).
      if (along < 6 || along <= perp) {
        // A move the other way (into the screen) or a cross-axis swipe: if a scroll container can
        // absorb it, bail out entirely and let it scroll.
        if (this.scrollConsumes(e.target as HTMLElement)) this.stop();
        return;
      }
      if (this.scrollConsumes(e.target as HTMLElement)) { this.stop(); return; }
      this.begin();
    }

    e.preventDefault();
    const pos = this.vertical ? e.clientY : e.clientX;
    const dt = e.timeStamp - this.lastTime;
    if (dt > 0) this.velocity = ((pos - this.lastPos) * this.sign) / dt;
    this.lastPos = pos;
    this.lastTime = e.timeStamp;

    // Rubber-band a pull the wrong way so the panel can't be dragged off its anchored edge.
    const offset = along >= 0 ? along : along * 0.2;
    this.translate(offset);
    this.fade(offset);
  };

  private onPointerUp = (e: PointerEvent): void => {
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);
    if (!this.down) return;
    this.down = false;
    if (!this.dragging) return;

    const along = Math.max(this.toward(e), 0);
    const dismiss = align(this.size) > 0
      ? along > this.size * this.options.closeThreshold || this.velocity > this.options.velocityThreshold
      : this.velocity > this.options.velocityThreshold;

    if (dismiss) this.animateOut();
    else this.springBack();
  };

  private begin(): void {
    this.dragging = true;
    this.content.setAttribute('data-dragging', '');
    this.content.style.transition = 'none';
    if (this.overlay) this.overlay.style.transition = 'none';
  }

  private translate(offset: number): void {
    const px = this.sign * offset;
    this.content.style.transform = this.vertical
      ? `translate3d(0, ${px}px, 0)`
      : `translate3d(${px}px, 0, 0)`;
  }

  private fade(offset: number): void {
    if (!this.overlay) return;
    const progress = align(this.size) > 0 ? Math.min(Math.max(offset / this.size, 0), 1) : 0;
    this.overlay.style.opacity = String(1 - progress);
  }

  // Slide the rest of the way off, then ask C# to close. data-dragging stays set so the skin's exit
  // animation is suppressed (presence.ts finishes at once) - the transform already did the motion.
  private animateOut(): void {
    const px = this.sign * (this.size || window.innerHeight);
    this.content.style.transition = EASE;
    this.content.style.transform = this.vertical ? `translate3d(0, ${px}px, 0)` : `translate3d(${px}px, 0, 0)`;
    if (this.overlay) {
      this.overlay.style.transition = 'opacity 0.3s ease-out';
      this.overlay.style.opacity = '0';
    }
    const done = () => {
      clearTimeout(this.settleTimer);
      this.content.removeEventListener('transitionend', onEnd);
      void this.ref.invokeMethodAsync('OnDragDismiss');
    };
    const onEnd = (ev: TransitionEvent) => { if (ev.target === this.content && ev.propertyName === 'transform') done(); };
    this.content.addEventListener('transitionend', onEnd);
    this.settleTimer = window.setTimeout(done, 360); // fallback for hidden tabs / no transitionend
    this.dragging = false;
  }

  // Glide back to the anchored edge and restore the resting state.
  private springBack(): void {
    this.content.style.transition = EASE;
    this.content.style.transform = 'translate3d(0, 0, 0)';
    if (this.overlay) { this.overlay.style.transition = 'opacity 0.3s ease-out'; this.overlay.style.opacity = ''; }
    const reset = () => {
      clearTimeout(this.settleTimer);
      this.content.removeEventListener('transitionend', onEnd);
      this.content.removeAttribute('data-dragging');
      this.content.style.transition = '';
      this.content.style.transform = '';
      if (this.overlay) this.overlay.style.transition = '';
    };
    const onEnd = (ev: TransitionEvent) => { if (ev.target === this.content && ev.propertyName === 'transform') reset(); };
    this.content.addEventListener('transitionend', onEnd);
    this.settleTimer = window.setTimeout(reset, 360);
    this.dragging = false;
  }

  // True when a scroll container under the pointer can still scroll in the direction the gesture
  // would otherwise move it - then the gesture should scroll, not drag the drawer.
  private scrollConsumes(from: EventTarget | null): boolean {
    let node = from instanceof HTMLElement ? from : null;
    while (node && node !== this.content) {
      const style = getComputedStyle(node);
      if (this.vertical) {
        const scrollable = /(auto|scroll)/.test(style.overflowY) && node.scrollHeight > node.clientHeight;
        if (scrollable) {
          // bottom drawer dismisses on drag-down, which would scroll up: blocked unless at the top.
          if (this.dir === 'bottom' && node.scrollTop > 0) return true;
          if (this.dir === 'top' && node.scrollTop < node.scrollHeight - node.clientHeight) return true;
        }
      } else {
        const scrollable = /(auto|scroll)/.test(style.overflowX) && node.scrollWidth > node.clientWidth;
        if (scrollable) {
          if (this.dir === 'right' && Math.abs(node.scrollLeft) > 0) return true;
          if (this.dir === 'left' && Math.abs(node.scrollLeft) < node.scrollWidth - node.clientWidth) return true;
        }
      }
      node = node.parentElement;
    }
    return false;
  }

  // Abandon a gesture that turned out to be a scroll/tap.
  private stop(): void {
    this.down = false;
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);
  }

  dispose(): void {
    clearTimeout(this.settleTimer);
    this.content.removeEventListener('pointerdown', this.onPointerDown);
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);
  }
}

// Tiny guard so a zero/NaN size (headless layout, display:none) never divides.
function align(n: number): number {
  return Number.isFinite(n) && n > 0 ? n : 0;
}

// Map a logical horizontal edge (start/end) to the physical edge it sits on, flipping under RTL.
// Vertical edges are already physical.
function resolvePhysical(content: HTMLElement, edge: Edge): Direction {
  if (edge === 'top' || edge === 'bottom') return edge;
  const rtl = getComputedStyle(content).direction === 'rtl';
  if (edge === 'start') return rtl ? 'right' : 'left';
  return rtl ? 'left' : 'right'; // end
}

/**
 * Wires drag-to-dismiss onto a drawer. `contentId` is the content element's DOM id (the Dialog
 * ContentId); the overlay is found by the `data-drawer-overlay` tag carrying the same id.
 */
export function createDrawerDrag(
  contentId: string,
  ref: DotNetObjectReference,
  options: Options,
): DrawerDrag | null {
  const content = document.getElementById(contentId);
  if (!content) return null;
  const overlay = document.querySelector<HTMLElement>(`[data-drawer-overlay="${CSS.escape(contentId)}"]`);
  return new DrawerDrag(content, overlay, ref, options);
}
