/**
 * FocusScope - manages keyboard focus for overlay surfaces (dialog, popover, menu).
 *
 * Ported and modernised from ArcBlazor's focusScope.ts:
 *  - Removed the dead/commented dismiss-layer + auto-focus stubs. Dismissal (Esc / outside
 *    pointer) is now its own primitive (DismissableLayer), a clean separation of concerns.
 *  - mount / unmount auto-focus now actually round-trip to .NET and honour preventDefault,
 *    so OnMountAutoFocus / OnUnmountAutoFocus are real extension points (they were no-ops).
 *  - Tabbable detection no longer discards <a> elements (the old `tagName !== "A"` bug).
 *  - Strict TypeScript, no `any`; the redundant C#-driven keydown path is gone.
 */

import { invokeDotNetResult } from './core';

const FOCUS_OPTIONS: FocusOptions = { preventScroll: true };

export interface FocusScopeOptions {
  /** Tab from the last item wraps to the first (and Shift+Tab from first wraps to last). */
  loop: boolean;
  /** Focus cannot leave the container by keyboard, pointer, or programmatic focus. */
  trapped: boolean;
  /** A .NET OnMountAutoFocus handler is registered - round-trip before auto-focusing. */
  hasMountAutoFocus: boolean;
  /** A .NET OnUnmountAutoFocus handler is registered - round-trip before restoring focus. */
  hasUnmountAutoFocus: boolean;
  /**
   * Claim the scope stack and nothing else: no auto-focus on mount, no focus restore on dispose.
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

    if (!this.options.passive) void this.autoFocusOnMount();
  }

  /** Focus the first tabbable on mount, unless .NET prevents it or focus is already inside. */
  private autoFocusOnMount = async (): Promise<void> => {
    if (this.container.contains(document.activeElement)) return;

    const prevented =
      this.options.hasMountAutoFocus &&
      (await invokeDotNetResult(this.dotNetRef, false, 'HandleMountAutoFocus'));
    if (prevented) return;

    // Positioned surfaces (popover, select, ...) stay visibility:hidden until their first
    // placement lands (positioning.ts) - focus() on a hidden element is a silent no-op, which
    // made the first-ever open (cold module imports, slowest placement) skip the autofocus.
    await this.whenVisible();
    if (this.container.contains(document.activeElement)) return; // focus landed elsewhere meanwhile

    this.focusFirst(this.getTabbableCandidates(), { select: true });
    if (document.activeElement === this.previouslyFocused) this.focus(this.container);

    // A first-open can still lose the focus we just set: a portal hop (portal.ts moves the
    // surface to <body> - moving an element clears its focus) or a late cold-circuit re-render
    // replacing the subtree. Re-assert once after those settle; a no-op on every later open.
    setTimeout(() => {
      if (this.paused || !this.container.isConnected) return;
      if (this.container.contains(document.activeElement)) return;
      this.focusFirst(this.getTabbableCandidates(), { select: true });
      if (document.activeElement === this.previouslyFocused) this.focus(this.container);
    }, 80);
  };

  /** Resolves once the container is no longer visibility:hidden (bounded, ~500ms). */
  private whenVisible(): Promise<void> {
    return new Promise((resolve) => {
      const started = performance.now();
      const check = (): void => {
        if (
          !this.container.isConnected ||
          getComputedStyle(this.container).visibility !== 'hidden' ||
          performance.now() - started > 500
        ) {
          resolve();
        } else {
          setTimeout(check, 16); // not rAF: background tabs starve rAF entirely
        }
      };
      check();
    });
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
    this.container.removeEventListener('keydown', this.onKeyDown);
    document.removeEventListener('focusin', this.onFocusIn);
    document.removeEventListener('focusout', this.onFocusOut);
    this.mutationObserver?.disconnect();

    FocusScope.stack = FocusScope.stack.filter((scope) => scope !== this);
    FocusScope.stack[0]?.resume();

    // A passive scope only ever claimed the stack - whoever drives its focus restores it too.
    if (this.options.passive) return;

    // Guarded: a navigation can dispose the scope's DotNetObjectReference while this unmount
    // round-trip is in flight (dialog closes, page moves on) - treat that as "not prevented".
    const prevented =
      this.options.hasUnmountAutoFocus &&
      (await invokeDotNetResult(this.dotNetRef, false, 'HandleUnmountAutoFocus'));

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
