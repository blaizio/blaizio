// Heading discovery + scrollspy for a table-of-contents nav ("On This Page").
//
// Two-phase, like the other modules: `scan` is a pure call that collects the content's headings
// (assigning slug ids to any that lack one) and returns them for C# to render as links; `createSpy`
// then attaches to the rendered nav - it highlights the link for the section currently in view
// (data-active, pure DOM so no per-scroll interop) and smooth-scrolls on link clicks. By default
// the page scrolls with the window (the docs layout keeps side panels sticky); pass a rootSelector
// to instead track a scrollable container (a fixed-height overflow-y-auto box).

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

    // Plain passive listeners, deliberately not rAF-throttled: the list is small and rAF never
    // fires on hidden pages, which would freeze the highlight.
    (this.root ?? window).addEventListener('scroll', this.onScroll, { passive: true });
    window.addEventListener('resize', this.onScroll);
    this.nav.addEventListener('click', this.onClick);
    this.update();
  }

  private onScroll = (): void => this.update();

  /** The viewport line headings are measured against: the container's top, or 0 for the window. */
  private get referenceTop(): number {
    return this.root ? this.root.getBoundingClientRect().top : 0;
  }

  private get atBottom(): boolean {
    return this.root
      ? this.root.scrollTop + this.root.clientHeight >= this.root.scrollHeight - 2
      : window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2;
  }

  /** Active = the last heading at or above the offset line; the bottom pins the last one. */
  private update(): void {
    if (!this.headings.length) return;

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
    for (const link of this.nav.querySelectorAll<HTMLElement>('a[data-toc-id]')) {
      if (link.dataset.tocId === id) link.setAttribute('data-active', 'true');
      else link.removeAttribute('data-active');
    }
  }

  private onClick = (event: MouseEvent): void => {
    const link = (event.target as HTMLElement).closest<HTMLElement>('a[data-toc-id]');
    if (!link || !this.nav.contains(link)) return;
    const target = document.getElementById(link.dataset.tocId ?? '');
    if (!target) return;

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
    window.removeEventListener('resize', this.onScroll);
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
