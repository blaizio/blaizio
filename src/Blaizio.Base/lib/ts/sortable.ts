// Pointer drag-and-drop for a BaseSortable list. The golden rule (Blazor owns the DOM): this module
// NEVER reorders real DOM nodes - it moves them with CSS transforms only, computes the resulting
// index, and reports it to C# as a move. C# updates its data and re-renders in the new order; a
// MutationObserver watches for that patch and clears the transforms in the same frame (before paint),
// so the visual arrangement hands over seamlessly. Mirrors ts/resizable.ts / ts/drawerDrag.ts
// (JS owns the motion, C# owns the meaning).
//
//   NotifyDragStart(index)                        a drag began on the item at `index`
//   NotifyReorder(from, to, fromId, toId, swap)   a committed move; `to` is the destination slot
//   NotifyReceive(from, to, fromId, toId, swap)   sent to the DESTINATION list of a cross-list move
//   NotifyDragEnd(index)                          the drag ended
//
// SMOOTHNESS: every item's slot rect is measured ONCE at drag start (and shifted arithmetically when
// we autoscroll) - hit-testing never reads live geometry mid-drag, so there is no layout feedback loop
// and no per-move reflow. The drop target is computed from the DRAGGED CARD's centre (its slot centre
// plus the pointer delta), not the raw pointer, so where you grip the card doesn't skew the result.
// Transform writes are diffed - an item's style is only touched when its target slot actually changes.
//
// VARIABLE SIZES: for a vertical/horizontal list the preview layout is computed by WALKING the new
// sequence and stacking each item's own measured size (plus the list's gap) - not by swapping items
// between fixed slots - so a wide chip dropped before a narrow one lays out exactly, no overlap. A
// grid keeps the slot-permutation model (grids are uniform by nature; a walk can't predict row wraps).
//
// Two strategies: 'sort' (items shift to open a gap) and 'swap' (the dragged item and the nearest
// other item trade places). Lists that share a non-empty `group` register in a module-level map, so a
// drag can cross from one into another (subject to each side's transferOut/transferIn), and the
// resulting move carries both container ids.

import { invokeDotNet } from './core';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

type Strategy = 'sort' | 'swap';
type Orientation = 'vertical' | 'horizontal' | 'grid';
type SwapMode = 'swap' | 'drop';
type Animation = 'dynamic' | 'spring' | 'none';

interface Options {
  id: string;
  strategy: Strategy;
  orientation: Orientation;
  group: string | null;
  animationMs: number;
  disabled: boolean;
  sort: boolean; // reorder within this list (SortableJS `sort`)
  transferOut: boolean; // items may leave for another list (SortableJS `group.pull`)
  transferIn: boolean; // items may arrive from another list (SortableJS `group.put`)
  delayMs: number; // hold this long before a drag can start (SortableJS `delay`)
  lockAxis: boolean; // constrain the dragged item to the list's axis (Swapy `dragAxis`)
  swapMode: SwapMode; // 'swap' previews the move live; 'drop' holds still until release (Swapy `swapMode`)
  placeholder: boolean; // show a marker where the item will land
  placeholderClass: string | null; // classes for the marker (styled layer supplies them)
  animation: Animation; // motion preset; 'none' (and prefers-reduced-motion) disables all gliding
}

interface Point {
  x: number;
  y: number;
}

// A slot rect snapshot (plain object so we can shift it when autoscroll moves the content).
interface Rect {
  left: number;
  top: number;
  right: number;
  bottom: number;
  width: number;
  height: number;
  cx: number;
  cy: number;
}

interface DestCache {
  items: HTMLElement[];
  slots: Rect[];
  step: Point;
  gap: number;
  rtl: boolean;
  pad: { left: number; top: number; right: number }; // content inset, for landing in an empty list
}

const ITEM = '[data-sortable-item]';
const HANDLE = '[data-sortable-handle]';
const CONTAINER = '[data-slot=sortable]';
// Controls that own the pointer never start a drag (unless the press is on a handle).
const INTERACTIVE = 'a, button, input, textarea, select, [contenteditable], [data-no-drag]';
const START_THRESHOLD = 5; // px before a press becomes a drag
const SCROLL_EDGE = 40; // px from a scrollable edge that triggers autoscroll
const SCROLL_SPEED = 12; // px per frame
const PATCH_TIMEOUT = 600; // ms to wait for the Blazor re-render before force-clearing

// group name -> live instances, so a cross-list drag can find its siblings.
const registry = new Map<string, Set<Sortable>>();

// Users who ask the OS for less motion get instant snaps regardless of the animation option.
const REDUCED_MOTION = typeof matchMedia === 'function' ? matchMedia('(prefers-reduced-motion: reduce)') : null;

function toRect(r: DOMRect): Rect {
  return {
    left: r.left, top: r.top, right: r.right, bottom: r.bottom,
    width: r.width, height: r.height, cx: r.left + r.width / 2, cy: r.top + r.height / 2,
  };
}

function shiftRect(r: Rect, dx: number, dy: number): Rect {
  return {
    left: r.left + dx, top: r.top + dy, right: r.right + dx, bottom: r.bottom + dy,
    width: r.width, height: r.height, cx: r.cx + dx, cy: r.cy + dy,
  };
}

class Sortable {
  private pressed = false;
  private dragging = false;
  private pointerId = -1;
  private downTime = 0;
  private start: Point = { x: 0, y: 0 };
  private lastPointer: Point = { x: 0, y: 0 };
  // Extra displacement from autoscroll: content moved under a stationary pointer.
  private scrollOffset: Point = { x: 0, y: 0 };
  private dragEl: HTMLElement | null = null;
  private fromIndex = 0;
  private rtl = false;
  // Geometry snapshots, taken at drag start and shifted on autoscroll - never re-read mid-drag.
  private sourceItems: HTMLElement[] = [];
  private slots: Rect[] = [];
  private step: Point = { x: 0, y: 0 };
  private gap = 0;
  private containerRects = new Map<Sortable, Rect>();
  private destCache = new Map<Sortable, DestCache>();
  private pending: { container: Sortable; index: number; swap: boolean } | null = null;
  // Where the dragged item will rest after a same-list drop (feeds the drop FLIP glide).
  private dropTarget: { left: number; top: number } | null = null;
  // The JS-owned drop marker (never part of Blazor's render tree, so the differ never sees it).
  private placeholderEl: HTMLElement | null = null;
  // Marker geometry from the last layout pass: a 'gap' box at the landing spot, or (drop mode, sort)
  // a thin 'line' at the insertion boundary since nothing has moved aside.
  private phRect: { left: number; top: number; width: number; height: number; variant: 'gap' | 'line' } | null = null;
  // Measured grid preview for the current target index (see simulateGrid).
  private gridSim: { to: number; shifts: Map<HTMLElement, string>; target: Rect } | null = null;
  // el -> the transform we last wrote, so a pass only touches elements whose target changed.
  private painted = new Map<HTMLElement, string>();
  private transitioned = new Set<HTMLElement>();
  private scrollRAF = 0;
  private flushPatch: (() => void) | null = null;
  private cleanupTimer = 0;

  constructor(
    private readonly container: HTMLElement,
    private readonly ref: DotNetObjectReference,
    private readonly options: Options,
  ) {
    container.addEventListener('pointerdown', this.onPointerDown);
    container.addEventListener('keydown', this.onKeyDown);
    if (options.group) {
      let set = registry.get(options.group);
      if (!set) registry.set(options.group, (set = new Set()));
      set.add(this);
    }
  }

  // The transition to glide with, or null for instant snaps ('none', or the OS asks for less motion).
  private get easing(): string | null {
    if (this.options.animation === 'none' || REDUCED_MOTION?.matches) return null;
    if (this.options.animation === 'spring') {
      // The overshoot needs a little more time to read as a bounce.
      return `transform ${this.options.animationMs * 1.5}ms cubic-bezier(0.34, 1.56, 0.64, 1)`;
    }
    return `transform ${this.options.animationMs}ms cubic-bezier(0.2, 0, 0, 1)`;
  }

  private get vertical(): boolean {
    return this.options.orientation === 'vertical';
  }

  private get horizontal(): boolean {
    return this.options.orientation === 'horizontal';
  }

  // Own items only (a nested sortable's items belong to that nested container).
  private items(container: HTMLElement = this.container): HTMLElement[] {
    return Array.from(container.querySelectorAll<HTMLElement>(ITEM)).filter(
      (el) => el.closest(CONTAINER) === container,
    );
  }

  private groupContainers(): Sortable[] {
    if (!this.options.group) return [this];
    const set = registry.get(this.options.group);
    return set ? Array.from(set) : [this];
  }

  // Suppress the browser's default scroll for the keys C# uses to reorder (C# runs the logic).
  private onKeyDown = (e: KeyboardEvent): void => {
    const target = e.target as HTMLElement | null;
    if (!target?.closest(ITEM)) return;
    const nav = ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', ' ', 'Spacebar'];
    if (nav.includes(e.key)) e.preventDefault();
  };

  private onPointerDown = (e: PointerEvent): void => {
    if (this.options.disabled || e.button !== 0) return;
    const target = e.target as HTMLElement | null;
    if (!target) return;
    const item = target.closest<HTMLElement>(ITEM);
    if (!item || item.closest(CONTAINER) !== this.container) return;
    if (item.hasAttribute('data-sortable-disabled')) return;

    // If the item has a handle, only a press on the handle drags; otherwise avoid hijacking controls.
    const hasHandle = !!item.querySelector(HANDLE);
    if (hasHandle) {
      if (!target.closest(HANDLE)) return;
    } else if (target.closest(INTERACTIVE)) {
      return;
    }

    this.pressed = true;
    this.pointerId = e.pointerId;
    this.downTime = e.timeStamp;
    this.dragEl = item;
    this.start = { x: e.clientX, y: e.clientY };
    this.lastPointer = { ...this.start };
    window.addEventListener('pointermove', this.onPointerMove, { passive: false });
    window.addEventListener('pointerup', this.onPointerUp);
    window.addEventListener('pointercancel', this.onPointerUp);
  };

  private begin(): void {
    // Flush any still-pending drop handoff and reset its residue, so this drag measures clean geometry.
    this.flushPatch?.();
    this.clearAll();
    const el = this.dragEl!;
    this.sourceItems = this.items();
    this.fromIndex = this.sourceItems.indexOf(el);
    if (this.fromIndex < 0) { this.pressed = false; return; }

    this.dragging = true;
    this.rtl = getComputedStyle(this.container).direction === 'rtl';
    this.scrollOffset = { x: 0, y: 0 };
    this.pending = { container: this, index: this.fromIndex, swap: false };
    this.dropTarget = null;
    this.gridSim = null;

    // One measurement pass; everything after this is arithmetic.
    this.slots = this.sourceItems.map((it) => toRect(it.getBoundingClientRect()));
    this.step = measureStep(this.slots);
    this.gap = axisGap(this.slots, this.options.orientation, this.rtl);
    for (const s of this.groupContainers()) {
      // Only containers a transfer could actually land in take part in hit-testing.
      if (s !== this && !(this.options.transferOut && s.options.transferIn && !s.options.disabled)) continue;
      this.containerRects.set(s, toRect(s.container.getBoundingClientRect()));
    }

    el.style.transition = 'none';
    el.style.willChange = 'transform';
    el.style.zIndex = '999';
    el.setAttribute('data-dragging', 'true');
    this.transitioned.add(el); // so a later clearAll always finds and resets it
    document.body.style.userSelect = 'none';
    void invokeDotNet(this.ref, 'NotifyDragStart', this.fromIndex);
  }

  private onPointerMove = (e: PointerEvent): void => {
    if (!this.pressed || e.pointerId !== this.pointerId) return;
    this.lastPointer = { x: e.clientX, y: e.clientY };

    if (!this.dragging) {
      const moved = Math.hypot(e.clientX - this.start.x, e.clientY - this.start.y);
      // Hold-to-drag: moving early means a scroll/tap, not a drag - give the pointer back.
      if (this.options.delayMs > 0 && e.timeStamp - this.downTime < this.options.delayMs) {
        if (moved > START_THRESHOLD) this.abandon();
        return;
      }
      if (moved < START_THRESHOLD) return;
      this.begin();
      if (!this.dragging) return;
    }

    e.preventDefault();
    this.track(this.lastPointer);
    this.autoscroll();
  };

  // One drag frame: move the dragged card, pick the target container + slot, paint the gap.
  private track(p: Point): void {
    let dx = p.x - this.start.x + this.scrollOffset.x;
    let dy = p.y - this.start.y + this.scrollOffset.y;
    if (this.options.lockAxis) {
      if (this.vertical) dx = 0;
      else if (this.horizontal) dy = 0;
    }
    this.dragEl!.style.transform = `translate(${dx}px, ${dy}px)`;

    // Hit-test with the dragged CARD's centre, not the pointer - where you grip doesn't matter.
    const probe: Point = { x: this.slots[this.fromIndex].cx + dx, y: this.slots[this.fromIndex].cy + dy };
    const active = this.hitContainer(p);
    const desired = new Map<HTMLElement, string>();
    if (active === this) {
      if (this.options.strategy === 'swap') this.swapLayout(probe, desired);
      else this.sortLayout(probe, desired);
    } else {
      this.crossLayout(active, probe, desired);
    }
    // Drop mode: the layout pass still picks the target (pending/dropTarget/placeholder), but nothing
    // moves out of the way until release.
    if (this.options.swapMode === 'drop') desired.clear();
    this.paint(desired);
    this.updatePlaceholder();
  }

  // Same-list reorder preview. Grids permute items among their (uniform) slots; 1-D lists WALK the
  // new sequence, stacking each item's own size + the list gap, so variable sizes lay out exactly.
  private sortLayout(probe: Point, desired: Map<HTMLElement, string>): void {
    if (!this.options.sort) {
      this.pending = { container: this, index: this.fromIndex, swap: false };
      this.dropTarget = null;
      this.phRect = null;
      return;
    }
    const others = this.slots.filter((_, i) => i !== this.fromIndex);
    const to = this.insertIndex(others, probe, this.options.orientation, this.rtl);
    this.pending = { container: this, index: to, swap: false };

    const dragR = this.slots[this.fromIndex];
    if (this.options.orientation === 'grid') {
      // A grid REFLOWS on reorder (col/row spans move between tracks), so per-slot math can't place
      // anything. Measure the candidate order for real in a hidden clone instead.
      this.simulateGrid(to);
      const sim = this.gridSim;
      if (sim) {
        for (const [el, t] of sim.shifts) desired.set(el, t);
        this.dropTarget = { left: sim.target.left, top: sim.target.top };
        this.phRect = this.options.swapMode === 'drop'
          ? this.lineRect(others, to, 'grid', this.rtl, this.gap, dragR)
          : { left: sim.target.left, top: sim.target.top, width: sim.target.width, height: sim.target.height, variant: 'gap' };
      } else {
        // Simulation unavailable (clone mismatch): fall back to slot permutation - exact for uniform cells.
        for (let i = 0; i < this.sourceItems.length; i++) {
          if (i === this.fromIndex) continue;
          const posInOthers = i < this.fromIndex ? i : i - 1;
          const newSlot = posInOthers < to ? posInOthers : posInOthers + 1;
          if (newSlot !== i) desired.set(this.sourceItems[i], delta(this.slots[i], this.slots[newSlot]));
        }
        this.dropTarget = { left: this.slots[to].left, top: this.slots[to].top };
        this.phRect = this.options.swapMode === 'drop'
          ? this.lineRect(others, to, 'grid', this.rtl, this.gap, dragR)
          : { left: this.dropTarget.left, top: this.dropTarget.top, width: dragR.width, height: dragR.height, variant: 'gap' };
      }
      return;
    }

    // 1-D walk: lay the new order out along the axis from the first slot's start edge.
    const vertical = this.vertical;
    const dir = vertical || !this.rtl ? 1 : -1;
    const order: number[] = [];
    for (let i = 0; i < this.slots.length; i++) if (i !== this.fromIndex) order.push(i);
    order.splice(to, 0, this.fromIndex);

    let cursor = vertical ? this.slots[0].top : this.rtl ? this.slots[0].right : this.slots[0].left;
    for (const idx of order) {
      const r = this.slots[idx];
      const startEdge = vertical ? r.top : this.rtl ? r.right : r.left;
      const shift = cursor - startEdge;
      if (idx === this.fromIndex) {
        this.dropTarget = vertical
          ? { left: r.left, top: r.top + shift }
          : { left: r.left + shift, top: r.top }; // an RTL right-edge shift equals the left-edge shift
      } else if (Math.abs(shift) > 0.5) {
        desired.set(this.sourceItems[idx], vertical ? `translate(0px, ${shift}px)` : `translate(${shift}px, 0px)`);
      }
      cursor += dir * ((vertical ? r.height : r.width) + this.gap);
    }
    this.phRect = this.options.swapMode === 'drop'
      ? this.lineRect(others, to, this.options.orientation, this.rtl, this.gap, dragR)
      : this.dropTarget
        ? { left: this.dropTarget.left, top: this.dropTarget.top, width: dragR.width, height: dragR.height, variant: 'gap' }
        : null;
  }

  // Swap: the nearest other item slides into the dragged item's slot.
  private swapLayout(probe: Point, desired: Map<HTMLElement, string>): void {
    let best = -1;
    let bestDist = Infinity;
    for (let i = 0; i < this.slots.length; i++) {
      if (i === this.fromIndex) continue;
      const d = Math.hypot(probe.x - this.slots[i].cx, probe.y - this.slots[i].cy);
      if (d < bestDist) { bestDist = d; best = i; }
    }
    // Only arm the swap once the card has clearly left home (nearest = its own slot until then).
    const home = Math.hypot(probe.x - this.slots[this.fromIndex].cx, probe.y - this.slots[this.fromIndex].cy);
    if (best < 0 || home <= bestDist) {
      this.pending = { container: this, index: this.fromIndex, swap: false };
      this.dropTarget = { left: this.slots[this.fromIndex].left, top: this.slots[this.fromIndex].top };
      this.phRect = null; // still at home - nothing to point at
      return;
    }
    desired.set(this.sourceItems[best], delta(this.slots[best], this.slots[this.fromIndex]));
    this.pending = { container: this, index: best, swap: true };
    this.dropTarget = { left: this.slots[best].left, top: this.slots[best].top };
    // Either mode: highlight the partner's slot - that's where the dragged item will land.
    const b = this.slots[best];
    this.phRect = { left: b.left, top: b.top, width: b.width, height: b.height, variant: 'gap' };
  }

  // Cross-list: close the gap the item leaves in the source, open one (sized to the dragged item)
  // in the destination. Each side uses its OWN orientation, direction, and gap.
  private crossLayout(dest: Sortable, probe: Point, desired: Map<HTMLElement, string>): void {
    const dragR = this.slots[this.fromIndex];
    this.dropTarget = null; // cross-list drops don't FLIP (the element is recreated in the new list)

    if (this.options.orientation === 'grid') {
      // Each following item steps back into the previous (uniform) slot.
      for (let i = this.fromIndex + 1; i < this.sourceItems.length; i++) {
        desired.set(this.sourceItems[i], delta(this.slots[i], this.slots[i - 1]));
      }
    } else {
      const v = this.vertical;
      const gapSize = (v ? dragR.height : dragR.width) + this.gap;
      const shift = v
        ? `translate(0px, ${-gapSize}px)`
        : `translate(${this.rtl ? gapSize : -gapSize}px, 0px)`;
      for (let i = this.fromIndex + 1; i < this.sourceItems.length; i++) {
        desired.set(this.sourceItems[i], shift);
      }
    }

    const cache = this.ensureDest(dest);
    const index = this.insertIndex(cache.slots, probe, dest.options.orientation, cache.rtl);
    if (dest.options.orientation === 'grid') {
      for (let j = index; j < cache.items.length; j++) {
        // Step each following item into the next slot; the last one extrapolates a column onward.
        const target = j + 1 < cache.slots.length
          ? cache.slots[j + 1]
          : shiftRect(cache.slots[j], cache.rtl ? -cache.step.x : cache.step.x, 0);
        desired.set(cache.items[j], delta(cache.slots[j], target));
      }
    } else {
      const v = dest.options.orientation === 'vertical';
      const gapSize = (v ? dragR.height : dragR.width) + cache.gap;
      const shift = v
        ? `translate(0px, ${gapSize}px)`
        : `translate(${cache.rtl ? -gapSize : gapSize}px, 0px)`;
      for (let j = index; j < cache.items.length; j++) {
        desired.set(cache.items[j], shift);
      }
    }
    this.pending = { container: dest, index, swap: false };

    const landing = this.crossLanding(dest, cache, index, dragR);
    this.phRect = this.options.swapMode === 'drop' && cache.slots.length > 0
      ? this.lineRect(cache.slots, index, dest.options.orientation, cache.rtl, cache.gap, cache.slots[0])
      : landing
        ? { left: landing.left, top: landing.top, width: dragR.width, height: dragR.height, variant: 'gap' }
        : null;
  }

  // Where a cross-list drop would rest in the destination (top-left, in viewport coordinates).
  private crossLanding(
    dest: Sortable, cache: DestCache, index: number, dragR: Rect,
  ): { left: number; top: number } | null {
    const o = dest.options.orientation;
    if (cache.slots.length === 0) {
      const c = this.containerRects.get(dest);
      if (!c) return null;
      const left = o === 'horizontal' && cache.rtl ? c.right - cache.pad.right - dragR.width : c.left + cache.pad.left;
      return { left, top: c.top + cache.pad.top };
    }
    const last = cache.slots[cache.slots.length - 1];
    if (o === 'grid') {
      if (index < cache.slots.length) return { left: cache.slots[index].left, top: cache.slots[index].top };
      return { left: last.left + (cache.rtl ? -cache.step.x : cache.step.x), top: last.top };
    }
    if (o === 'vertical') {
      if (index < cache.slots.length) return { left: cache.slots[index].left, top: cache.slots[index].top };
      return { left: last.left, top: last.bottom + cache.gap };
    }
    if (index < cache.slots.length) {
      const s = cache.slots[index];
      return { left: cache.rtl ? s.right - dragR.width : s.left, top: s.top };
    }
    return { left: cache.rtl ? last.left - cache.gap - dragR.width : last.right + cache.gap, top: last.top };
  }

  // Exact preview for a same-list grid reorder. The container is cloned, the clones are reordered to
  // the candidate sequence, and the clone is laid out hidden at the container's own width - then every
  // card's TRUE landing rect is read off the clone. One extra layout per target-index change (not per
  // pointer move), and it is exact for anything CSS can lay out, including col-span/row-span cards.
  private simulateGrid(to: number): void {
    if (this.gridSim?.to === to) return;
    this.gridSim = null;

    const order: number[] = [];
    for (let i = 0; i < this.slots.length; i++) if (i !== this.fromIndex) order.push(i);
    order.splice(to, 0, this.fromIndex);

    const clone = this.container.cloneNode(true) as HTMLElement;
    const cloneItems = Array.from(clone.querySelectorAll<HTMLElement>(ITEM)).filter(
      (el) => el.closest(CONTAINER) === clone,
    );
    if (cloneItems.length !== this.sourceItems.length) return;

    for (const idx of order) {
      const c = cloneItems[idx];
      c.style.transform = 'none';
      c.style.transition = 'none';
      clone.appendChild(c); // re-appending in candidate order makes DOM order = layout order
    }

    const rect = this.containerRects.get(this) ?? toRect(this.container.getBoundingClientRect());
    clone.removeAttribute('id');
    clone.style.position = 'absolute';
    clone.style.top = '0';
    clone.style.left = '-99999px';
    clone.style.width = `${rect.width}px`;
    clone.style.height = 'auto';
    clone.style.visibility = 'hidden';
    clone.style.pointerEvents = 'none';
    clone.style.margin = '0';
    // Same parent = same inherited styles/vars, so the clone lays out exactly like the original.
    (this.container.parentElement ?? document.body).appendChild(clone);

    const cloneRect = clone.getBoundingClientRect();
    const measured = Array.from(clone.querySelectorAll<HTMLElement>(ITEM)).filter(
      (el) => el.closest(CONTAINER) === clone,
    );
    const shifts = new Map<HTMLElement, string>();
    let target: Rect = this.slots[this.fromIndex];
    for (let k = 0; k < order.length; k++) {
      const m = measured[k].getBoundingClientRect();
      // Clone-relative offset re-anchored to the real container's viewport position.
      const abs = shiftRect(toRect(m), rect.left - cloneRect.left, rect.top - cloneRect.top);
      const origIdx = order[k];
      if (origIdx === this.fromIndex) { target = abs; continue; }
      const dx = abs.left - this.slots[origIdx].left;
      const dy = abs.top - this.slots[origIdx].top;
      if (Math.abs(dx) > 0.5 || Math.abs(dy) > 0.5) {
        shifts.set(this.sourceItems[origIdx], `translate(${dx}px, ${dy}px)`);
      }
    }
    clone.remove();
    this.gridSim = { to, shifts, target };
  }

  // A thin insertion bar between rects[idx-1] and rects[idx], in the ORIGINAL (unmoved) geometry -
  // the drop-mode marker for the Sort strategy. `ref` supplies the cross-axis span.
  private lineRect(
    rects: Rect[], idx: number, orientation: Orientation, rtl: boolean, gap: number, ref: Rect,
  ): { left: number; top: number; width: number; height: number; variant: 'line' } | null {
    if (rects.length === 0) return null;
    if (orientation === 'grid') {
      const r = idx < rects.length ? rects[idx] : rects[rects.length - 1];
      const before = idx < rects.length; // bar on the leading edge of the cell it lands before, else trailing
      const x = before === !rtl ? r.left - 3 : r.right - 1;
      return { left: x, top: r.top, width: 4, height: r.height, variant: 'line' };
    }
    if (orientation === 'vertical') {
      const y = idx === 0 ? rects[0].top - gap / 2 : rects[idx - 1].bottom + gap / 2;
      return { left: ref.left, top: y - 2, width: ref.width, height: 4, variant: 'line' };
    }
    const x = idx === 0
      ? (rtl ? rects[0].right + gap / 2 : rects[0].left - gap / 2)
      : (rtl ? rects[idx - 1].left - gap / 2 : rects[idx - 1].right + gap / 2);
    return { left: x - 2, top: ref.top, width: 4, height: ref.height, variant: 'line' };
  }

  // Create/position the drop marker per the last layout pass; JS owns this element entirely.
  private updatePlaceholder(): void {
    if (!this.options.placeholder) return;
    const rect = this.dragging ? this.phRect : null;
    if (!rect) { this.removePlaceholder(); return; }
    let el = this.placeholderEl;
    if (!el) {
      el = document.createElement('div');
      el.setAttribute('data-slot', 'sortable-placeholder');
      el.style.position = 'fixed';
      el.style.pointerEvents = 'none';
      el.style.boxSizing = 'border-box';
      el.style.zIndex = '998'; // under the dragged item (999), over everything else
      if (this.options.placeholderClass) el.className = this.options.placeholderClass;
      document.body.appendChild(el);
      this.placeholderEl = el;
    }
    el.setAttribute('data-variant', rect.variant);
    if (!this.options.placeholderClass) {
      // Headless fallback so the marker is visible with no styled layer at all.
      if (rect.variant === 'line') {
        el.style.border = 'none';
        el.style.background = 'rgba(120, 120, 120, 0.6)';
        el.style.borderRadius = '2px';
      } else {
        el.style.border = '2px dashed rgba(120, 120, 120, 0.5)';
        el.style.background = 'rgba(120, 120, 120, 0.08)';
        el.style.borderRadius = '8px';
      }
    }
    el.style.left = `${rect.left}px`;
    el.style.top = `${rect.top}px`;
    el.style.width = `${rect.width}px`;
    el.style.height = `${rect.height}px`;
  }

  private removePlaceholder(): void {
    this.placeholderEl?.remove();
    this.placeholderEl = null;
  }

  // Measure a destination container once, on first entry (its items are untransformed then).
  private ensureDest(dest: Sortable): DestCache {
    let cache = this.destCache.get(dest);
    if (!cache) {
      const items = this.items(dest.container);
      const slots = items.map((it) => toRect(it.getBoundingClientRect()));
      const cs = getComputedStyle(dest.container);
      const rtl = cs.direction === 'rtl';
      this.destCache.set(dest, (cache = {
        items,
        slots,
        step: slots.length ? measureStep(slots) : this.step,
        gap: axisGap(slots, dest.options.orientation, rtl),
        rtl,
        pad: {
          left: parseFloat(cs.paddingLeft) || 0,
          top: parseFloat(cs.paddingTop) || 0,
          right: parseFloat(cs.paddingRight) || 0,
        },
      }));
    }
    return cache;
  }

  // Which group container is under the pointer (the source wins ties; cached rects, no reflow).
  private hitContainer(p: Point): Sortable {
    let hit: Sortable | null = null;
    for (const [s, r] of this.containerRects) {
      if (p.x >= r.left && p.x <= r.right && p.y >= r.top && p.y <= r.bottom) {
        if (s === this) return this;
        hit = s;
      }
    }
    return hit ?? this;
  }

  // Insertion slot (0..len) among `rects` for the probe point, per orientation.
  private insertIndex(rects: Rect[], p: Point, orientation: Orientation, rtl: boolean): number {
    if (orientation === 'horizontal') {
      let n = 0;
      // In RTL the row reads right-to-left, so an item precedes the probe when the probe is to its left.
      for (const r of rects) if (rtl ? p.x < r.cx : p.x > r.cx) n++;
      return n;
    }
    if (orientation === 'vertical') {
      let n = 0;
      for (const r of rects) if (p.y > r.cy) n++;
      return n;
    }
    // Grid (row-major): nearest rect by centre decides the row/cell; insert after it only when the
    // probe is on a LOWER row, or past its centre within the SAME row. (A centre-line heuristic here
    // would misread same-row jitter as "next row" and make left-moves impossible.)
    if (rects.length === 0) return 0;
    let best = 0;
    let bestDist = Infinity;
    for (let i = 0; i < rects.length; i++) {
      const d = Math.hypot(p.x - rects[i].cx, p.y - rects[i].cy);
      if (d < bestDist) { bestDist = d; best = i; }
    }
    const r = rects[best];
    const sameRow = p.y >= r.top && p.y <= r.bottom;
    const after = p.y > r.bottom || (sameRow && (rtl ? p.x < r.cx : p.x > r.cx));
    return after ? best + 1 : best;
  }

  // Apply the desired transform per element, touching only what changed since the last pass.
  private paint(desired: Map<HTMLElement, string>): void {
    for (const [el] of this.painted) {
      if (!desired.has(el)) el.style.transform = '';
    }
    for (const [el, t] of desired) {
      if (this.painted.get(el) === t) continue;
      if (!this.transitioned.has(el)) {
        el.style.transition = this.easing ?? 'none';
        el.style.willChange = 'transform';
        this.transitioned.add(el);
      }
      el.style.transform = t;
    }
    this.painted = desired;
  }

  private onPointerUp = (e: PointerEvent): void => {
    if (e.pointerId !== this.pointerId) return;
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);
    cancelAnimationFrame(this.scrollRAF);
    this.scrollRAF = 0;
    if (!this.pressed) return;
    this.pressed = false;

    if (!this.dragging) {
      this.dragEl = null;
      return;
    }
    this.dragging = false;

    const el = this.dragEl!;
    const drop = this.pending;
    this.pending = null;
    this.removePlaceholder();
    el.removeAttribute('data-dragging');
    void invokeDotNet(this.ref, 'NotifyDragEnd', this.fromIndex);

    const changed = drop !== null && !(drop.container === this && drop.index === this.fromIndex && !drop.swap);
    if (!changed) { this.springBack(el); return; }

    // Committed move: keep every transform in place (the shifted items ARE the new order, visually),
    // tell C#, and clear when its re-render lands. For a same-list move the dragged element survives
    // the patch (keyed), so glide it from where it was released into its new slot (a FLIP drop).
    const sameList = drop!.container === this;
    const lastRect = toRect(el.getBoundingClientRect());
    const finalRect = sameList ? this.dropTarget : null;

    this.awaitPatch([this.container, drop!.container.container], () => {
      this.clearAll(el);
      if (finalRect && el.isConnected) this.dropGlide(el, lastRect, finalRect);
      else this.resetEl(el);
    });
    void invokeDotNet(this.ref, 
      'NotifyReorder', this.fromIndex, drop!.index, this.options.id, drop!.container.options.id, drop!.swap);
    if (!sameList) {
      // The receiving list gets its own notification (its handler may live elsewhere entirely).
      void invokeDotNet(drop!.container.ref, 
        'NotifyReceive', this.fromIndex, drop!.index, this.options.id, drop!.container.options.id, drop!.swap);
    }
  };

  // After the patch the element sits naturally in its new slot; start it from where the pointer
  // dropped it and glide home.
  private dropGlide(el: HTMLElement, last: Rect, final: { left: number; top: number }): void {
    const ease = this.easing;
    if (!ease) { this.resetEl(el); return; } // no-motion: it's already in place after the patch
    el.style.transition = 'none';
    el.style.transform = `translate(${last.left - final.left}px, ${last.top - final.top}px)`;
    void el.offsetHeight; // paint the displaced state before animating home
    el.style.transition = ease;
    el.style.transform = '';
    this.cleanupTimer = window.setTimeout(() => this.resetEl(el), this.options.animationMs * 2 + 80);
  }

  // Nothing changed: glide the dragged card back to its slot, let the shifted items glide back too.
  private springBack(el: HTMLElement): void {
    const ease = this.easing;
    if (!ease) { this.paint(new Map()); this.clearAll(); return; } // instant snap home
    el.style.transition = ease;
    el.style.transform = '';
    this.paint(new Map()); // shifted items still carry their inline transition -> they glide home
    this.cleanupTimer = window.setTimeout(() => this.clearAll(), this.options.animationMs * 2 + 80);
  }

  // Run `fn` as soon as Blazor patches either container's children (a microtask before the next
  // paint - no flash), with a timeout in case the consumer never applies the change.
  private awaitPatch(containers: HTMLElement[], fn: () => void): void {
    const observer = new MutationObserver(() => flush());
    const timer = window.setTimeout(() => flush(), PATCH_TIMEOUT);
    const flush = (): void => {
      observer.disconnect();
      clearTimeout(timer);
      this.flushPatch = null;
      fn();
    };
    this.flushPatch = flush;
    const seen = new Set<HTMLElement>();
    for (const c of containers) {
      if (seen.has(c)) continue;
      seen.add(c);
      observer.observe(c, { childList: true, subtree: true });
    }
  }

  // Scroll the nearest scrollable ancestor when the pointer nears an edge (kept alive by rAF).
  private autoscroll(): void {
    if (this.scrollRAF) return;
    const tick = (): void => {
      this.scrollRAF = 0;
      if (!this.dragging) return;
      const p = this.lastPointer;
      const scroller = this.scrollable();
      if (scroller) {
        const isRoot = scroller === document.scrollingElement;
        const r = isRoot
          ? { left: 0, top: 0, right: window.innerWidth, bottom: window.innerHeight }
          : (scroller as HTMLElement).getBoundingClientRect();
        let dx = 0;
        let dy = 0;
        if (p.y - r.top < SCROLL_EDGE) dy = -SCROLL_SPEED;
        else if (r.bottom - p.y < SCROLL_EDGE) dy = SCROLL_SPEED;
        if (p.x - r.left < SCROLL_EDGE) dx = -SCROLL_SPEED;
        else if (r.right - p.x < SCROLL_EDGE) dx = SCROLL_SPEED;
        if (dx || dy) {
          const beforeX = scroller.scrollLeft;
          const beforeY = scroller.scrollTop;
          scroller.scrollBy(dx, dy);
          // Content moved under the pointer: shift every cached rect and re-track.
          const movedX = scroller.scrollLeft - beforeX;
          const movedY = scroller.scrollTop - beforeY;
          if (movedX || movedY) {
            this.scrollOffset.x += movedX;
            this.scrollOffset.y += movedY;
            this.shiftCaches(-movedX, -movedY);
            this.track(p);
          }
        }
      }
      this.scrollRAF = requestAnimationFrame(tick);
    };
    this.scrollRAF = requestAnimationFrame(tick);
  }

  private shiftCaches(dx: number, dy: number): void {
    this.slots = this.slots.map((r) => shiftRect(r, dx, dy));
    for (const [s, r] of this.containerRects) this.containerRects.set(s, shiftRect(r, dx, dy));
    for (const cache of this.destCache.values()) cache.slots = cache.slots.map((r) => shiftRect(r, dx, dy));
    if (this.dropTarget) this.dropTarget = { left: this.dropTarget.left + dx, top: this.dropTarget.top + dy };
    if (this.gridSim) this.gridSim.target = shiftRect(this.gridSim.target, dx, dy); // shifts are deltas - unaffected
  }

  private scrollable(): Element | null {
    let node: HTMLElement | null = this.container;
    while (node) {
      const style = getComputedStyle(node);
      const scrollableY = /(auto|scroll)/.test(style.overflowY) && node.scrollHeight > node.clientHeight;
      const scrollableX = /(auto|scroll)/.test(style.overflowX) && node.scrollWidth > node.clientWidth;
      if (scrollableY || scrollableX) return node;
      node = node.parentElement;
    }
    return document.scrollingElement;
  }

  // A press that turned out to be a scroll/tap (e.g. moved before the hold delay elapsed).
  private abandon(): void {
    this.pressed = false;
    this.dragEl = null;
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);
  }

  private resetEl(el: HTMLElement): void {
    el.style.transition = '';
    el.style.transform = '';
    el.style.willChange = '';
    el.style.zIndex = '';
    el.removeAttribute('data-dragging');
  }

  // Reset every element this drag touched (optionally sparing one, e.g. mid drop-glide).
  private clearAll(except?: HTMLElement): void {
    clearTimeout(this.cleanupTimer);
    this.removePlaceholder();
    for (const el of this.transitioned) if (el !== except) this.resetEl(el);
    for (const [el] of this.painted) if (el !== except) this.resetEl(el);
    this.transitioned.clear();
    this.painted = new Map();
    this.destCache.clear();
    this.containerRects.clear();
    document.body.style.userSelect = '';
  }

  // C# calls this after it re-renders. Normally the MutationObserver has already handled the patch;
  // this is the belt-and-braces path (e.g. a consumer that re-renders without changing the DOM).
  settle(): void {
    this.flushPatch?.();
  }

  setDisabled(disabled: boolean): void {
    this.options.disabled = disabled;
  }

  dispose(): void {
    this.container.removeEventListener('pointerdown', this.onPointerDown);
    this.container.removeEventListener('keydown', this.onKeyDown);
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);
    cancelAnimationFrame(this.scrollRAF);
    this.flushPatch?.();
    this.clearAll();
    if (this.options.group) registry.get(this.options.group)?.delete(this);
  }
}

function measureStep(rects: Rect[]): Point {
  if (rects.length < 2) {
    const r = rects[0];
    return r ? { x: r.width, y: r.height } : { x: 0, y: 0 };
  }
  return { x: rects[1].left - rects[0].left, y: rects[1].top - rects[0].top };
}

// The whitespace between adjacent items along the list axis (flex/grid gap, margins - whatever it is).
function axisGap(rects: Rect[], orientation: Orientation, rtl: boolean): number {
  if (rects.length < 2) return 8;
  if (orientation === 'vertical') return Math.max(0, rects[1].top - rects[0].bottom);
  if (orientation === 'horizontal') {
    return Math.max(0, rtl ? rects[0].left - rects[1].right : rects[1].left - rects[0].right);
  }
  return 0; // grid: unused (slot-permutation model)
}

function delta(from: Rect, to: Rect): string {
  return `translate(${to.left - from.left}px, ${to.top - from.top}px)`;
}

/** Wires pointer drag-and-drop onto a BaseSortable container; returns a handle with dispose()/settle(). */
export function createSortable(
  container: HTMLElement,
  ref: DotNetObjectReference,
  options: Options,
): Sortable {
  return new Sortable(container, ref, options);
}
