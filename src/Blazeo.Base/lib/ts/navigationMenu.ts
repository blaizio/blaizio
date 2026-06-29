// Measurement for BaseNavigationMenu's shared viewport + active-item indicator.
//
// The viewport is one container that morphs to the size of whichever item's content is showing; this
// module measures that content and writes --bz-nav-vw / --bz-nav-vh on the viewport (the CSS animates
// width/height between them). The indicator is an arrow that slides under the open trigger; we write
// --bz-nav-ind-left / --bz-nav-ind-width from that trigger's box. A MutationObserver (data-state +
// child swaps) and a ResizeObserver keep both in sync. Pure DOM - no .NET round-trips.

class NavMenu {
  private readonly ro: ResizeObserver;
  private readonly mo: MutationObserver;

  constructor(private readonly root: HTMLElement) {
    this.ro = new ResizeObserver(() => this.measure());
    this.mo = new MutationObserver(() => {
      this.observe();
      this.measure();
    });
    this.mo.observe(root, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-state'] });
    root.addEventListener('keydown', this.onKeyDown);
    this.observe();
    this.measure();
  }

  // The focusable top-level entries (triggers + plain links), in document order. Links inside a content
  // panel are excluded - they're not direct children of a top-level item.
  private topLevelItems(): HTMLElement[] {
    const list = this.root.querySelector('[data-slot=navigation-menu-list]');
    if (!list) return [];
    return Array.from(
      list.querySelectorAll(
        ':scope > [data-slot=navigation-menu-item] > [data-slot=navigation-menu-trigger],' +
          ':scope > [data-slot=navigation-menu-item] > [data-slot=navigation-menu-link]',
      ),
    ) as HTMLElement[];
  }

  // Roving Arrow/Home/End across the menubar (mirrored under RTL). Escape stays with .NET.
  private onKeyDown = (e: KeyboardEvent) => {
    if (e.key !== 'ArrowRight' && e.key !== 'ArrowLeft' && e.key !== 'Home' && e.key !== 'End') return;
    const items = this.topLevelItems();
    const idx = items.indexOf(document.activeElement as HTMLElement);
    if (idx === -1) return; // focus isn't on a top-level entry - leave the keys alone
    e.preventDefault();
    let next = idx;
    if (e.key === 'Home') next = 0;
    else if (e.key === 'End') next = items.length - 1;
    else {
      const rtl = getComputedStyle(this.root).direction === 'rtl';
      const forward = (e.key === 'ArrowRight') !== rtl;
      next = forward ? Math.min(idx + 1, items.length - 1) : Math.max(idx - 1, 0);
    }
    items[next]?.focus();
  };

  private content(): HTMLElement | null {
    // The content can be position:absolute (out of flow), so measure IT, not its wrapper.
    return this.root.querySelector(
      '[data-slot=navigation-menu-viewport] [data-slot=navigation-menu-content]',
    ) as HTMLElement | null;
  }

  // (Re)attach the resize observer to the live viewport content and the list (its width bounds the indicator).
  private observe() {
    this.ro.disconnect();
    const content = this.content();
    if (content) this.ro.observe(content);
    const list = this.root.querySelector('[data-slot=navigation-menu-list]');
    if (list) this.ro.observe(list as HTMLElement);
  }

  measure() {
    const vp = this.root.querySelector('[data-slot=navigation-menu-viewport]') as HTMLElement | null;
    if (vp) {
      const content = this.content();
      if (content) {
        vp.style.setProperty('--bz-nav-vw', `${content.offsetWidth}px`);
        vp.style.setProperty('--bz-nav-vh', `${content.offsetHeight}px`);
      }
    }

    // The arrow is always in the DOM (a fixed cascade keeps C# from re-rendering it), so the JS owns its
    // visibility AND position - both follow the open trigger, which it already tracks.
    const arrow = this.root.querySelector('[data-slot=navigation-menu-arrow]') as HTMLElement | null;
    const openTrigger = this.root.querySelector(
      '[data-slot=navigation-menu-trigger][data-state=open]',
    ) as HTMLElement | null;
    if (arrow) {
      const want = openTrigger ? 'visible' : 'hidden';
      if (arrow.getAttribute('data-state') !== want) arrow.setAttribute('data-state', want);
      if (openTrigger) {
        const item = (openTrigger.closest('[data-slot=navigation-menu-item]') as HTMLElement | null) ?? openTrigger;
        // Position relative to the root (the arrow's positioned ancestor), robust to offsetParent.
        const rootRect = this.root.getBoundingClientRect();
        const itemRect = item.getBoundingClientRect();
        arrow.style.setProperty('--bz-nav-ind-width', `${itemRect.width}px`);
        arrow.style.setProperty('--bz-nav-ind-left', `${itemRect.left - rootRect.left}px`);
      }
    }
  }

  dispose() {
    this.ro.disconnect();
    this.mo.disconnect();
    this.root.removeEventListener('keydown', this.onKeyDown);
  }
}

/** Wires the viewport/indicator measurement onto a navigation-menu root; returns a handle with dispose(). */
export function createNavMenu(root: HTMLElement): NavMenu {
  return new NavMenu(root);
}
