// Chat transcript scroller. C# renders the structure and wires this on first render; everything
// here is pure DOM - no .NET round-trips. The controller:
//   - opens at the configured edge (end for chats, start for reading a saved transcript);
//   - follows the live edge while the reader is at the bottom (autoScroll), disengaging the
//     moment they scroll up and re-engaging when they return to the bottom;
//   - anchors new turns: an appended item with data-scroll-anchor scrolls near the viewport top
//     (minus a peek of the previous turn), the response streams into the space reserved below
//     it, and once the response passes the fold the view follows it like any stream;
//   - preserves the reader's position when older history is prepended above;
//   - drives the scroll buttons' data-active from the live metrics and handles their clicks.
//
// Expected structure under the root element:
//   [data-slot=message-scroller]                    <- the root passed to createMessageScroller
//     [data-slot=message-scroller-viewport]         <- the scroller (overflow-y-auto)
//       [data-slot=message-scroller-content]        <- the transcript column
//         [data-slot=message-scroller-item]*        <- one wrapper per row (data-message-id,
//                                                      optional data-scroll-anchor)
//     [data-slot=message-scroller-button][data-direction=start|end]  <- optional controls

const ENGAGE_PX = 2; // at-bottom tolerance - sub-pixel scroll rounding
const BUTTON_PX = 48; // how far from an edge before that edge's button appears
const SETTLE_MAX_MS = 1000; // longest an engine-initiated smooth scroll is waited for

interface Options {
  autoScroll: boolean;
  defaultPosition: string; // 'end' | 'start'
  anchorPeek: number; // px of the previous turn kept visible above an anchored one
}

class MessageScrollerController {
  private readonly viewport: HTMLElement | null;
  private readonly content: HTMLElement | null;
  private following: boolean;
  private firstItem: Element | null = null;
  private firstItemTop = 0;
  private anchored: HTMLElement | null = null;
  // Rows forced to render for real while a reservation is measured against them.
  private revealed: HTMLElement[] = [];
  // An engine-initiated smooth scroll in flight: where it is going and what follow should be
  // once it gets there. The scroll events it fires pass through positions that mean nothing.
  private settling: { top: number; follow: boolean } | null = null;
  private settleTimer: ReturnType<typeof setTimeout> | undefined;
  // Where the last stick-to-end put the viewport, so its own scroll event is not mistaken for
  // the reader leaving the bottom (more text can land between the write and the event).
  private stuckAt: number | null = null;
  private readonly mo: MutationObserver;
  private readonly ro: ResizeObserver;

  constructor(
    private readonly root: HTMLElement,
    private readonly opts: Options,
  ) {
    this.viewport = root.querySelector<HTMLElement>('[data-slot="message-scroller-viewport"]');
    this.content = root.querySelector<HTMLElement>('[data-slot="message-scroller-content"]');
    this.following = opts.autoScroll && opts.defaultPosition !== 'start';

    if (this.viewport && opts.defaultPosition !== 'start')
      this.viewport.scrollTop = this.viewport.scrollHeight;

    this.viewport?.addEventListener('scroll', this.onScroll, { passive: true });
    this.viewport?.addEventListener('scrollend', this.onScrollEnd);
    this.viewport?.addEventListener('wheel', this.onWheel, { passive: true });
    this.viewport?.addEventListener('pointerdown', this.onUserScroll, { passive: true });
    this.viewport?.addEventListener('keydown', this.onUserScroll);
    root.addEventListener('click', this.onClick);

    // childList: appends (follow / anchor) and prepends (preserve position).
    this.mo = new MutationObserver((muts) => this.onMutations(muts));
    if (this.content) this.mo.observe(this.content, { childList: true });

    // Streamed text grows the content without childList changes - keep hugging the bottom (or
    // shrink an anchored turn's reserved space as the response fills it).
    this.ro = new ResizeObserver(() => {
      this.maintainReservedSpace();
      if (this.following) this.stickToEnd();
      this.update();
    });
    if (this.content) this.ro.observe(this.content);

    this.rememberFirst();
    this.update();
  }

  // MARK: public surface (mirrored on the C# component)

  scrollToEnd(): void {
    const vp = this.viewport;
    if (!vp) return;
    // To the end of real content - a reservation below it is blank, not a destination.
    this.settleTo(vp.scrollHeight - this.reservedPad() - vp.clientHeight, this.opts.autoScroll);
  }

  scrollToStart(): void {
    this.settleTo(0, false);
  }

  scrollToMessage(id: string): void {
    const item = this.content?.querySelector<HTMLElement>(
      `[data-slot="message-scroller-item"][data-message-id="${CSS.escape(id)}"]`,
    );
    if (!item || !this.viewport) return;
    this.settleTo(item.offsetTop - this.opts.anchorPeek, false);
  }

  // Drop what is left of the space reserved under the anchored turn. The engine hands it back
  // as the response fills it, so this only matters for a response that ended short of the fold
  // and a consumer who wants the blank gone before the next turn takes it over. A reader parked
  // inside the pad is clamped up with it, so follow is re-read from where they land.
  releaseReservedSpace(): void {
    this.anchored = null;
    this.clearReservedSpace();
    this.following = this.opts.autoScroll && this.distanceFromEnd() <= ENGAGE_PX;
    this.update();
  }

  dispose(): void {
    this.viewport?.removeEventListener('scroll', this.onScroll);
    this.viewport?.removeEventListener('scrollend', this.onScrollEnd);
    this.viewport?.removeEventListener('wheel', this.onWheel);
    this.viewport?.removeEventListener('pointerdown', this.onUserScroll);
    this.viewport?.removeEventListener('keydown', this.onUserScroll);
    this.root.removeEventListener('click', this.onClick);
    this.mo.disconnect();
    this.ro.disconnect();
    clearTimeout(this.settleTimer);
  }

  // MARK: internals

  // Distance from the viewport's bottom edge to the end of REAL content. The space reserved
  // under an anchored turn is blank, so it never counts: a reader with the latest line on screen
  // is at the live edge even with a viewport of reservation below them.
  private distanceFromEnd(at?: number): number {
    const vp = this.viewport;
    return vp ? vp.scrollHeight - this.reservedPad() - vp.clientHeight - (at ?? vp.scrollTop) : 0;
  }

  // Padding currently reserved under an anchored turn - blank, not content.
  private reservedPad(): number {
    return parseFloat(this.content?.style.paddingBottom ?? '') || 0;
  }

  // Keep the end of real content at the bottom. While a reservation still holds, the anchor
  // scroll owns the position: the response is guaranteed to fit above the fold until the pad
  // reaches zero, and an instant scrollTop write here would cancel that smooth scroll. The pad
  // hits zero exactly when the response reaches the fold, so following resumes without a jump.
  private stickToEnd(): void {
    const vp = this.viewport;
    if (!vp || this.reservedPad() > 0 || this.settling) return;
    vp.scrollTop = vp.scrollHeight;
    this.stuckAt = vp.scrollTop;
  }

  // Every smooth scroll the engine starts goes through here. The browser animates it over
  // several frames and fires a scroll event per frame, and at each of those positions the live
  // metrics say "not at the end": read naively they switch follow off, pull the scrollbar back,
  // and flash the end button, all to be undone on arrival. So while one is in flight follow is
  // whatever the caller meant, the buttons are judged at the destination, and scroll events are
  // only watched for arrival. scrollend, a user gesture, or the timeout end it too.
  private settleTo(top: number, follow: boolean): void {
    const vp = this.viewport;
    if (!vp) return;
    const target = Math.max(0, Math.min(top, vp.scrollHeight - vp.clientHeight));
    this.following = follow;
    this.settling = { top: target, follow };
    clearTimeout(this.settleTimer);
    this.settleTimer = setTimeout(() => this.settled(), SETTLE_MAX_MS);
    vp.scrollTo({ top: target, behavior: 'smooth' });
    this.update();
  }

  // Arrived (or waited long enough): apply the intent and go back to live metrics. Content
  // that grew during the trip is caught up with here if the intent was to follow.
  private settled(): void {
    const s = this.settling;
    if (!s) return;
    clearTimeout(this.settleTimer);
    this.settling = null;
    this.following = s.follow;
    if (this.following) this.stickToEnd();
    this.update();
  }

  // The reader took over mid-flight: the browser has dropped the smooth scroll, so drop the
  // intent with it and let their own scrolling decide.
  private abandonSettle(): void {
    if (!this.settling) return;
    clearTimeout(this.settleTimer);
    this.settling = null;
  }

  private rememberFirst(): void {
    this.firstItem =
      this.content?.querySelector('[data-slot="message-scroller-item"]') ?? null;
    this.firstItemTop = (this.firstItem as HTMLElement | null)?.offsetTop ?? 0;
  }

  private onScroll = (): void => {
    const vp = this.viewport;
    if (!vp) return;
    if (this.settling) {
      // In flight: only arrival matters.
      if (Math.abs(vp.scrollTop - this.settling.top) <= ENGAGE_PX) this.settled();
      return;
    }
    if (this.stuckAt !== null && Math.abs(vp.scrollTop - this.stuckAt) <= ENGAGE_PX) {
      // The echo of our own stick-to-end. Text that landed since may put the end below the
      // fold again, but that is the next resize's job, not a reader scrolling away.
      this.stuckAt = null;
      this.update();
      return;
    }
    this.stuckAt = null;
    // Reaching the bottom re-engages follow; being anywhere else disengages it. Programmatic
    // stick-to-end lands exactly at the bottom, so streaming never disengages itself. update()
    // is cheap (a handful of attribute writes), so no rAF throttle - it must also run in
    // environments whose animation clock is suspended.
    this.following = this.opts.autoScroll && this.distanceFromEnd() <= ENGAGE_PX;
    this.update();
  };

  private onScrollEnd = (): void => {
    if (this.settling) this.settled();
  };

  private onWheel = (e: WheelEvent): void => {
    this.abandonSettle();
    if (e.deltaY < 0) this.following = false;
  };

  private onUserScroll = (): void => {
    this.abandonSettle();
  };

  private onClick = (e: MouseEvent): void => {
    const button = (e.target as Element | null)?.closest<HTMLElement>(
      '[data-slot="message-scroller-button"]',
    );
    if (!button || !this.root.contains(button)) return;
    if (button.getAttribute('data-direction') === 'start') this.scrollToStart();
    else this.scrollToEnd();
  };

  private onMutations(muts: MutationRecord[]): void {
    const prevFirst = this.firstItem;
    const prevFirstTop = this.firstItemTop;
    let prepended = false;
    let anchor: HTMLElement | null = null;
    let appended = false;

    for (const m of muts) {
      for (const node of m.addedNodes) {
        if (!(node instanceof HTMLElement) || node.getAttribute('data-slot') !== 'message-scroller-item')
          continue;
        if (
          prevFirst &&
          prevFirst.isConnected &&
          node.compareDocumentPosition(prevFirst) & Node.DOCUMENT_POSITION_FOLLOWING
        ) {
          prepended = true;
        } else {
          appended = true;
          if (node.getAttribute('data-scroll-anchor') === 'true') anchor = node;
        }
      }
    }

    const vp = this.viewport;
    if (vp && prepended) {
      if (this.following) this.stickToEnd();
      else if (prevFirst?.isConnected)
        // Keep the row the reader was on where it was: shift by how far the old first item moved.
        vp.scrollTop += (prevFirst as HTMLElement).offsetTop - prevFirstTop;
    }

    if (vp && anchor && this.following) {
      // A new turn while at the live edge: reserve a viewport of space below it, put it near the
      // top with a peek of the previous turn above, and let the response stream into the space.
      // The reservation (content padding) shrinks as real content fills it, so the total height
      // never jumps. Follow stays on - the latest line is on screen throughout - and the
      // stream is picked up at the fold the moment the reservation is used up.
      this.anchored = anchor;
      this.maintainReservedSpace();
      this.settleTo(anchor.offsetTop - this.opts.anchorPeek, true);
    } else if (appended && this.following) {
      this.stickToEnd();
    }

    this.rememberFirst();
    this.update();
  }

  // While a turn is anchored, pad the content so the anchor can sit near the viewport top even
  // before the response exists; shrink the pad 1:1 as content grows below the anchor.
  private maintainReservedSpace(): void {
    const vp = this.viewport;
    const content = this.content;
    if (!vp || !content) return;
    if (!this.anchored?.isConnected) {
      // The anchored row left the DOM (cleared transcript, deleted turn, re-keyed render): its
      // pad goes with it, or the blank outlives the turn it was reserved for.
      if (this.anchored) this.clearReservedSpace();
      this.anchored = null;
      return;
    }
    this.revealFrom(this.anchored);
    const currentPad = this.reservedPad();
    // Measured from the rows, not content.scrollHeight: the column has a min-height of the
    // viewport, so while the rows plus the pad are shorter than that the scroll height sits on
    // the floor and does not move with the pad - a pad derived from it lands one step per
    // resize instead of at once, and the anchor scroll issued in between is clamped to nothing.
    const last = content.lastElementChild as HTMLElement | null;
    const end = last
      ? last.offsetTop + last.offsetHeight
      : this.anchored.offsetTop + this.anchored.offsetHeight;
    const below = end - this.anchored.offsetTop;
    const pad = Math.max(0, vp.clientHeight - this.opts.anchorPeek - below);
    if (pad !== currentPad) content.style.paddingBottom = pad > 0 ? `${pad}px` : '';
    if (pad === 0) {
      this.anchored = null;
      this.unreveal();
    }
  }

  // Rows carrying content-visibility:auto measure as their placeholder (contain-intrinsic-size)
  // from insertion until the browser's next visibility pass, one frame later - so a reservation
  // measured at insert time against a 10rem stand-in comes out wrong, usually zero. The anchored
  // turn and everything under it are on screen by construction: render them for real while the
  // reservation is being measured against them, and hand them back to auto when it is done.
  private revealFrom(anchor: HTMLElement): void {
    for (let el: Element | null = anchor; el; el = el.nextElementSibling) {
      const row = el as HTMLElement;
      if (row.style.contentVisibility === 'visible') continue;
      row.style.contentVisibility = 'visible';
      this.revealed.push(row);
    }
  }

  private unreveal(): void {
    for (const row of this.revealed) row.style.contentVisibility = '';
    this.revealed = [];
  }

  private clearReservedSpace(): void {
    if (this.content) this.content.style.paddingBottom = '';
    this.unreveal();
  }

  // Reflect the live metrics onto the DOM: button visibility.
  private update(): void {
    const vp = this.viewport;
    if (!vp) return;
    // Mid-flight the destination is the truth, not wherever the animation is this frame.
    const at = this.settling?.top ?? vp.scrollTop;
    const start = at > BUTTON_PX;
    const end = this.distanceFromEnd(at) > BUTTON_PX;
    for (const button of this.root.querySelectorAll<HTMLElement>(
      '[data-slot="message-scroller-button"]',
    )) {
      const active = button.getAttribute('data-direction') === 'start' ? start : end;
      button.setAttribute('data-active', active ? 'true' : 'false');
    }
  }
}

/** Wires the transcript scroller behavior onto a message-scroller root. */
export function createMessageScroller(
  root: HTMLElement,
  opts: Partial<Options> | null,
): { dispose(): void } {
  if (!(root instanceof HTMLElement)) return { dispose() {} };
  return new MessageScrollerController(root, {
    autoScroll: opts?.autoScroll ?? true,
    defaultPosition: opts?.defaultPosition ?? 'end',
    anchorPeek: opts?.anchorPeek ?? 16,
  });
}
