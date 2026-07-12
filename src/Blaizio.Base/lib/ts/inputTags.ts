/**
 * DOM-side keydown guard for a tags input's text box. Blazor owns ALL the logic (committing the typed
 * text on Enter/delimiter, removing on Backspace - see BaseInputTagsInput); what C# cannot do is
 * conditionally suppress a key's default action, so this tiny module swallows the boundary keys' defaults
 * at the source: Enter must not submit an enclosing form, and a delimiter key (e.g. ',') must not print
 * its character an instant before the commit clears the field. preventDefault does not stop propagation,
 * so Blazor's own keydown handler still fires and runs the commit.
 *
 * IME composition is left alone: an Enter/delimiter that is confirming a composition belongs to the IME,
 * not to us.
 */

export interface InputTagsOptions {
  /** Keys (KeyboardEvent.key values) that commit the text in addition to Enter. */
  delimiters: string[];
}

class InputTags {
  constructor(
    private readonly input: HTMLElement,
    private readonly options: InputTagsOptions,
  ) {
    this.input.addEventListener('keydown', this.onKeyDown);
  }

  private onKeyDown = (event: KeyboardEvent): void => {
    if (event.isComposing) return;
    if (event.key === 'Enter' || this.options.delimiters.includes(event.key)) {
      event.preventDefault();
    }
  };

  dispose(): void {
    this.input.removeEventListener('keydown', this.onKeyDown);
  }
}

const noop = { dispose() {} };

/**
 * Attaches the keydown guard to a tags input's text box. Returns a handle whose <code>dispose()</code>
 * detaches it; a no-op handle if <code>input</code> isn't an element.
 */
export function createInputTags(input: HTMLElement, options: InputTagsOptions): { dispose(): void } {
  if (!(input instanceof HTMLElement)) return noop;
  return new InputTags(input, options);
}

/**
 * Moves focus to the tags input's text box by id. Used by the root (a click anywhere in the box) and by
 * a chip's remove button to hand focus back so typing can continue.
 */
export function focusInput(id: string): void {
  document.getElementById(id)?.focus();
}
