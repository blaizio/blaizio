// Drag-to-dismiss + snap points for the Drawer - the vaul-style gesture. The Drawer otherwise reuses
// the Dialog machinery (presence exit-animation, focus trap, scroll lock, Escape, overlay
// outside-click); this module adds the pointer drag: the content follows the pointer toward its edge,
// the overlay fades with progress, and on release a past-threshold (or fast-flick) drag asks C# to
// close while a short one springs back.
//
// SNAP POINTS: when `snapPoints` is non-empty the panel rests at one of several positions (each a
// fraction 0..1 of the panel's size that is VISIBLE at that snap; 1 = fully open). JS then owns the
// resting transform: it opens to the active snap, drag moves between snaps (snapping to the nearest on
// release, with a fast flick skipping a step), and a drag below the lowest snap dismisses. The active
// snap index is reported to C# via OnSnapChanged for two-way binding.
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
  snapPoints: number[]; // visible-fractions (0..1), ascending; empty = no snapping
  activeSnap: number; // index of the starting/active snap point
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
  // Snapping state: the sorted visible-fractions and the current resting index.
  private readonly snaps: number[];
  private snapIndex: number;
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
    this.snaps = (options.snapPoints ?? []).filter((n) => n > 0 && n <= 1).sort((a, b) => a - b);
    this.snapIndex = clampIndex(options.activeSnap, this.snaps.length);
    content.addEventListener('pointerdown', this.onPointerDown);
  }

  private get snapping(): boolean {
    return this.snaps.length > 0;
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

  // Hidden distance (px toward the edge) for a visible-fraction: fraction 1 -> 0 (fully open).
  private offsetFor(fraction: number): number {
    return Math.max(0, (1 - fraction) * this.size);
  }

  // The resting offset for the current snap (0 when not snapping = fully open).
  private get baseOffset(): number {
    return this.snapping ? this.offsetFor(this.snaps[this.snapIndex]) : 0;
  }

  private measure(): void {
    this.size = (this.vertical ? this.content.offsetHeight : this.content.offsetWidth) || 0;
  }

  // Opens to the active snap (snapping only): slide from fully-hidden to the resting offset. Without
  // snap points the skin's CSS enter animation owns the open, so this is a no-op.
  //
  // Waits (via rAF) for the panel to have a real laid-out size before measuring: a just-mounted panel -
  // or one whose max-h-[Nvh] resolves against a not-yet-sized viewport - can briefly report ~0, which
  // would put the "65% snap" at ~0px (i.e. fully open).
  open(): void {
    if (!this.snapping) return;
    let tries = 0;
    const attempt = () => {
      this.measure();
      if (this.size <= 1 && tries++ < 20) { requestAnimationFrame(attempt); return; }
      this.content.style.transition = 'none';
      // The initial-hidden offset comes from a Tailwind translate-y-full class, which sets the CSS
      // `translate` property; our transform would otherwise STACK on top of it. Clear it so transform
      // alone drives the position from here on.
      this.content.style.translate = 'none';
      this.translate(this.size); // start fully hidden
      // Force a reflow so the browser paints the hidden state before we animate to the snap.
      void this.content.offsetHeight;
      this.content.style.transition = EASE;
      this.translate(this.baseOffset);
      this.clearTransitionAfterSettle();
    };
    requestAnimationFrame(attempt);
  }

  private onPointerDown = (e: PointerEvent): void => {
    if (!this.options.dismissible && !this.snapping) return;
    if (e.button !== 0) return;
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
    this.measure();
    window.addEventListener('pointermove', this.onPointerMove, { passive: false });
    window.addEventListener('pointerup', this.onPointerUp);
    window.addEventListener('pointercancel', this.onPointerUp);
  };

  private onPointerMove = (e: PointerEvent): void => {
    if (!this.down) return;
    const along = this.toward(e);

    if (!this.dragging) {
      const perp = this.vertical ? Math.abs(e.clientX - this.startX) : Math.abs(e.clientY - this.startY);
      // Begin only on a deliberate move that beats the cross-axis. With snap points either direction
      // can start a drag (you can pull the panel further open too); otherwise only a move toward the
      // edge counts (so a scroll/swipe the other way doesn't hijack it).
      const intent = this.snapping ? Math.abs(along) : along;
      if (intent < 6 || Math.abs(along) <= perp) {
        if (this.scrollConsumes(e.target as HTMLElement, along)) this.stop();
        return;
      }
      if (this.scrollConsumes(e.target as HTMLElement, along)) { this.stop(); return; }
      this.begin();
    }

    e.preventDefault();
    const pos = this.vertical ? e.clientY : e.clientX;
    const dt = e.timeStamp - this.lastTime;
    if (dt > 0) this.velocity = ((pos - this.lastPos) * this.sign) / dt;
    this.lastPos = pos;
    this.lastTime = e.timeStamp;

    // Offset from the resting base. Pulling further open than the top snap (offset < 0) rubber-bands;
    // without snapping the same rubber-band stops the panel leaving its anchored edge.
    let offset = this.baseOffset + along;
    if (offset < 0) offset *= 0.2;
    offset = Math.min(offset, this.size);
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

    if (this.snapping) { this.releaseToSnap(e); return; }

    const along = Math.max(this.toward(e), 0);
    const dismiss = align(this.size) > 0
      ? along > this.size * this.options.closeThreshold || this.velocity > this.options.velocityThreshold
      : this.velocity > this.options.velocityThreshold;

    if (dismiss) this.animateOut();
    else this.springBack();
  };

  // Resolve a snap-mode release: pick the destination snap (a fast flick skips one step in the flick
  // direction), or dismiss when leaving below the lowest snap.
  private releaseToSnap(e: PointerEvent): void {
    const offset = Math.min(Math.max(this.baseOffset + this.toward(e), 0), this.size);
    const flick = Math.abs(this.velocity) > this.options.velocityThreshold;
    let target: number;

    if (flick) {
      // Velocity sign: +toward edge (smaller snap / dismiss), -toward open (larger snap).
      target = this.velocity > 0 ? this.snapIndex - 1 : this.snapIndex + 1;
    } else {
      target = this.nearestSnap(offset);
    }

    if (target < 0) {
      // Below the lowest snap: dismiss if allowed, else fall back to the lowest snap.
      if (this.options.dismissible) { this.animateOut(); return; }
      target = 0;
    }
    target = Math.min(target, this.snaps.length - 1);
    this.snapTo(target);
  }

  private nearestSnap(offset: number): number {
    // A drag clearly below the lowest snap (more than halfway from it to fully closed) means dismiss.
    const lowest = this.offsetFor(this.snaps[0]);
    if (offset > lowest + (this.size - lowest) / 2) return -1;
    let best = 0;
    let bestDist = Infinity;
    for (let i = 0; i < this.snaps.length; i++) {
      const d = Math.abs(offset - this.offsetFor(this.snaps[i]));
      if (d < bestDist) { bestDist = d; best = i; }
    }
    return best;
  }

  // Glide to a snap index, report it, and restore the resting state.
  private snapTo(index: number): void {
    this.snapIndex = index;
    this.content.style.transition = EASE;
    this.translate(this.baseOffset);
    if (this.overlay) { this.overlay.style.transition = 'opacity 0.3s ease-out'; this.overlay.style.opacity = ''; }
    const settle = () => {
      this.content.removeAttribute('data-dragging');
      if (this.overlay) this.overlay.style.transition = '';
    };
    this.afterTransition(settle);
    this.dragging = false;
    void this.ref.invokeMethodAsync('OnSnapChanged', index);
  }

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
    // With snap points the overlay only fades once you drag below the lowest snap (the dismiss zone);
    // otherwise it fades proportionally to the whole panel.
    let progress = 0;
    if (align(this.size) > 0) {
      if (this.snapping) {
        const lowest = this.offsetFor(this.snaps[0]);
        progress = offset > lowest ? (offset - lowest) / Math.max(1, this.size - lowest) : 0;
      } else {
        progress = offset / this.size;
      }
    }
    this.overlay.style.opacity = String(1 - Math.min(Math.max(progress, 0), 1));
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
    this.afterTransition(() => void this.ref.invokeMethodAsync('OnDragDismiss'));
    this.dragging = false;
  }

  // Glide back to the anchored edge and restore the resting state.
  private springBack(): void {
    this.content.style.transition = EASE;
    this.content.style.transform = 'translate3d(0, 0, 0)';
    if (this.overlay) { this.overlay.style.transition = 'opacity 0.3s ease-out'; this.overlay.style.opacity = ''; }
    this.afterTransition(() => {
      this.content.removeAttribute('data-dragging');
      this.content.style.transition = '';
      this.content.style.transform = '';
      if (this.overlay) this.overlay.style.transition = '';
    });
    this.dragging = false;
  }

  // Clears the inline transition once the snap-open glide has settled, so later layout isn't pinned.
  private clearTransitionAfterSettle(): void {
    this.afterTransition(() => { this.content.style.transition = ''; });
  }

  // Run `fn` on the content's next transform transitionend, with a timer fallback for hidden tabs.
  private afterTransition(fn: () => void): void {
    clearTimeout(this.settleTimer);
    const done = () => {
      clearTimeout(this.settleTimer);
      this.content.removeEventListener('transitionend', onEnd);
      fn();
    };
    const onEnd = (ev: TransitionEvent) => { if (ev.target === this.content && ev.propertyName === 'transform') done(); };
    this.content.addEventListener('transitionend', onEnd);
    this.settleTimer = window.setTimeout(done, 360);
  }

  // True when a scroll container under the pointer can still scroll in the direction the gesture would
  // otherwise move it - then the gesture should scroll, not drag the drawer. `along` is the signed
  // displacement toward the dismiss edge (negative = pulling further open).
  private scrollConsumes(from: EventTarget | null, along: number): boolean {
    let node = from instanceof HTMLElement ? from : null;
    const towardEdge = along >= 0;
    while (node && node !== this.content) {
      const style = getComputedStyle(node);
      if (this.vertical) {
        const scrollable = /(auto|scroll)/.test(style.overflowY) && node.scrollHeight > node.clientHeight;
        if (scrollable) {
          if (this.dir === 'bottom') {
            if (towardEdge && node.scrollTop > 0) return true; // drag-down would scroll up first
            if (!towardEdge && node.scrollTop < node.scrollHeight - node.clientHeight) return true;
          }
          if (this.dir === 'top') {
            if (towardEdge && node.scrollTop < node.scrollHeight - node.clientHeight) return true;
            if (!towardEdge && node.scrollTop > 0) return true;
          }
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

function clampIndex(i: number, len: number): number {
  if (len === 0) return 0;
  if (!Number.isFinite(i)) return len - 1;
  return Math.min(Math.max(Math.trunc(i), 0), len - 1);
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
 * Wires drag-to-dismiss (and snap points) onto a drawer. `contentId` is the content element's DOM id
 * (the Dialog ContentId); the overlay is found by the `data-drawer-overlay` tag carrying the same id.
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
