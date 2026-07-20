// Pointer-drag for a resizable panel-group handle. The handle owns no sizing math - it just measures
// how far the pointer has moved along the group's axis, as a percentage of the group's size, and
// reports that delta to C# (which adjusts the two adjacent panels under their min/max constraints).
//
//   OnDragStart()      pointer went down on the handle - C# snapshots the current sizes
//   OnDrag(deltaPct)   total movement since down, signed % of the group (RTL-corrected for horizontal)
//   OnDragEnd()        pointer released

import { invokeDotNet } from './interop';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

class ResizeHandleDrag {
  private dragging = false;
  private startPos = 0;
  private groupSize = 1;
  private rtl = false;

  constructor(
    private readonly handle: HTMLElement,
    private readonly ref: DotNetObjectReference,
    private readonly horizontal: boolean,
  ) {
    handle.addEventListener('pointerdown', this.onDown);
    handle.addEventListener('keydown', this.onKeyDown);
  }

  // Stop the resize arrows (Home/End) from scrolling the page; C# does the actual resize via its own
  // keydown handler (preventDefault doesn't stop the event reaching Blazor).
  private onKeyDown = (e: KeyboardEvent) => {
    const keys = this.horizontal ? ['ArrowLeft', 'ArrowRight'] : ['ArrowUp', 'ArrowDown'];
    if (keys.includes(e.key) || e.key === 'Home' || e.key === 'End') e.preventDefault();
  };

  private onDown = (e: PointerEvent) => {
    if (e.button !== 0) return;
    const group = this.handle.closest('[data-slot=resizable-panel-group]') as HTMLElement | null;
    if (!group) return;
    this.groupSize = this.horizontal ? group.offsetWidth : group.offsetHeight;
    if (this.groupSize <= 0) return;
    e.preventDefault();
    this.rtl = this.horizontal && getComputedStyle(this.handle).direction === 'rtl';
    this.startPos = this.horizontal ? e.clientX : e.clientY;
    this.dragging = true;
    try { this.handle.setPointerCapture(e.pointerId); } catch { /* no capture */ }
    this.handle.setAttribute('data-resize-handle-active', 'pointer');
    document.body.style.cursor = this.horizontal ? 'ew-resize' : 'ns-resize';
    window.addEventListener('pointermove', this.onMove);
    window.addEventListener('pointerup', this.onUp);
    void invokeDotNet(this.ref, 'OnDragStart');
  };

  private onMove = (e: PointerEvent) => {
    if (!this.dragging) return;
    const cur = this.horizontal ? e.clientX : e.clientY;
    let delta = ((cur - this.startPos) / this.groupSize) * 100;
    if (this.rtl) delta = -delta; // in RTL the inline axis is mirrored
    void invokeDotNet(this.ref, 'OnDrag', delta);
  };

  private onUp = (e: PointerEvent) => {
    if (!this.dragging) return;
    this.dragging = false;
    try { this.handle.releasePointerCapture(e.pointerId); } catch { /* already released */ }
    this.handle.removeAttribute('data-resize-handle-active');
    document.body.style.cursor = '';
    window.removeEventListener('pointermove', this.onMove);
    window.removeEventListener('pointerup', this.onUp);
    void invokeDotNet(this.ref, 'OnDragEnd');
  };

  dispose() {
    this.handle.removeEventListener('pointerdown', this.onDown);
    this.handle.removeEventListener('keydown', this.onKeyDown);
    window.removeEventListener('pointermove', this.onMove);
    window.removeEventListener('pointerup', this.onUp);
  }
}

/** Wires pointer-drag onto a resize handle; returns a handle with dispose(). */
export function createResizeHandle(
  handle: HTMLElement,
  ref: DotNetObjectReference,
  horizontal: boolean,
): ResizeHandleDrag {
  return new ResizeHandleDrag(handle, ref, horizontal);
}
