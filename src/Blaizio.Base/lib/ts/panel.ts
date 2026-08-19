// Pointer/keyboard resize for a push panel's edge handle. Unlike the resizable panel group (where
// C# owns the two-neighbour math and every pointer move round-trips), a panel resize is a single
// clamp - so the gesture is applied entirely here (the --panel-size custom property on the panel
// root), and C# only hears the settled value:
//
//   OnResizeEnd(px)   drag released, or a key resize landed - the committed size in px
//
// The panel's min/max arrive as CSS lengths (any unit); they are resolved to px once, by probing.
// data-dragging is set on the root for the duration of a drag so the CSS transition that animates
// open/close (and keyboard nudges) gets out of the pointer's way.

import { invokeDotNet } from './core';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

type PanelDock = 'start' | 'end' | 'top' | 'bottom';

/** How far (px) one arrow-key press resizes by. */
const KEY_STEP = 16;

class PanelResize {
  private readonly horizontal: boolean;
  private minPx = 0;
  private maxPx = Number.MAX_SAFE_INTEGER;
  private dragging = false;
  private startPos = 0;
  private startSize = 0;
  private current = 0;
  private dragSign = 1;

  constructor(
    private readonly root: HTMLElement,
    private readonly handle: HTMLElement,
    private readonly ref: DotNetObjectReference,
    private readonly side: PanelDock,
    minSize: string,
    maxSize: string,
  ) {
    this.horizontal = side === 'start' || side === 'end';
    this.minPx = this.probe(minSize);
    this.maxPx = Math.max(this.probe(maxSize), this.minPx);
    handle.addEventListener('pointerdown', this.onDown);
    handle.addEventListener('keydown', this.onKey);
    this.current = this.measure();
    this.updateAria(this.current);
  }

  /** Resolve a CSS length to px by measuring a hidden probe inside the root. */
  private probe(length: string): number {
    const el = document.createElement('div');
    el.style.position = 'absolute';
    el.style.visibility = 'hidden';
    if (this.horizontal) el.style.width = length;
    else el.style.height = length;
    this.root.appendChild(el);
    const rect = el.getBoundingClientRect();
    const px = this.horizontal ? rect.width : rect.height;
    el.remove();
    return px;
  }

  /** The panel's current size: the fixed-size inner surface (it always equals --panel-size). */
  private measure(): number {
    const inner = (this.root.querySelector('[data-slot=panel-inner]') as HTMLElement | null) ?? this.root;
    const rect = inner.getBoundingClientRect();
    return this.horizontal ? rect.width : rect.height;
  }

  // +1 when pointer movement along the +axis grows the panel. The handle sits on the content-facing
  // edge: a start-docked panel grows as the pointer moves right (+x, LTR), an end-docked one as it
  // moves left; RTL mirrors the inline pair. Top grows downward (+y), bottom upward.
  private sign(): number {
    if (!this.horizontal) return this.side === 'top' ? 1 : -1;
    const base = this.side === 'start' ? 1 : -1;
    return getComputedStyle(this.root).direction === 'rtl' ? -base : base;
  }

  private apply(px: number): number {
    const clamped = Math.min(Math.max(px, this.minPx), this.maxPx);
    this.root.style.setProperty('--panel-size', `${clamped}px`);
    this.current = clamped;
    this.updateAria(clamped);
    return clamped;
  }

  private updateAria(px: number) {
    const range = this.maxPx - this.minPx;
    const pct = range > 0 ? ((px - this.minPx) / range) * 100 : 0;
    this.handle.setAttribute('aria-valuenow', String(Math.round(Math.min(Math.max(pct, 0), 100))));
  }

  private commit() {
    void invokeDotNet(this.ref, 'OnResizeEnd', this.current);
  }

  private onDown = (e: PointerEvent) => {
    if (e.button !== 0 || this.root.getAttribute('data-state') !== 'open') return;
    e.preventDefault();
    this.dragging = true;
    this.dragSign = this.sign();
    this.startPos = this.horizontal ? e.clientX : e.clientY;
    this.startSize = this.measure();
    try { this.handle.setPointerCapture(e.pointerId); } catch { /* no capture */ }
    this.root.setAttribute('data-dragging', '');
    document.body.style.cursor = this.horizontal ? 'ew-resize' : 'ns-resize';
    window.addEventListener('pointermove', this.onMove);
    window.addEventListener('pointerup', this.onUp);
  };

  private onMove = (e: PointerEvent) => {
    if (!this.dragging) return;
    const cur = this.horizontal ? e.clientX : e.clientY;
    this.apply(this.startSize + (cur - this.startPos) * this.dragSign);
  };

  private onUp = (e: PointerEvent) => {
    if (!this.dragging) return;
    this.dragging = false;
    try { this.handle.releasePointerCapture(e.pointerId); } catch { /* already released */ }
    this.root.removeAttribute('data-dragging');
    document.body.style.cursor = '';
    window.removeEventListener('pointermove', this.onMove);
    window.removeEventListener('pointerup', this.onUp);
    this.commit();
  };

  // Arrows move the separator physically (so the grow direction depends on the docked edge, same as
  // the pointer); Home/End jump to min/max. Each press commits - it is already a settled size.
  private onKey = (e: KeyboardEvent) => {
    if (this.root.getAttribute('data-state') !== 'open') return;
    let next: number;
    switch (e.key) {
      case 'ArrowLeft':
      case 'ArrowRight':
        if (!this.horizontal) return;
        next = this.measure() + (e.key === 'ArrowRight' ? KEY_STEP : -KEY_STEP) * this.sign();
        break;
      case 'ArrowUp':
      case 'ArrowDown':
        if (this.horizontal) return;
        next = this.measure() + (e.key === 'ArrowDown' ? KEY_STEP : -KEY_STEP) * this.sign();
        break;
      case 'Home': next = this.minPx; break;
      case 'End': next = this.maxPx; break;
      default: return;
    }
    e.preventDefault();
    this.apply(next);
    this.commit();
  };

  dispose() {
    this.handle.removeEventListener('pointerdown', this.onDown);
    this.handle.removeEventListener('keydown', this.onKey);
    window.removeEventListener('pointermove', this.onMove);
    window.removeEventListener('pointerup', this.onUp);
    if (this.dragging) {
      this.root.removeAttribute('data-dragging');
      document.body.style.cursor = '';
    }
  }
}

/** Wires pointer/keyboard resize onto a panel's edge handle; returns a handle with dispose(). */
export function createPanelResize(
  root: HTMLElement,
  handle: HTMLElement,
  ref: DotNetObjectReference,
  side: PanelDock,
  minSize: string,
  maxSize: string,
): PanelResize {
  return new PanelResize(root, handle, ref, side, minSize, maxSize);
}
