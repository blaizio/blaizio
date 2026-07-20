/**
 * Menu navigation for a single menu level (the WAI-ARIA menu keyboard pattern), attached to a
 * [data-bz-menu-content] surface. Vertical Arrow movement, Home/End, typeahead, and Enter/Space
 * activation are owned here - wholly DOM-side, no per-keystroke interop (the sibling of
 * rovingFocus.ts). Selection still flows through each item's own Blazor onclick: Enter/Space
 * synthesizes a click on the focused item, exactly like rovingFocus.ts's selectOnFocus.
 *
 * Submenus add two concerns this module owns too, because both need synchronous DOM access a Blazor
 * round-trip is too slow (a flicker) or too coarse (a guessed timer) for:
 *  - The inline-start Arrow (ArrowLeft in LTR, ArrowRight in RTL) closes the submenu. We refocus its
 *    trigger SYNCHRONOUSLY so the highlight never blinks off, and stopPropagation so the same key
 *    doesn't also collapse every outer submenu on the way up (their surfaces are DOM ancestors).
 *  - The "safe triangle": while the pointer sweeps from a sub-trigger toward its open
 *    submenu it crosses sibling items - those must NOT steal the highlight, and the submenu must NOT
 *    close. We track the pointer's horizontal direction and a grace polygon to recognise that intent.
 * A submenu otherwise closes when focus leaves it for anything but its own trigger (the pointer
 * landing on a real sibling), routed to C# RequestClose through the surface's .NET ref.
 *
 * Deliberately NOT handled here, so they reach the surface's own C# handlers: ArrowRight (open a
 * submenu) on a sub-trigger, and Escape / Tab dismissal.
 *
 * Levels nest: a submenu's surface is a DOM descendant of its parent's, so a keydown bubbles through
 * both listeners. Each instance acts only when focus is within ITS level (nearest menu surface ===
 * its container) and stops propagation once it handles a key, so the parent never double-handles.
 */

import { invokeDotNet } from './interop';

export interface MenuOptions {
  /**
   * Where to place focus the instant the surface mounts (decided by how the menu was opened).
   * 'selected' (a select listbox) lands on the [aria-selected] option, scrolled into view, falling
   * back to the first option.
   */
  initialFocus: 'none' | 'first' | 'last' | 'selected';
  /** Milliseconds of inactivity before the typeahead buffer resets. */
  typeaheadTimeout: number;
  /** Arrow navigation wraps at the ends (past the last item returns to the first). */
  loop: boolean;
  /** A submenu surface: closes on the inline-start arrow and on focus leaving it for a non-trigger. */
  isSubmenu: boolean;
  /** Selector for the trigger this surface returns focus to on close (the sub-trigger, for a submenu). */
  triggerSelector: string;
}

/** A Blazor DotNetObjectReference handed to JS for callbacks into .NET. */
interface DotNetReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

interface Point {
  x: number;
  y: number;
}

/** The pointer's heading + the polygon it must stay inside to count as "moving to the submenu". */
interface GraceIntent {
  area: Point[];
  side: 'left' | 'right';
}

const ITEM_SELECTOR = '[data-bz-menu-item]';
const SURFACE_SELECTOR = '[data-bz-menu-content]';
// A sub-trigger is the only menuitem that owns a submenu; it carries aria-haspopup="menu" +
// aria-controls. (The root trigger has it too but lives outside any content surface, so the
// closest-surface guard below skips it.)
const SUB_TRIGGER_SELECTOR = '[aria-haspopup="menu"]';

/** Ray-casting point-in-polygon test, for the safe-triangle hit test. */
function isPointInPolygon(point: Point, polygon: Point[]): boolean {
  const { x, y } = point;
  let inside = false;
  for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
    const pi = polygon[i]!;
    const pj = polygon[j]!;
    const intersect =
      pi.y > y !== pj.y > y && x < ((pj.x - pi.x) * (y - pi.y)) / (pj.y - pi.y) + pi.x;
    if (intersect) inside = !inside;
  }
  return inside;
}

class Menu {
  private search = '';
  private searchTimer?: number;
  private focusTimer = 0;
  private focusSettled = false;
  private clearTimer = 0;

  // Safe-triangle state. pointerDir is the pointer's last horizontal heading; graceIntent is the
  // polygon (set when leaving one of OUR sub-triggers toward its open submenu) that, while the
  // heading matches, marks the items underneath as "in transit" - not to be highlighted or to close
  // the submenu. graceIntent self-clears after 300ms so a parked pointer resumes normal hovering.
  private pointerDir: 'left' | 'right' = 'right';
  private lastPointerX = 0;
  private graceIntent: GraceIntent | null = null;
  private graceTimer = 0;

  constructor(
    private readonly container: HTMLElement,
    private readonly options: MenuOptions,
    private readonly ref: DotNetReference | null,
  ) {
    this.container.addEventListener('keydown', this.onKeyDown);
    this.container.addEventListener('pointermove', this.onPointerMove);
    this.container.addEventListener('pointerleave', this.onPointerLeave);
    this.container.addEventListener('pointerout', this.onPointerOut);
    // Only a submenu self-closes when focus moves away; the root is dismissed by its own layer /
    // Escape / Tab. The listener is on the document, not this surface: the focus often moves to a
    // sibling in the PARENT menu (a DOM ancestor's child, never inside this surface), so a
    // surface-scoped listener would miss it.
    if (this.options.isSubmenu) document.addEventListener('focusin', this.onFocusIn);

    this.focusWhenReady();
  }

  /**
   * Place the opening focus once the surface can actually take it, then keep it there through a short
   * settle window. ts/positioning.js opens the surface visibility:hidden until its first frame lands,
   * and a hidden element silently rejects focus() - so a naive synchronous focus drops on a cold open,
   * leaving the first item un-highlighted (focus stranded on the trigger). We re-place focus while it
   * sits OUTSIDE this surface's whole subtree, and leave it the moment it's anywhere inside (the item
   * we placed, one the user navigated to, or a submenu they opened) - never yanking. 'first'/'last'
   * land on an item; 'none' (pointer-opened) lands on the surface so the first Arrow key has somewhere
   * to step from.
   *
   * The retry is a setTimeout, NOT requestAnimationFrame: rAF is paused in background tabs, so an rAF
   * retry would never run if the menu opened while the tab was hidden, stranding focus on the trigger.
   */
  private focusWhenReady = (attemptsLeft = 30): void => {
    if (!this.container.isConnected || this.focusSettled) return;

    if (!this.container.contains(document.activeElement)) {
      const target = this.initialTarget() ?? (attemptsLeft === 0 ? this.container : null);
      target?.focus({ preventScroll: true });
    }

    // The instant focus is inside (the item we placed, the surface, or a submenu the user opened),
    // settle and STOP - re-checked AFTER placing so we never schedule another tick once landed.
    // Continuing to poll would re-grab focus the moment the user moves it back OUT (e.g. up to a
    // parent sibling right after a submenu opened), fighting their own navigation.
    if (this.container.contains(document.activeElement)) {
      this.focusSettled = true;
      // A select opens with the chosen option focused; bring it into view in case the list scrolls
      // (no-op if already visible). preventScroll above keeps the page itself from jumping.
      if (this.options.initialFocus === 'selected') {
        (document.activeElement as HTMLElement | null)?.scrollIntoView({ block: 'nearest' });
      }
      return;
    }

    if (attemptsLeft > 0) {
      this.focusTimer = window.setTimeout(() => this.focusWhenReady(attemptsLeft - 1), 16);
    }
  };

  /** The element the opening focus should land on, or null while the items haven't rendered yet. */
  private initialTarget(): HTMLElement | null {
    if (this.options.initialFocus === 'none') return this.container;
    const items = this.getItems();
    if (this.options.initialFocus === 'selected') {
      const selected = items.find((el) => el.getAttribute('aria-selected') === 'true');
      return selected ?? items[0] ?? null;
    }
    const item = this.options.initialFocus === 'last' ? items[items.length - 1] : items[0];
    return item ?? null;
  }

  /** Enabled items that belong to THIS level - excludes those nested in a child submenu surface. */
  private getItems(): HTMLElement[] {
    return Array.from(this.container.querySelectorAll<HTMLElement>(ITEM_SELECTOR)).filter(
      (el) =>
        el.closest(SURFACE_SELECTOR) === this.container &&
        !(el as HTMLElement & { disabled?: boolean }).disabled &&
        !el.hasAttribute('data-disabled') &&
        el.getAttribute('aria-disabled') !== 'true',
    );
  }

  /** The focused item, but only when it sits at this level (not in a nested submenu). */
  private currentItem(): HTMLElement | null {
    const item = (document.activeElement as HTMLElement | null)?.closest<HTMLElement>(ITEM_SELECTOR);
    return item && item.closest(SURFACE_SELECTOR) === this.container ? item : null;
  }

  /** Whether the focused item at this level owns a submenu (so the inline-end arrow opens it). */
  private currentItemIsSubTrigger(): boolean {
    return this.currentItem()?.matches(SUB_TRIGGER_SELECTOR) ?? false;
  }

  /** Whether keyboard focus currently rests on this surface (the container itself or one of its items). */
  private get focusIsHere(): boolean {
    return (document.activeElement as HTMLElement | null)?.closest(SURFACE_SELECTOR) === this.container;
  }

  private onKeyDown = (event: KeyboardEvent): void => {
    if (event.altKey || event.ctrlKey || event.metaKey) return;
    // A nested submenu (a descendant surface) owns its own keys; ignore unless focus is at our level.
    if (!this.focusIsHere) return;

    // The inline-start arrow closes a submenu and steps back to its trigger. Done here (not C#) so
    // the trigger refocuses synchronously (no highlight blink) and the key is consumed before it
    // bubbles to an outer submenu surface and collapses the whole stack.
    if (this.options.isSubmenu && this.isCloseKey(event.key)) {
      this.consume(event);
      this.trigger()?.focus({ preventScroll: true });
      this.requestClose();
      return;
    }

    // Inside a menubar, the inline arrows step along the bar to the adjacent menu (the desktop
    // menu-bar pattern) - from the root menu AND from any nested submenu. Owned here because only the
    // DOM knows whether the focused item is a sub-trigger (where the inline-END arrow opens that
    // submenu instead of switching) and which menubar (if any) this surface lives in.
    if (this.handleCrossMenu(event)) return;

    switch (event.key) {
      case 'ArrowDown':
        this.consume(event);
        this.move(1);
        return;
      case 'ArrowUp':
        this.consume(event);
        this.move(-1);
        return;
      case 'Home':
        this.consume(event);
        this.focusEdge('first');
        return;
      case 'End':
        this.consume(event);
        this.focusEdge('last');
        return;
      case 'Enter':
      case ' ': {
        const current = this.currentItem();
        if (current) {
          this.consume(event);
          // A plain item closes the whole menu on activation. Return focus to the trigger
          // SYNCHRONOUSLY, before the close re-renders, so a trigger that shows an "open" highlight (a
          // menubar's) runs it unbroken into its focus highlight rather than dropping it for the close
          // animation and snapping it back - the same continuity the Escape path already gets. Skipped
          // for items that DON'T necessarily close on activation (a sub-trigger opens its submenu;
          // a checkbox/radio item may preventDefault to stay open), so we never pull focus out of a menu
          // that stays open; and for a submenu level, whose whole stack unmounts so its parent restores.
          // The C# close still restores focus too - a no-op once focus is already on the trigger here.
          const closesMenu =
            current.getAttribute('role') === 'menuitem' && !current.hasAttribute('aria-haspopup');
          current.click();
          if (closesMenu && !this.options.isSubmenu) this.trigger()?.focus({ preventScroll: true });
        }
        return;
      }
      default:
        // Single printable character (and not a modifier-only key) drives typeahead.
        if (event.key.length === 1 && /\S/.test(event.key)) {
          event.stopPropagation();
          this.typeahead(event.key);
        }
    }
  };

  /**
   * Pointer moved over the surface. Hovering one of our enabled items focuses it (the highlight
   * follows the pointer); hovering anything else at this level - a label, a separator, a disabled
   * item, or the padding between items - clears the highlight, so it tracks the pointer exactly
   * instead of leaving the last item lit until the pointer leaves the whole surface. That clear is
   * the per-item pointer-leave a single delegated listener can't get for free, and it is what makes
   * the highlight follow the mouse 1:1 rather than sticking on the last item over a gap or a label.
   */
  private onPointerMove = (event: PointerEvent): void => {
    if (event.pointerType !== 'mouse') return;
    this.trackPointerDirection(event);

    // While the pointer sweeps toward an open submenu, freeze the highlight where it is: don't move it
    // onto the items being crossed, and don't clear it off the sub-trigger either (that would pull
    // focus out of the submenu and close it). Once the heading turns away - or the grace expires -
    // hovering resumes.
    if (this.isPointerMovingToSubmenu(event)) return;

    const item = (event.target as HTMLElement | null)?.closest<HTMLElement>(ITEM_SELECTOR);
    const ownItem =
      item && item.closest(SURFACE_SELECTOR) === this.container && !this.isDisabled(item) ? item : null;

    if (ownItem) {
      // Landing on an item cancels any pending clear, then moves the highlight straight onto it - a single
      // focus() takes it off the previous item atomically, so the highlight never blinks off in between.
      this.cancelClear();
      if (ownItem !== document.activeElement) ownItem.focus({ preventScroll: true });
      return;
    }

    // Over the surface but off any enabled item - a label, a separator, or the gap between groups. Clear
    // the highlight, but on the NEXT frame, and cancel it the instant the pointer reaches an item. So
    // sweeping ACROSS a separator or inter-group gap toward another item never blinks the highlight off
    // (the landing cancels the pending clear); only RESTING on a non-item actually drops it.
    this.scheduleClear();
  };

  /**
   * Clear the highlight on the next frame unless the pointer reaches an item first. Deferring to a frame
   * (rAF, which runs after that frame's pointer events) lets a fast sweep across a gap cancel the clear by
   * landing on an item, so only resting on a label/separator/gap actually drops the highlight.
   */
  private scheduleClear(): void {
    if (this.clearTimer) return;
    this.clearTimer = requestAnimationFrame(() => {
      this.clearTimer = 0;
      if (this.currentItem()) this.container.focus({ preventScroll: true });
    });
  }

  private cancelClear(): void {
    if (this.clearTimer) {
      cancelAnimationFrame(this.clearTimer);
      this.clearTimer = 0;
    }
  }

  /**
   * The pointer left this surface (e.g. straight off an item and out the edge, with no intervening
   * move over a gap to clear it). The highlight is just :focus, so pull focus back to the surface to
   * clear the lit item. We skip it when the pointer is headed to another menu level (a submenu, or
   * back to a parent) - that level claims focus itself - and only act when focus is actually parked
   * on one of OUR items.
   */
  private onPointerLeave = (event: PointerEvent): void => {
    const to = event.relatedTarget as HTMLElement | null;
    if (to?.closest(SURFACE_SELECTOR)) return;
    // A real exit from the surface is definitive - clear now (and drop any pending deferred clear).
    this.cancelClear();
    if (this.currentItem()) this.container.focus({ preventScroll: true });
  };

  /**
   * The pointer left one of OUR sub-triggers while its submenu is open. Lay down the safe triangle:
   * a polygon from the pointer's exit point out to the four corners of the open submenu, so the items
   * between the trigger and the panel read as "in transit" (see onPointerMove). Includes the small
   * clientX bleed that keeps the exit vertex inside the polygon, and the 300ms expiry.
   */
  private onPointerOut = (event: PointerEvent): void => {
    if (event.pointerType !== 'mouse') return;
    const trigger = (event.target as HTMLElement | null)?.closest<HTMLElement>(SUB_TRIGGER_SELECTOR);
    if (!trigger || trigger.closest(SURFACE_SELECTOR) !== this.container) return;
    // pointerout also fires when moving onto a child of the trigger; only act on a real exit.
    const to = event.relatedTarget as HTMLElement | null;
    if (to && trigger.contains(to)) return;
    if (trigger.getAttribute('aria-expanded') !== 'true') return;

    const contentId = trigger.getAttribute('aria-controls');
    const content = contentId ? document.getElementById(contentId) : null;
    if (!content) return;

    const rect = content.getBoundingClientRect();
    const side = content.getAttribute('data-side') === 'left' ? 'left' : 'right';
    const rightSide = side === 'right';
    const bleed = rightSide ? -5 : 5;
    const nearEdge = rightSide ? rect.left : rect.right; // submenu edge closest to the trigger
    const farEdge = rightSide ? rect.right : rect.left;
    this.setGraceIntent({
      side,
      area: [
        { x: event.clientX + bleed, y: event.clientY },
        { x: nearEdge, y: rect.top },
        { x: farEdge, y: rect.top },
        { x: farEdge, y: rect.bottom },
        { x: nearEdge, y: rect.bottom },
      ],
    });
  };

  /**
   * Focus moved somewhere on the page. Close this submenu when it landed on a real element outside
   * us and outside our trigger - a sibling the pointer settled on in the parent menu - which is what
   * dismisses a submenu on mouse-out, promptly and without a guessed timer (the same focus-outside
   * dismissal the dismissable layer uses). Keyed on the focus TARGET, not where it came from, so it fires
   * even when focus was resting on our trigger (the pointer was over it) rather than inside us. Stays
   * open when focus is now inside us (an item, or a nested submenu - a DOM descendant) or on our own
   * trigger (the pointer returned to it; ArrowLeft handles that close explicitly).
   */
  private onFocusIn = (event: FocusEvent): void => {
    const target = event.target as HTMLElement | null;
    if (!target) return;
    if (this.container.contains(target)) return;
    const trigger = this.trigger();
    if (trigger && (target === trigger || trigger.contains(target))) return;
    this.requestClose();
  };

  private trackPointerDirection(event: PointerEvent): void {
    if (event.clientX === this.lastPointerX) return;
    this.pointerDir = event.clientX > this.lastPointerX ? 'right' : 'left';
    this.lastPointerX = event.clientX;
  }

  /** The pointer is heading into the safe triangle of an open submenu (matching side, inside polygon). */
  private isPointerMovingToSubmenu(event: PointerEvent): boolean {
    return (
      this.graceIntent !== null &&
      this.pointerDir === this.graceIntent.side &&
      isPointInPolygon({ x: event.clientX, y: event.clientY }, this.graceIntent.area)
    );
  }

  private setGraceIntent(intent: GraceIntent): void {
    this.graceIntent = intent;
    clearTimeout(this.graceTimer);
    this.graceTimer = window.setTimeout(() => (this.graceIntent = null), 300);
  }

  /** This submenu's trigger element, to refocus on close and to recognise in onFocusIn. */
  private trigger(): HTMLElement | null {
    return this.options.triggerSelector
      ? document.querySelector<HTMLElement>(this.options.triggerSelector)
      : null;
  }

  /** Ask C# to close this submenu level (fire-and-forget; the close animates through presence). */
  private requestClose(): void {
    void invokeDotNet(this.ref, 'OnSubCloseRequested');
  }

  /**
   * Menubar cross-menu step. Active at EVERY menu level inside a [data-bz-menubar] (the root menu AND
   * any nested submenu, detected via the DOM so no per-surface flag is needed): the inline-start arrow
   * steps to the previous menu, the inline-end arrow to the next - UNLESS focus is on a sub-trigger,
   * where the inline-end arrow opens that submenu instead (left to the trigger's own handler). In a
   * submenu the inline-start arrow is the CLOSE key (handled earlier and never reaching here), so from
   * inside a submenu only the inline-end arrow on a leaf item escapes to the adjacent top menu. Returns
   * whether it consumed the key (false when not in a menubar, or at a non-wrapping end with nowhere to go).
   */
  private handleCrossMenu(event: KeyboardEvent): boolean {
    const bar = this.container.closest<HTMLElement>('[data-bz-menubar]');
    if (!bar) return false;
    const rtl = getComputedStyle(this.container).direction === 'rtl';
    const prevKey = rtl ? 'ArrowRight' : 'ArrowLeft';
    const nextKey = rtl ? 'ArrowLeft' : 'ArrowRight';
    let step: number;
    if (event.key === prevKey) step = -1;
    else if (event.key === nextKey && !this.currentItemIsSubTrigger()) step = 1;
    else return false;
    if (!this.stepToAdjacentMenu(bar, step)) return false;
    this.consume(event);
    return true;
  }

  /**
   * Open the menubar menu `step` away from the one currently open - located by its expanded top-level
   * trigger, so this resolves the same whether focus is on the root menu or deep inside a submenu. A
   * synthetic click on the target trigger reuses the trigger's own keyboard-open path (detail 0 -> first
   * item focused); the bar's single open-slot closes the whole current stack as the next opens. Wraps
   * past the ends only when the bar loops. Returns whether a switch was made.
   */
  private stepToAdjacentMenu(bar: HTMLElement, step: number): boolean {
    const triggers = Array.from(bar.querySelectorAll<HTMLElement>('[data-bz-dropdown-menu-anchor]'));
    const current = bar.querySelector<HTMLElement>('[data-bz-dropdown-menu-anchor][aria-expanded="true"]');
    const index = current ? triggers.indexOf(current) : -1;
    if (index === -1) return false;
    const count = triggers.length;
    const loop = bar.getAttribute('data-bz-menubar-loop') !== 'false';
    const nextIndex = loop ? (index + step + count) % count : index + step;
    const target = triggers[nextIndex];
    if (!target || target === current) return false;
    target.click();
    return true;
  }

  /** The inline-start arrow for this surface's reading direction (ArrowLeft LTR, ArrowRight RTL). */
  private isCloseKey(key: string): boolean {
    const rtl = getComputedStyle(this.container).direction === 'rtl';
    return key === (rtl ? 'ArrowRight' : 'ArrowLeft');
  }

  private isDisabled(item: HTMLElement): boolean {
    return (
      (item as HTMLElement & { disabled?: boolean }).disabled === true ||
      item.hasAttribute('data-disabled') ||
      item.getAttribute('aria-disabled') === 'true'
    );
  }

  /** Move the focused item by `delta` - wrapping at the ends when loop is on, else stopping there. */
  private move(delta: number): void {
    const items = this.getItems();
    if (items.length === 0) return;
    const current = this.currentItem();
    const index = current ? items.indexOf(current) : delta > 0 ? -1 : 0;
    const next = this.options.loop
      ? (index + delta + items.length) % items.length
      : Math.min(Math.max(index + delta, 0), items.length - 1);
    items[next]?.focus({ preventScroll: true });
  }

  private focusEdge(edge: 'first' | 'last'): void {
    const items = this.getItems();
    const target = edge === 'first' ? items[0] : items[items.length - 1];
    target?.focus({ preventScroll: true });
  }

  private typeahead(char: string): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = window.setTimeout(() => (this.search = ''), this.options.typeaheadTimeout);
    this.search += char.toLowerCase();

    const items = this.getItems();
    const labels = items.map((item) => (item.textContent ?? '').trim().toLowerCase());
    // Anchor the search just after the focused item so repeated presses cycle through matches.
    const current = this.currentItem();
    const start = current ? items.indexOf(current) : -1;
    // A run of the same key ("p","p","p") cycles same-initial items rather than demanding "ppp".
    const allSame = this.search.length > 1 && this.search.split('').every((c) => c === this.search[0]);
    const needle = allSame ? this.search[0]! : this.search;
    const offset = allSame ? 1 : 0;

    for (let i = 0; i < items.length; i++) {
      const probe = (start + offset + i) % items.length;
      if (labels[probe]!.startsWith(needle)) {
        items[probe]!.focus({ preventScroll: true });
        return;
      }
    }
  }

  private consume(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
  }

  dispose(): void {
    clearTimeout(this.searchTimer);
    clearTimeout(this.focusTimer);
    clearTimeout(this.graceTimer);
    cancelAnimationFrame(this.clearTimer);
    this.container.removeEventListener('keydown', this.onKeyDown);
    this.container.removeEventListener('pointermove', this.onPointerMove);
    this.container.removeEventListener('pointerleave', this.onPointerLeave);
    this.container.removeEventListener('pointerout', this.onPointerOut);
    document.removeEventListener('focusin', this.onFocusIn);
  }
}

const noop = { dispose() {} };

/**
 * Attaches menu navigation to a <code>[data-bz-menu-content]</code> surface. <code>ref</code> is the
 * surface's .NET reference, used only by a submenu to request its own close. Returns a handle whose
 * <code>dispose()</code> detaches the listeners; a no-op handle if <code>container</code> isn't an element.
 */
export function createMenu(
  container: HTMLElement,
  options: MenuOptions,
  ref: DotNetReference | null = null,
): { dispose(): void } {
  if (!(container instanceof HTMLElement)) return noop;
  return new Menu(container, options, ref);
}

const TRIGGER_NAV_KEYS = new Set(['ArrowDown', 'ArrowUp']);

/**
 * Stop the page scrolling when ArrowDown / ArrowUp open a menu from its (closed) trigger. The
 * trigger's own Blazor onkeydown still opens the menu - this only suppresses the native scroll,
 * which a splatted Blazor handler can't preventDefault in time (it round-trips to .NET first). Pure
 * DOM, no callback. The listener only ever fires while the trigger itself holds focus (menu closed);
 * once open, focus is in the content and ts/menu.js owns the arrows. Returns a dispose() handle.
 */
export function guardTrigger(selector: string): { dispose(): void } {
  const trigger = document.querySelector<HTMLElement>(selector);
  if (!trigger) return noop;
  const onKeyDown = (event: KeyboardEvent): void => {
    if (TRIGGER_NAV_KEYS.has(event.key) && !event.altKey && !event.ctrlKey && !event.metaKey) {
      event.preventDefault();
    }
  };
  trigger.addEventListener('keydown', onKeyDown);
  return {
    dispose() {
      trigger.removeEventListener('keydown', onKeyDown);
    },
  };
}

/**
 * Return focus to a menu's trigger (matched by <code>selector</code>) when the menu closes. Used by
 * MenuContentBase instead of FocusScope's previouslyFocused, which is captured at mount and races
 * ts/menu.js's own opening focus - so it can latch onto an item that then unmounts, dropping focus
 * to &lt;body&gt;. A no-op if the trigger has left the DOM (the whole menu tore down), so a parent's
 * own restore wins.
 *
 * <code>onlyIfStranded</code> (submenus): skip the restore when focus already rests on a live element
 * - the pointer moved it to a sibling, or our ArrowLeft close already placed it on the trigger - so
 * a mouse-driven close never yanks the highlight back off where the user is now pointing. Restore
 * only when focus was orphaned to &lt;body&gt;.
 *
 * <code>within</code>: also treat focus as stranded while it still sits INSIDE the closing surface
 * (a keyboard close runs before the surface unmounts). Focus the user already moved elsewhere -
 * e.g. the dismissing click landed on a text input - is never stolen back to the trigger.
 */
export function focusTrigger(
  selector: string,
  onlyIfStranded = false,
  within: Element | null = null,
): void {
  if (onlyIfStranded) {
    const active = document.activeElement;
    const stranded =
      !active || active === document.body || (within !== null && within.contains(active));
    if (!stranded) return;
  }
  document.querySelector<HTMLElement>(selector)?.focus({ preventScroll: true });
}
