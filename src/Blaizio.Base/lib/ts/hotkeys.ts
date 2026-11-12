// Global hotkey binding + platform detection for Kbd / BzHotkey.
//
// Combo syntax and matching live in core.ts (parseCombo / matchesCombo); this module owns only
// the window-level binding and its .NET callback (invokeMethodAsync, like FocusScope). BzKbd
// calls getPlatform through this module - keep the re-export.

import {
  invokeDotNet,
  isEditableTarget,
  matchesCombo,
  parseCombo,
  type ParsedCombo,
} from './core';
import type { DotNetObjectReference } from './core';

export { getPlatform } from './core';

class Hotkey {
  constructor(
    private readonly combo: ParsedCombo,
    private readonly ref: DotNetObjectReference,
    private readonly preventDefault: boolean,
    private readonly skipContentEditable: boolean,
    private enabled: boolean,
  ) {
    window.addEventListener('keydown', this.onKeyDown);
  }

  /** Switches the hotkey off and on without unregistering it. Off, the key is left to the page. */
  setEnabled(enabled: boolean): void {
    this.enabled = enabled;
  }

  private onKeyDown = (e: KeyboardEvent) => {
    // Checked here and not only in C#: a disabled hotkey that still prevented the default left
    // the combo dead for the browser as well.
    if (!this.enabled || e.repeat) return;
    // A bare combo (no ctrl/meta/alt - shift alone still counts as bare) must not fire while the
    // user is typing: single letters like "o"/"r" would hijack every text field. Combos with a
    // "real" modifier (Ctrl/⌘/Alt) still fire everywhere, so app shortcuts like ⌘Z keep working.
    if (!this.combo.ctrl && !this.combo.meta && !this.combo.alt && isEditableTarget(e.target)) return;
    // A modifier combo that is also an editing command (Ctrl/⌘+B is bold) stays with a rich-text
    // editor when its owner asks for that.
    if (this.skipContentEditable && (e.target as HTMLElement | null)?.isContentEditable) return;
    if (!matchesCombo(e, this.combo)) return;
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
  skipContentEditable = false,
  enabled = true,
): Hotkey {
  return new Hotkey(parseCombo(combo), ref, preventDefault, skipContentEditable, enabled);
}
