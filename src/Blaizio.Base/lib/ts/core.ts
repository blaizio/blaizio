// Blaizio core: the shared plumbing every other module leans on, plus the browser-side services
// Blazor itself cannot provide.
//
// Three concerns live here:
//   1. Guarded .NET callbacks (invokeDotNet / invokeDotNetResult) - see below.
//   2. Key-combo parsing + platform detection, shared with hotkeys.ts.
//   3. What Blazor cannot do from C#:
//      - Conditional preventDefault. The browser applies an event's default action synchronously
//        during dispatch; a C# handler runs after (Server: a network round-trip later). So a
//        per-key decision MUST be made in JS. `data-bz-prevent-keys="enter, mod+s"` on any element
//        marks combos whose default is suppressed by ONE delegated capture listener - the event
//        still reaches the Blazor handler, only the default dies. Deliberately no imperative
//        preventDefault API in C#: it would be timing-dependent by construction.
//      - Reliable focus. ElementReference.FocusAsync silently fails while the target is still
//        display:none (open animation), not yet painted, or not yet connected. focusElement
//        retries across animation frames until focus sticks.

// ---------------------------------------------------------------------------------------------
// Guarded .NET callbacks. A JS-side event (animationend, a scroll report, a dismiss press) can
// fire while - or just after - the owning component disposes its DotNetObjectReference: the
// invocation then rejects with "There is no tracked object with id ..." and, because callback
// sites are fire-and-forget, surfaces as an uncaught promise rejection in the console. That
// teardown race is benign (the component is gone; there is nobody left to notify), so these
// wrappers swallow exactly that rejection and keep every other failure loud.
// ---------------------------------------------------------------------------------------------

interface DotNetCallbackRef {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

/** The dispatcher's disposed-reference rejection (raced teardown), as opposed to a real failure. */
const isDisposedRefError = (e: unknown): boolean => {
  const message = e instanceof Error ? e.message : String(e);
  return message.includes('no tracked object') || message.includes('already disposed');
};

/**
 * Fire-and-forget callback into .NET. Null/undefined refs no-op; a disposed-reference rejection
 * is swallowed; anything else rethrows (stays an unhandled rejection, exactly as loud as before).
 */
export const invokeDotNet = (
  ref: DotNetCallbackRef | null | undefined,
  method: string,
  ...args: unknown[]
): Promise<unknown> =>
  ref
    ? ref.invokeMethodAsync(method, ...args).catch((e: unknown) => {
        if (!isDisposedRefError(e)) throw e;
      })
    : Promise.resolve();

/**
 * Result-bearing callback into .NET. A disposed-reference rejection resolves to
 * <paramref>fallback</paramref> instead; anything else rethrows.
 */
export const invokeDotNetResult = async <T>(
  ref: DotNetCallbackRef,
  fallback: T,
  method: string,
  ...args: unknown[]
): Promise<T> => {
  try {
    return (await ref.invokeMethodAsync(method, ...args)) as T;
  } catch (e) {
    if (isDisposedRefError(e)) return fallback;
    throw e;
  }
};

// ---------------------------------------------------------------------------------------------
// Platform + key-combo parsing (shared with hotkeys.ts).
//
// Combos are "+"-joined parts, e.g. "mod+shift+k": modifier names (mod | ctrl | meta | alt |
// shift) plus exactly one key. "mod" is the platform primary modifier - ⌘ on mac, Ctrl elsewhere.
// Matching is exact on modifiers, so "enter" is Enter alone and "shift+enter" is a different combo.
// ---------------------------------------------------------------------------------------------

/** The user's platform family, for display conventions. */
export function getPlatform(): 'mac' | 'windows' | 'linux' {
  const platform =
    (navigator as { userAgentData?: { platform?: string } }).userAgentData?.platform ??
    navigator.platform ??
    '';
  if (/mac|iphone|ipad|ipod/i.test(platform)) return 'mac';
  if (/win/i.test(platform)) return 'windows';
  return 'linux';
}

export interface ParsedCombo {
  ctrl: boolean;
  meta: boolean;
  alt: boolean;
  shift: boolean;
  key: string;
}

/** True when focus is in a text-entry surface (input, textarea, select, or contenteditable). */
export function isEditableTarget(target: EventTarget | null): boolean {
  const el = target as HTMLElement | null;
  if (!el || !el.tagName) return false;
  const tag = el.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || el.isContentEditable;
}

export function parseCombo(combo: string): ParsedCombo {
  const isMac = getPlatform() === 'mac';
  const parsed: ParsedCombo = { ctrl: false, meta: false, alt: false, shift: false, key: '' };
  for (const raw of combo.toLowerCase().split('+')) {
    const part = raw.trim();
    if (part === 'mod') (isMac ? (parsed.meta = true) : (parsed.ctrl = true));
    else if (part === 'ctrl' || part === 'control') parsed.ctrl = true;
    else if (part === 'meta' || part === 'cmd' || part === 'win' || part === 'super') parsed.meta = true;
    else if (part === 'alt' || part === 'option') parsed.alt = true;
    else if (part === 'shift') parsed.shift = true;
    else parsed.key = part;
  }
  return parsed;
}

/** Exact-modifier combo match against a live keyboard event ("space" / "esc" aliases included). */
export function matchesCombo(e: KeyboardEvent, combo: ParsedCombo): boolean {
  if (
    e.ctrlKey !== combo.ctrl ||
    e.metaKey !== combo.meta ||
    e.altKey !== combo.alt ||
    e.shiftKey !== combo.shift
  )
    return false;
  const key = e.key.toLowerCase();
  return (
    key === combo.key ||
    (combo.key === 'space' && key === ' ') ||
    (combo.key === 'esc' && key === 'escape')
  );
}

// ---------------------------------------------------------------------------------------------
// Declarative guards - attribute-driven behaviors served by ONE set of document-level listeners,
// installed once when this module first loads (any Blaizio module importing core brings it in;
// ICore.EnsureGuardsAsync loads it explicitly):
//
//   data-bz-prevent-keys="enter, mod+s"  suppress those combos' browser default (capture keydown;
//                                        only preventDefault, never stopPropagation, so the event
//                                        still reaches Blazor's root delegation)
//   data-bz-autoselect                   select the field's content whenever it gains focus
//   data-bz-autofocus                    focus the element when it MOUNTS (a MutationObserver, so
//                                        content that appears later - a dialog's body - focuses
//                                        when it appears, which native autofocus cannot do for
//                                        Blazor-rendered markup)
//
// The attributes may sit on the control itself or on a wrapping container: autofocus descends to
// the first focusable thing inside (an OTP's overlay input, a date field's first segment), and
// autoselect matches the focused control against closest().
// ---------------------------------------------------------------------------------------------

const PREVENT_ATTR = 'data-bz-prevent-keys';

// Parsed combos cached per raw attribute string - the attr is static markup, so a handful of
// distinct values serve every keystroke.
const comboCache = new Map<string, ParsedCombo[]>();

function combosFor(attr: string): ParsedCombo[] {
  let combos = comboCache.get(attr);
  if (!combos) {
    combos = attr
      .split(',')
      .map((c) => c.trim())
      .filter(Boolean)
      .map(parseCombo);
    comboCache.set(attr, combos);
  }
  return combos;
}

function onGuardKeydown(e: KeyboardEvent): void {
  if (e.defaultPrevented) return;
  const target = e.target;
  if (!(target instanceof Element)) return;
  const host = target.closest(`[${PREVENT_ATTR}]`);
  if (!host) return;
  const attr = host.getAttribute(PREVENT_ATTR);
  if (!attr) return;
  for (const combo of combosFor(attr)) {
    if (matchesCombo(e, combo)) {
      e.preventDefault();
      return;
    }
  }
}

const AUTOFOCUS_ATTR = 'data-bz-autofocus';
const AUTOSELECT_ATTR = 'data-bz-autoselect';

/** Select-on-focus: the focused control opted in itself or sits inside a container that did. */
function onGuardFocusIn(e: FocusEvent): void {
  const target = e.target;
  if (!(target instanceof Element) || !target.closest(`[${AUTOSELECT_ATTR}]`)) return;
  if (!(target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement)) return;
  const el = target;
  // Deferred, not inline: after the focus events the browser restores the field's previous
  // caret, which would silently overwrite a select() made during the dispatch.
  setTimeout(() => {
    if (document.activeElement === el) el.select();
  }, 0);
  // A mouse focus is still mid-gesture here - the coming mouseup would place the caret at the
  // click point and collapse the selection. Cancel exactly that one; the timed removal keeps a
  // keyboard focus from eating an unrelated later click.
  const up = (ev: Event): void => ev.preventDefault();
  el.addEventListener('mouseup', up, { once: true });
  setTimeout(() => el.removeEventListener('mouseup', up), 400);
}

// What counts as focusable, and what the descent looks for inside a wrapper. Deliberately no
// buttons or links in the descendant list: a wrapper's incidental controls (a tags field's
// chip-remove buttons) must not win over the control the wrapper exists for.
const FOCUSABLE_SELF = 'input, textarea, select, [contenteditable], [tabindex]';
const FOCUSABLE_DESCENDANT =
  'input:not([type=hidden]):not([disabled]), textarea, select, [contenteditable], [tabindex]:not([tabindex="-1"])';

/**
 * An id or element, resolved to the thing focus should land on: the element itself when it can
 * take focus, else the first focusable control inside it (a wrapper's overlay input, a segment
 * row's first segment), else the element as given.
 */
function resolveFocusable(target: HTMLElement | string | null): HTMLElement | null {
  const el = typeof target === 'string' ? document.getElementById(target) : target;
  if (!el) return null;
  if (el.matches(FOCUSABLE_SELF)) return el;
  return el.querySelector<HTMLElement>(FOCUSABLE_DESCENDANT) ?? el;
}

// One shot per element INSTANCE: a re-created element (a keyed re-render, a dialog reopening)
// focuses again, an attribute merely touched in place does not.
const autofocused = new WeakSet<Element>();

function runAutofocus(marked: Element): void {
  if (autofocused.has(marked)) return;
  autofocused.add(marked);
  const target = resolveFocusable(marked as HTMLElement);
  if (!target) return;
  void focusElement(target, { select: target.closest(`[${AUTOSELECT_ATTR}]`) !== null });
}

function scanAutofocus(root: Element): void {
  if (root.hasAttribute(AUTOFOCUS_ATTR)) runAutofocus(root);
  for (const el of root.querySelectorAll(`[${AUTOFOCUS_ATTR}]`)) runAutofocus(el);
}

// Idempotent across duplicate module instances (a hard reload racing a stale chunk) via a window
// flag rather than module state.
const GUARD_FLAG = '__blaizioGuards';

export function installGuards(): void {
  const w = window as unknown as Record<string, unknown>;
  if (w[GUARD_FLAG]) return;
  w[GUARD_FLAG] = true;
  document.addEventListener('keydown', onGuardKeydown, true);
  document.addEventListener('focusin', onGuardFocusIn, true);
  new MutationObserver((records) => {
    for (const record of records) {
      for (const node of record.addedNodes) {
        if (node instanceof Element) scanAutofocus(node);
      }
    }
  }).observe(document.body, { childList: true, subtree: true });
  // Whatever was already in the DOM when the module loaded (the component renders first, then
  // its OnAfterRender import lands here) - the observer only sees insertions after this point.
  scanAutofocus(document.body);
}

installGuards();

// ---------------------------------------------------------------------------------------------
// Reliable focus.
// ---------------------------------------------------------------------------------------------

export interface FocusOptions {
  /** Skip the browser's scroll-into-view on focus. */
  preventScroll?: boolean;
  /** Also select the content (text-entry elements only). */
  select?: boolean;
  /** Extra animation frames to wait for the element to become focusable. Default 3. */
  retries?: number;
}

const nextFrame = (): Promise<void> => new Promise((r) => requestAnimationFrame(() => r()));

/**
 * Focus with retry: waits across animation frames while the element is disconnected or not yet
 * rendered (display:none mid-animation, pre-paint), then focuses and verifies it stuck. Returns
 * whether the element ended up the active element.
 *
 * Takes an element or an id. An id is re-resolved on every attempt - it may name an element that
 * has not rendered yet - and either form descends from a wrapper to its first focusable control,
 * so a component that only exposes an id on its container still lands on the right element.
 */
export async function focusElement(
  target: HTMLElement | string | null,
  opts?: FocusOptions,
): Promise<boolean> {
  const retries = opts?.retries ?? 3;
  for (let attempt = 0; attempt <= retries; attempt++) {
    const el = resolveFocusable(target);
    if (el?.isConnected && el.getClientRects().length > 0) {
      el.focus({ preventScroll: opts?.preventScroll ?? false });
      if (opts?.select && (el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement)) {
        const field = el;
        field.select();
        // Again on the next tick. Focusing a field can provoke work that lands AFTER this call
        // and collapses the selection: the browser restoring its own caret, or a component
        // re-rendering off its focus event (InputNumber swaps its formatted display for the
        // editable number). Selecting twice costs nothing and survives both.
        setTimeout(() => {
          if (document.activeElement === field) field.select();
        }, 0);
      }
      if (document.activeElement === el) return true;
    }
    if (attempt < retries) await nextFrame();
  }
  return false;
}
