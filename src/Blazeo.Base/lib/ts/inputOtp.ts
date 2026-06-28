// Controller for BaseInputOtp - a port of the input-otp library's hidden-input technique.
//
// A single transparent <input> is overlaid on the slot boxes; it owns all typing/paste/focus and its
// text selection drives which slot is "active". This module wires that input and reports state to C#:
//   - OnChange(value)            value edited (filtered to maxLength + the optional pattern)
//   - OnFocus(start, end)        focus snaps the caret to the active slot range
//   - OnBlur()                   focus left - C# clears the mirror so no slot is active
//   - OnSelectionChange(s, e)    the document selection moved; reported as a mirrored [start,end)
// C# turns the mirror selection + value into per-slot {char, isActive, hasFakeCaret} and re-renders.

interface DotNetObjectReference {
  invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

const STYLE_ID = 'bz-input-otp-style';

// Hide the transparent input's native selection highlight (we draw our own active-slot ring instead).
function injectStyle(): void {
  if (document.getElementById(STYLE_ID)) return;
  const el = document.createElement('style');
  el.id = STYLE_ID;
  el.textContent =
    '[data-input-otp]::selection{background:transparent !important;color:transparent !important;}';
  document.head.appendChild(el);
}

class InputOtp {
  private readonly pattern: RegExp | null;
  private prevStart: number | null = null;
  private prevEnd: number | null = null;
  private lastValue: string;
  private readonly ro: ResizeObserver;

  constructor(
    private readonly input: HTMLInputElement,
    private readonly ref: DotNetObjectReference,
    private readonly maxLength: number,
    patternSource: string | null,
  ) {
    this.pattern = patternSource ? new RegExp(patternSource) : null;
    this.lastValue = input.value;
    injectStyle();

    input.addEventListener('input', this.onInput);
    input.addEventListener('paste', this.onPaste);
    input.addEventListener('focus', this.onFocus);
    input.addEventListener('blur', this.onBlur);
    document.addEventListener('selectionchange', this.onSelectionChange, { capture: true });

    this.ro = new ResizeObserver(this.updateRootHeight);
    this.ro.observe(input);
    this.updateRootHeight();

    if (document.activeElement === input) this.onFocus();
  }

  // The slot height becomes the hidden input's font-size (the input-otp alignment trick).
  private updateRootHeight = () => {
    this.input.parentElement?.style.setProperty('--bz-otp-root-height', `${this.input.clientHeight}px`);
  };

  private onInput = () => {
    const v = this.input.value.slice(0, this.maxLength);
    if (v.length > 0 && this.pattern && !this.pattern.test(v)) {
      // Reject the edit: restore the last good value (text is transparent, so this is invisible).
      this.input.value = this.lastValue;
      return;
    }
    const deleted = v.length < this.lastValue.length;
    this.input.value = v;
    this.lastValue = v;
    // Deleting doesn't fire selectionchange on its own - nudge it so the mirror re-syncs.
    if (deleted) document.dispatchEvent(new Event('selectionchange'));
    void this.ref.invokeMethodAsync('OnChange', v);
  };

  private onPaste = (e: ClipboardEvent) => {
    if (!e.clipboardData) return;
    e.preventDefault();
    const content = e.clipboardData.getData('text/plain');
    const cur = this.input.value;
    const s = this.input.selectionStart ?? cur.length;
    const en = this.input.selectionEnd ?? s;
    const merged = (cur.slice(0, s) + content + cur.slice(en)).slice(0, this.maxLength);
    if (merged.length > 0 && this.pattern && !this.pattern.test(merged)) return;
    this.input.value = merged;
    this.lastValue = merged;
    const start = Math.min(merged.length, this.maxLength - 1);
    const end = merged.length;
    this.input.setSelectionRange(start, end);
    void this.ref.invokeMethodAsync('OnChange', merged);
    this.report(start, end);
  };

  private onFocus = () => {
    const len = this.input.value.length;
    const start = Math.min(len, this.maxLength - 1);
    const end = len;
    this.input.setSelectionRange(start, end);
    this.prevStart = start;
    this.prevEnd = end;
    void this.ref.invokeMethodAsync('OnFocus', start, end);
  };

  private onBlur = () => {
    void this.ref.invokeMethodAsync('OnBlur');
  };

  // The selection-clamp algorithm (ported from input-otp): turn a single caret into the [i, i+1)
  // range of the slot it sits in, so exactly one slot lights up (and the end caret selects the last).
  private onSelectionChange = () => {
    if (document.activeElement !== this.input) return; // onBlur clears the mirror
    const s = this.input.selectionStart;
    const e = this.input.selectionEnd;
    const ml = this.maxLength;
    const val = this.input.value;

    let start = -1;
    let end = -1;
    if (val.length !== 0 && s !== null && e !== null) {
      const isSingleCaret = s === e;
      const isInsertMode = s === val.length && val.length < ml;
      if (isSingleCaret && !isInsertMode) {
        const c = s;
        if (c === 0) {
          start = 0;
          end = 1;
        } else if (c === ml) {
          start = c - 1;
          end = c;
        } else if (ml > 1 && val.length > 1) {
          let offset = 0;
          if (this.prevStart !== null && this.prevEnd !== null) {
            const backward = c < this.prevEnd;
            const wasInserting = this.prevStart === this.prevEnd && this.prevStart < ml;
            if (backward && !wasInserting) offset = -1;
          }
          start = offset + c;
          end = offset + c + 1;
        }
      }
      if (start !== -1 && end !== -1 && start !== end) {
        this.input.setSelectionRange(start, end);
      }
    }

    const fs = start !== -1 ? start : s;
    const fe = end !== -1 ? end : e;
    this.prevStart = fs;
    this.prevEnd = fe;
    this.report(fs, fe);
  };

  private report(start: number | null, end: number | null) {
    void this.ref.invokeMethodAsync('OnSelectionChange', start, end);
  }

  dispose() {
    this.input.removeEventListener('input', this.onInput);
    this.input.removeEventListener('paste', this.onPaste);
    this.input.removeEventListener('focus', this.onFocus);
    this.input.removeEventListener('blur', this.onBlur);
    document.removeEventListener('selectionchange', this.onSelectionChange, { capture: true });
    this.ro.disconnect();
  }
}

/** Wires the OTP controller onto a transparent input; returns a handle with dispose(). */
export function createInputOtp(
  input: HTMLInputElement,
  ref: DotNetObjectReference,
  maxLength: number,
  patternSource: string | null,
): InputOtp {
  return new InputOtp(input, ref, maxLength, patternSource);
}
