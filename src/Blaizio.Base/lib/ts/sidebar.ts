// Viewport (is-mobile) observation + cookie persistence for BzSidebarProvider.
//
// The provider swaps its desktop fixed panel for a Sheet below a breakpoint, so it needs to know
// when the viewport crosses that width - observeMobile sets up a matchMedia listener that round-trips
// each change to C# (OnMobileChanged), and matchesMobile reads the current value once on first render.
// getCookie/setCookie persist the open/collapsed state across reloads (the vaul-style cookie).

import { invokeDotNet } from './interop';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

class MobileWatcher {
  private readonly mql: MediaQueryList;

  constructor(
    private readonly ref: DotNetObjectReference,
    maxWidthPx: number,
  ) {
    // max-width is inclusive, so the breakpoint itself counts as desktop (matches Tailwind's md:).
    this.mql = window.matchMedia(`(max-width: ${maxWidthPx - 0.02}px)`);
    this.mql.addEventListener('change', this.onChange);
  }

  private onChange = (e: MediaQueryListEvent) => {
    void invokeDotNet(this.ref, 'OnMobileChanged', e.matches);
  };

  dispose() {
    this.mql.removeEventListener('change', this.onChange);
  }
}

/** Reads whether the viewport is currently below the mobile breakpoint (one-shot). */
export function matchesMobile(maxWidthPx: number): boolean {
  return window.matchMedia(`(max-width: ${maxWidthPx - 0.02}px)`).matches;
}

/** Subscribes to viewport crossings of the mobile breakpoint; returns a handle with dispose(). */
export function observeMobile(ref: DotNetObjectReference, maxWidthPx: number): MobileWatcher {
  return new MobileWatcher(ref, maxWidthPx);
}

/** Reads a cookie value by name; empty string when absent. */
export function getCookie(name: string): string {
  const escaped = name.replace(/([.$?*|{}()[\]\\/+^])/g, '\\$1');
  const match = document.cookie.match(new RegExp(`(?:^|; )${escaped}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : '';
}

/** Writes a root-path cookie with the given max-age (seconds). */
export function setCookie(name: string, value: string, maxAgeSeconds: number): void {
  document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${maxAgeSeconds}; SameSite=Lax`;
}
