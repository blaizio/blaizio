/**
 * Tooltip helpers.
 *
 * hasVisibleFocus: whether the tooltip's anchor currently matches :focus-visible. The trigger
 * opens on focusin, but focus also lands on it programmatically - e.g. a dropdown sharing the
 * trigger restores focus there when it closes - and a tooltip popping up under a mouse that is
 * nowhere near the trigger reads as a glitch. :focus-visible tracks the user's input modality:
 * keyboard focus (Tab, or Escape-closing an overlay) matches and should show the tooltip
 * immediately; pointer-driven or programmatic focus does not, and the tooltip stays closed
 * until a genuine hover.
 *
 * One case slips past :focus-visible alone: returning to the tab. The browser restores focus to
 * the last-focused element and re-evaluates the heuristic as if the focus were keyboard-driven,
 * so a button the user merely CLICKED before switching away suddenly matches - and its tooltip
 * pops with the pointer nowhere near it. A focusin that lands in the same breath as the window
 * regaining focus is the restore, not the user, so it never opens. The stamp is taken on the
 * focusin event itself (synchronously, at module scope), because the interop call asking about
 * it arrives a round-trip later.
 */

let windowFocusedAt = -Infinity;
let focusinWasTabReturn = false;

window.addEventListener('focus', () => {
  windowFocusedAt = performance.now();
});
document.addEventListener(
  'focusin',
  () => {
    focusinWasTabReturn = performance.now() - windowFocusedAt < 100;
  },
  true,
);

export const hasVisibleFocus = (anchorId: string): boolean => {
  if (focusinWasTabReturn) return false;
  const el = document.querySelector(`[data-bz-tooltip-anchor='${anchorId}']`);
  return el instanceof HTMLElement && el.matches(':focus-visible');
};
