// Anchored positioning for floating surfaces (tooltip, and later popover/dropdown/select) over
// @floating-ui/dom. Keeps the floating element pinned to its anchor and on-screen (flip + shift),
// re-positioning on scroll/resize/layout shift via autoUpdate, and positions an optional arrow.
//
// Division of labour with Blazor: the high-frequency left/top are written straight to the element's
// inline style here (a .NET round-trip per scroll frame would be far too chatty, and Blazor doesn't
// manage a style attribute we never render). The RESOLVED side/align are reported back to C# only
// when they actually change (a flip), so Blazor stays the owner of data-side/data-align. The element
// stays visibility:hidden until the FIRST position lands, so it never paints a frame at (0,0).
import { computePosition, autoUpdate, offset, flip, shift, size, arrow, type Placement } from '@floating-ui/dom';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

interface PositionOptions {
  side: 'top' | 'right' | 'bottom' | 'left';
  align: 'start' | 'center' | 'end';
  sideOffset: number;
  alignOffset: number;
  collisionPadding: number;
}

// The side the arrow sits on is opposite the placement side, so its tip points back at the anchor.
const opposite = { top: 'bottom', right: 'left', bottom: 'top', left: 'right' } as const;

class Positioning {
  private cleanup: () => void;
  private side = '';
  private align = '';
  private readonly arrowEl: HTMLElement | null;

  constructor(
    private readonly anchor: HTMLElement,
    private readonly floating: HTMLElement,
    private readonly opts: PositionOptions,
    private readonly ref: DotNetObjectReference | null,
  ) {
    this.arrowEl = floating.querySelector('[data-bz-tooltip-arrow]');
    // Fixed strategy escapes overflow/transform ancestors; hidden until the first computePosition so
    // the open animation never plays a frame in the top-left corner.
    this.floating.style.position = 'fixed';
    this.floating.style.top = '0';
    this.floating.style.left = '0';
    this.floating.style.visibility = 'hidden';
    // autoUpdate invokes update() once now, then on scroll/resize/ancestor-scroll/layout shift.
    this.cleanup = autoUpdate(this.anchor, this.floating, this.update);
  }

  private update = async (): Promise<void> => {
    const placement = (this.opts.align === 'center'
      ? this.opts.side
      : `${this.opts.side}-${this.opts.align}`) as Placement;

    const middleware = [
      offset({ mainAxis: this.opts.sideOffset, crossAxis: this.opts.alignOffset }),
      flip({ padding: this.opts.collisionPadding }),
      shift({ padding: this.opts.collisionPadding }),
      // Expose how much room is left between the anchor and the viewport edge as CSS custom
      // properties on the floating element. A surface that can outgrow the screen (a long menu)
      // caps its height with max-height: var(--bz-available-height) and scrolls; surfaces that
      // never do (tooltip) simply ignore the vars. Last before arrow so it sees the flipped side.
      size({
        padding: this.opts.collisionPadding,
        apply: ({ availableWidth, availableHeight, rects, elements }) => {
          elements.floating.style.setProperty('--bz-available-width', `${Math.max(0, Math.floor(availableWidth))}px`);
          elements.floating.style.setProperty('--bz-available-height', `${Math.max(0, Math.floor(availableHeight))}px`);
          // The anchor's width, so a surface can match its trigger (a select listbox). Surfaces that
          // don't want it simply ignore the var.
          elements.floating.style.setProperty('--bz-anchor-width', `${Math.round(rects.reference.width)}px`);
        },
      }),
    ];
    if (this.arrowEl) middleware.push(arrow({ element: this.arrowEl, padding: 6 }));

    const { x, y, placement: resolved, middlewareData } =
      await computePosition(this.anchor, this.floating, { strategy: 'fixed', placement, middleware });

    this.floating.style.left = `${Math.round(x)}px`;
    this.floating.style.top = `${Math.round(y)}px`;
    this.floating.style.visibility = 'visible';

    // floating-ui returns e.g. "top" or "top-start"; split into the side + align Blazor renders.
    const [side, align = 'center'] = resolved.split('-');

    if (this.arrowEl && middlewareData.arrow) {
      const { x: ax, y: ay } = middlewareData.arrow;
      const staticSide = opposite[side as keyof typeof opposite];
      // Sit the box's base ~1px inside the surface (hidden behind it, same fill) so the WHOLE
      // triangle shows beyond the edge - the wide base reads as the arrow, not just the tip.
      const poke = this.arrowEl.offsetWidth - 1;
      this.arrowEl.style.left = ax != null ? `${ax}px` : '';
      this.arrowEl.style.top = ay != null ? `${ay}px` : '';
      this.arrowEl.style.right = '';
      this.arrowEl.style.bottom = '';
      this.arrowEl.style.setProperty(staticSide, `${-poke}px`);
    }

    if (side !== this.side || align !== this.align) {
      this.side = side;
      this.align = align;
      void this.ref?.invokeMethodAsync('OnPlaced', side, align);
    }
  };

  dispose(): void {
    this.cleanup();
    // Leave the element hidden. A surface that unmounts on close (tooltip/popover/dropdown) is about
    // to be removed, so this is harmless; one that stays mounted and only toggles `hidden` (a select
    // listbox) must not flash at this stale position when it is shown again before the next
    // computePosition lands - the next createPositioning re-hides + repositions from scratch.
    this.floating.style.visibility = 'hidden';
  }
}

const noop = { dispose() {} };

/**
 * Anchors <code>floating</code> to the element matched by <code>anchorSelector</code> (a
 * data-attribute selector, so it resolves through an AsChild trigger), positioning an optional
 * descendant <code>[data-bz-tooltip-arrow]</code>. Returns a handle whose <code>dispose()</code>
 * stops the autoUpdate loop; a no-op handle if the anchor isn't in the DOM.
 */
export function createPositioning(
  anchorSelector: string,
  floating: HTMLElement,
  opts: PositionOptions,
  ref: DotNetObjectReference | null,
): { dispose(): void } {
  const anchor = document.querySelector(anchorSelector);
  if (!(anchor instanceof HTMLElement)) return noop;
  return new Positioning(anchor, floating, opts, ref);
}

const clamp = (value: number, min: number, max: number): number =>
  Math.min(Math.max(value, min), max);

/**
 * Item-aligned positioning for a select listbox: overlays the listbox on the trigger so the SELECTED
 * option (or the first, when nothing is selected) sits directly over it - the native-select feel,
 * rather than floating below like a popover. Computed once on open from the selected option's offset:
 *  - When the whole list fits the viewport, the panel is moved so the selected option's centre lines
 *    up with the trigger's, clamped so the panel stays on screen.
 *  - When the list is taller than the viewport, the panel is capped to the viewport height and the
 *    list is scrolled instead, so the selected option still meets the trigger.
 * The panel then stays pinned to the trigger via autoUpdate (it tracks the trigger as the page scrolls
 * or resizes), keeping the alignment line fixed while leaving the user's own list scrolling untouched.
 */
class ItemAlignedPositioning {
  private readonly cleanup: () => void;
  // The y-offset, within the panel, of the alignment line (the selected option's centre at the chosen
  // scroll). Captured once on open; afterwards the panel top tracks the trigger so this line stays on it.
  private anchorWithinPanel = 0;
  private placed = false;

  constructor(
    private readonly anchor: HTMLElement,
    private readonly floating: HTMLElement,
    private readonly opts: PositionOptions,
  ) {
    this.floating.style.position = 'fixed';
    this.floating.style.top = '0';
    this.floating.style.left = '0';
    this.floating.style.visibility = 'hidden';
    this.cleanup = autoUpdate(this.anchor, this.floating, this.update);
  }

  private selectedItem(): HTMLElement | null {
    return (
      this.floating.querySelector<HTMLElement>('[data-bz-menu-item][aria-selected="true"]') ??
      this.floating.querySelector<HTMLElement>('[data-bz-menu-item]')
    );
  }

  private update = (): void => {
    const padding = this.opts.collisionPadding;
    const trigger = this.anchor.getBoundingClientRect();
    const viewportW = window.innerWidth;
    const viewportH = window.innerHeight;

    // Match (at least) the trigger's width, like the popper path's size middleware does.
    this.floating.style.setProperty('--bz-anchor-width', `${Math.round(trigger.width)}px`);
    this.floating.style.minWidth = `${Math.round(trigger.width)}px`;

    const triggerCentre = trigger.top + trigger.height / 2;
    const item = this.selectedItem();

    // First pass decides the regime (fits vs. overflows) and the alignment line + any internal scroll.
    if (!this.placed) {
      this.placed = true;
      if (item) {
        const maxHeight = viewportH - padding * 2;
        this.floating.style.maxHeight = '';
        const overflows = this.floating.offsetHeight > maxHeight;
        const itemCentre = item.offsetTop + item.offsetHeight / 2;

        if (overflows) {
          // Cap to the viewport and scroll the list so the selected option meets the trigger centre.
          this.floating.style.maxHeight = `${maxHeight}px`;
          const scroll = clamp(itemCentre - (triggerCentre - padding), 0, this.floating.scrollHeight - maxHeight);
          this.floating.scrollTop = scroll;
          this.anchorWithinPanel = itemCentre - scroll;
        } else {
          this.floating.scrollTop = 0;
          this.anchorWithinPanel = itemCentre;
        }
      } else {
        // No items to align to: fall back to the panel's own midline.
        this.anchorWithinPanel = this.floating.offsetHeight / 2;
      }
    }

    // Keep the alignment line on the trigger centre, clamped so the panel stays fully on screen.
    const height = this.floating.offsetHeight;
    const width = this.floating.offsetWidth;
    const top = clamp(triggerCentre - this.anchorWithinPanel, padding, Math.max(padding, viewportH - padding - height));
    const left = clamp(trigger.left, padding, Math.max(padding, viewportW - padding - width));
    this.floating.style.top = `${Math.round(top)}px`;
    this.floating.style.left = `${Math.round(left)}px`;
    this.floating.style.visibility = 'visible';
  };

  dispose(): void {
    this.cleanup();
    // Match createPositioning: leave it hidden so a stale frame never flashes if the surface is reused.
    this.floating.style.visibility = 'hidden';
  }
}

/**
 * Anchors <code>floating</code> over the element matched by <code>anchorSelector</code> in the
 * item-aligned mode (see <code>ItemAlignedPositioning</code>). Returns a handle whose
 * <code>dispose()</code> stops the autoUpdate loop; a no-op handle if the anchor isn't in the DOM.
 */
export function createItemAlignedPositioning(
  anchorSelector: string,
  floating: HTMLElement,
  opts: PositionOptions,
  _ref: DotNetObjectReference | null,
): { dispose(): void } {
  const anchor = document.querySelector(anchorSelector);
  if (!(anchor instanceof HTMLElement)) return noop;
  return new ItemAlignedPositioning(anchor, floating, opts);
}
