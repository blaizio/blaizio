/**
 * Single-row overflow for a multi-select trigger's tokens. Given a container that lays its tokens out
 * on one line (flex, nowrap, overflow:hidden) with a trailing "+N" counter element, this hides the
 * tokens that don't fit and writes the hidden count into the counter - so the trigger keeps a stable
 * height instead of wrapping or clipping mid-token. It re-measures whenever the container resizes
 * (ResizeObserver) and whenever Blazor re-renders the token set (the owner calls update()).
 *
 * Pure DOM, no interop per frame. Each pass resets every token to visible first, so a stale
 * display:none from a previous selection never lingers; Blazor owns the elements, this only toggles
 * their inline display and the counter's text.
 *
 *   <div data-bz-select-tokens>            // flex, flex-nowrap, overflow-hidden
 *     <span data-bz-token>Apple ✕</span>
 *     <span data-bz-token>Banana ✕</span>
 *     <span data-bz-token-more hidden>+2</span>
 *   </div>
 */

const TOKEN = '[data-bz-token]';
const MORE = '[data-bz-token-more]';

class TokenOverflow {
  private readonly observer: ResizeObserver;

  constructor(private readonly container: HTMLElement) {
    this.observer = new ResizeObserver(() => this.measure());
    this.observer.observe(this.container);
    this.measure();
  }

  measure = (): void => {
    const tokens = Array.from(this.container.querySelectorAll<HTMLElement>(TOKEN));
    const more = this.container.querySelector<HTMLElement>(MORE);

    // Reset to the all-visible baseline before measuring (clears any prior pass's hidden tokens).
    for (const token of tokens) token.style.display = '';
    if (more) {
      more.hidden = true;
      more.textContent = '';
    }

    // Everything fits on the line (or there's no counter to show): nothing to hide.
    if (!more || this.container.scrollWidth <= this.container.clientWidth) return;

    // Reveal the counter, then hide tokens from the end until the row (counter included) fits.
    more.hidden = false;
    let hidden = 0;
    for (let i = tokens.length - 1; i >= 0 && this.container.scrollWidth > this.container.clientWidth; i--) {
      tokens[i]!.style.display = 'none';
      hidden += 1;
      more.textContent = `+${hidden}`;
    }

    if (hidden === 0) {
      more.hidden = true;
      more.textContent = '';
    }
  };

  dispose(): void {
    this.observer.disconnect();
  }
}

const noop = { update() {}, dispose() {} };

/**
 * Attaches single-row token overflow to <code>container</code>. Returns a handle whose
 * <code>update()</code> re-measures (call it after the token set changes) and <code>dispose()</code>
 * detaches the observer; a no-op handle if <code>container</code> isn't an element.
 */
export function createTokenOverflow(container: HTMLElement): { update(): void; dispose(): void } {
  if (!(container instanceof HTMLElement)) return noop;
  const instance = new TokenOverflow(container);
  return { update: instance.measure, dispose: () => instance.dispose() };
}
