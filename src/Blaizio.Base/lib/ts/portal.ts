// Body portal for floating surfaces (tooltip, popover, menus, select, dialog...). A surface
// declared inside an ancestor that creates a stacking context (fixed/sticky+z, transform, filter,
// backdrop-blur, container-type, opacity<1) or an overflow clip can be painted over or cut at that
// ancestor's edge no matter its own z-index - z-order is decided by the ancestor stacking context,
// not by position:fixed. The fix is physical: move the element to document.body while it is shown.
//
// Blazor owns the element's lifetime, so the move must be reversible: a placeholder comment marks
// the element's home, and restore() puts it back BEFORE Blazor unmounts it (Blazor removes nodes
// by reference, so teardown is safe once the node is home). When the whole subtree unmounted first
// (navigation), the placeholder is gone - the element is simply removed instead.

/**
 * The element's way home. Carried on the element itself (not module state) so ordering works
 * across bundle copies and survives module reloads; a plain string key, same in every copy.
 */
const ANCHOR = '__bzPortalAnchor';

type Portaled = HTMLElement & { [ANCHOR]?: Comment };

/**
 * Opt-in theme-scope carrier: an ancestor with <code>data-bz-portal-frame="some classes"</code>
 * declares that surfaces declared inside it must keep those classes' cascade after the move to
 * body (scoped CSS like a theme pin is ancestry-based, and the portal physically severs the
 * ancestry). Matching elements portal into a shared body-level frame <div> that carries the
 * classes (display:contents, so it has no box and no layout effect) instead of bare body.
 */
const FRAME_ATTR = 'data-bz-portal-frame';

/** The body-level container for <code>el</code>: a class-carrying frame when its home sits under
 *  a FRAME_ATTR ancestor (the frame itself carries FRAME_ATTR, so nested portals resolve to the
 *  same frame), else document.body. */
function containerFor(placeholder: Comment): HTMLElement {
  const scope = placeholder.parentElement?.closest(`[${FRAME_ATTR}]`);
  if (!scope) return document.body;
  const classes = scope.getAttribute(FRAME_ATTR) ?? '';
  for (const child of document.body.children)
    if (child instanceof HTMLElement && child.getAttribute(FRAME_ATTR) === classes) return child;
  const frame = document.createElement('div');
  frame.setAttribute(FRAME_ATTR, classes);
  frame.className = classes;
  frame.style.display = 'contents';
  document.body.appendChild(frame);
  return frame;
}

/**
 * Moves <code>el</code> to <code>document.body</code>, leaving a placeholder comment at its
 * original spot. Portaled elements keep their DECLARATION order relative to each other: the
 * element is inserted before the first portaled body child whose placeholder follows ours in the
 * document, so e.g. a dialog's overlay stays painted under its content even when their JS attach
 * calls land out of order. Returns a handle whose <code>restore()</code> moves the element back
 * (or removes it when its home subtree is already gone).
 */
export function portalToBody(el: HTMLElement): { restore(): void } {
  const placeholder = document.createComment('bz-portal');
  el.before(placeholder);
  (el as Portaled)[ANCHOR] = placeholder;

  const container = containerFor(placeholder);
  let before: Element | null = null;
  for (const child of container.children) {
    const anchor = (child as Portaled)[ANCHOR];
    if (!anchor?.isConnected) continue;
    if (placeholder.compareDocumentPosition(anchor) & Node.DOCUMENT_POSITION_FOLLOWING) {
      before = child;
      break;
    }
  }
  container.insertBefore(el, before);

  return {
    restore() {
      delete (el as Portaled)[ANCHOR];
      if (placeholder.isConnected) placeholder.replaceWith(el);
      else el.remove();
      // A frame we created is disposable scaffolding - drop it once its last surface leaves.
      if (container !== document.body && container.childNodes.length === 0) container.remove();
    },
  };
}

const noop = { dispose() {} };

/**
 * Interop entry for overlay surfaces (dialog content/overlay) that have no positioning module to
 * inherit the portal from: portals <code>el</code> on attach, restores on <code>dispose()</code>.
 * A no-op handle if <code>el</code> is not an element (already unmounted).
 */
export function createPortal(el: HTMLElement): { dispose(): void } {
  if (!(el instanceof HTMLElement)) return noop;
  const portal = portalToBody(el);
  return { dispose: () => portal.restore() };
}
