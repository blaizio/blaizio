// Global hotkey binding + platform detection for Kbd / BzHotkey.
//
// Combos are "+"-joined parts, e.g. "mod+shift+k": modifier names (mod | ctrl | meta | alt |
// shift) plus exactly one key. "mod" is the platform primary modifier - ⌘ on mac, Ctrl elsewhere.
// Callback interop: the hotkey round-trips to C# via invokeMethodAsync (like FocusScope).

import { invokeDotNet } from './interop';

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

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

interface ParsedCombo {
  ctrl: boolean;
  meta: boolean;
  alt: boolean;
  shift: boolean;
  key: string;
}

/** True when focus is in a text-entry surface (input, textarea, select, or contenteditable). */
function isEditableTarget(target: EventTarget | null): boolean {
  const el = target as HTMLElement | null;
  if (!el || !el.tagName) return false;
  const tag = el.tagName;
  return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || el.isContentEditable;
}

function parse(combo: string): ParsedCombo {
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

class Hotkey {
  constructor(
    private readonly combo: ParsedCombo,
    private readonly ref: DotNetObjectReference,
    private readonly preventDefault: boolean,
  ) {
    window.addEventListener('keydown', this.onKeyDown);
  }

  private onKeyDown = (e: KeyboardEvent) => {
    if (e.repeat) return;
    if (
      e.ctrlKey !== this.combo.ctrl ||
      e.metaKey !== this.combo.meta ||
      e.altKey !== this.combo.alt ||
      e.shiftKey !== this.combo.shift
    )
      return;
    // A bare combo (no ctrl/meta/alt - shift alone still counts as bare) must not fire while the
    // user is typing: single letters like "o"/"r" would hijack every text field. Combos with a
    // "real" modifier (Ctrl/⌘/Alt) still fire everywhere, so app shortcuts like ⌘Z keep working.
    if (!this.combo.ctrl && !this.combo.meta && !this.combo.alt && isEditableTarget(e.target)) return;
    const key = e.key.toLowerCase();
    const wanted = this.combo.key;
    if (key !== wanted && !(wanted === 'space' && key === ' ') && !(wanted === 'esc' && key === 'escape'))
      return;
    if (this.preventDefault) e.preventDefault();
    void invokeDotNet(this.ref, 'OnHotkey');
  };

  dispose() {
    window.removeEventListener('keydown', this.onKeyDown);
  }
}

/** Registers a global hotkey; returns a handle with dispose(). */
export function createHotkey(
  combo: string,
  ref: DotNetObjectReference,
  preventDefault: boolean,
): Hotkey {
  return new Hotkey(parse(combo), ref, preventDefault);
}
