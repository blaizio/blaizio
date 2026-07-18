// Chat transcript scroller. C# renders the structure and wires this on first render; everything
// here is pure DOM - no .NET round-trips. The controller:
//   - opens at the configured edge (end for chats, start for reading a saved transcript);
//   - follows the live edge while the reader is at the bottom (autoScroll), disengaging the
//     moment they scroll up and re-engaging when they return to the bottom;
//   - anchors new turns: an appended item with data-scroll-anchor scrolls near the viewport top
//     (minus a peek of the previous turn) so the response streams into view below it;
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
    this.viewport?.addEventListener('wheel', this.onWheel, { passive: true });
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
    this.following = this.opts.autoScroll;
    this.viewport?.scrollTo({ top: this.viewport.scrollHeight, behavior: 'smooth' });
  }

  scrollToStart(): void {
    this.following = false;
    this.viewport?.scrollTo({ top: 0, behavior: 'smooth' });
  }

  scrollToMessage(id: string): void {
    const item = this.content?.querySelector<HTMLElement>(
      `[data-slot="message-scroller-item"][data-message-id="${CSS.escape(id)}"]`,
    );
    if (!item || !this.viewport) return;
    this.following = false;
    this.viewport.scrollTo({
      top: Math.max(0, item.offsetTop - this.opts.anchorPeek),
      behavior: 'smooth',
    });
  }

  dispose(): void {
    this.viewport?.removeEventListener('scroll', this.onScroll);
    this.viewport?.removeEventListener('wheel', this.onWheel);
    this.root.removeEventListener('click', this.onClick);
    this.mo.disconnect();
    this.ro.disconnect();
  }

  // MARK: internals

  private distanceFromEnd(): number {
    const vp = this.viewport;
    return vp ? vp.scrollHeight - vp.clientHeight - vp.scrollTop : 0;
  }

  private stickToEnd(): void {
    const vp = this.viewport;
    if (vp) vp.scrollTop = vp.scrollHeight;
  }

  private rememberFirst(): void {
    this.firstItem =
      this.content?.querySelector('[data-slot="message-scroller-item"]') ?? null;
    this.firstItemTop = (this.firstItem as HTMLElement | null)?.offsetTop ?? 0;
  }

  private onScroll = (): void => {
    // Reaching the bottom re-engages follow; being anywhere else disengages it. Programmatic
    // stick-to-end lands exactly at the bottom, so streaming never disengages itself. update()
    // is cheap (a handful of attribute writes), so no rAF throttle - it must also run in
    // environments whose animation clock is suspended.
    this.following = this.opts.autoScroll && this.distanceFromEnd() <= ENGAGE_PX;
    this.update();
  };

  private onWheel = (e: WheelEvent): void => {
    if (e.deltaY < 0) this.following = false;
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
      // never jumps.
      this.following = false;
      this.anchored = anchor;
      this.maintainReservedSpace();
      vp.scrollTo({
        top: Math.max(0, anchor.offsetTop - this.opts.anchorPeek),
        behavior: 'smooth',
      });
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
      this.anchored = null;
      return;
    }
    const currentPad = parseFloat(content.style.paddingBottom) || 0;
    const below = content.scrollHeight - currentPad - this.anchored.offsetTop;
    const pad = Math.max(0, vp.clientHeight - this.opts.anchorPeek - below);
    if (pad !== currentPad) content.style.paddingBottom = pad > 0 ? `${pad}px` : '';
    if (pad === 0) this.anchored = null;
  }

  // Reflect the live metrics onto the DOM: button visibility and the autoscrolling flag.
  private update(): void {
    const vp = this.viewport;
    if (!vp) return;
    vp.setAttribute('data-autoscrolling', this.following ? 'true' : 'false');
    const start = vp.scrollTop > BUTTON_PX;
    const end = this.distanceFromEnd() > BUTTON_PX;
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
