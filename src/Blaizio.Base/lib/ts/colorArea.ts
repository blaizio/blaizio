/**
 * ColorArea - pointer-drag + keyboard geometry for the 2D saturation/value surface of a color
 * picker, built in the same spirit as slider.ts: wholly DOM-side, the module never owns the value.
 * It turns a pointer position or key press into a proposed (x, y) pair - both 0..1, x from the
 * left edge, y from the BOTTOM edge (so y maps directly onto HSV value: bottom = black) - and
 * reports it to C# (`OnInput`); C# clamps and re-renders the thumb's fraction custom properties.
 *
 * Also hosts the EyeDropper API bridge (`supportsEyeDropper` / `openEyeDropper`) - it is color
 * territory and too small for a module of its own.
 */

import { invokeDotNet } from './core';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

const THUMB_SELECTOR = '[data-bz-color-area-thumb]';

const STEP = 0.01;
const BIG_STEP = 0.1;

class ColorArea {
  private dragging = false;
  private pointerId: number | null = null;

  constructor(
    private readonly root: HTMLElement,
    private readonly ref: DotNetObjectReference,
  ) {
    if (!root) return;
    this.root.addEventListener('pointerdown', this.onPointerDown);
    this.root.addEventListener('keydown', this.onKeyDown);
  }

  private get disabled(): boolean {
    return this.root.hasAttribute('data-disabled');
  }

  private get thumb(): HTMLElement | null {
    return this.root.querySelector<HTMLElement>(THUMB_SELECTOR);
  }

  /** Map a pointer position to (x, y) in [0, 1]^2 - x from the left, y from the bottom. */
  private fromPointer(clientX: number, clientY: number): { x: number; y: number } {
    const rect = this.root.getBoundingClientRect();
    const x = rect.width > 0 ? (clientX - rect.left) / rect.width : 0;
    const y = rect.height > 0 ? (rect.bottom - clientY) / rect.height : 0;
    return { x: Math.min(1, Math.max(0, x)), y: Math.min(1, Math.max(0, y)) };
  }

  private report(x: number, y: number): void {
    void invokeDotNet(this.ref, 'OnInput', x, y);
  }

  // --- pointer ---

  private onPointerDown = (event: PointerEvent): void => {
    if (this.disabled || event.button !== 0) return;

    // Map the position FIRST: focusing the thumb below can scroll it into view, which moves the
    // rect out from under the event's (stale) client coordinates.
    const { x, y } = this.fromPointer(event.clientX, event.clientY);

    this.dragging = true;
    this.pointerId = event.pointerId;
    // Capture so the drag keeps tracking outside the surface; can throw for a synthetic event -
    // the drag still works off the root-level move/up listeners.
    try {
      this.root.setPointerCapture(event.pointerId);
    } catch {
      /* no active pointer to capture - ignore */
    }
    event.preventDefault();
    this.thumb?.focus();

    this.root.addEventListener('pointermove', this.onPointerMove);
    this.root.addEventListener('pointerup', this.onPointerUp);
    this.root.addEventListener('pointercancel', this.onPointerUp);

    this.report(x, y);
  };

  private onPointerMove = (event: PointerEvent): void => {
    if (!this.dragging) return;
    const { x, y } = this.fromPointer(event.clientX, event.clientY);
    this.report(x, y);
  };

  private onPointerUp = (): void => {
    if (this.pointerId !== null && this.root.hasPointerCapture(this.pointerId)) {
      try {
        this.root.releasePointerCapture(this.pointerId);
      } catch {
        /* already released - ignore */
      }
    }
    this.pointerId = null;
    this.dragging = false;
    this.root.removeEventListener('pointermove', this.onPointerMove);
    this.root.removeEventListener('pointerup', this.onPointerUp);
    this.root.removeEventListener('pointercancel', this.onPointerUp);
    void invokeDotNet(this.ref, 'OnCommit');
  };

  // --- keyboard (on the thumb) ---

  private onKeyDown = (event: KeyboardEvent): void => {
    if (this.disabled || event.metaKey || event.ctrlKey || event.altKey) return;

    const thumb = (event.target as HTMLElement | null)?.closest<HTMLElement>(THUMB_SELECTOR);
    if (!thumb || !this.root.contains(thumb)) return;

    const x = parseFloat(thumb.getAttribute('data-x') ?? '0');
    const y = parseFloat(thumb.getAttribute('data-y') ?? '0');
    const step = event.shiftKey ? BIG_STEP : STEP;

    let dx = 0;
    let dy = 0;
    switch (event.key) {
      case 'ArrowRight':
        dx = step;
        break;
      case 'ArrowLeft':
        dx = -step;
        break;
      case 'ArrowUp':
        dy = step;
        break;
      case 'ArrowDown':
        dy = -step;
        break;
      case 'Home':
        dx = -1;
        break;
      case 'End':
        dx = 1;
        break;
      case 'PageUp':
        dy = BIG_STEP;
        break;
      case 'PageDown':
        dy = -BIG_STEP;
        break;
      default:
        return;
    }

    event.preventDefault();
    // Keyboard commits each press: apply the pair, then fire the commit event.
    void invokeDotNet(this.ref, 'OnInput', x + dx, y + dy).then(() => invokeDotNet(this.ref, 'OnCommit'));
  };

  dispose = (): void => {
    this.root.removeEventListener('pointerdown', this.onPointerDown);
    this.root.removeEventListener('keydown', this.onKeyDown);
    this.root.removeEventListener('pointermove', this.onPointerMove);
    this.root.removeEventListener('pointerup', this.onPointerUp);
    this.root.removeEventListener('pointercancel', this.onPointerUp);
  };
}

/** Attaches 2D drag + keyboard behaviour to a color area root element. */
export const createColorArea = (root: HTMLElement, ref: DotNetObjectReference): ColorArea =>
  new ColorArea(root, ref);

interface EyeDropperResult {
  sRGBHex: string;
}

interface EyeDropperConstructor {
  new (): { open(): Promise<EyeDropperResult> };
}

/** Whether the native EyeDropper API exists here (desktop Chromium only, as of 2026). */
export const supportsEyeDropper = (): boolean =>
  typeof (globalThis as { EyeDropper?: unknown }).EyeDropper === 'function';

/** Open the native eye dropper; resolves the picked #rrggbb, or null when unsupported/dismissed. */
export const openEyeDropper = async (): Promise<string | null> => {
  const ctor = (globalThis as { EyeDropper?: EyeDropperConstructor }).EyeDropper;
  if (typeof ctor !== 'function') return null;
  try {
    const result = await new ctor().open();
    return result.sRGBHex;
  } catch {
    return null; // dismissed with Escape - not an error
  }
};
