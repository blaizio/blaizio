/**
 * Numeric guard for BaseInputNumber's text box - it refuses characters that could never be part of
 * a number, so letters never land in the field at all.
 *
 * The field is a `type="text"` box on purpose (role=spinbutton owns the semantics; `type="number"`
 * brings a native spinner that duplicates the stepper buttons, a scroll wheel that changes the
 * value by accident, and a `value` that reads back EMPTY for anything the browser dislikes - which
 * would hide the very text C# needs to parse). The cost of that choice is that nothing stops a
 * letter being typed, and it cannot be undone from C#: the rejected keystroke leaves the model's
 * text unchanged, so the render tree is unchanged, so Blazor patches nothing and the stray
 * character stays on screen. Refusing it here, before it is ever inserted, is the only place the
 * fix holds - and the caret and undo stack survive.
 *
 * `beforeinput` covers typing, paste, drops and IME commits alike. What the guard allows is
 * deliberately loose: anything that could still GROW into a number ("-", "1.", ".5") passes, and
 * C# does the real parsing on input and the final clamp/snap on blur.
 */

/** What shapes count as numeric for a given field. */
export interface NumericOptions {
  /** Allow a decimal separator (false for an integral TValue). */
  decimal: boolean;
  /** Allow a leading minus (false when the field cannot go below zero). */
  negative: boolean;
  /** The culture's decimal separator; "." is always accepted too, since keypads emit it. */
  separator: string;
}

/** Insertion types that replace the whole field, where a mid-edit partial should not be required. */
const guards = new WeakMap<HTMLInputElement, () => void>();

/** Whether `text` is a number or could still become one by typing more characters. */
const acceptable = (text: string, options: NumericOptions): boolean => {
  if (text === '') return true;

  const separators = options.decimal ? (options.separator === '.' ? '.' : `.${options.separator}`) : '';
  const escaped = separators.replace(/[.\\]/g, '\\$&');
  // A lone sign, a lone separator and a trailing separator are all valid mid-edit states.
  const sign = options.negative ? '-?' : '';
  const body = options.decimal
    ? `\\d*[${escaped}]?\\d*`
    : '\\d*';
  return new RegExp(`^${sign}${body}$`).test(text);
};

/**
 * Refuse non-numeric input on `input`. Returns a disposer; call it from the component's dispose.
 * Re-guarding an already-guarded input replaces the previous guard rather than stacking one.
 */
export const guardNumeric = (input: HTMLInputElement, options: NumericOptions): void => {
  guards.get(input)?.();

  const onBeforeInput = (event: InputEvent): void => {
    // Deletions, undo/redo and history events carry no data and can never introduce a bad
    // character - let the browser do them.
    if (event.data === null || event.inputType.startsWith('delete') || event.inputType.startsWith('history')) return;

    const value = input.value;
    const start = input.selectionStart ?? value.length;
    const end = input.selectionEnd ?? start;
    const next = value.slice(0, start) + event.data + value.slice(end);

    if (!acceptable(next, options)) event.preventDefault();
  };

  input.addEventListener('beforeinput', onBeforeInput as EventListener);
  guards.set(input, () => input.removeEventListener('beforeinput', onBeforeInput as EventListener));
};

/** Drop the guard on an input - call from the component's dispose. */
export const releaseNumeric = (input: HTMLInputElement): void => {
  guards.get(input)?.();
  guards.delete(input);
};
