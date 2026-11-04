// Copy to the clipboard from a click, inside the click itself.
//
// Firefox and Safari only let a page write to the clipboard while a user gesture's activation is
// still live. A C# click handler runs after the event - on Blazor Server a network round-trip
// later - by which time the activation can have expired and the write is refused. So the copy
// happens here, in ONE delegated capture listener, during the click's own dispatch; .NET only hears
// the outcome. The marked element can be anything the consumer renders (the RenderAs case), so
// nothing here holds an element reference.
//
//   data-bz-copy="<token>"          marks a copy control; the token routes the outcome to .NET
//   data-bz-copy-text="..."         the text to copy, or
//   data-bz-copy-from="<selector>"  the element whose text is copied. Inside it, elements marked
//                                   data-bz-copy-line are copied one per line (a line holding only
//                                   "\n" is a blank line); without any, its whole textContent is.

import { invokeDotNet } from './core';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

const owners = new Map<string, DotNetObjectReference>();

/** Routes the outcome of clicks on [data-bz-copy="token"] to the owner's OnCopyResult(bool). */
export function register(token: string, ref: DotNetObjectReference): void {
  owners.set(token, ref);
}

export function unregister(token: string): void {
  owners.delete(token);
}

function textOf(source: Element): string {
  const lines = source.querySelectorAll('[data-bz-copy-line]');
  if (lines.length === 0) return source.textContent ?? '';
  return Array.from(lines, (line) => (line.textContent === '\n' ? '' : (line.textContent ?? ''))).join(
    '\n',
  );
}

function resolveText(el: Element): string | null {
  const text = el.getAttribute('data-bz-copy-text');
  if (text !== null) return text;
  const from = el.getAttribute('data-bz-copy-from');
  if (!from) return null;
  let source: Element | null = null;
  try {
    source = document.querySelector(from);
  } catch {
    // An invalid selector is a failed copy, reported like any other.
  }
  return source ? textOf(source) : null;
}

/**
 * The fallback for pages without the async clipboard API - plain http on a LAN address is not a
 * secure context. A selected off-screen textarea plus execCommand still works there.
 */
function copyWithSelection(text: string): boolean {
  const area = document.createElement('textarea');
  area.value = text;
  area.setAttribute('readonly', '');
  area.style.cssText = 'position:fixed;top:0;left:-9999px;opacity:0';
  const previous = document.activeElement as HTMLElement | null;
  document.body.appendChild(area);
  area.select();
  let copied = false;
  try {
    copied = document.execCommand('copy');
  } catch {
    copied = false;
  }
  area.remove();
  previous?.focus({ preventScroll: true });
  return copied;
}

/** Writes text to the clipboard and resolves whether it landed. Call during a user gesture. */
export function writeText(text: string): Promise<boolean> {
  if (window.isSecureContext && navigator.clipboard?.writeText) {
    return navigator.clipboard.writeText(text).then(
      () => true,
      () => false,
    );
  }
  return Promise.resolve(copyWithSelection(text));
}

function onClick(e: MouseEvent): void {
  const target = e.target;
  if (!(target instanceof Element)) return;
  const el = target.closest('[data-bz-copy]');
  if (!el || el.matches(':disabled, [aria-disabled="true"]')) return;
  const owner = owners.get(el.getAttribute('data-bz-copy') ?? '');
  const report = (copied: boolean): void => {
    void invokeDotNet(owner, 'OnCopyResult', copied);
  };
  const text = resolveText(el);
  if (text === null) {
    report(false);
    return;
  }
  void writeText(text).then(report);
}

// Idempotent across duplicate module instances (a hard reload racing a stale chunk) via a window
// flag rather than module state - same as core's guards.
const COPY_FLAG = '__blaizioCopy';

export function install(): void {
  const w = window as unknown as Record<string, unknown>;
  if (w[COPY_FLAG]) return;
  w[COPY_FLAG] = true;
  document.addEventListener('click', onClick, true);
}

install();
