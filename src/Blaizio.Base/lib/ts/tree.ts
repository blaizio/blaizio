// Pointer drag-and-drop for a BaseTree. Same golden rule as ts/sortable.ts (Blazor owns the DOM):
// nothing in the real tree ever moves during a drag. A lightweight GHOST (a clone of the grabbed
// row) follows the pointer, the grabbed node is dimmed via data-dragging, and a JS-owned INDICATOR
// marks where the node would land - an insertion line before/after a row (indented to the target
// depth) or a box over a branch row for an "inside" drop. On release the drop is reported to C# as
// (sourceValue, targetValue, position) and the consumer applies it to its own data.
//
//   NotifyDragStart(value)                                     a drag began on the node `value`
//   NotifyHoverExpand(value)                                   hovered a collapsed branch long enough
//   NotifyMove(source, target, position, fromId, toId)         a committed drop (raised on the source)
//   NotifyReceiveMove(source, target, position, fromId, toId)  sent to the DESTINATION tree of a cross-tree drop
//   NotifyDragEnd(value)                                       the drag ended
//
// Hit-testing uses elementFromPoint each move rather than cached rects: mid-drag the tree really
// can change (hover-expanding a branch inserts rows), so live geometry is the only truth. Rows are
// identified by the data contract BaseTreeNode emits: [data-tree-row] with data-value/data-depth/
// data-branch, inside a [data-slot=tree] container. Dropping into the source node's own subtree is
// rejected by DOM containment. Trees that share a non-empty `group` register in a module-level map
// so a drag can cross between them (subject to each side's transferOut/transferIn). This module
// also suppresses the browser's default scrolling for the keys C# drives the tree with.

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

type DropPosition = 'before' | 'after' | 'inside';

interface Options {
  id: string;
  group: string | null;
  disabled: boolean;
  dragEnabled: boolean;
  delayMs: number; // hold this long before a drag can start
  hoverExpandMs: number; // hover a collapsed branch this long to expand it mid-drag
  transferIn: boolean; // nodes may arrive from another tree
  transferOut: boolean; // nodes may leave for another tree
  indicatorClass: string | null; // classes for the drop indicator (styled layer supplies them)
}

interface Point {
  x: number;
  y: number;
}

interface Pending {
  tree: Tree;
  value: string | null; // null = the tree's root level (empty tree / blank space below the rows)
  position: DropPosition;
}

const ROW = '[data-tree-row]';
const NODE = '[data-tree-node]';
const CONTAINER = '[data-slot=tree]';
// Controls that own the pointer never start a drag.
const INTERACTIVE = 'a, button, input, textarea, select, [contenteditable], [role=checkbox], [data-no-drag]';
const START_THRESHOLD = 5; // px before a press becomes a drag
const SCROLL_EDGE = 40; // px from a scrollable edge that triggers autoscroll
const SCROLL_SPEED = 12; // px per frame
const NAV_KEYS = ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', ' ', 'Spacebar'];

// group name -> live instances (cross-tree drags), and container -> instance (hit-test resolution).
const groups = new Map<string, Set<Tree>>();
const byContainer = new Map<HTMLElement, Tree>();

class Tree {
  private pressed = false;
  private dragging = false;
  private pointerId = -1;
  private downTime = 0;
  private start: Point = { x: 0, y: 0 };
  private lastPointer: Point = { x: 0, y: 0 };
  private sourceRow: HTMLElement | null = null;
  private sourceNode: HTMLElement | null = null;
  private sourceValue = '';
  private ghost: HTMLElement | null = null;
  private indicator: HTMLElement | null = null;
  private pending: Pending | null = null;
  private hoverTimer = 0;
  private hoverValue: string | null = null;
  private scrollRAF = 0;

  constructor(
    private readonly container: HTMLElement,
    private readonly ref: DotNetObjectReference,
    private readonly options: Options,
  ) {
    container.addEventListener('pointerdown', this.onPointerDown);
    container.addEventListener('keydown', this.onKeyDown);
    byContainer.set(container, this);
    if (options.group) {
      let set = groups.get(options.group);
      if (!set) groups.set(options.group, (set = new Set()));
      set.add(this);
    }
  }

  // Suppress the browser's default handling (scrolling, select-all) for the keys C# drives.
  private onKeyDown = (e: KeyboardEvent): void => {
    const target = e.target as HTMLElement | null;
    if (!target?.closest(NODE)) return;
    if (target.closest('input, textarea, [contenteditable]')) return; // the rename input keeps its keys
    if (NAV_KEYS.includes(e.key)) e.preventDefault();
    if ((e.ctrlKey || e.metaKey) && (e.key === 'a' || e.key === 'A')) {
      if (this.container.getAttribute('aria-multiselectable') === 'true') e.preventDefault();
    }
  };

  private onPointerDown = (e: PointerEvent): void => {
    if (this.options.disabled || !this.options.dragEnabled || e.button !== 0) return;
    const target = e.target as HTMLElement | null;
    if (!target || target.closest(INTERACTIVE)) return;
    const row = target.closest<HTMLElement>(ROW);
    if (!row || row.closest(CONTAINER) !== this.container) return;
    const node = row.closest<HTMLElement>(NODE);
    if (!node || row.hasAttribute('data-disabled') || row.hasAttribute('data-drag-disabled')) return;

    this.pressed = true;
    this.pointerId = e.pointerId;
    this.downTime = e.timeStamp;
    this.sourceRow = row;
    this.sourceNode = node;
    this.sourceValue = row.getAttribute('data-value') ?? '';
    this.start = { x: e.clientX, y: e.clientY };
    this.lastPointer = { ...this.start };
    window.addEventListener('pointermove', this.onPointerMove, { passive: false });
    window.addEventListener('pointerup', this.onPointerUp);
    window.addEventListener('pointercancel', this.onPointerUp);
  };

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

  private begin(): void {
    const row = this.sourceRow!;
    this.dragging = true;

    // The ghost: a frozen clone of the row that follows the pointer (never part of Blazor's DOM).
    const rect = row.getBoundingClientRect();
    const ghost = row.cloneNode(true) as HTMLElement;
    ghost.setAttribute('data-slot', 'tree-ghost');
    ghost.removeAttribute('id');
    ghost.style.position = 'fixed';
    ghost.style.left = `${rect.left}px`;
    ghost.style.top = `${rect.top}px`;
    ghost.style.width = `${rect.width}px`;
    ghost.style.height = `${rect.height}px`;
    ghost.style.margin = '0';
    ghost.style.pointerEvents = 'none';
    ghost.style.zIndex = '999';
    ghost.style.opacity = '0.9';
    document.body.appendChild(ghost);
    this.ghost = ghost;

    this.sourceNode!.setAttribute('data-dragging', 'true');
    document.body.style.userSelect = 'none';
    window.addEventListener('keydown', this.onEscape, true);
    void this.ref.invokeMethodAsync('NotifyDragStart', this.sourceValue);
  }

  private onEscape = (e: KeyboardEvent): void => {
    if (e.key !== 'Escape' || !this.dragging) return;
    e.stopPropagation();
    this.pending = null;
    this.finish(true);
  };

  // One drag frame: move the ghost, resolve the drop target under the pointer, paint the indicator.
  private track(p: Point): void {
    this.ghost!.style.transform = `translate(${p.x - this.start.x}px, ${p.y - this.start.y}px)`;

    const pending = this.resolve(p);
    this.pending = pending;
    this.updateHoverExpand(pending);
    this.paintIndicator(pending);
  }

  // The drop target under the pointer, or null when there is none (drop would be a no-op).
  private resolve(p: Point): Pending | null {
    const hit = document.elementFromPoint(p.x, p.y) as HTMLElement | null;
    if (!hit) return null;

    const row = hit.closest<HTMLElement>(ROW);
    const containerEl = (row ?? hit).closest<HTMLElement>(CONTAINER);
    if (!containerEl) return null;
    const tree = byContainer.get(containerEl);
    if (!tree || !this.canDropInto(tree)) return null;

    if (!row || row.closest(CONTAINER) !== containerEl) {
      // Blank space in a tree: land at the end of its root level.
      return this.rootDrop(tree, p);
    }

    // Never onto itself or into its own subtree (that would orphan the node).
    if (tree === this && (row === this.sourceRow || this.sourceNode!.contains(row))) return null;

    const rect = row.getBoundingClientRect();
    const rel = rect.height > 0 ? (p.y - rect.top) / rect.height : 0.5;
    const isBranch = row.hasAttribute('data-branch');
    const position: DropPosition = isBranch
      ? rel < 0.25 ? 'before' : rel > 0.75 ? 'after' : 'inside'
      : rel < 0.5 ? 'before' : 'after';

    return { tree, value: row.getAttribute('data-value'), position };
  }

  private canDropInto(tree: Tree): boolean {
    if (tree === this) return true;
    return (
      !!this.options.group &&
      tree.options.group === this.options.group &&
      this.options.transferOut &&
      tree.options.transferIn &&
      !tree.options.disabled
    );
  }

  // A drop on a tree's blank space: after its last root-level row (or straight inside when empty).
  private rootDrop(tree: Tree, p: Point): Pending | null {
    const rows = tree.rows();
    if (rows.length === 0) return { tree, value: null, position: 'inside' };

    const last = rows[rows.length - 1];
    if (p.y <= last.getBoundingClientRect().bottom) return null; // between rows but not on one (padding/gap)
    for (let i = rows.length - 1; i >= 0; i--) {
      if (rows[i].getAttribute('data-depth') === '0') {
        const row = rows[i];
        if (tree === this && (row === this.sourceRow || this.sourceNode!.contains(row))) return { tree, value: null, position: 'inside' };
        return { tree, value: row.getAttribute('data-value'), position: 'after' };
      }
    }
    return { tree, value: null, position: 'inside' };
  }

  private rows(): HTMLElement[] {
    return Array.from(this.container.querySelectorAll<HTMLElement>(ROW)).filter(
      (el) => el.closest(CONTAINER) === this.container,
    );
  }

  // Hovering the middle of a collapsed branch for a while expands it (C# owns the state).
  private updateHoverExpand(pending: Pending | null): void {
    const wants =
      pending !== null &&
      pending.position === 'inside' &&
      pending.value !== null &&
      this.options.hoverExpandMs > 0;
    const value = wants ? pending!.value : null;
    if (value === this.hoverValue) return;

    clearTimeout(this.hoverTimer);
    this.hoverValue = value;
    if (value === null) return;

    const tree = pending!.tree;
    const row = tree.rowByValue(value);
    const node = row?.closest<HTMLElement>(NODE);
    if (!node || node.getAttribute('aria-expanded') !== 'false') return; // already open (or a leaf)

    this.hoverTimer = window.setTimeout(() => {
      void tree.ref.invokeMethodAsync('NotifyHoverExpand', value);
    }, this.options.hoverExpandMs);
  }

  private rowByValue(value: string): HTMLElement | null {
    return this.rows().find((r) => r.getAttribute('data-value') === value) ?? null;
  }

  // Paint the drop marker: a line at the insertion boundary (indented to the sibling depth), or a
  // box over the whole row for an "inside" drop. JS owns this element entirely.
  private paintIndicator(pending: Pending | null): void {
    if (!pending) {
      this.indicator?.remove();
      this.indicator = null;
      return;
    }

    let el = this.indicator;
    if (!el) {
      el = document.createElement('div');
      el.setAttribute('data-slot', 'tree-drop-indicator');
      el.style.position = 'fixed';
      el.style.pointerEvents = 'none';
      el.style.boxSizing = 'border-box';
      el.style.zIndex = '998'; // under the ghost (999), over everything else
      if (this.options.indicatorClass) el.className = this.options.indicatorClass;
      document.body.appendChild(el);
      this.indicator = el;
    }

    const target = pending.value === null ? null : pending.tree.rowByValue(pending.value);
    if (pending.value !== null && !target) return;

    let rect: { left: number; top: number; width: number; height: number };
    let variant: 'line' | 'inside';
    if (pending.value === null || pending.position === 'inside') {
      if (target) {
        const r = target.getBoundingClientRect();
        rect = { left: r.left, top: r.top, width: r.width, height: r.height };
      } else {
        const r = pending.tree.container.getBoundingClientRect();
        rect = { left: r.left, top: r.top, width: r.width, height: r.height };
      }
      variant = 'inside';
    } else {
      // Rows are indented by their own depth (margin), so the row rect IS the sibling span.
      const r = target!.getBoundingClientRect();
      const y = pending.position === 'before' ? r.top - 1.5 : r.bottom - 1.5;
      rect = { left: r.left, top: y, width: r.width, height: 3 };
      variant = 'line';
    }

    el.setAttribute('data-variant', variant);
    if (!this.options.indicatorClass) {
      // Headless fallback so the marker is visible with no styled layer at all.
      if (variant === 'line') {
        el.style.border = 'none';
        el.style.background = 'rgba(120, 120, 120, 0.7)';
        el.style.borderRadius = '2px';
      } else {
        el.style.border = '2px solid rgba(120, 120, 120, 0.6)';
        el.style.background = 'rgba(120, 120, 120, 0.08)';
        el.style.borderRadius = '6px';
      }
    }
    el.style.left = `${rect.left}px`;
    el.style.top = `${rect.top}px`;
    el.style.width = `${rect.width}px`;
    el.style.height = `${rect.height}px`;
  }

  private onPointerUp = (e: PointerEvent): void => {
    if (e.pointerId !== this.pointerId) return;
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);
    if (!this.pressed) return;
    this.pressed = false;

    if (!this.dragging) {
      this.sourceRow = null;
      this.sourceNode = null;
      return;
    }

    this.finish(false);
  };

  private finish(cancelled: boolean): void {
    this.dragging = false;
    this.pressed = false;
    cancelAnimationFrame(this.scrollRAF);
    this.scrollRAF = 0;
    clearTimeout(this.hoverTimer);
    this.hoverValue = null;
    window.removeEventListener('keydown', this.onEscape, true);
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);

    this.ghost?.remove();
    this.ghost = null;
    this.indicator?.remove();
    this.indicator = null;
    this.sourceNode?.removeAttribute('data-dragging');
    document.body.style.userSelect = '';

    const drop = cancelled ? null : this.pending;
    this.pending = null;
    const value = this.sourceValue;
    this.sourceRow = null;
    this.sourceNode = null;

    void this.ref.invokeMethodAsync('NotifyDragEnd', value);
    if (!drop) return;

    void this.ref.invokeMethodAsync(
      'NotifyMove', value, drop.value, drop.position, this.options.id, drop.tree.options.id);
    if (drop.tree !== this) {
      // The receiving tree gets its own notification (its handler may live elsewhere entirely).
      void drop.tree.ref.invokeMethodAsync(
        'NotifyReceiveMove', value, drop.value, drop.position, this.options.id, drop.tree.options.id);
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
          scroller.scrollBy(dx, dy);
          this.track(p); // live hit-testing: just re-resolve under the (stationary) pointer
        }
      }
      this.scrollRAF = requestAnimationFrame(tick);
    };
    this.scrollRAF = requestAnimationFrame(tick);
  }

  private scrollable(): Element | null {
    // Prefer the tree under the pointer (a cross-tree drag should scroll the destination).
    let node: HTMLElement | null = this.pending?.tree.container ?? this.container;
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
    this.sourceRow = null;
    this.sourceNode = null;
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);
  }

  update(next: Partial<Pick<Options, 'disabled' | 'dragEnabled'>>): void {
    if (typeof next.disabled === 'boolean') this.options.disabled = next.disabled;
    if (typeof next.dragEnabled === 'boolean') this.options.dragEnabled = next.dragEnabled;
  }

  dispose(): void {
    this.container.removeEventListener('pointerdown', this.onPointerDown);
    this.container.removeEventListener('keydown', this.onKeyDown);
    window.removeEventListener('keydown', this.onEscape, true);
    window.removeEventListener('pointermove', this.onPointerMove);
    window.removeEventListener('pointerup', this.onPointerUp);
    window.removeEventListener('pointercancel', this.onPointerUp);
    cancelAnimationFrame(this.scrollRAF);
    clearTimeout(this.hoverTimer);
    this.ghost?.remove();
    this.indicator?.remove();
    document.body.style.userSelect = '';
    byContainer.delete(this.container);
    if (this.options.group) groups.get(this.options.group)?.delete(this);
  }
}

/** Wires pointer drag-and-drop (and key-scroll suppression) onto a BaseTree container. */
export function createTree(
  container: HTMLElement,
  ref: DotNetObjectReference,
  options: Options,
): Tree {
  return new Tree(container, ref, options);
}
