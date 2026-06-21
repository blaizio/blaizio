// Window row virtualizer with NO height restriction: rows live in normal document flow (the PAGE
// scrolls, not an inner fixed-height box), and only the rows whose vertical span intersects the
// viewport are mounted - the rest are stood in for by two spacer rows the host renders above and
// below. Two paths: a fast FIXED path where every row is the same estimated height (pure index math,
// scales to millions of rows), and a DYNAMIC path that measures each rendered row, keeps a running
// offset map, and anchors the viewport so a re-measure never makes the page jump. All geometry is
// read live off the root's data-attributes (like slider.ts), so a count / height / filter change
// needs no re-init - only a `refresh()`. Reports the visible [start, end) range back to C#; in
// fixed mode it passes -1 spacer sentinels and lets C# own the (testable) spacer math, in dynamic
// mode it passes the measured spacer pixels it alone can know.

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

const clamp = (value: number, min: number, max: number): number =>
  value < min ? min : value > max ? max : value;

class Virtualizer {
  private readonly viewport: HTMLElement | Window;
  private frame = 0;
  private start = -1;
  private end = -1;
  private lastTop = -1;
  private lastBottom = -1;

  // Dynamic mode only: measured[i] is row i's real pixel height once rendered + measured; offsets[i]
  // is the cumulative top of row i within the content (offsets[count] is the total scroll height).
  private measured: number[] = [];
  private offsets: number[] = [];
  private offsetsDirty = true;

  constructor(
    private readonly root: HTMLElement,
    private readonly ref: DotNetObjectReference,
  ) {
    this.viewport = this.resolveViewport();
    this.viewport.addEventListener('scroll', this.onScroll, { passive: true });
    window.addEventListener('resize', this.onScroll, { passive: true });
    this.measure();
  }

  private resolveViewport(): HTMLElement | Window {
    const selector = this.root.dataset.bzVirtualViewport;
    const el = selector ? document.querySelector<HTMLElement>(selector) : null;
    return el ?? window;
  }

  // ---- live config, read off the root each tick (a runtime change needs no re-init) ----

  private get count(): number {
    return this.intAttr('bzVirtualCount', 0);
  }

  private get rowHeight(): number {
    const h = this.numAttr('bzVirtualRowHeight', 40);
    return h > 0 ? h : 40;
  }

  private get overscan(): number {
    return Math.max(0, this.intAttr('bzVirtualOverscan', 4));
  }

  private get dynamic(): boolean {
    return this.root.dataset.bzVirtualDynamic === 'true';
  }

  private intAttr(key: string, fallback: number): number {
    const v = parseInt(this.root.dataset[key] ?? '', 10);
    return Number.isFinite(v) ? v : fallback;
  }

  private numAttr(key: string, fallback: number): number {
    const v = parseFloat(this.root.dataset[key] ?? '');
    return Number.isFinite(v) ? v : fallback;
  }

  // ---- viewport geometry, mapped into the content's row-0-relative Y space ----

  private get scrollTop(): number {
    return this.viewport === window ? window.scrollY : (this.viewport as HTMLElement).scrollTop;
  }

  private get viewportHeight(): number {
    return this.viewport === window ? window.innerHeight : (this.viewport as HTMLElement).clientHeight;
  }

  // Document-Y (or container-scroll-Y) of the content's row-0 top. The spacers live INSIDE the root,
  // so the root's own top is exactly where row 0 begins; measuring it live survives header/layout shifts.
  private contentTop(): number {
    const rect = this.root.getBoundingClientRect();
    if (this.viewport === window) return rect.top + window.scrollY;
    const host = this.viewport as HTMLElement;
    return rect.top - host.getBoundingClientRect().top + host.scrollTop;
  }

  private onScroll = (): void => {
    if (this.frame) return;
    // rAF-coalesce bursts of scroll/resize into one measure per frame.
    this.frame = requestAnimationFrame(() => {
      this.frame = 0;
      this.measure();
    });
  };

  private measure(): void {
    const count = this.count;
    if (count <= 0) {
      this.report(0, 0, 0, 0);
      return;
    }

    if (!this.dynamic) {
      this.measureFixed(count);
      return;
    }
    this.measureDynamic(count);
  }

  // Uniform-height fast path: pure index math, no per-row measurement. Spacers are left to C# (the
  // -1 sentinels) so the fixed geometry stays pure, testable C# rather than living only in JS.
  private measureFixed(count: number): void {
    const rowH = this.rowHeight;
    const overscan = this.overscan;
    const viewTop = this.scrollTop - this.contentTop();
    const viewBottom = viewTop + this.viewportHeight;

    const start = clamp(Math.floor(viewTop / rowH) - overscan, 0, count);
    const end = clamp(Math.ceil(viewBottom / rowH) + overscan, start, count);
    this.report(start, end, -1, -1);
  }

  // Measured-height path: fold in any new row measurements, rebuild the offset map (anchoring the
  // viewport so the shift never shows), then resolve the range and its real spacer pixels.
  private measureDynamic(count: number): void {
    this.syncMeasurements();
    this.rebuildOffsets(count);

    const viewTop = this.scrollTop - this.contentTop();
    const viewBottom = viewTop + this.viewportHeight;
    const overscan = this.overscan;

    const start = clamp(this.indexAtOrBefore(viewTop, count) - overscan, 0, count);
    const end = clamp(this.indexAtOrAfter(viewBottom, count) + overscan, start, count);
    this.report(start, end, this.offsets[start], this.offsets[count] - this.offsets[end]);
  }

  // Read every rendered row's real height into the cache; any change dirties the offset map.
  private syncMeasurements(): void {
    for (const row of this.root.querySelectorAll<HTMLElement>('[data-bz-virtual-index]')) {
      const i = parseInt(row.dataset.bzVirtualIndex ?? '', 10);
      if (!Number.isFinite(i)) continue;
      const h = row.getBoundingClientRect().height;
      if (h > 0 && this.measured[i] !== h) {
        this.measured[i] = h;
        this.offsetsDirty = true;
      }
    }
  }

  // Rebuild cumulative offsets (unmeasured rows fall back to the estimate). We deliberately do NOT
  // mutate the scroll position to "anchor" a row here: doing so while the user is scrolling compounds
  // with their input into a runaway self-scroll. Rows above the viewport are already measured (they were
  // measured while visible on the way down), so offsets[start] is stable and the visible band stays put.
  private rebuildOffsets(count: number): void {
    if (!this.offsetsDirty && this.offsets.length === count + 1) return;

    const rowH = this.rowHeight;
    const offsets = new Array<number>(count + 1);
    offsets[0] = 0;
    for (let i = 0; i < count; i++) offsets[i + 1] = offsets[i] + (this.measured[i] ?? rowH);
    this.offsets = offsets;
    this.offsetsDirty = false;
  }

  // Largest i with offsets[i] <= y (the first row at or straddling the viewport top).
  private indexAtOrBefore(y: number, count: number): number {
    let lo = 0;
    let hi = count;
    while (lo < hi) {
      const mid = (lo + hi + 1) >> 1;
      if (this.offsets[mid] <= y) lo = mid;
      else hi = mid - 1;
    }
    return lo;
  }

  // Smallest i with offsets[i] >= y (the first row fully past the viewport bottom = exclusive end).
  private indexAtOrAfter(y: number, count: number): number {
    let lo = 0;
    let hi = count;
    while (lo < hi) {
      const mid = (lo + hi) >> 1;
      if (this.offsets[mid] >= y) hi = mid;
      else lo = mid + 1;
    }
    return lo;
  }

  private report(start: number, end: number, paddingTop: number, paddingBottom: number): void {
    if (start === this.start && end === this.end && paddingTop === this.lastTop && paddingBottom === this.lastBottom) {
      return;
    }
    this.start = start;
    this.end = end;
    this.lastTop = paddingTop;
    this.lastBottom = paddingBottom;
    void this.ref.invokeMethodAsync('OnRangeChanged', start, end, paddingTop, paddingBottom);
  }

  // Re-evaluate after a host-driven change (sort / filter / row-count change). Stale measurements are
  // dropped - a reorder means row i is now a different item - so dynamic mode re-measures from scratch.
  refresh = (): void => {
    this.measured = [];
    this.offsets = [];
    this.offsetsDirty = true;
    this.start = this.end = this.lastTop = this.lastBottom = -1;
    this.measure();
  };

  dispose = (): void => {
    if (this.frame) cancelAnimationFrame(this.frame);
    this.viewport.removeEventListener('scroll', this.onScroll);
    window.removeEventListener('resize', this.onScroll);
  };
}

/** Attaches a window virtualizer to the content root (the element holding the spacer + visible rows). */
export function createVirtualizer(root: HTMLElement, ref: DotNetObjectReference): Virtualizer {
  return new Virtualizer(root, ref);
}
