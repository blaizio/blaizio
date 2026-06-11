/**
 * FocusScope — manages keyboard focus for overlay surfaces (dialog, popover, menu).
 *
 * Ported and modernised from ArcBlazor's focusScope.ts:
 *  - Removed the dead/commented dismiss-layer + auto-focus stubs. Dismissal (Esc / outside
 *    pointer) is now its own primitive (DismissableLayer), matching Radix's separation.
 *  - mount / unmount auto-focus now actually round-trip to .NET and honour preventDefault,
 *    so OnMountAutoFocus / OnUnmountAutoFocus are real extension points (they were no-ops).
 *  - Tabbable detection no longer discards <a> elements (the old `tagName !== "A"` bug).
 *  - Strict TypeScript, no `any`; the redundant C#-driven keydown path is gone.
 */

const FOCUS_OPTIONS: FocusOptions = { preventScroll: true };

export interface FocusScopeOptions {
  /** Tab from the last item wraps to the first (and Shift+Tab from first wraps to last). */
  loop: boolean;
  /** Focus cannot leave the container by keyboard, pointer, or programmatic focus. */
  trapped: boolean;
  /** A .NET OnMountAutoFocus handler is registered — round-trip before auto-focusing. */
  hasMountAutoFocus: boolean;
  /** A .NET OnUnmountAutoFocus handler is registered — round-trip before restoring focus. */
  hasUnmountAutoFocus: boolean;
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

    void this.autoFocusOnMount();
  }

  /** Focus the first tabbable on mount, unless .NET prevents it or focus is already inside. */
  private autoFocusOnMount = async (): Promise<void> => {
    if (this.container.contains(document.activeElement)) return;

    const prevented =
      this.options.hasMountAutoFocus &&
      (await this.dotNetRef.invokeMethodAsync<boolean>('HandleMountAutoFocus'));
    if (prevented) return;

    this.focusFirst(this.getTabbableCandidates(), { select: true });
    if (document.activeElement === this.previouslyFocused) this.focus(this.container);
  };

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

    const prevented =
      this.options.hasUnmountAutoFocus &&
      (await this.dotNetRef.invokeMethodAsync<boolean>('HandleUnmountAutoFocus'));
    if (!prevented) this.focus(this.previouslyFocused ?? document.body, { select: true });
  };
}

export const createFocusScope = (
  container: HTMLElement,
  dotNetRef: DotNetReference,
  options: FocusScopeOptions,
): FocusScope => new FocusScope(container, dotNetRef, options);
