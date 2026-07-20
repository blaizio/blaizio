// Measurement for BaseNavigationMenu's shared viewport + active-item indicator.
//
// The viewport is one container that morphs to the size of whichever item's content is showing; this
// module measures that content and writes --bz-nav-vw / --bz-nav-vh on the viewport (the CSS animates
// width/height between them). The indicator is an arrow that slides under the open trigger; we write
// --bz-nav-ind-left / --bz-nav-ind-width from that trigger's box. A MutationObserver (data-state +
// child swaps) and a ResizeObserver keep both in sync. Pure DOM - no .NET round-trips.

import { invokeDotNet } from './interop';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

class NavMenu {
  private readonly ro: ResizeObserver;
  private readonly mo: MutationObserver;

  constructor(
    private readonly root: HTMLElement,
    private readonly ref: DotNetObjectReference | null,
  ) {
    this.ro = new ResizeObserver(() => this.measure());
    this.mo = new MutationObserver(() => {
      this.observe();
      this.measure();
    });
    this.mo.observe(root, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-state'] });
    root.addEventListener('keydown', this.onKeyDown);
    // Close the open panel when a pointer goes down outside the whole menu (the menu is otherwise
    // dismissed only by hovering away, Escape or choosing a link). Capture so we see it first; pass-through
    // is preserved (we don't preventDefault) to keep the floating tier non-blocking.
    document.addEventListener('pointerdown', this.onDocPointerDown, true);
    this.observe();
    this.measure();
  }

  private onDocPointerDown = (e: PointerEvent): void => {
    if (!this.ref) return;
    if (!this.openTrigger()) return; // nothing open
    if (this.root.contains(e.target as Node)) return; // inside the menu (bar or panel) - leave it
    void invokeDotNet(this.ref, 'CloseFromOutside');
  };

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

  // Roving Arrow/Home/End across the menubar (mirrored under RTL), ArrowDown to open + dive in, and
  // Arrow up/down THROUGH the open panel. Escape is owned by .NET (it closes); here we just restore focus.
  private onKeyDown = (e: KeyboardEvent) => {
    const active = document.activeElement as HTMLElement | null;

    // Escape: the root's .NET handler closes the panel a tick later; bring focus back to the trigger now
    // (it still reads data-state=open at this point) so the user isn't dumped at the top of the page.
    if (e.key === 'Escape') {
      const trigger = this.openTrigger();
      if (trigger && (active === trigger || this.inOpenContent(active))) trigger.focus();
      return;
    }

    // Inside an open panel: all four arrows (plus Home/End) walk its focusable items, WRAPPING at the
    // ends so focus stays contained in the panel - you leave with Escape (back to the trigger) or Enter
    // (follow a link), not by arrowing off the top.
    if (this.inOpenContent(active) && this.isPanelNavKey(e.key)) {
      // A panel can run its own keyboard model (e.g. the two-pane tablist that moves a selection with
      // aria-activedescendant); we still swallow the key so the page doesn't scroll under it.
      if (active?.closest('[data-nav-manual]')) { e.preventDefault(); return; }
      const items = this.panelFocusables();
      if (!items.length) return;
      e.preventDefault();
      const idx = items.indexOf(active as HTMLElement);
      const forward = e.key === 'ArrowDown' || e.key === 'ArrowRight';
      let next: number;
      if (e.key === 'Home') next = 0;
      else if (e.key === 'End') next = items.length - 1;
      else if (idx === -1) next = 0;
      else next = forward ? (idx + 1) % items.length : (idx - 1 + items.length) % items.length;
      items[next]?.focus();
      return;
    }

    // ArrowDown on a focused trigger opens its panel AND dives focus into it in one press. If it was
    // closed, the panel renders a frame or two later (a .NET round-trip), so we poll until it's there.
    if (e.key === 'ArrowDown') {
      if (active?.getAttribute('data-slot') === 'navigation-menu-trigger' && this.topLevelItems().includes(active)) {
        e.preventDefault();
        if (active.getAttribute('data-state') !== 'open') active.click(); // open via .NET
        this.focusFirstPanelLink();
      }
      return;
    }

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

  // Focus the first FOCUSABLE thing in the open panel (a link with href / button - the lead card can be a
  // plain non-focusable <a>, so skip those). Retries across a few frames while the panel renders in.
  private focusFirstPanelLink(tries = 0) {
    const focusable = this.panelFocusables()[0];
    if (focusable) focusable.focus();
    else if (tries < 20) requestAnimationFrame(() => this.focusFirstPanelLink(tries + 1));
  }

  // The open trigger (the one whose panel is showing), or null when nothing is open.
  private openTrigger(): HTMLElement | null {
    return this.root.querySelector('[data-slot=navigation-menu-trigger][data-state=open]') as HTMLElement | null;
  }

  // Is focus currently somewhere inside an open content panel?
  private inOpenContent(el: HTMLElement | null): boolean {
    return !!el?.closest('[data-slot=navigation-menu-content]');
  }

  // The keys that walk the open panel's items.
  private isPanelNavKey(key: string): boolean {
    return key === 'ArrowDown' || key === 'ArrowUp' || key === 'ArrowLeft' || key === 'ArrowRight' ||
      key === 'Home' || key === 'End';
  }

  // The focusable items in the open panel, in document order (links with href, buttons, tabbable nodes).
  private panelFocusables(): HTMLElement[] {
    const content = this.root.querySelector('[data-slot=navigation-menu-content]');
    if (!content) return [];
    return Array.from(
      content.querySelectorAll('a[href],button,[tabindex]:not([tabindex="-1"])'),
    ) as HTMLElement[];
  }

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

  // Write a CSS custom property only when it actually changes. measure() runs on every Resize/Mutation
  // tick (continuously during the open morph); re-writing a var that a transitioned property reads
  // (width/transform) restarts that transition every frame, so it never settles - the arrow stayed at
  // width:0 / opacity:0. Guarding the writes lets each transition complete and still slide on a real change.
  private setVar(el: HTMLElement, name: string, value: string) {
    if (el.style.getPropertyValue(name) !== value) el.style.setProperty(name, value);
  }

  measure() {
    const vp = this.root.querySelector('[data-slot=navigation-menu-viewport]') as HTMLElement | null;
    if (vp) {
      const content = this.content();
      if (content) {
        this.setVar(vp, '--bz-nav-vw', `${content.offsetWidth}px`);
        this.setVar(vp, '--bz-nav-vh', `${content.offsetHeight}px`);
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
        this.setVar(arrow, '--bz-nav-ind-width', `${itemRect.width}px`);
        this.setVar(arrow, '--bz-nav-ind-left', `${itemRect.left - rootRect.left}px`);
      }
    }
  }

  dispose() {
    this.ro.disconnect();
    this.mo.disconnect();
    this.root.removeEventListener('keydown', this.onKeyDown);
    document.removeEventListener('pointerdown', this.onDocPointerDown, true);
  }
}

/** Wires the viewport/indicator measurement onto a navigation-menu root; returns a handle with dispose(). */
export function createNavMenu(root: HTMLElement, ref: DotNetObjectReference | null = null): NavMenu {
  return new NavMenu(root, ref);
}
