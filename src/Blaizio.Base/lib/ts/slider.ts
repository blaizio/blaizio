/**
 * Slider - pointer-drag + keyboard geometry for BzSlider, ported in the same spirit as rovingFocus:
 * wholly DOM-side, reading state live from the markup so a runtime change (Min/Max/Step, orientation,
 * inverted, reading direction) needs no re-init.
 *
 *  - The module never owns the value. It turns a pointer position or a key press into a *proposed*
 *    value for one thumb and reports it to C# (`OnInput`); C# snaps to Step, clamps to [Min, Max] and
 *    enforces MinStepsBetweenThumbs, then re-renders the thumb's aria-valuenow + fraction. That keeps
 *    the value math in one place and avoids a Blazor-diff vs imperative-DOM tug-of-war over position.
 *  - Reading direction is read LIVE from computed style at the event (like rovingFocus), so an RTL
 *    switch flips the arrow keys and the pointer mapping immediately.
 *  - Min/Max/Step/orientation/inverted are read off the root's data-attributes each event, so the
 *    component never re-initialises the instance.
 */

import { invokeDotNet } from './interop';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

const THUMB_SELECTOR = '[data-bz-slider-thumb]';
const TRACK_SELECTOR = '[data-bz-slider-track]';

class Slider {
  private activeIndex = -1;
  private pointerId: number | null = null;

  constructor(
    private readonly root: HTMLElement,
    private readonly ref: DotNetObjectReference,
  ) {
    if (!root) return;
    this.root.addEventListener('pointerdown', this.onPointerDown);
    this.root.addEventListener('keydown', this.onKeyDown);
  }

  // --- live state, read from the DOM each time so nothing needs re-initialising ---

  private get disabled(): boolean {
    return this.root.hasAttribute('data-disabled');
  }

  private get vertical(): boolean {
    return this.root.getAttribute('data-orientation') === 'vertical';
  }

  private get inverted(): boolean {
    return this.root.hasAttribute('data-inverted');
  }

  private get rtl(): boolean {
    return getComputedStyle(this.root).direction === 'rtl';
  }

  private attr(name: string, fallback: number): number {
    const value = parseFloat(this.root.getAttribute(name) ?? '');
    return Number.isFinite(value) ? value : fallback;
  }

  private get min(): number {
    return this.attr('data-min', 0);
  }

  private get max(): number {
    return this.attr('data-max', 100);
  }

  private get step(): number {
    const step = this.attr('data-step', 1);
    return step > 0 ? step : 1;
  }

  private get thumbs(): HTMLElement[] {
    return Array.from(this.root.querySelectorAll<HTMLElement>(THUMB_SELECTOR));
  }

  private indexOf(thumb: HTMLElement): number {
    const index = parseInt(thumb.getAttribute('data-bz-slider-index') ?? '', 10);
    return Number.isFinite(index) ? index : -1;
  }

  private thumbAt(index: number): HTMLElement | undefined {
    return this.thumbs.find((thumb) => this.indexOf(thumb) === index);
  }

  // --- geometry ---

  /** Map a pointer position to a value in [Min, Max], honouring orientation, direction and inversion. */
  private valueFromPointer(clientX: number, clientY: number): number {
    const rect = (this.root.querySelector<HTMLElement>(TRACK_SELECTOR) ?? this.root).getBoundingClientRect();

    let ratio = this.vertical
      ? rect.height > 0
        ? (rect.bottom - clientY) / rect.height // vertical: Min at the bottom
        : 0
      : rect.width > 0
        ? (this.rtl ? rect.right - clientX : clientX - rect.left) / rect.width
        : 0;

    if (this.inverted) ratio = 1 - ratio;
    ratio = Math.min(1, Math.max(0, ratio));
    return this.min + ratio * (this.max - this.min);
  }

  /** Index of the thumb whose current value is nearest a target value (for clicks on the track). */
  private closestThumb(value: number): number {
    let best = -1;
    let bestDistance = Infinity;
    for (const thumb of this.thumbs) {
      const current = parseFloat(thumb.getAttribute('aria-valuenow') ?? '');
      const distance = Math.abs((Number.isFinite(current) ? current : 0) - value);
      if (distance < bestDistance) {
        bestDistance = distance;
        best = this.indexOf(thumb);
      }
    }
    return best;
  }

  // --- pointer ---

  private onPointerDown = (event: PointerEvent): void => {
    if (this.disabled || event.button !== 0) return;

    const targetThumb = (event.target as HTMLElement | null)?.closest<HTMLElement>(THUMB_SELECTOR) ?? null;
    const value = this.valueFromPointer(event.clientX, event.clientY);
    const index = targetThumb ? this.indexOf(targetThumb) : this.closestThumb(value);
    if (index < 0) return;

    this.activeIndex = index;
    this.pointerId = event.pointerId;
    // Capture so the drag keeps tracking outside the thin rail; can throw if the pointer is already
    // gone (or for a synthetic event) - the drag still works off the document-level move/up listeners.
    try {
      this.root.setPointerCapture(event.pointerId);
    } catch {
      /* no active pointer to capture - ignore */
    }
    event.preventDefault();
    this.thumbAt(index)?.focus();

    this.root.addEventListener('pointermove', this.onPointerMove);
    this.root.addEventListener('pointerup', this.onPointerUp);
    this.root.addEventListener('pointercancel', this.onPointerUp);

    // Grabbing the rail jumps the nearest thumb to the click; grabbing a thumb just begins the drag.
    if (!targetThumb) void invokeDotNet(this.ref, 'OnInput', index, value);
  };

  private onPointerMove = (event: PointerEvent): void => {
    if (this.activeIndex < 0) return;
    void invokeDotNet(this.ref, 'OnInput', this.activeIndex, this.valueFromPointer(event.clientX, event.clientY));
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
    this.activeIndex = -1;
    this.root.removeEventListener('pointermove', this.onPointerMove);
    this.root.removeEventListener('pointerup', this.onPointerUp);
    this.root.removeEventListener('pointercancel', this.onPointerUp);
    void invokeDotNet(this.ref, 'OnCommit');
  };

  // --- keyboard ---

  private onKeyDown = (event: KeyboardEvent): void => {
    if (this.disabled || event.metaKey || event.ctrlKey || event.altKey) return;

    const thumb = (event.target as HTMLElement | null)?.closest<HTMLElement>(THUMB_SELECTOR);
    if (!thumb || !this.root.contains(thumb)) return;

    const index = this.indexOf(thumb);
    if (index < 0) return;

    const current = parseFloat(thumb.getAttribute('aria-valuenow') ?? '');
    const target = this.keyToTarget(event.key, event.shiftKey, Number.isFinite(current) ? current : this.min);
    if (target === null) return;

    event.preventDefault();
    // Keyboard commits each press: apply the value, then fire the commit event.
    void invokeDotNet(this.ref, 'OnInput', index, target).then(() => invokeDotNet(this.ref, 'OnCommit'));
  };

  /** The value a key targets, or null if the key isn't a slider key. PageUp/Down + Shift use a big step. */
  private keyToTarget(key: string, shift: boolean, current: number): number | null {
    const { min, max, step } = this;
    const big = step * 10;

    switch (key) {
      case 'Home':
        return min;
      case 'End':
        return max;
      case 'PageUp':
        return current + big;
      case 'PageDown':
        return current - big;
    }

    let sign: number;
    switch (key) {
      case 'ArrowUp':
        sign = 1;
        break;
      case 'ArrowDown':
        sign = -1;
        break;
      case 'ArrowRight':
        sign = this.rtl ? -1 : 1;
        break;
      case 'ArrowLeft':
        sign = this.rtl ? 1 : -1;
        break;
      default:
        return null;
    }

    if (this.inverted) sign = -sign;
    return current + sign * (shift ? big : step);
  }

  dispose = (): void => {
    this.root.removeEventListener('pointerdown', this.onPointerDown);
    this.root.removeEventListener('keydown', this.onKeyDown);
    this.root.removeEventListener('pointermove', this.onPointerMove);
    this.root.removeEventListener('pointerup', this.onPointerUp);
    this.root.removeEventListener('pointercancel', this.onPointerUp);
  };
}

/** Attaches slider drag + keyboard behaviour to a BzSlider root element. */
export const createSlider = (root: HTMLElement, ref: DotNetObjectReference): Slider => new Slider(root, ref);
