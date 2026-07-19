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
 */
export const hasVisibleFocus = (anchorId: string): boolean => {
  const el = document.querySelector(`[data-bz-tooltip-anchor='${anchorId}']`);
  return el instanceof HTMLElement && el.matches(':focus-visible');
};
