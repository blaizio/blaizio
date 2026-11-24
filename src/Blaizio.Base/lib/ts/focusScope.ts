/**
 * FocusScope - manages keyboard focus for overlay surfaces (dialog, popover, menu).
 *
 * What it does beyond a plain focus trap:
 *  - Focus only. Dismissal (Esc / outside pointer) is its own primitive (DismissableLayer).
 *  - The auto-focus on first render and the focus restore on removal round-trip to .NET and
 *    honour preventDefault, so OnOpenAutoFocus / OnCloseAutoFocus are real extension points.
 *  - Links (<a>) count as tabbable.
 *  - Tab handling is wholly DOM-side; C# sees no keydown.
 */

import { invokeDotNetResult } from './core';

const FOCUS_OPTIONS: FocusOptions = { preventScroll: true };

export interface FocusScopeOptions {
  /** Tab from the last item wraps to the first (and Shift+Tab from first wraps to last). */
  loop: boolean;
  /** Focus cannot leave the container by keyboard, pointer, or programmatic focus. */
  trapped: boolean;
  /** A .NET OnOpenAutoFocus handler is registered - round-trip before auto-focusing. */
  hasOpenAutoFocus: boolean;
  /** A .NET OnCloseAutoFocus handler is registered - round-trip before restoring focus. */
  hasCloseAutoFocus: boolean;
  /**
   * Claim the scope stack and nothing else: no auto-focus on first render, no focus restore on dispose.
   * For surfaces whose focus is driven elsewhere (ts/menu.js owns the select listbox - it opens on
   * the SELECTED option and restores the trigger itself). What the stack membership buys is an
   * enclosing dialog's trap standing down: the listbox portals to the body, so focus landing on an
   * option is OUTSIDE the dialog's subtree, and an unpaused trap yanks it straight back.
   */
  passive?: boolean;
}

export class FocusScope {
  /** Active scopes, innermost first. Nested scopes pause the ones beneath them. */
  private static stack: FocusScope[] = [];

  private readonly previouslyFocused: HTMLElement | null;
  private lastFocused: HTMLElement | null = null;
  private paused = false;
  private disposed = false;
  private mutationObserver?: MutationObserver;

  constructor(
    private readonly container: HTMLElement,
    private readonly dotNetRef: DotNetReference,
    private readonly options: FocusScopeOptions,
  ) {
    this.previouslyFocused = document.activeElement as HTMLElement | null;
    this.mount();
  }

  private mount(): void {
    FocusScope.stack.forEach((scope) => scope.pause());
    FocusScope.stack.unshift(this);

    this.container.addEventListener('keydown', this.onKeyDown);
    if (this.options.trapped) {
      document.addEventListener('focusin', this.onFocusIn);
      document.addEventListener('focusout', this.onFocusOut);
      this.mutationObserver = new MutationObserver(this.onMutation);
      this.mutationObserver.observe(this.container, { childList: true, subtree: true });
    }

    if (!this.options.passive) void this.autoFocusOnOpen();
  }

  /** Focus the first tabbable on first render, unless .NET prevents it or focus is already inside. */
  private autoFocusOnOpen = async (reopened = false): Promise<void> => {
    if (this.container.contains(document.activeElement)) return;

    const prevented =
      this.options.hasOpenAutoFocus &&
      (await invokeDotNetResult(this.dotNetRef, false, 'HandleOpenAutoFocus'));
    if (prevented) return;

    // Positioned surfaces (popover, select, ...) stay visibility:hidden until their first
    // placement lands (positioning.ts) - focus() on a hidden element is a silent no-op, which
    // made the first-ever open (cold module imports, slowest placement) skip the autofocus.
    await this.whenVisible();
    // Closed while the opening focus was still on its way: dispose() has put focus back where it
    // belongs, and the surface (still in the page for its exit animation) must not take it again.
    if (this.disposed) return;
    if (this.container.contains(document.activeElement)) return; // focus landed elsewhere meanwhile

    // Where focus stays when nothing inside takes it: the container takes it then. A surface
    // opened again while closing finds focus wherever the last press left it (its trigger, the
    // page, nothing at all), not where its first opening found it.
    const origin = reopened ? document.activeElement : this.previouslyFocused;
    this.focusFirst(this.getTabbableCandidates(), { select: true });
    if (document.activeElement === origin) this.focus(this.container);
    // The element was neither moved nor rendered anew: nothing takes this focus away again.
    if (reopened) return;

    // A first-open can still lose the focus we just set: a portal hop (portal.ts moves the
    // surface to <body> - moving an element clears its focus) or a late cold-circuit re-render
    // replacing the subtree. Re-assert once after those settle; a no-op on every later open.
    setTimeout(() => {
      if (this.disposed || this.paused || !this.container.isConnected) return;
      if (this.container.contains(document.activeElement)) return;
      this.focusFirst(this.getTabbableCandidates(), { select: true });
      if (document.activeElement === this.previouslyFocused) this.focus(this.container);
    }, 80);
  };

  /**
   * Resolves once the container is no longer visibility:hidden and a control in it can take
   * focus (bounded, ~500ms in all).
   *
   * The second half is about one frame. Under reduced motion blaizio.css gives every property of
   * every element a 0.01ms transition; a control inherits its surface's visibility, an inherited
   * value is a property like any other, and a transition's first frame still shows the value it
   * starts from. So for that frame the surface is visible and its controls are not, focus() on
   * them does nothing, and the surface itself took the focus meant for its first field.
   */
  private whenVisible(): Promise<void> {
    return new Promise((resolve) => {
      const started = performance.now();
      const check = (): void => {
        if (!this.container.isConnected || this.canPlaceFocus() || performance.now() - started > 500) {
          resolve();
        } else {
          setTimeout(check, 16); // not rAF: background tabs starve rAF entirely
        }
      };
      check();
    });
  }

  // Ready when a control can take focus, or when waiting would change nothing: no control at all,
  // or none that is drawn (display:none never shows up a frame later). Then the container takes
  // the focus. Controls that are drawn and stay visibility:hidden wait out the bound first.
  private canPlaceFocus(): boolean {
    if (getComputedStyle(this.container).visibility === 'hidden') return false;
    let hiddenForNow = false;
    for (const el of this.getTabbableCandidates()) {
      if (!this.isDrawn(el)) continue;
      if (getComputedStyle(el).visibility !== 'hidden') return true;
      hiddenForNow = true;
    }
    return !hiddenForNow;
  }

  /** No display:none on the node or between it and the container. */
  private isDrawn(node: HTMLElement): boolean {
    let current: HTMLElement | null = node;
    while (current && current !== this.container) {
      if (getComputedStyle(current).display === 'none') return false;
      current = current.parentElement;
    }
    return true;
  }

  /**
   * The scope on <code>container</code> places its opening focus again. A surface opened again
   * while it was still closing keeps its element and this scope, and the opening focus is placed
   * once per scope. The press that closed the surface (on its trigger, on the page, on a dialog's
   * backdrop) or the one that opened it again had moved focus out, and nothing brought it back:
   * Tab then started from the page behind the open surface.
   *
   * Asks as the first opening does: focus that is inside already stays where it is, and the .NET
   * handler can prevent the rest. Does nothing for a passive scope (whoever drives its focus
   * places it), for one under another surface (paused), and when no scope is on the element.
   */
  public static refocus(container: HTMLElement): void {
    const scope = FocusScope.stack.find((candidate) => candidate.container === container);
    if (!scope || scope.options.passive || scope.paused) return;
    void scope.autoFocusOnOpen(true);
  }

  public pause = (): void => {
    this.paused = true;
  };

  public resume = (): void => {
    this.paused = false;
    if (this.lastFocused) this.focus(this.lastFocused, { select: true });
  };

  private onKeyDown = (event: KeyboardEvent): void => {
    if ((!this.options.loop && !this.options.trapped) || this.paused) return;

    const isTab = event.key === 'Tab' && !event.altKey && !event.ctrlKey && !event.metaKey;
    if (!isTab) return;

    const focused = document.activeElement as HTMLElement | null;
    const [first, last] = this.getTabbableEdges();

    if (!first || !last) {
      // Nothing tabbable: keep focus pinned to the container.
      if (focused === this.container) event.preventDefault();
      return;
    }

    if (!event.shiftKey && focused === last) {
      event.preventDefault();
      if (this.options.loop) this.focus(first, { select: true });
    } else if (event.shiftKey && focused === first) {
      event.preventDefault();
      if (this.options.loop) this.focus(last, { select: true });
    }
  };

  private onFocusIn = (event: FocusEvent): void => {
    if (this.paused) return;
    const target = event.target as HTMLElement | null;
    if (target && this.container.contains(target)) this.lastFocused = target;
    else this.focus(this.lastFocused, { select: true });
  };

  private onFocusOut = (event: FocusEvent): void => {
    if (this.paused) return;
    const next = event.relatedTarget as HTMLElement | null;
    if (next && !this.container.contains(next)) this.focus(this.lastFocused, { select: true });
  };

  private onMutation = (mutations: MutationRecord[]): void => {
    // If the focused node was removed (focus fell to <body>), pull it back to the container.
    if (document.activeElement !== document.body) return;
    if (mutations.some((m) => m.removedNodes.length > 0)) this.focus(this.container);
  };

  private getTabbableEdges(): [HTMLElement | null, HTMLElement | null] {
    const candidates = this.getTabbableCandidates();
    const first = this.findVisible(candidates) ?? null;
    const last = this.findVisible([...candidates].reverse()) ?? null;
    return [first, last];
  }

  /** Tabbable descendants in DOM order. Includes anchors, unlike the original. */
  private getTabbableCandidates(): HTMLElement[] {
    const nodes: HTMLElement[] = [];
    const walker = document.createTreeWalker(this.container, NodeFilter.SHOW_ELEMENT, {
      acceptNode: (node) => {
        const el = node as HTMLElement & { disabled?: boolean };
        const isHiddenInput = el instanceof HTMLInputElement && el.type === 'hidden';
        if (el.hidden || el.disabled === true || isHiddenInput) return NodeFilter.FILTER_SKIP;
        return el.tabIndex >= 0 ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_SKIP;
      },
    });
    while (walker.nextNode()) nodes.push(walker.currentNode as HTMLElement);
    return nodes;
  }

  private findVisible(elements: HTMLElement[]): HTMLElement | undefined {
    return elements.find((el) => !this.isHidden(el));
  }

  private isHidden(node: HTMLElement | null): boolean {
    if (!node || getComputedStyle(node).visibility === 'hidden') return true;
    let current: HTMLElement | null = node;
    while (current) {
      if (current === this.container) return false;
      if (getComputedStyle(current).display === 'none') return true;
      current = current.parentElement;
    }
    return false;
  }

  private focus(element: HTMLElement | null, { select = false } = {}): void {
    if (!element?.focus) return;
    const previous = document.activeElement;
    element.focus(FOCUS_OPTIONS);
    if (element !== previous && element instanceof HTMLInputElement && select) element.select();
  }

  private focusFirst(candidates: HTMLElement[], { select = false } = {}): void {
    const previous = document.activeElement;
    for (const candidate of candidates) {
      this.focus(candidate, { select });
      if (document.activeElement !== previous) return;
    }
  }

  public dispose = async (): Promise<void> => {
    this.disposed = true;
    this.container.removeEventListener('keydown', this.onKeyDown);
    document.removeEventListener('focusin', this.onFocusIn);
    document.removeEventListener('focusout', this.onFocusOut);
    this.mutationObserver?.disconnect();

    FocusScope.stack = FocusScope.stack.filter((scope) => scope !== this);
    FocusScope.stack[0]?.resume();

    // A passive scope only ever claimed the stack - whoever drives its focus restores it too.
    if (this.options.passive) return;

    // Guarded: a navigation can dispose the scope's DotNetObjectReference while this removal
    // round-trip is in flight (dialog closes, page moves on) - treat that as "not prevented".
    const prevented =
      this.options.hasCloseAutoFocus &&
      (await invokeDotNetResult(this.dotNetRef, false, 'HandleCloseAutoFocus'));

    // Restore focus to where it was before the scope opened - UNLESS the user already moved it
    // somewhere else outside the scope (e.g. the dismissing click landed on a text input: the
    // input focuses on mousedown, the layer closes, and yanking focus back to the trigger would
    // steal it right out of the field the user just clicked).
    const active = document.activeElement;
    const focusClaimedElsewhere =
      active !== null && active !== document.body && !this.container.contains(active);
    if (!prevented && !focusClaimedElsewhere)
      this.focus(this.previouslyFocused ?? document.body, { select: true });
  };
}

export const createFocusScope = (
  container: HTMLElement,
  dotNetRef: DotNetReference,
  options: FocusScopeOptions,
): FocusScope => new FocusScope(container, dotNetRef, options);

/**
 * Places the opening focus again in the scope on <code>container</code>, for a surface that was
 * opened again while it was closing (see <code>FocusScope.refocus</code>). By element, not by
 * instance: the surface that knows it was reopened holds the element, its focus scope component
 * holds the instance.
 */
export const refocus = (container: HTMLElement): void => FocusScope.refocus(container);
