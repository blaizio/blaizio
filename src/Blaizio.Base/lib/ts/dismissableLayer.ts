/**
 * DismissableLayer - closes a floating surface (popover, menu, ...) on an outside pointer-down,
 * and optionally on Escape. This is the dismissal half that focusScope.ts refers to ("Pairs with
 * DismissableLayer ... a separate primitive"); FocusScope owns focus, this owns dismissal.
 *
 * Non-blocking by design: the outside-pointerdown listener never preventDefault/stopPropagation, so
 * a press both dismisses this layer AND reaches whatever was under it - the page stays interactive
 * (no overlay, no swallowed "first click"). Layers stack; only the topmost reacts, so nested
 * surfaces dismiss inner-first.
 */

import { invokeDotNet } from './interop';

export interface DismissableLayerOptions {
  /** Selector for the trigger/anchor; a press on it is NOT "outside" - the trigger toggles itself. */
  anchorSelector: string | null;
  /** Also dismiss on Escape from here. Popover leaves Escape to its C# onkeydown, so passes false. */
  dismissOnEscape: boolean;
  /**
   * Also dismiss when anything outside the surface scrolls. For surfaces anchored to a fixed point
   * (the context menu's click position) a page scroll strands them; surfaces anchored to an element
   * reposition instead, so they leave this off.
   */
  dismissOnScroll?: boolean;
}

class DismissableLayer {
  /** Active layers in mount order; the last entry is topmost. */
  private static stack: DismissableLayer[] = [];

  constructor(
    private readonly element: HTMLElement,
    private readonly dotNetRef: DotNetReference,
    private readonly options: DismissableLayerOptions,
  ) {
    DismissableLayer.stack.push(this);
    // Capture phase: observe the press before the trigger's own click handler and before focus
    // shifts, so the topmost + anchor checks are reliable. The event is never consumed.
    document.addEventListener('pointerdown', this.onPointerDown, true);
    if (this.options.dismissOnEscape) document.addEventListener('keydown', this.onKeyDown, true);
    if (this.options.dismissOnScroll) {
      document.addEventListener('scroll', this.onScroll, { capture: true, passive: true });
    }
  }

  private get isTopmost(): boolean {
    return DismissableLayer.stack[DismissableLayer.stack.length - 1] === this;
  }

  private onPointerDown = (event: PointerEvent): void => {
    if (!this.isTopmost) return;
    const target = event.target as Node | null;
    if (!target) return;
    // Inside the surface -> keep it open.
    if (this.element.contains(target)) return;
    // On the trigger/anchor -> let the trigger toggle; don't also dismiss (would double-close).
    // Resolved per-event (not cached) so a re-rendered trigger is still matched.
    if (this.options.anchorSelector) {
      const anchor = document.querySelector(this.options.anchorSelector);
      if (anchor?.contains(target)) return;
    }
    void invokeDotNet(this.dotNetRef, 'OnDismissRequested');
  };

  private onKeyDown = (event: KeyboardEvent): void => {
    if (this.isTopmost && event.key === 'Escape') {
      void invokeDotNet(this.dotNetRef, 'OnDismissRequested');
    }
  };

  private onScroll = (event: Event): void => {
    if (!this.isTopmost) return;
    // Scrolling the surface's own overflow (a long item list) keeps it open; any outside scroll
    // (the page, another container) dismisses.
    const target = event.target as Node | null;
    if (target && target !== document && this.element.contains(target)) return;
    void invokeDotNet(this.dotNetRef, 'OnDismissRequested');
  };

  dispose(): void {
    document.removeEventListener('pointerdown', this.onPointerDown, true);
    document.removeEventListener('keydown', this.onKeyDown, true);
    document.removeEventListener('scroll', this.onScroll, true);
    DismissableLayer.stack = DismissableLayer.stack.filter((layer) => layer !== this);
  }
}

const noop = { dispose() {} };

/**
 * Attaches a dismissable layer to <code>element</code>. Returns a handle whose <code>dispose()</code>
 * removes the listeners and pops the stack; a no-op handle if <code>element</code> is not an element.
 */
export function createDismissableLayer(
  element: HTMLElement,
  dotNetRef: DotNetReference,
  options: DismissableLayerOptions,
): { dispose(): void } {
  if (!(element instanceof HTMLElement)) return noop;
  return new DismissableLayer(element, dotNetRef, options);
}
