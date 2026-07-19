/**
 * Data-table row clicks, delegated on the table root.
 *
 * A Blazor @onclick on each <tr> cannot see the event target, so it fires for EVERY click that
 * bubbles out of the row - including the selection checkbox, a row-actions menu trigger, or a
 * link in a cell. Delegating in JS lets the row click honour its contract ("anywhere outside an
 * interactive cell control"): clicks that originate inside an interactive element are the
 * control's own; everything else on a tagged row reports the row's index back to .NET.
 */

/** Controls that own their clicks - a row click never fires from inside these. */
const INTERACTIVE =
  "button, a[href], input, select, textarea, label, [contenteditable='true'], " +
  "[role='button'], [role='checkbox'], [role='menuitem'], [role='option'], [role='link']";

class DataTableRowClicks {
  constructor(
    private readonly root: HTMLElement,
    private readonly dotNetRef: DotNetReference,
  ) {
    root.addEventListener('click', this.onClick);
  }

  private onClick = (event: MouseEvent): void => {
    const target = event.target as Element | null;
    const row = target?.closest<HTMLElement>('tr[data-bz-row-index]');
    if (!row || !this.root.contains(row)) return;

    // A click on (or inside) an interactive control within the row is that control's click.
    const control = target?.closest(INTERACTIVE);
    if (control && row.contains(control)) return;

    const index = Number(row.dataset.bzRowIndex);
    if (Number.isInteger(index)) void this.dotNetRef.invokeMethodAsync('HandleRowClick', index);
  };

  public dispose = (): void => {
    this.root.removeEventListener('click', this.onClick);
  };
}

export const createRowClicks = (root: HTMLElement, dotNetRef: DotNetReference): DataTableRowClicks =>
  new DataTableRowClicks(root, dotNetRef);
