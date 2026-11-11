// Heading discovery + scrollspy for a table-of-contents nav ("On This Page").
//
// Two-phase, like the other modules: `scan` is a pure call that collects the content's headings
// (assigning slug ids to any that lack one) and returns them for C# to render as links; `createSpy`
// then attaches to the rendered nav - it highlights the link for the section currently in view
// (data-active, pure DOM so no per-scroll interop) and smooth-scrolls on link clicks. By default
// the page scrolls with the window (the docs layout keeps side panels sticky); pass a rootSelector
// to instead track a scrollable container (a fixed-height overflow-y-auto box).
//
// Alongside data-active the spy publishes three custom properties on the nav, which the styled
// variants read and nothing else depends on: --bz-toc-indicator-y / --bz-toc-indicator-h (the
// active link's box, so one indicator element can slide between sections via a CSS transition),
// --bz-toc-links-top / --bz-toc-links-h (the links' extent, so a rail can skip a title above them),
// --bz-toc-progress (0..1 through the tracked content) and --bz-toc-index per link.

export interface TocHeading {
  id: string;
  text: string;
  level: number;
}

const slugify = (text: string): string =>
  text
    .toLowerCase()
    .trim()
    .replace(/[^\p{L}\p{N}\s-]/gu, '')
    .replace(/\s+/g, '-')
    .replace(/-+/g, '-');

/** Collects the headings under `contentSelector`, giving each a stable id when it has none. */
export function scan(contentSelector: string, headingSelector: string): TocHeading[] {
  const content = document.querySelector(contentSelector);
  if (!content) return [];

  return [...content.querySelectorAll<HTMLElement>(headingSelector)].map((heading) => {
    if (!heading.id) {
      const base = slugify(heading.textContent ?? '') || 'section';
      let id = base;
      for (let n = 2; document.getElementById(id); n++) id = `${base}-${n}`;
      heading.id = id;
    }
    return {
      id: heading.id,
      text: (heading.textContent ?? '').trim(),
      level: Number(heading.tagName[1]) || 2,
    };
  });
}

class TocSpy {
  private readonly headings: HTMLElement[];
  private active = '';

  constructor(
    private readonly nav: HTMLElement,
    contentSelector: string,
    headingSelector: string,
    private readonly topOffset: number,
    /** The scroll container to track, or null to track the window. */
    private readonly root: HTMLElement | null,
    /** Whether a link click writes the section to the URL (as `/path#id`). */
    private readonly updateHash: boolean,
  ) {
    const content = document.querySelector(contentSelector);
    this.headings = content ? [...content.querySelectorAll<HTMLElement>(headingSelector)] : [];

    // Ordinal per link, for styling that staggers off position (the marks variant's reveal), and
    // the link's full address. The links are rendered as `#id`, which the browser resolves
    // against <base href>, not against this page: a Ctrl or middle click (left to the browser),
    // "open in new tab" and "copy link" all got the site root. A plain click never used the href.
    const here = `${location.pathname}${location.search}`;
    [...this.nav.querySelectorAll<HTMLElement>('a[data-toc-id]')].forEach((link, index) => {
      link.style.setProperty('--bz-toc-index', String(index));
      const id = link.dataset.tocId;
      if (id) link.setAttribute('href', `${here}#${id}`);
    });

    // Plain passive listeners, deliberately not rAF-throttled: the list is small and rAF never
    // fires on hidden pages, which would freeze the highlight.
    (this.root ?? window).addEventListener('scroll', this.onScroll, { passive: true });
    window.addEventListener('resize', this.onResize);
    this.nav.addEventListener('click', this.onClick);
    this.update();
  }

  private onScroll = (): void => this.update();

  /** Layout moved under us: the highlight line and the indicator's geometry both need remeasuring. */
  private onResize = (): void => {
    this.update();
    this.placeIndicator();
  };

  /** The viewport line headings are measured against: the container's top, or 0 for the window. */
  private get referenceTop(): number {
    return this.root ? this.root.getBoundingClientRect().top : 0;
  }

  private get atBottom(): boolean {
    return this.root
      ? this.root.scrollTop + this.root.clientHeight >= this.root.scrollHeight - 2
      : window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2;
  }

  /** How far through the tracked content we are, 0 to 1 (0 when there is nothing to scroll). */
  private get progress(): number {
    const [scrolled, span] = this.root
      ? [this.root.scrollTop, this.root.scrollHeight - this.root.clientHeight]
      : [window.scrollY, document.documentElement.scrollHeight - window.innerHeight];
    return span > 0 ? Math.min(1, Math.max(0, scrolled / span)) : 0;
  }

  /**
   * Geometry of the active link within the nav, published as custom properties so a single
   * indicator element can slide between sections through a CSS transition (no per-link chrome).
   */
  private placeIndicator(): void {
    const links = [...this.nav.querySelectorAll<HTMLElement>('a[data-toc-id]')];
    const active = links.find((link) => link.hasAttribute('data-active'));
    if (!links.length || !active) {
      this.nav.dataset.indicator = 'false';
      return;
    }

    // Everything is measured against the nav's own top, so a title (or any other header content)
    // above the links never skews the rail: the links' extent is published separately.
    const top = this.nav.getBoundingClientRect().top - this.nav.scrollTop;
    const first = links[0].getBoundingClientRect();
    const last = links[links.length - 1].getBoundingClientRect();
    const rect = active.getBoundingClientRect();

    this.nav.style.setProperty('--bz-toc-indicator-y', `${rect.top - top}px`);
    this.nav.style.setProperty('--bz-toc-indicator-h', `${rect.height}px`);
    this.nav.style.setProperty('--bz-toc-links-top', `${first.top - top}px`);
    this.nav.style.setProperty('--bz-toc-links-h', `${last.bottom - first.top}px`);
    this.nav.dataset.indicator = 'true';
  }

  /** Active = the last heading at or above the offset line; the bottom pins the last one. */
  private update(): void {
    if (!this.headings.length) return;

    this.nav.style.setProperty('--bz-toc-progress', this.progress.toFixed(4));

    let id = this.headings[0].id;
    if (this.atBottom) {
      id = this.headings[this.headings.length - 1].id;
    } else {
      const line = this.referenceTop + this.topOffset + 1;
      for (const heading of this.headings) {
        if (heading.getBoundingClientRect().top <= line) id = heading.id;
      }
    }

    if (id !== this.active) this.setActive(id);
  }

  private setActive(id: string): void {
    this.active = id;
    let activeLink: HTMLElement | null = null;
    for (const link of this.nav.querySelectorAll<HTMLElement>('a[data-toc-id]')) {
      if (link.dataset.tocId === id) {
        link.setAttribute('data-active', 'true');
        activeLink = link;
      } else link.removeAttribute('data-active');
    }
    if (activeLink) this.reveal(activeLink);
    this.placeIndicator();
  }

  /**
   * A long list outgrows its box (the nav's own max-height, or a sticky aside with overflow-y-auto),
   * so the active link can sit below the fold of that box while the page is far down. Scroll the
   * nearest scrolling ancestor just enough to bring it back, keeping a margin so the neighbours
   * show too. Never the document: that would yank the page the reader is scrolling.
   */
  private reveal(link: HTMLElement): void {
    let box = link.parentElement;
    while (box && box !== document.body && box !== document.documentElement) {
      const overflow = getComputedStyle(box).overflowY;
      if ((overflow === 'auto' || overflow === 'scroll') && box.scrollHeight > box.clientHeight) break;
      box = box.parentElement;
    }
    if (!box || box === document.body || box === document.documentElement) return;

    // The ends pin the box to its own ends, so a title above the first link shows again.
    const links = this.nav.querySelectorAll<HTMLElement>('a[data-toc-id]');
    if (link === links[0]) {
      box.scrollTop = 0;
      return;
    }
    if (link === links[links.length - 1]) {
      box.scrollTop = box.scrollHeight;
      return;
    }

    const view = box.getBoundingClientRect();
    const rect = link.getBoundingClientRect();
    const margin = Math.min(48, box.clientHeight / 4);
    if (rect.top < view.top + margin) box.scrollTop -= view.top + margin - rect.top;
    else if (rect.bottom > view.bottom - margin) box.scrollTop += rect.bottom - (view.bottom - margin);
  }

  private onClick = (event: MouseEvent): void => {
    const link = (event.target as HTMLElement).closest<HTMLElement>('a[data-toc-id]');
    if (!link || !this.nav.contains(link)) return;
    const target = document.getElementById(link.dataset.tocId ?? '');
    if (!target) return;
    // Ctrl/⌘/Shift/Alt click means "open elsewhere" (new tab, new window, download): leave it.
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

    event.preventDefault();
    const behavior: ScrollBehavior = matchMedia('(prefers-reduced-motion: reduce)').matches
      ? 'auto'
      : 'smooth';

    if (this.root) {
      // Position of the heading within the container's scrollable content.
      const top =
        target.getBoundingClientRect().top - this.referenceTop + this.root.scrollTop - this.topOffset;
      this.root.scrollTo({ top, behavior });
    } else {
      window.scrollTo({
        top: target.getBoundingClientRect().top + window.scrollY - this.topOffset,
        behavior,
      });
    }

    if (this.updateHash) {
      // Keep the page path + query; write `/path#id`, never a bare `#id` that drops the route.
      history.replaceState(null, '', `${location.pathname}${location.search}#${target.id}`);
    }
    this.setActive(target.id);
  };

  dispose(): void {
    (this.root ?? window).removeEventListener('scroll', this.onScroll);
    window.removeEventListener('resize', this.onResize);
    this.nav.removeEventListener('click', this.onClick);
  }
}

/**
 * Attaches active-section tracking + anchor scrolling to a rendered TOC nav. Pass `rootSelector`
 * to track a scrollable container instead of the window.
 */
export function createSpy(
  nav: HTMLElement,
  contentSelector: string,
  headingSelector: string,
  topOffset: number,
  rootSelector: string | null,
  updateHash: boolean,
): TocSpy {
  const root = rootSelector ? document.querySelector<HTMLElement>(rootSelector) : null;
  return new TocSpy(nav, contentSelector, headingSelector, topOffset, root, updateHash);
}
