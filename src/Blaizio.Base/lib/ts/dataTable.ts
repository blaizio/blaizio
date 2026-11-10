/**
 * Data-table interop, delegated on the table root: row clicks, the spreadsheet-style keyboard grid
 * for editable tables, and the column features - sizing, pinning and moving (DataTableColumns,
 * further down, documents its own contract).
 *
 * ROW CLICKS. A Blazor @onclick on each <tr> cannot see the event target, so it fires for EVERY
 * click that bubbles out of the row - including the selection checkbox, a row-actions menu trigger,
 * or a link in a cell. Delegating in JS lets the row click honour its contract ("anywhere outside an
 * interactive cell control"): clicks that originate inside an interactive element are the
 * control's own; everything else on a tagged row reports the row's index back to .NET.
 *
 * KEYBOARD GRID. The CURSOR lives here and never round-trips: arrows, Home/End, PageUp/PageDown,
 * Tab and (in Cell mode) Enter move the active cell - a roving tabindex this module writes (C#
 * renders only the first one, so Tab reaches the grid before this script attaches) - so moving
 * costs no .NET call and no render. .NET owns the DATA: it hears about a begin, a commit, a cancel,
 * a toggle, a clear or a paste, renders the one editor (a Blaizio control), and runs the parse /
 * validate / write pipeline. Keys typed while .NET is busy (the editor not rendered yet, a commit
 * in flight) are buffered and replayed, so a fast typist on a slow connection loses nothing.
 *
 * HOW AN EDIT STARTS decides how the editor arrives, and the pointer and the keyboard differ. A
 * click selects; a double-click (or a tap on the active cell) edits in place with the control ready:
 * the caret where the text was clicked, a list or calendar open, the time segment under the pointer.
 * With options.editOnClick a single click or tap does that already. A press on a pick list's or
 * date's mark opens its popup. On the keyboard, a typed character replaces the cell (quick entry:
 * arrows then commit), F2 edits in place, Alt+ArrowDown and Space open a popup. A key typed over a
 * pick list opens it and is typed into the list's type-ahead. While a popup is open, one Escape
 * closes it and cancels the edit, and Tab picks the highlighted option and moves on.
 *
 * THE CURSOR SHOWS FOR THE KEYBOARD. Like :focus-visible, the cursor's cell is marked (data-active)
 * only while the keyboard drives the grid: after Tab brings focus in, or after a key pressed in the
 * table. A press with the pointer clears the mark until the next key, so a click selects without
 * drawing a ring.
 *
 * EDIT ACTIONS. Cells marked [data-bz-edit-actions] (an edit actions column) belong to the edit of
 * their row: a press on one of their controls keeps the focus in the editor (so a Cancel button
 * cancels instead of the blur committing first), focus moving onto one is no leave, and Tab from the
 * row's last editor walks them before it leaves the row. Enter and Space press them.
 *
 * Contract with BzDataTable:
 *   tr[data-bz-row-index]          a body row and its index into the rows .NET navigates
 *   td / th cellIndex              the column; options.columns maps it to a key + editor kind
 *   [data-bz-cell-editor=kind]     the wrapper around an editor .NET rendered (text | number |
 *                                  select | checkbox | switch | date | time | custom). Its arrival
 *                                  starts an edit here, its departure ends one. A text or number
 *                                  field's text is read here and sent with the commit; every other
 *                                  control keeps its draft in .NET.
 *   [data-bz-pick]                 a pick list's or date's mark in an idle cell
 *   td[data-bz-edit-actions]       a cell whose controls act on its row's edit (see EDIT ACTIONS)
 *   [data-active]                  written here: the cursor's cell, while the keyboard drives
 *   table[data-bz-grid]            set here once the grid has attached
 *   .NET methods                   BeginEdit(row, col, seed, start, popup) -> bool
 *                                  EndEdit(commit, values, discardOnError) -> { ok, focus }
 *                                  Toggle(row, col), Clear(row, col), Paste(row, col, text)
 *   called by .NET                 values(), commit(), update(options), formClosed(row),
 *                                  rememberFocus(), restoreFocus()
 */

import { focusElement, invokeDotNet, invokeDotNetResult, isEditableTarget } from './core';
import { virtualizerFor } from './virtualizer';

/** Controls that own their clicks - a row click never fires from inside these. */
const INTERACTIVE =
  "button, a[href], input, select, textarea, label, [contenteditable='true'], " +
  "[role='button'], [role='checkbox'], [role='switch'], [role='menuitem'], [role='option'], [role='link']";

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
    if (Number.isInteger(index)) void invokeDotNet(this.dotNetRef, 'HandleRowClick', index);
  };

  public dispose = (): void => {
    this.root.removeEventListener('click', this.onClick);
  };
}

export const createRowClicks = (root: HTMLElement, dotNetRef: DotNetReference): DataTableRowClicks =>
  new DataTableRowClicks(root, dotNetRef);

// ---------------------------------------------------------------------------------------------
// Keyboard grid
// ---------------------------------------------------------------------------------------------

type ColumnKind = 'text' | 'number' | 'select' | 'checkbox' | 'switch' | 'date' | 'time' | 'custom' | 'none';
type EditMode = 'cell' | 'row' | 'dialog' | 'drawer';

/** How an edit started: what .NET hears, and how the editor is primed when it arrives. */
type Start = 'pointer' | 'key' | 'typing';

interface GridColumn {
  key: string;
  kind: ColumnKind;
}

interface GridOptions {
  /** Cell edits one cell at a time; Row opens every editor in the row; Dialog and Drawer edit the row in a form. */
  mode: EditMode;
  /** The visible data columns in order (the selection column is not listed). */
  columns: GridColumn[];
  /** A leading selection-checkbox column is present. */
  selectable: boolean;
  /** Body rows the cursor can reach: the whole view when virtualized, else the rendered rows. */
  rowCount: number;
  /** What may start typing over a number cell in the current culture: digits, signs, the decimal separator. */
  numberChars: string;
  /** A single click or tap edits a cell in place, as a double-click does (EditOnClick). */
  editOnClick: boolean;
}

/** A double-click on a cell's text: the text shown and the character offset clicked (-1: none). */
interface CaretHint {
  text: string;
  offset: number;
}

/** How a pending edit was started, beyond its typed text. */
interface Activation {
  start: Start;
  /** Ask for the editor's list or calendar open. */
  popup: boolean;
  /** Where a double-click put the caret (text and number editors). */
  caret?: CaretHint | null;
  /** The pointer's x, for the time segment under it. */
  pointX?: number | null;
}

interface EndResult {
  ok: boolean;
  /** Row mode: the key of the first invalid column, whose editor takes focus. */
  focus?: string | null;
}

interface Cell {
  /** Body row index (-1 = the header row). */
  row: number;
  /** cellIndex, so the selection column (when present) is 0. */
  col: number;
}

type Move = 'none' | 'up' | 'down' | 'left' | 'right' | 'next' | 'prev' | 'rowNext' | 'rowPrev';

interface KeyInput {
  key: string;
  shift: boolean;
  ctrl: boolean;
  alt: boolean;
  meta: boolean;
}

interface PendingEdit {
  row: number;
  col: number;
  /** Entry (started by typing over a cell in Cell mode): arrows commit and move. Otherwise they belong to the editor. */
  entry: boolean;
  /** Started by typing (or Backspace): the typed text replaces the value instead of adding to it. */
  replace: boolean;
  /** Before the editor exists: a replacing edit holds the whole typed text, an in-place edit the keys to append. */
  text: string | null;
  /** An Enter / Tab / Escape pressed before the editor existed, run as soon as it does. */
  queuedEnd: { commit: boolean; move: Move } | null;
  /** Where a double-click asked for the caret. */
  caret: CaretHint | null;
  /** A double-click's x, for a time field's segment. */
  pointX: number | null;
}

const EDITOR = '[data-bz-cell-editor]';
/** A cell whose controls act on its row's edit (an edit actions column). */
const ACTIONS = '[data-bz-edit-actions]';
/** The controls of an edit actions cell, which the sweep took out of the tab sequence (tabindex -1). */
const ACTION_CONTROLS =
  "button:not([disabled]), a[href], input:not([type='hidden']):not([disabled]), select:not([disabled]), " +
  "textarea:not([disabled]), [role='button']:not([aria-disabled='true'])";
/** Keys that only modify another key: pressing one alone is no sign the keyboard drives the grid. */
const MODIFIERS = new Set(['Shift', 'Control', 'Alt', 'Meta', 'AltGraph', 'CapsLock', 'OS']);
const FOCUSABLE =
  "a[href], button, input, select, textarea, [contenteditable='true'], [tabindex]:not([tabindex='-1'])";
/** What Tab walks inside an open editor (a date's segments and calendar button, a template's fields). */
const TABBABLE =
  "input:not([type='hidden']):not([disabled]), select:not([disabled]), textarea:not([disabled]), " +
  "button:not([disabled]), [contenteditable='true'], [tabindex]:not([tabindex='-1'])";
const ARROWS = new Set(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight']);
/** Editors whose field's text is the value: read here, sent with the commit, parsed in .NET. */
const TEXT_KINDS = new Set<string>(['text', 'number']);

const toKey = (e: KeyboardEvent): KeyInput => ({
  key: e.key,
  shift: e.shiftKey,
  ctrl: e.ctrlKey,
  alt: e.altKey,
  meta: e.metaKey,
});

// One printable character. AltGr arrives as Ctrl+Alt on Windows, so that pair still types.
const isPrintable = (k: KeyInput): boolean => k.key.length === 1 && !k.meta && (!k.ctrl || k.alt);

const isToggle = (kind: ColumnKind | undefined): boolean => kind === 'checkbox' || kind === 'switch';

/** Editors with a popup: a pick list's list, a date's calendar, and whatever a template opens. */
const hasPopup = (kind: ColumnKind | undefined): boolean => kind === 'select' || kind === 'date' || kind === 'custom';

/** Editors whose typed text replaces the value, and which Backspace empties to start again. */
const TYPED_KINDS = new Set<string>(['text', 'number', 'date', 'time', 'custom']);

const arrowMove = (key: string): Move =>
  key === 'ArrowUp' ? 'up' : key === 'ArrowDown' ? 'down' : key === 'ArrowLeft' ? 'left' : 'right';

const isDigit = (key: string): boolean => key.length === 1 && key >= '0' && key <= '9';

// The page's last input, the way :focus-visible decides: a key (other than a lone modifier) or a
// pointer press, heard first on the document. One pair of listeners for every grid on the page.
let lastInput: 'key' | 'pointer' = 'pointer';
let tracking = false;
const trackInput = (): void => {
  if (tracking) return;
  tracking = true;
  document.addEventListener('keydown', (e) => {
    if (!MODIFIERS.has(e.key)) lastInput = 'key';
  }, true);
  document.addEventListener('pointerdown', () => {
    lastInput = 'pointer';
  }, true);
};

type CaretDocument = Document & {
  caretPositionFromPoint?: (x: number, y: number) => { offsetNode: Node; offset: number } | null;
  caretRangeFromPoint?: (x: number, y: number) => Range | null;
};

/**
 * The character a double-click landed on, in the cell's text: the editor puts its caret there when
 * it starts from the same text. Offset -1 when the point is on no text.
 */
const caretHint = (cell: HTMLElement, x: number, y: number): CaretHint => {
  const raw = cell.textContent ?? '';
  const text = raw.trim();
  const doc = document as CaretDocument;
  let node: Node | null = null;
  let offset = 0;
  if (doc.caretPositionFromPoint) {
    const position = doc.caretPositionFromPoint(x, y);
    if (position) {
      node = position.offsetNode;
      offset = position.offset;
    }
  } else if (doc.caretRangeFromPoint) {
    const range = doc.caretRangeFromPoint(x, y);
    if (range) {
      node = range.startContainer;
      offset = range.startOffset;
    }
  }
  if (!node || node.nodeType !== Node.TEXT_NODE || !cell.contains(node)) return { text, offset: -1 };
  let total = 0;
  const walker = document.createTreeWalker(cell, NodeFilter.SHOW_TEXT);
  for (let n = walker.nextNode(); n; n = walker.nextNode()) {
    if (n === node) {
      total += offset;
      break;
    }
    total += n.textContent?.length ?? 0;
  }
  const lead = raw.length - raw.trimStart().length;
  return { text, offset: Math.min(Math.max(0, total - lead), text.length) };
};

class DataTableGrid {
  private readonly table: HTMLTableElement;
  private readonly container: HTMLElement;
  private readonly observer: MutationObserver;
  private readonly retired = new WeakSet<Element>();
  private readonly lastValues = new Map<string, string>();

  private active: Cell | null = null;
  private activeEl: HTMLElement | null = null;
  /** The grid has held focus: from then on the cursor can show. */
  private painted = false;
  /** The keyboard drives the grid (a key in the table, or Tab brought focus in): the cursor shows. A press clears it. */
  private keyboard = false;
  /** EditOnClick: a click on another cell while an edit was ending, run once that edit has closed. */
  private afterEnd: { pos: Cell; how: Activation } | null = null;
  private pendingFocus = false;
  private jumpRetries = 0;
  private focusedEl: Element | null = null;

  private phase: 'nav' | 'beginning' | 'editing' | 'ending' = 'nav';
  private edit: PendingEdit | null = null;
  private editorEl: HTMLElement | null = null;
  private queue: KeyInput[] = [];
  private tabRunCol: number | null = null;
  /** The header cells' inline widths before an edit pinned them (see lockWidths). */
  private pinned: { cell: HTMLElement; width: string }[] | null = null;
  private pinnedLayout = '';
  private watching = false;
  /** The last press: its pointer type, its cell and whether that cell held the cursor and focus. */
  private press: { type: string; cell: HTMLTableCellElement | null; wasActive: boolean } | null = null;
  /** Keys typed over a pick list, replayed into its list's type-ahead once the list holds focus. */
  private typeahead: string | null = null;
  private typeaheadTimer = 0;
  /** Tab in an open list: the move to make once the pick has committed (see commit). */
  private afterPick: Move | null = null;
  /** A form closed: focus goes to the cursor's cell once the surface lets go of it, until then. */
  private returnUntil = 0;
  private returnTimer = 0;
  /** The form field that held focus when a close asked Discard or Keep editing. */
  private remembered: HTMLElement | null = null;

  constructor(
    private readonly root: HTMLElement,
    private readonly ref: DotNetReference,
    private options: GridOptions,
  ) {
    this.table = root.querySelector<HTMLTableElement>('[data-slot="table-container"] > table')!;
    this.container = this.table.parentElement as HTMLElement;

    trackInput();
    root.addEventListener('keydown', this.onEditKeyDown, true);
    root.addEventListener('keydown', this.onKeyDown);
    root.addEventListener('pointerdown', this.onPointerDown, true);
    root.addEventListener('mousedown', this.onMouseDown);
    root.addEventListener('click', this.onClick);
    root.addEventListener('dblclick', this.onDoubleClick);
    root.addEventListener('focusin', this.onFocusIn);
    root.addEventListener('input', this.onInput);
    root.addEventListener('copy', this.onCopy);
    root.addEventListener('paste', this.onPaste);

    this.observer = new MutationObserver(this.onMutations);
    this.observer.observe(this.table, { childList: true, subtree: true });

    this.syncActive();
    this.sweep(this.table);
    this.syncEditor();
    // Attached: from here on keys and clicks reach the grid (tests and styles can wait on it).
    this.table.setAttribute('data-bz-grid', '');

    // Tab reached the server-rendered tab stop before this script attached: take it from there.
    const focused = document.activeElement;
    if (focused && this.container.contains(focused)) this.noteFocus(focused);
  }

  /** The open editors' text by column key (other controls report null): what a commit from .NET sends. */
  public values = (): Record<string, string | null> => this.collect(this.editScope());

  /**
   * Commits the open edit as Enter would, without moving: a pick from a cell's list, a template's
   * Commit. Through here rather than straight from .NET, so the focus is back on the cell before
   * the editor (and the list holding focus) goes. On the next task: .NET asks from inside the
   * pick's own event.
   */
  public commit = (): void => {
    setTimeout(() => {
      // Tab picked from an open list: commit and move on, as Tab does in an editor.
      const move = this.afterPick ?? 'none';
      this.afterPick = null;
      if (this.phase === 'editing') void this.endEdit(true, move);
    }, 0);
  };

  /** New columns, row count or mode from .NET (a sort, search, page or column toggle changed the view). */
  public update = (options: GridOptions): void => {
    this.options = options;
    if (this.active) this.active = this.clamp(this.active);
    this.syncActive();
  };

  /**
   * A form (Dialog, Drawer) closed: the cursor moves to the row it showed last (Previous / Next may
   * have moved on), in the same column, and takes the focus once the surface has let go of it - after
   * the exit animation, before or after the dialog's own focus return.
   */
  public formClosed = (row: number): void => {
    const col = this.active?.col ?? this.offset;
    this.setActive({ row, col }, false);
    this.returnUntil = Date.now() + 3000;
    clearTimeout(this.returnTimer);
    this.returnFocus();
  };

  /** A form asks Discard or Keep editing: keep the field that held focus, for Keep editing. */
  public rememberFocus = (): void => {
    const now = document.activeElement;
    this.remembered = now instanceof HTMLElement && now !== document.body ? now : null;
  };

  /** Keep editing: back to that field, or (a press on Cancel asked) the footer's Cancel once it is back. */
  public restoreFocus = (): void => {
    const remembered = this.remembered;
    this.remembered = null;
    let tries = 0;
    const attempt = (): void => {
      if (remembered?.isConnected && !remembered.matches('[data-bz-edit-cancel]')) {
        remembered.focus({ preventScroll: true });
        return;
      }
      const cancel = document.querySelector<HTMLElement>('[role=dialog] [data-bz-edit-cancel]');
      if (cancel) cancel.focus({ preventScroll: true });
      else if (tries++ < 20) setTimeout(attempt, 16);
    };
    attempt();
  };

  public dispose = (): void => {
    this.observer.disconnect();
    this.unlockWidths();
    clearTimeout(this.typeaheadTimer);
    clearTimeout(this.returnTimer);
    this.table.removeAttribute('data-bz-grid');
    this.root.removeEventListener('keydown', this.onEditKeyDown, true);
    this.root.removeEventListener('keydown', this.onKeyDown);
    this.root.removeEventListener('pointerdown', this.onPointerDown, true);
    this.root.removeEventListener('mousedown', this.onMouseDown);
    this.root.removeEventListener('click', this.onClick);
    this.root.removeEventListener('dblclick', this.onDoubleClick);
    this.root.removeEventListener('focusin', this.onFocusIn);
    this.watchFocus(false);
    this.root.removeEventListener('input', this.onInput);
    this.root.removeEventListener('copy', this.onCopy);
    this.root.removeEventListener('paste', this.onPaste);
  };

  // ---- geometry: rows, cells, columns ----

  private get tbody(): HTMLTableSectionElement | null {
    return this.table.tBodies[0] ?? null;
  }

  private get offset(): number {
    return this.options.selectable ? 1 : 0;
  }

  private get colCount(): number {
    return this.options.columns.length + this.offset;
  }

  private get hasHeader(): boolean {
    return (this.table.tHead?.rows.length ?? 0) > 0;
  }

  private rowEl(row: number): HTMLTableRowElement | null {
    if (row < 0) return this.table.tHead?.rows[0] ?? null;
    return this.tbody?.querySelector<HTMLTableRowElement>(`:scope > tr[data-bz-row-index="${row}"]`) ?? null;
  }

  private cellAt(cell: Cell): HTMLTableCellElement | null {
    return this.rowEl(cell.row)?.cells[cell.col] ?? null;
  }

  /** The grid position of a td / th inside this table, or null. */
  private position(cell: Element | null): Cell | null {
    if (!(cell instanceof HTMLTableCellElement) || cell.closest('table') !== this.table) return null;
    const tr = cell.parentElement as HTMLTableRowElement;
    const head = this.table.tHead;
    if (head && tr.parentElement === head) return tr === head.rows[0] ? { row: -1, col: cell.cellIndex } : null;
    const index = Number(tr.dataset.bzRowIndex);
    return tr.dataset.bzRowIndex !== undefined && Number.isInteger(index) ? { row: index, col: cell.cellIndex } : null;
  }

  private cellOf(target: Element): HTMLTableCellElement | null {
    const cell = target.closest<HTMLTableCellElement>('td, th');
    return cell && cell.closest('table') === this.table ? cell : null;
  }

  /** The column at a cellIndex: a data column, or the selection column (kind 'none', key ''). */
  private columnAt(col: number): GridColumn | null {
    if (col < this.offset) return col >= 0 ? { key: '', kind: 'none' } : null;
    return this.options.columns[col - this.offset] ?? null;
  }

  private get anyEditable(): boolean {
    return this.options.columns.some((c) => c.kind !== 'none');
  }

  /** Dialog and Drawer modes: the row edits in a form .NET renders outside the table. */
  private get form(): boolean {
    return this.options.mode === 'dialog' || this.options.mode === 'drawer';
  }

  /** Whether a typed character can start editing this kind of cell (quick entry). */
  private typeable(kind: ColumnKind, key: string): boolean {
    switch (kind) {
      case 'number':
        return this.options.numberChars.includes(key);
      case 'date':
      case 'time':
        return isDigit(key);
      case 'text':
      case 'select':
      case 'custom':
        return true;
      default:
        return false;
    }
  }

  // After a form closed: once no dialog holds the focus (the surface is gone), it goes to the
  // cursor's cell; a press or a key elsewhere in the meantime wins.
  private returnFocus = (): void => {
    if (!this.returnUntil) return;
    if (Date.now() > this.returnUntil) {
      this.returnUntil = 0;
      return;
    }
    const now = document.activeElement;
    const inDialog = !!now?.closest('[role=dialog], [role=alertdialog]');
    const free = !now || now === document.body || this.container.contains(now);
    if (!inDialog && !free) {
      // The user moved on to something outside the table.
      this.returnUntil = 0;
      return;
    }
    if (!inDialog && this.phase === 'nav' && this.active) {
      const cell = this.cellAt(this.active);
      if (cell && now !== cell) this.go(this.active);
      else if (!cell) this.go(this.active);
    }
    // Keep watching a moment: the dialog's own focus return can land after this one.
    this.returnTimer = window.setTimeout(this.returnFocus, 50);
  };

  /** Whether F2, typing or a double-click on this cell opens an editor (and Enter does in Row and Dialog modes). */
  private canBegin(pos: Cell): boolean {
    if (pos.row < 0) return false;
    const column = this.columnAt(pos.col);
    if (!column || pos.col < this.offset) return false;
    if (this.options.mode === 'cell') return column.kind !== 'none' && !isToggle(column.kind);
    return this.anyEditable;
  }

  private clamp(cell: Cell): Cell {
    const minRow = this.hasHeader ? -1 : 0;
    const maxRow = Math.max(minRow, this.options.rowCount - 1);
    return {
      row: Math.min(Math.max(cell.row, minRow), maxRow),
      col: Math.min(Math.max(cell.col, 0), Math.max(0, this.colCount - 1)),
    };
  }

  private get rtl(): boolean {
    return getComputedStyle(this.table).direction === 'rtl';
  }

  // ---- the cursor ----

  private setActive(cell: Cell, focus: boolean): void {
    const target = this.clamp(cell);
    this.active = target;
    const el = this.cellAt(target);
    this.bind(el);
    if (!focus) return;
    if (el) {
      el.focus({ preventScroll: true });
      this.reveal(el);
    } else if (target.row >= 0) {
      // A virtualized row outside the window: bring it in; the observer focuses it on arrival.
      this.pendingFocus = true;
      this.jumpRetries = 0;
      this.jump(target.row);
    }
  }

  // Scroll a virtualized row into the window. scrollToIndex puts the row's top at the viewport's
  // top, under a sticky header if there is one, so step back by the header's height: plain
  // arithmetic on the scroll position. Measuring the row instead is unreliable right after the
  // jump, while the spacer rows around the new band can still lay out at their old heights.
  private jump(row: number): void {
    const virtualizer = virtualizerFor(this.tbody);
    if (!virtualizer) return;
    virtualizer.scrollToIndex(row);
    const head = this.stickyHeadHeight();
    if (head <= 0) return;
    const selector = this.tbody?.dataset.bzVirtualViewport;
    const box = selector ? document.querySelector<HTMLElement>(selector) : null;
    // Near the end of the list the scroll position is clamped and the row sits lower, clear of the
    // header: nothing to step back from. Worked out from the row count and height, not scrollHeight,
    // which can be just as stale as the rows here.
    const rowHeight = Number(this.tbody?.dataset.bzVirtualRowHeight) || 40;
    const viewport = box ? box.clientHeight : window.innerHeight;
    if ((this.options.rowCount - row) * rowHeight <= viewport) return;
    if (box) box.scrollTop -= head;
    else window.scrollBy({ top: -head, behavior: 'instant' });
  }

  private stickyHeadHeight(): number {
    const head = this.table.tHead;
    const cell = head?.rows[0]?.cells[0];
    return head && cell && isStickyHead(cell) ? head.getBoundingClientRect().height : 0;
  }

  // Move the roving tab stop (and the painted marker) onto el.
  private bind(el: HTMLTableCellElement | null): void {
    if (el === this.activeEl) {
      // A render can take the attribute away (an emptied table's header stop): put it back.
      if (el && el.getAttribute('tabindex') !== '0') el.tabIndex = 0;
      if (el) el.toggleAttribute('data-active', this.showsCursor);
      return;
    }
    if (this.activeEl) {
      this.activeEl.removeAttribute('data-active');
      // Out of the tab sequence. A cell that still holds focus keeps -1 rather than losing the
      // attribute, which would make it unfocusable and drop focus to the body mid-move.
      if (this.activeEl === document.activeElement) this.activeEl.tabIndex = -1;
      else this.activeEl.removeAttribute('tabindex');
    }
    this.activeEl = el;
    if (!el) return;
    el.tabIndex = 0;
    if (this.showsCursor) el.setAttribute('data-active', '');
  }

  // The cursor's cell is marked once the grid has held focus, and only while the keyboard drives it.
  private get showsCursor(): boolean {
    return this.painted && this.keyboard;
  }

  // Follow the page's last input: a key shows the cursor, a pointer press hides it until the next key.
  private syncInput(): void {
    const keyboard = lastInput === 'key';
    if (keyboard === this.keyboard) return;
    this.keyboard = keyboard;
    this.activeEl?.toggleAttribute('data-active', this.showsCursor);
  }

  // scrollIntoView, minus the part a sticky header covers, and the parts pinned columns cover.
  private reveal(cell: HTMLElement): void {
    const head = this.table.tHead?.rows[0];
    const headCell = head?.cells[0];
    const sticky = cell.tagName === 'TD' && headCell && isStickyHead(headCell);
    cell.style.scrollMarginTop = sticky ? `${this.table.tHead!.getBoundingClientRect().height}px` : '';
    let start = 0;
    let end = 0;
    if (head && !cell.hasAttribute('data-pinned')) {
      for (const th of Array.from(head.cells)) {
        const pin = th.dataset.pinned;
        if (pin === 'start') start += th.getBoundingClientRect().width;
        else if (pin === 'end') end += th.getBoundingClientRect().width;
      }
    }
    cell.style.scrollMarginInlineStart = start > 0 ? `${start}px` : '';
    cell.style.scrollMarginInlineEnd = end > 0 ? `${end}px` : '';
    cell.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }

  private syncActive(): void {
    if (!this.active) {
      const rows = this.options.rowCount;
      if (rows > 0) this.active = { row: 0, col: Math.min(this.offset, this.colCount - 1) };
      else if (this.hasHeader) this.active = { row: -1, col: 0 };
      else return;
    }
    const el = this.cellAt(this.active);
    this.bind(el);
    if (el && this.pendingFocus) {
      // Arrived after a jump through the virtualizer, which already scrolled the row in (see
      // jump). Focus in place: a scrollIntoView here would measure the stale layout and scroll
      // the row back out.
      this.pendingFocus = false;
      el.focus({ preventScroll: true });
      this.settle();
    }
  }

  // The last step of a jump, on the next task: the cursor's cell should still be rendered and
  // hold focus. A later re-render can have re-created or scrolled it away (Blazor re-focuses a
  // re-created row without preventScroll); then go through the virtualizer again, at most twice.
  private settle(): void {
    setTimeout(() => {
      if (!this.active || this.phase !== 'nav') return;
      const el = this.cellAt(this.active);
      if (!el) {
        if (this.jumpRetries++ < 2 && this.active.row >= 0) {
          this.pendingFocus = true;
          this.jump(this.active.row);
        }
        return;
      }
      const now = document.activeElement;
      if (now !== el && (!now || now === document.body || this.container.contains(now))) el.focus({ preventScroll: true });
    }, 0);
  }

  private go(cell: Cell): void {
    this.setActive(cell, true);
  }

  private arrowTarget(from: Cell, key: string, jump: boolean): Cell {
    const ltr = !this.rtl;
    const lastRow = this.options.rowCount - 1;
    const firstRow = this.hasHeader && !jump ? -1 : 0;
    switch (key) {
      case 'ArrowUp':
        return { row: jump ? 0 : Math.max(firstRow, from.row - 1), col: from.col };
      case 'ArrowDown':
        return { row: jump ? lastRow : from.row + 1, col: from.col };
      case 'ArrowLeft':
        return { row: from.row, col: jump ? (ltr ? 0 : this.colCount - 1) : from.col + (ltr ? -1 : 1) };
      default:
        return { row: from.row, col: jump ? (ltr ? this.colCount - 1 : 0) : from.col + (ltr ? 1 : -1) };
    }
  }

  // Reading order with wrap; null past either end of the body (the browser's Tab leaves the grid).
  private tabTarget(from: Cell, back: boolean): Cell | null {
    let { row, col } = from;
    col += back ? -1 : 1;
    if (col >= this.colCount) {
      row += 1;
      col = 0;
    } else if (col < 0) {
      row -= 1;
      col = this.colCount - 1;
    }
    if (from.row < 0 && row >= 0) return { row: 0, col: 0 }; // off the header's end: into the body
    if (row < (from.row < 0 ? -1 : 0) || row >= this.options.rowCount) return null;
    return { row, col };
  }

  private pageRows(): number {
    const row = this.activeEl?.parentElement as HTMLElement | null;
    const height = row?.offsetHeight || 40;
    const scrolls = this.container.scrollHeight > this.container.clientHeight + 1 &&
      /(auto|scroll)/.test(getComputedStyle(this.container).overflowY);
    const view = scrolls ? this.container.clientHeight : window.innerHeight;
    return Math.max(1, Math.floor(view / height) - 1);
  }

  // Where a move lands. Down returns to the column a run of Tabs started from, spreadsheet-style:
  // Tab across a row of entries, Enter, and the cursor is at the start of the next row.
  private resolveMove(from: Cell, move: Move): Cell {
    switch (move) {
      case 'up':
        this.tabRunCol = null;
        return { row: from.row - 1, col: from.col };
      case 'down': {
        const col = this.tabRunCol ?? from.col;
        this.tabRunCol = null;
        return { row: from.row + 1, col };
      }
      case 'left':
      case 'right':
        this.tabRunCol = null;
        return this.arrowTarget(from, move === 'left' ? 'ArrowLeft' : 'ArrowRight', false);
      case 'next':
      case 'prev':
        return this.tabTarget(from, move === 'prev') ?? from;
      case 'rowNext':
        return { row: from.row + 1, col: this.offset };
      case 'rowPrev':
        return { row: from.row - 1, col: this.colCount - 1 };
      default:
        return from;
    }
  }

  // ---- key routing ----

  // Idle, keys reach the grid after the cell's own controls (a menu trigger in a cell keeps its
  // arrows). While an edit is open the grid hears them first, on the way down: Enter, Tab, Escape,
  // F2 and an entry's arrows are the grid's even when the editor's own control would act on them
  // (a pick list opens on an arrow, a number field steps).
  private onEditKeyDown = (e: KeyboardEvent): void => {
    // A key in the table: the cursor shows from here on (the document heard it first).
    if (e.target instanceof Node && this.container.contains(e.target)) this.syncInput();
    if (this.phase !== 'nav') this.route(e);
  };

  private onKeyDown = (e: KeyboardEvent): void => {
    if (this.phase === 'nav') this.route(e);
  };

  private route(e: KeyboardEvent): void {
    if (e.defaultPrevented || e.isComposing || e.key === 'Process' || e.key === 'Unidentified') return;
    const target = e.target as HTMLElement | null;
    if (!target || !(this.container.contains(target))) return;
    this.returnUntil = 0; // a key in the table: a closed form's focus return is over
    if (!this.dispatch(toKey(e), target)) return;
    e.preventDefault();
    // A consumed key is the grid's alone. Blazor handles keys from a listener on the document, and
    // the editor that would hear this one may be gone already: a commit renders it away while this
    // event is still on its way.
    e.stopPropagation();
  }

  /** Routes one key by phase and origin. True when the grid consumed it. */
  private dispatch(k: KeyInput, target: Element | null): boolean {
    if (this.phase === 'beginning') return this.bufferKey(k);
    if (this.phase === 'ending') {
      this.queue.push(k);
      return true;
    }

    const scope = this.phase === 'editing' ? this.editScope() : null;
    if (scope && (!target || scope.contains(target) || this.isAction(target))) return this.editKey(k, target);

    if (target === this.container || !target) return this.active ? this.navKey(k, this.active, false) : false;

    const cell = this.cellOf(target);
    const pos = this.position(cell);
    if (!cell || !pos) return false;

    const control = target !== cell ? target.closest(INTERACTIVE) : null;
    const fromControl = !!control && cell.contains(control);
    if (fromControl && isEditableTarget(target)) {
      // A consumer's own text control in a template cell keeps its keys; Escape steps back out.
      if (k.key !== 'Escape') return false;
      cell.focus();
      return true;
    }
    return this.navKey(k, pos, fromControl);
  }

  private navKey(k: KeyInput, pos: Cell, fromControl: boolean): boolean {
    const mode = this.options.mode;
    const column = this.columnAt(pos.col);
    const kind = column?.kind ?? 'none';
    const plain = !k.ctrl && !k.meta && !k.alt;
    const body = pos.row >= 0 && pos.col >= this.offset;

    // Alt+ArrowDown opens a pick list's list or a date's calendar (the row's, in Row mode).
    if (k.key === 'ArrowDown' && k.alt && !k.ctrl && !k.meta && !k.shift) {
      if (!body || fromControl || this.form || !hasPopup(kind) || !this.canBegin(pos)) return false;
      void this.beginEdit(pos, null, { start: 'key', popup: true });
      return true;
    }

    if (ARROWS.has(k.key)) {
      if (k.alt || k.shift) return false;
      this.tabRunCol = null;
      this.go(this.arrowTarget(pos, k.key, k.ctrl || k.meta));
      return true;
    }

    switch (k.key) {
      case 'Home':
      case 'End': {
        if (k.alt || k.shift) return false;
        const end = k.key === 'End';
        const jump = k.ctrl || k.meta;
        this.tabRunCol = null;
        this.go({ row: jump ? (end ? this.options.rowCount - 1 : 0) : pos.row, col: end ? this.colCount - 1 : 0 });
        return true;
      }
      case 'PageUp':
      case 'PageDown': {
        if (!plain || k.shift) return false;
        const step = this.pageRows();
        this.tabRunCol = null;
        this.go({ row: Math.max(0, pos.row + (k.key === 'PageDown' ? step : -step)), col: pos.col });
        return true;
      }
      case 'Tab': {
        if (!plain) return false;
        const next = this.tabTarget(pos, k.shift);
        if (!next) return false;
        if (!k.shift && this.tabRunCol === null) this.tabRunCol = pos.col;
        this.go(next);
        return true;
      }
      case 'Enter':
        if (!plain || fromControl) return false;
        // The header: sort, or select all.
        if (pos.row < 0) return k.shift ? false : this.pressControl(pos);
        if (mode === 'cell') {
          // A read-only cell holding a control (row actions, a link): Enter steps into it.
          if (!k.shift && kind === 'none' && pos.col >= this.offset && this.enterControl(pos)) return true;
          // Spreadsheet-style: Enter moves down (back to where a run of Tabs began), Shift+Enter up.
          this.go(this.resolveMove(pos, k.shift ? 'up' : 'down'));
          return true;
        }
        // Row, Dialog and Drawer modes: Enter opens the row's editors or its form.
        if (k.shift) return false;
        if (this.canBegin(pos)) {
          void this.beginEdit(pos, null, { start: 'key', popup: false });
          return true;
        }
        return this.enterControl(pos);
      case 'F2':
        // In place: the current value, the caret at the end; a pick list opens its list.
        if (!plain || !this.canBegin(pos)) return false;
        void this.beginEdit(pos, null, { start: 'key', popup: kind === 'select' });
        return true;
      case ' ':
        if (k.ctrl || k.meta || k.alt) return false;
        if (k.shift) return this.options.selectable && pos.row >= 0 ? this.pressSelect(pos.row) : false;
        if (fromControl) return false;
        if (!body) return this.pressControl(pos);
        if (isToggle(kind)) {
          // Cell mode flips it; the other modes open their editor with the box flipped.
          this.toggle(pos);
          return true;
        }
        // A pick list opens its list (a form opens with Enter or F2 instead).
        if (kind === 'select' && !this.form && this.canBegin(pos)) {
          void this.beginEdit(pos, null, { start: 'key', popup: true });
          return true;
        }
        break; // a space is text: falls through to typing below
      case 'Delete':
        if (!plain || fromControl || mode !== 'cell' || !body || kind === 'none') return false;
        void invokeDotNet(this.ref, 'Clear', pos.row, pos.col);
        return true;
      case 'Backspace':
        if (!plain || fromControl || this.form || !body || kind === 'none') return false;
        // A typed value starts over empty (quick entry); any other value clears, as Delete does.
        if (TYPED_KINDS.has(kind)) void this.beginEdit(pos, '', { start: 'typing', popup: false });
        else if (mode === 'cell') void invokeDotNet(this.ref, 'Clear', pos.row, pos.col);
        else return false;
        return true;
      default:
        break;
    }

    // Typing over a cell (quick entry): the editor opens holding only the typed character, in Cell
    // mode (where the arrows then commit and move) and in the row's editor of this column in Row
    // mode. A number takes only what can start one; a date or time a digit; a pick list opens and
    // types it into its list. A checkbox or switch ignores it, and a form opens with Enter or F2.
    if (!isPrintable(k) || fromControl || !body || this.form || !column) return false;
    if (isToggle(kind) || !this.typeable(kind, k.key)) return false;
    if (mode === 'row' && !this.anyEditable) return false;
    void this.beginEdit(pos, k.key, { start: 'typing', popup: kind === 'select' });
    return true;
  }

  private editKey(k: KeyInput, target: Element | null): boolean {
    const scope = this.editScope();
    if (!scope) return false;
    const cell = this.options.mode === 'cell';

    // One Escape cancels, even with the editor's list or calendar open.
    if (k.key === 'Escape' && !k.ctrl && !k.meta && !k.alt) {
      void this.endEdit(false, 'none');
      return true;
    }
    // On an edit action (Save, Cancel): Tab walks on with the editors, every other key is the
    // control's own (Enter and Space press it).
    if (target && this.isAction(target) && !target.closest(EDITOR)) {
      if (k.key !== 'Tab' || k.ctrl || k.meta || k.alt) return false;
      if (this.tabWithin(scope, target, k.shift)) return true;
      void this.endEdit(true, cell ? (k.shift ? 'prev' : 'next') : k.shift ? 'rowPrev' : 'rowNext');
      return true;
    }
    if (this.popupOpen(scope)) {
      // Typed over a pick list before its list took the focus: the keys wait for its type-ahead.
      if (this.typeahead !== null && isPrintable(k)) {
        this.typeahead += k.key;
        return true;
      }
      return false;
    }

    switch (k.key) {
      case 'Enter':
        if (k.alt) return false;
        void this.endEdit(true, k.ctrl || k.meta ? 'none' : k.shift ? 'up' : 'down');
        return true;
      case 'Tab': {
        if (k.ctrl || k.meta || k.alt) return false;
        // Tab walks the editor's own fields first (a date's segments, a row's editors), and commits
        // and moves on past the last one.
        if (this.tabWithin(scope, target, k.shift)) return true;
        if (cell && !k.shift && this.tabRunCol === null && this.edit) this.tabRunCol = this.edit.col;
        void this.endEdit(true, cell ? (k.shift ? 'prev' : 'next') : k.shift ? 'rowPrev' : 'rowNext');
        return true;
      }
      case 'F2':
        // Switch between entry (arrows commit and move) and editing in place (arrows stay in the editor).
        if (!cell || !this.edit || !this.editorEl) return false;
        this.edit.entry = !this.edit.entry;
        this.editorEl.toggleAttribute('data-entry', this.edit.entry);
        return true;
      default:
        if (ARROWS.has(k.key) && cell && this.edit?.entry && !k.shift && !k.ctrl && !k.meta && !k.alt) {
          void this.endEdit(true, arrowMove(k.key));
          return true;
        }
        return false;
    }
  }

  // Tab inside an open edit: to the next of its fields, when there is one past the focused field.
  // After the row's last editor come its edit actions (Save, Cancel), wherever their column sits.
  private tabWithin(scope: HTMLElement, target: Element | null, back: boolean): boolean {
    const fields = this.tabbables(scope);
    const current = fields.findIndex((el) => el === target || el.contains(target));
    const next = current + (back ? -1 : 1);
    if (current < 0 || next < 0 || next >= fields.length) return false;
    const field = fields[next];
    const wrapper = field.closest<HTMLElement>(EDITOR);
    // Into another cell's editor or an edit action: the cursor follows.
    const pos = this.position(field.closest('td'));
    if (pos && this.edit && (pos.row !== this.active?.row || pos.col !== this.active?.col)) {
      this.edit.col = pos.col;
      this.setActive(pos, false);
    }
    if (wrapper) this.editorEl = wrapper;
    field.focus({ preventScroll: true });
    if (field instanceof HTMLInputElement && isTextInput(field)) field.select();
    return true;
  }

  private tabbables(scope: Element): HTMLElement[] {
    const fields: HTMLElement[] = [];
    scope.querySelectorAll<HTMLElement>(EDITOR).forEach((editor) =>
      editor.querySelectorAll<HTMLElement>(TABBABLE).forEach((el) => {
        if (el.tabIndex >= 0 && !el.closest('[inert]') && el.getClientRects().length > 0) fields.push(el);
      }),
    );
    // A row's Save and Cancel come after its editors. A cell edit keeps spreadsheet Tab: commit and move.
    if (this.options.mode === 'row') fields.push(...this.actionControls(scope));
    return fields;
  }

  // Before the editor exists: typing builds the text, an ending key waits for the editor.
  private bufferKey(k: KeyInput): boolean {
    const edit = this.edit;
    if (!edit) return true;
    if (edit.queuedEnd) {
      this.queue.push(k);
      return true;
    }
    const cell = this.options.mode === 'cell';
    if (isPrintable(k)) edit.text = (edit.text ?? '') + k.key;
    else if (k.key === 'Backspace') edit.text = (edit.text ?? '').slice(0, -1);
    else if (k.key === 'Escape') edit.queuedEnd = { commit: false, move: 'none' };
    else if (k.key === 'Enter') edit.queuedEnd = { commit: true, move: k.ctrl || k.meta ? 'none' : k.shift ? 'up' : 'down' };
    else if (k.key === 'Tab') edit.queuedEnd = { commit: true, move: cell ? (k.shift ? 'prev' : 'next') : k.shift ? 'rowPrev' : 'rowNext' };
    else if (ARROWS.has(k.key) && edit.entry && cell) edit.queuedEnd = { commit: true, move: arrowMove(k.key) };
    return true;
  }

  // Replays keys typed while .NET was busy, through the same routing as live keys.
  private drain(): void {
    while (this.queue.length > 0 && (this.phase === 'nav' || this.phase === 'editing' || this.phase === 'beginning')) {
      const k = this.queue.shift()!;
      if (this.phase === 'editing') this.replayIntoEditor(k);
      else this.dispatch(k, this.phase === 'nav' ? this.activeEl : null);
    }
  }

  // A queued key landing in a live editor: text appends to the field being typed into, ending keys end.
  private replayIntoEditor(k: KeyInput): void {
    const field = this.editorEl ? this.typingField(this.editorEl) : null;
    if (field && (isPrintable(k) || k.key === 'Backspace')) {
      field.value = isPrintable(k) ? field.value + k.key : field.value.slice(0, -1);
      field.dispatchEvent(new Event('input', { bubbles: true }));
      this.remember(this.editorEl!);
    } else {
      this.editKey(k, document.activeElement);
    }
  }

  // ---- edit lifecycle ----

  private async beginEdit(pos: Cell, seed: string | null, how: Activation): Promise<void> {
    if (this.phase !== 'nav') return;
    this.afterEnd = null;
    const typing = how.start === 'typing';
    this.phase = 'beginning';
    this.edit = {
      row: pos.row,
      col: pos.col,
      entry: typing && this.options.mode === 'cell',
      replace: typing,
      text: seed,
      queuedEnd: null,
      caret: how.caret ?? null,
      pointX: how.pointX ?? null,
    };
    this.setActive(pos, false);
    // Widths first, measured before the editor arrives: nothing the editor brings can move them.
    if (!this.form) this.lockWidths();

    let ok = false;
    try {
      ok = await invokeDotNetResult<boolean>(this.ref, false, 'BeginEdit', pos.row, pos.col, seed, how.start, how.popup);
    } catch (e) {
      this.resetEdit();
      throw e;
    }

    if (!ok || this.form) {
      // Refused, or the form owns the edit from here (focus returns to this row when it closes).
      this.resetEdit();
      this.drain();
      return;
    }
    // The render may already be here; otherwise the observer adopts the editor when it lands.
    this.syncEditor();
  }

  private resetEdit(): void {
    this.phase = 'nav';
    this.edit = null;
    this.editorEl = null;
    this.typeahead = null;
    this.afterPick = null;
    clearTimeout(this.typeaheadTimer);
    this.lastValues.clear();
    this.unlockWidths();
    this.watchFocus(false);
  }

  // An editor .NET rendered: started here (beginning) or by .NET itself (BeginEditAsync).
  private adopt(found: HTMLElement): void {
    const edit = this.edit;
    const foundPos = this.position(found.closest('td'));
    if (!foundPos) return;

    // Row mode: focus the editor in the column the edit started from; when .NET opened the row, the
    // column it asked for (BeginEditAsync with a key: [data-bz-focus]) or the cursor's column; else
    // the row's first.
    let editor = found;
    if (this.options.mode === 'row') {
      const own = edit && edit.row === foundPos.row ? edit.col : -1;
      const asked = own < 0 ? this.rowEl(foundPos.row)?.querySelector<HTMLElement>(`${EDITOR}[data-bz-focus]`) : null;
      const col = own >= 0 ? own : this.active?.row === foundPos.row ? this.active.col : -1;
      const wanted = asked ?? (col >= 0 ? this.cellAt({ row: foundPos.row, col })?.querySelector<HTMLElement>(EDITOR) : null);
      editor = wanted ?? found;
    }
    const pos = this.position(editor.closest('td'))!;
    const primed: PendingEdit = {
      row: pos.row,
      col: pos.col,
      entry: !!edit?.entry && this.options.mode === 'cell',
      replace: !!edit?.replace,
      text: edit?.text ?? null,
      queuedEnd: null,
      caret: edit?.caret ?? null,
      pointX: edit?.pointX ?? null,
    };
    const queued = edit?.queuedEnd ?? null;

    this.lockWidths(); // an edit .NET opened by itself never passed through beginEdit
    this.watchFocus(true);
    this.phase = 'editing';
    this.edit = { ...primed, text: null, caret: null, pointX: null };
    this.editorEl = editor;
    this.setActive(pos, false);
    this.fit();
    this.prime(editor, primed);

    if (queued) void this.endEdit(queued.commit, queued.move);
    else this.drain();
  }

  // A date field wider than its column gives up its calendar button (typing and the arrow keys
  // still edit it), so it stays inside the cell instead of laying over the next one.
  private fit(): void {
    this.editScope()?.querySelectorAll<HTMLElement>('[data-bz-cell-editor=date]').forEach((wrapper) => {
      const field = wrapper.firstElementChild as HTMLElement | null;
      const button = field?.querySelector<HTMLElement>(':scope > button:last-child');
      const room = wrapper.clientWidth - 4; // the editor's own hairline padding
      // A calendar opened from the start keeps its button: the popup is anchored to it.
      if (field && button && field.offsetWidth > room && button.getAttribute('aria-expanded') !== 'true') button.style.display = 'none';
    });
  }

  // Focus the editor and hand it what was typed before it existed: typing replaces the field's text,
  // an in-place edit appends. The caret goes where a double-click asked when the text is the one
  // that was clicked, else to the end; a time field focuses the segment under the pointer. Typed
  // over a pick list, the keys go to its list's type-ahead once the list holds the focus.
  private prime(editor: HTMLElement, edit: PendingEdit): void {
    editor.toggleAttribute('data-entry', edit.entry);
    const kind = editor.dataset.bzCellEditor ?? '';
    const text = edit.text;
    if (kind === 'select' && edit.replace && text) this.queueTypeahead(editor, text);
    if (kind === 'date' && editor.querySelector('[aria-controls][aria-expanded="true"]')) this.focusCalendar(editor);

    const place = (): void => {
      const field = this.typingField(editor);
      if (field && (edit.replace ? text !== null : !!text)) {
        field.value = edit.replace ? text! : field.value + text;
        // The control keeps its own state in step with its field (a number's value, a date segment).
        field.dispatchEvent(new Event('input', { bubbles: true }));
      }
      // A text box starts with the caret at the end, or where a double-click landed; a date or
      // time segment keeps its own select-all, so a typed digit replaces it.
      const focused = document.activeElement;
      const caret = TEXT_KINDS.has(kind) || kind === 'custom';
      if (caret && focused instanceof HTMLInputElement && editor.contains(focused) && isTextInput(focused)) {
        const hint = edit.caret;
        const at = hint && hint.offset >= 0 && !edit.replace && focused.value === hint.text ? hint.offset : focused.value.length;
        focused.setSelectionRange(at, at);
      }
      if (this.activeEl) this.reveal(this.activeEl);
      this.rememberAll();
    };

    // The editor's popup may already hold the focus (its list or calendar opened at once): leave it.
    const now = document.activeElement;
    const scope = this.editScope();
    if (now && scope && this.ownsPopup(scope, now)) {
      place();
      return;
    }
    const focusing = focusElement(this.focusTarget(editor, edit.pointX), { preventScroll: true });
    // The first try focuses synchronously; a field still mid-render gets the retries.
    if (document.activeElement && editor.contains(document.activeElement)) place();
    else void focusing.then(place);
  }

  // A date's calendar opened with the edit: the focus moves onto its current day, as opening it
  // from its button does, so the arrows walk the days (the popup only takes focus once it is shown).
  private focusCalendar(editor: HTMLElement): void {
    let tries = 0;
    const attempt = (): void => {
      if (this.phase !== 'editing' || this.editorEl !== editor) return;
      const trigger = editor.querySelector<HTMLElement>('[aria-controls][aria-expanded="true"]');
      const popup = trigger ? document.getElementById(trigger.getAttribute('aria-controls') ?? '') : null;
      // The calendar's own focus target, else the picked day, else today, else the month's first.
      const grid = popup?.querySelector<HTMLElement>('[data-slot=calendar-month-grid]');
      const day = grid?.querySelector<HTMLElement>('button[tabindex="0"]') ??
        grid?.querySelector<HTMLElement>('button[data-selected]:not([data-outside])') ??
        grid?.querySelector<HTMLElement>('button[data-today]:not([data-outside])') ??
        grid?.querySelector<HTMLElement>('button:not([data-outside]):not([aria-disabled="true"])');
      if (popup && day && day.getClientRects().length > 0 && getComputedStyle(popup).visibility !== 'hidden') {
        day.focus({ preventScroll: true });
        if (document.activeElement === day) return;
      }
      if (tries++ < 60) window.setTimeout(attempt, 16);
    };
    attempt();
  }

  // Keys typed over a pick list: once its list holds the focus (the menu script places it on the
  // current option), they are typed into the list as its own type-ahead, which moves the highlight.
  private queueTypeahead(editor: HTMLElement, text: string): void {
    this.typeahead = text;
    let tries = 0;
    const flush = (): void => {
      if (this.phase !== 'editing' || this.editorEl !== editor || this.typeahead === null) return;
      const scope = this.editScope();
      const now = document.activeElement;
      if (scope && now && now.matches('[role=option]') && this.ownsPopup(scope, now)) {
        const keys = this.typeahead;
        this.typeahead = null;
        for (const key of keys) {
          (document.activeElement ?? now).dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
        }
        return;
      }
      if (tries++ < 60) this.typeaheadTimer = window.setTimeout(flush, 16);
      else this.typeahead = null;
    };
    flush();
  }

  // Where focus goes in an editor: its first field, or the time segment under a double-click. A
  // pick list, a checkbox or a switch is a button, which the shared focus helper would not look for
  // inside a wrapper.
  private focusTarget(editor: HTMLElement, pointX: number | null = null): HTMLElement {
    const fields = Array.from(editor.querySelectorAll<HTMLElement>(TABBABLE)).filter((el) => el.tabIndex >= 0);
    if (pointX !== null && editor.dataset.bzCellEditor === 'time') {
      const under = fields.find((el) => {
        const box = el.getBoundingClientRect();
        return el instanceof HTMLInputElement && pointX >= box.left && pointX <= box.right;
      });
      if (under) return under;
    }
    return fields[0] ?? editor;
  }

  // The field typed keys go to: a text or number editor's input, else the focused text field inside
  // the editor (a date or time segment, a template's own text box). A pick list has none.
  private typingField(editor: HTMLElement): HTMLInputElement | HTMLTextAreaElement | null {
    const own = this.textField(editor);
    if (own) return own;
    const focused = document.activeElement;
    if (focused instanceof HTMLTextAreaElement && editor.contains(focused)) return focused;
    return focused instanceof HTMLInputElement && editor.contains(focused) && isTextInput(focused) ? focused : null;
  }

  // The field whose text an editor commits: a text or number editor's input.
  private textField(editor: Element): HTMLInputElement | HTMLTextAreaElement | null {
    if (!TEXT_KINDS.has((editor as HTMLElement).dataset.bzCellEditor ?? '')) return null;
    return editor.querySelector<HTMLInputElement | HTMLTextAreaElement>('input, textarea');
  }

  private async endEdit(commit: boolean, move: Move, blur = false): Promise<void> {
    if (this.phase !== 'editing' || !this.edit) return;
    const from: Cell = { row: this.edit.row, col: this.edit.col };
    const scope = this.editScope();
    const values = commit ? this.collect(scope) : null;
    scope?.querySelectorAll(EDITOR).forEach((el) => this.retired.add(el));
    this.phase = 'ending';

    // Hand focus to the cell before .NET re-renders the editor away. Under WebAssembly that render
    // runs inside the call below, and removing a focused element drops focus to the page body,
    // where the next keys would land outside the grid. On the cell they queue until the result.
    if (!blur) this.cellAt(from)?.focus({ preventScroll: true });

    // A click-away to somewhere outside the grid discards an invalid edit instead of pulling focus back.
    const focusInGrid = (): boolean => {
      const now = document.activeElement;
      return !now || now === document.body || this.container.contains(now);
    };

    let result: EndResult;
    try {
      result = await invokeDotNetResult<EndResult>(this.ref, { ok: true }, 'EndEdit', commit, values, blur && !focusInGrid());
    } catch (e) {
      this.resetEdit();
      throw e;
    }

    if (!result.ok) {
      // Invalid: keep editing, back in the (first invalid) editor.
      scope?.querySelectorAll(EDITOR).forEach((el) => this.retired.delete(el));
      this.phase = 'editing';
      this.queue = [];
      this.afterEnd = null;
      const editor = this.editorFor(result.focus ?? null) ?? this.editorEl;
      if (editor && editor.isConnected) {
        this.editorEl = editor;
        const pos = this.position(editor.closest('td'));
        if (pos) {
          this.edit.row = pos.row;
          this.edit.col = pos.col;
          this.setActive(pos, false);
        }
        void focusElement(this.focusTarget(editor), { preventScroll: true });
      }
      return;
    }

    this.resetEdit();
    if (!blur) this.go(this.resolveMove(from, move));
    this.drain();
    this.runAfterEnd();
  }

  // The live element an edit spans: the cell in Cell mode, the row in Row mode.
  private editScope(): HTMLElement | null {
    const editor = this.editorEl;
    if (!editor || !editor.isConnected) return null;
    return this.options.mode === 'row' ? editor.closest('tr') : editor.closest('td');
  }

  // A control in one of the editing row's edit actions cells (Save, Cancel, a consumer's own): it
  // belongs to the edit, though it sits outside the editors.
  private isAction(el: Element | null): boolean {
    if (!el || !this.editorEl?.isConnected) return false;
    const cell = el.closest<HTMLElement>(ACTIONS);
    return !!cell && el !== cell && cell.parentElement === this.editorEl.closest('tr') && cell.closest('table') === this.table;
  }

  // Inside the open edit: its editors, the popups they own, and its row's edit actions.
  private inEdit(scope: Element, el: Element): boolean {
    return scope.contains(el) || this.ownsPopup(scope, el) || this.isAction(el);
  }

  // The editing row's action controls, in the order Tab reaches them.
  private actionControls(scope: Element): HTMLElement[] {
    const row = scope instanceof HTMLTableRowElement ? scope : scope.closest('tr');
    if (!row) return [];
    const controls: HTMLElement[] = [];
    for (const cell of Array.from(row.cells)) {
      if (!cell.matches(ACTIONS)) continue;
      cell.querySelectorAll<HTMLElement>(ACTION_CONTROLS).forEach((el) => {
        if (!el.closest('[inert]') && el.getClientRects().length > 0) controls.push(el);
      });
    }
    return controls;
  }

  private editorFor(key: string | null): HTMLElement | null {
    if (!key || !this.edit) return null;
    const index = this.options.columns.findIndex((c) => c.key === key);
    if (index < 0) return null;
    return this.cellAt({ row: this.edit.row, col: index + this.offset })?.querySelector<HTMLElement>(EDITOR) ?? null;
  }

  // A text or number editor reports its field's text; every other control reports null (.NET holds
  // its draft), after a focused field bound on change (a template's) flushes it.
  private collect(scope: HTMLElement | null): Record<string, string | null> {
    const values: Record<string, string | null> = {};
    if (!scope) {
      this.lastValues.forEach((value, key) => (values[key] = value));
      return values;
    }
    scope.querySelectorAll<HTMLElement>(EDITOR).forEach((editor) => {
      const key = this.keyOf(editor);
      if (key === null) return;
      const field = this.textField(editor);
      if (field) {
        values[key] = field.value;
        return;
      }
      const focused = document.activeElement;
      if ((focused instanceof HTMLInputElement || focused instanceof HTMLTextAreaElement) && editor.contains(focused))
        focused.dispatchEvent(new Event('change', { bubbles: true }));
      values[key] = null;
    });
    return values;
  }

  private keyOf(editor: Element): string | null {
    const pos = this.position(editor.closest('td'));
    return pos ? (this.columnAt(pos.col)?.key ?? null) : null;
  }

  // Keep the latest typed values, so an editor whose row scrolls out of a virtualized window can
  // still commit what it held.
  private onInput = (e: Event): void => {
    if (this.phase !== 'editing' || !(e.target instanceof Element)) return;
    const editor = e.target.closest<HTMLElement>(EDITOR);
    if (editor) this.remember(editor);
  };

  private remember(editor: HTMLElement): void {
    const key = this.keyOf(editor);
    const field = this.textField(editor);
    if (key !== null && field) this.lastValues.set(key, field.value);
  }

  private rememberAll(): void {
    this.editScope()?.querySelectorAll<HTMLElement>(EDITOR).forEach((el) => this.remember(el));
  }

  private popupOpen(scope: Element): boolean {
    return scope.querySelector('[aria-expanded="true"]') !== null;
  }

  // Focus inside a popup the editor controls (a select's listbox, a date's calendar) is still the edit.
  private ownsPopup(scope: Element, el: Element): boolean {
    for (const owner of scope.querySelectorAll('[aria-controls], [aria-owns]')) {
      const ids = `${owner.getAttribute('aria-controls') ?? ''} ${owner.getAttribute('aria-owns') ?? ''}`.split(/\s+/);
      for (const id of ids) {
        if (id && document.getElementById(id)?.contains(el)) return true;
      }
    }
    return false;
  }

  // Focus leaving an open edit commits it, and keys inside its popup are the edit's too. Heard on the
  // document, and only while an edit is open: the editor's own popup (a list, a calendar) lives
  // outside the table.
  private watchFocus(on: boolean): void {
    if (on === this.watching) return;
    this.watching = on;
    if (on) {
      document.addEventListener('focusout', this.onFocusOut, true);
      document.addEventListener('keydown', this.onPopupKeyDown, true);
    } else {
      document.removeEventListener('focusout', this.onFocusOut, true);
      document.removeEventListener('keydown', this.onPopupKeyDown, true);
    }
  }

  // Keys inside the open editor's popup, heard first (capture, on the document): one Escape closes
  // the popup and cancels the edit, and Tab in a list picks the highlighted option and moves on, as
  // Tab does in an editor. Everything else (arrows, type-ahead, Enter) is the popup's own.
  private onPopupKeyDown = (e: KeyboardEvent): void => {
    if (this.phase !== 'editing' || e.defaultPrevented || e.isComposing) return;
    const target = e.target as Element | null;
    const scope = this.editScope();
    if (!target || !scope || this.container.contains(target) || !this.ownsPopup(scope, target)) return;
    this.syncInput();
    if (e.ctrlKey || e.metaKey || e.altKey) return;

    if (e.key === 'Escape') {
      e.preventDefault();
      e.stopPropagation();
      void this.endEdit(false, 'none');
      return;
    }
    if (e.key !== 'Tab') return;
    const option = target.closest<HTMLElement>('[role=option]');
    if (!option) return;
    e.preventDefault();
    e.stopPropagation();
    if (this.options.mode === 'cell') {
      // The pick commits through .NET (commit), which then moves as this Tab asked.
      if (!e.shiftKey && this.tabRunCol === null && this.edit) this.tabRunCol = this.edit.col;
      this.afterPick = e.shiftKey ? 'prev' : 'next';
      option.click();
      return;
    }
    // Row mode: the pick is the row's draft; then on to the row's next editor.
    const trigger = scope.querySelector<HTMLElement>('[aria-expanded="true"]');
    option.click();
    const back = e.shiftKey;
    setTimeout(() => {
      if (this.phase !== 'editing') return;
      const live = this.editScope();
      if (live && !this.tabWithin(live, trigger, back)) void this.endEdit(true, back ? 'rowPrev' : 'rowNext');
    }, 0);
  };

  private onFocusOut = (e: FocusEvent): void => {
    if (this.phase !== 'editing') return;
    const scope = this.editScope();
    const target = e.target as Element | null;
    if (!scope || !target || !this.inEdit(scope, target)) return;

    // Onto another part of the edit (its popup, the row's Save or Cancel): still editing.
    const next = e.relatedTarget as Element | null;
    if (next && this.inEdit(scope, next)) return;

    // Commit on the next task, never inside focusout. Under WebAssembly the .NET call runs
    // synchronously, so committing here would re-render and remove the editor while the browser is
    // still moving focus to whatever was clicked, and the browser then abandons that focus move.
    setTimeout(() => {
      if (this.phase !== 'editing') return;
      const live = this.editScope();
      if (!live) return;
      const now = document.activeElement;
      if (now && now !== document.body) {
        if (this.inEdit(live, now)) return;
      } else if (this.popupOpen(live)) {
        // Focus dropped to the page while the editor's popup is open (a press on an item that takes
        // no focus): the popup is still the edit's.
        return;
      }
      void this.endEdit(true, 'none', true);
    }, 0);
  };

  // ---- widths ----

  // Pin every column at its current width while an edit is open: whatever the editor holds (a
  // longer value, a wider control), the columns stay where they are. Let go when the edit ends.
  // A table whose widths the column script lays out (data-bz-sized) keeps them fixed already.
  private lockWidths(): void {
    if (this.pinned || this.table.hasAttribute('data-bz-sized')) return;
    const head = this.table.tHead?.rows[0];
    if (!head) return;
    const cells = Array.from(head.cells);
    const widths = cells.map((c) => c.getBoundingClientRect().width); // every read before any write
    this.pinned = cells.map((cell) => ({ cell, width: cell.style.width }));
    this.pinnedLayout = this.table.style.tableLayout;
    cells.forEach((cell, i) => (cell.style.width = `${widths[i]}px`));
    this.table.style.tableLayout = 'fixed';
  }

  private unlockWidths(): void {
    if (!this.pinned) return;
    for (const { cell, width } of this.pinned) cell.style.width = width;
    // The column script took over the layout mid-edit: its fixed layout stands.
    if (!this.table.hasAttribute('data-bz-sized')) this.table.style.tableLayout = this.pinnedLayout;
    this.pinned = null;
  }

  // ---- one-shot cell actions ----

  private toggle(pos: Cell): void {
    void invokeDotNet(this.ref, 'Toggle', pos.row, pos.col);
  }

  private pressSelect(row: number): boolean {
    const box = this.cellAt({ row, col: 0 })?.querySelector<HTMLElement>(INTERACTIVE);
    if (!box) return false;
    box.click();
    return true;
  }

  // Enter / Space on a header cell or the selection column: press its control (sort, select all).
  private pressControl(pos: Cell): boolean {
    const control = this.cellAt(pos)?.querySelector<HTMLElement>(INTERACTIVE);
    if (!control) return false;
    control.click();
    return true;
  }

  // Enter on a read-only cell holding a control (row actions, a link): step into it.
  private enterControl(pos: Cell): boolean {
    const control = this.cellAt(pos)?.querySelector<HTMLElement>(INTERACTIVE);
    if (!control) return false;
    control.focus();
    return true;
  }

  // ---- pointer ----

  // Before mousedown: which pointer pressed (a tap edits the cell it lands on only when that cell
  // already held the cursor), and the end of any closed form's focus return.
  private onPointerDown = (e: PointerEvent): void => {
    // Any press in the table hides the cursor's mark until the next key (the document heard it first).
    if (e.target instanceof Node && this.container.contains(e.target)) this.syncInput();
    if (e.button !== 0) return;
    this.returnUntil = 0;
    const target = e.target as Element | null;
    const cell = target ? this.cellOf(target) : null;
    this.press = {
      type: e.pointerType || 'mouse',
      cell,
      wasActive: !!cell && cell === this.activeEl && document.activeElement === cell,
    };
  };

  private onMouseDown = (e: MouseEvent): void => {
    if (e.button !== 0 || this.phase === 'beginning' || this.phase === 'ending') return;
    const target = e.target as Element | null;
    if (!target) return;
    const scope = this.phase === 'editing' ? this.editScope() : null;
    // A press on the editing row's Save, Cancel or own action keeps the focus in the editor: no
    // blur commits the edit before the button's click runs, and the cursor stays put.
    const control = scope ? target.closest(INTERACTIVE) : null;
    if (control && this.isAction(control)) {
      e.preventDefault();
      return;
    }
    if (scope?.contains(target)) {
      // A press on the editor's own frame or padding (no field under it) would focus the cell and
      // end the edit: keep the focus where it is instead.
      const field = target.closest(TABBABLE);
      if (!field || !scope.contains(field) || field instanceof HTMLTableCellElement) e.preventDefault();
      return;
    }
    const cell = this.cellOf(target);
    const pos = this.position(cell);
    if (!cell || !pos) return;

    // A pick list's or date's mark: the editor opens with its list or calendar (the mark shows on
    // hover, so a touch press counts only on the cell that already holds the cursor).
    const mark = this.phase === 'nav' ? target.closest('[data-bz-pick]') : null;
    if (mark && cell.contains(mark) && this.canBegin(pos) && (this.press?.type === 'mouse' || this.press?.wasActive)) {
      e.preventDefault();
      this.tabRunCol = null;
      this.go(pos);
      void this.beginEdit(pos, null, { start: 'pointer', popup: true });
      return;
    }

    // No word selection on a double-click: it opens the editor instead.
    if (e.detail > 1) e.preventDefault();
    this.tabRunCol = null;
    // Tab stop first, so the press's own default focuses this cell (unless it hit a control).
    this.setActive(pos, false);
  };

  // Touch and pen: a tap on the cell that already held the cursor edits it in place, as a
  // double-click does with a mouse (a first tap only selects). With EditOnClick any click or tap
  // does, the first one included.
  private onClick = (e: MouseEvent): void => {
    const press = this.press;
    if (!press) return;
    const instant = this.options.editOnClick;
    if (!instant && (press.type === 'mouse' || !press.wasActive || this.phase !== 'nav')) return;
    const target = e.target as Element | null;
    const cell = target ? this.cellOf(target) : null;
    const pos = this.position(cell);
    if (!target || !cell || !pos || cell !== press.cell || !this.canBegin(pos)) return;
    const control = target.closest(INTERACTIVE);
    if (control && cell.contains(control)) return;
    const how = this.pointerActivation(cell, pos, e.clientX, e.clientY);
    if (this.phase === 'nav') {
      void this.beginEdit(pos, null, how);
      return;
    }
    // A click on another cell while an edit is open: the press committed it on its way out (the
    // blur), and the clicked cell's editor opens once that commit has landed. A click inside the
    // open edit is the editor's own.
    const scope = this.phase === 'editing' || this.phase === 'ending' ? this.editScope() : null;
    if (this.phase === 'beginning' || (scope && scope.contains(cell))) return;
    this.afterEnd = { pos, how };
  };

  // EditOnClick: the click that ended an edit opens its own cell's editor now.
  private runAfterEnd(): void {
    const next = this.afterEnd;
    this.afterEnd = null;
    if (!next || this.phase !== 'nav' || !this.canBegin(next.pos)) return;
    void this.beginEdit(next.pos, null, next.how);
  }

  private onDoubleClick = (e: MouseEvent): void => {
    const target = e.target as Element | null;
    if (!target || this.phase !== 'nav') return;
    const cell = this.cellOf(target);
    const pos = this.position(cell);
    if (!cell || !pos || !this.canBegin(pos)) return;
    const control = target.closest(INTERACTIVE);
    if (control && cell.contains(control)) return;
    void this.beginEdit(pos, null, this.pointerActivation(cell, pos, e.clientX, e.clientY));
  };

  // A double-click or tap edits in place with the control ready: the caret at the clicked
  // character, a list or calendar open, the time segment under the pointer.
  private pointerActivation(cell: HTMLTableCellElement, pos: Cell, x: number, y: number): Activation {
    const kind = this.columnAt(pos.col)?.kind ?? 'none';
    return {
      start: 'pointer',
      popup: hasPopup(kind),
      caret: TEXT_KINDS.has(kind) || kind === 'custom' ? caretHint(cell, x, y) : null,
      pointX: kind === 'time' ? x : null,
    };
  }

  private onFocusIn = (e: FocusEvent): void => {
    const target = e.target as Element | null;
    if (target && this.container.contains(target)) this.noteFocus(target);
  };

  private noteFocus(target: Element): void {
    this.focusedEl = target;
    this.syncInput();
    if (!this.painted) {
      this.painted = true;
      this.activeEl?.toggleAttribute('data-active', this.showsCursor);
    }
    if (this.phase !== 'nav' || target.closest(EDITOR)) return;
    // Keep the cursor where focus went (Tab in, a click on a control inside a cell).
    const pos = this.position(this.cellOf(target));
    if (pos && (pos.row !== this.active?.row || pos.col !== this.active?.col)) this.setActive(pos, false);
  }

  // ---- clipboard ----

  private onCopy = (e: ClipboardEvent): void => {
    if (this.phase !== 'nav' || !this.activeEl || document.activeElement !== this.activeEl) return;
    const selection = window.getSelection();
    if (selection && !selection.isCollapsed) return; // copying a text selection: the browser's own
    const box = this.activeEl.querySelector('[role="checkbox"], [role="switch"]');
    const text = box ? (box.getAttribute('aria-checked') === 'true' ? 'TRUE' : 'FALSE') : this.activeEl.innerText.trim();
    e.clipboardData?.setData('text/plain', text);
    e.preventDefault();
  };

  private onPaste = (e: ClipboardEvent): void => {
    if (this.phase !== 'nav' || this.options.mode !== 'cell' || !this.active || this.active.row < 0) return;
    if (!this.activeEl || document.activeElement !== this.activeEl) return;
    const text = e.clipboardData?.getData('text/plain');
    if (text === undefined || text === '') return;
    e.preventDefault();
    void invokeDotNet(this.ref, 'Paste', this.active.row, this.active.col, text);
  };

  // ---- DOM changes from .NET renders ----

  private onMutations = (records: MutationRecord[]): void => {
    for (const record of records) {
      for (const node of record.addedNodes) {
        if (node instanceof Element) this.sweep(node);
      }
    }
    this.syncActive();
    this.syncEditor();
    this.parkIfStranded();
  };

  // One tab stop for the whole grid: controls inside cells leave the tab sequence (the cursor
  // reaches them; Enter steps in), and a cell rendered with the server's first tab stop gives it up
  // unless the cursor is on it. Editors are focused directly, so they are left alone.
  private sweep(root: Element): void {
    const mark = (el: Element): void => {
      if (el instanceof HTMLTableCellElement) {
        if (el !== this.activeEl && el !== document.activeElement && el.closest('table') === this.table)
          el.removeAttribute('tabindex');
        return;
      }
      if (el.closest(EDITOR)) return;
      // The cursor only walks the header row and the body. A control in the footer or the caption
      // keeps its own tab stop, or nothing could reach it.
      if (el.closest('tfoot, caption')) return;
      if (el.getAttribute('tabindex') !== '-1') el.setAttribute('tabindex', '-1');
    };
    if (root.matches(FOCUSABLE)) mark(root);
    root.querySelectorAll(FOCUSABLE).forEach(mark);
  }

  private syncEditor(): void {
    const found = Array.from(this.table.querySelectorAll<HTMLElement>(EDITOR)).find((el) => !this.retired.has(el)) ?? null;
    if (found) {
      if (this.phase === 'beginning' || this.phase === 'nav') this.adopt(found);
      else if (this.phase === 'editing' && this.editorEl && !this.editorEl.isConnected) this.readopt();
      return;
    }
    if (this.phase !== 'editing' || !this.edit) return;

    // The editor left without this module ending it.
    const rowStillHere = this.rowEl(this.edit.row);
    if (!rowStillHere && this.lastValues.size > 0) {
      // Its row scrolled out of a virtualized window: commit what it held.
      void this.commitDetached();
      return;
    }
    // .NET ended the edit itself (a pick from a list, a template's Commit, CommitEditAsync,
    // CancelEditAsync): the focus it held goes back to the cell.
    const hadFocus = !document.activeElement || document.activeElement === document.body;
    this.resetEdit();
    if (hadFocus && this.active) this.go(this.active);
    else this.reclaimFocus();
    this.runAfterEnd();
  }

  // The editor's popup may still hold focus and drop it to the page when it closes: if it does,
  // the cursor takes it back.
  private reclaimFocus(): void {
    setTimeout(() => {
      const now = document.activeElement;
      if (this.phase === 'nav' && this.active && (!now || now === document.body)) this.go(this.active);
    }, 0);
  }

  // The editor was rendered again as a new element (seen when a virtualized window changes at the
  // end of the list), taking focus with it. The edit is still open in .NET: carry it over to the
  // new editors - the text typed so far, the focus, the caret at the end.
  private readopt(): void {
    if (!this.edit) return;
    const cell = this.cellAt({ row: this.edit.row, col: this.edit.col });
    const editor = cell?.querySelector<HTMLElement>(EDITOR);
    if (!cell || !editor) return;

    const now = document.activeElement;
    const hadFocus = !now || now === document.body;
    this.editorEl = editor;
    this.bind(cell);
    this.editScope()?.querySelectorAll<HTMLElement>(EDITOR).forEach((el) => {
      const key = this.keyOf(el);
      const text = key === null ? undefined : this.lastValues.get(key);
      const field = this.textField(el);
      if (text !== undefined && field) field.value = text;
    });
    editor.toggleAttribute('data-entry', this.edit.entry);
    if (!hadFocus) return;
    void focusElement(this.focusTarget(editor), { preventScroll: true }).then(() => {
      const focused = document.activeElement;
      if (focused instanceof HTMLInputElement && editor.contains(focused) && isTextInput(focused)) {
        const end = focused.value.length;
        focused.setSelectionRange(end, end);
      }
    });
  }

  private async commitDetached(): Promise<void> {
    const values: Record<string, string | null> = {};
    this.lastValues.forEach((value, key) => (values[key] = value));
    this.phase = 'ending';
    try {
      await invokeDotNetResult<EndResult>(this.ref, { ok: true }, 'EndEdit', true, values, true);
    } finally {
      this.resetEdit();
      // The editor took focus with it when its row left the window: keep the keyboard in the grid.
      if (!document.activeElement || document.activeElement === document.body) this.park();
      this.drain();
    }
  }

  // The focused cell left the DOM (a virtualized scroll recycled its row): park focus on the
  // container so the next key still lands here and brings the cursor back into view.
  private parkIfStranded(): void {
    // Mid-edit the module moves focus itself once .NET answers; only an idle grid gets parked.
    if (this.phase !== 'nav') return;
    const now = document.activeElement;
    if (!this.focusedEl || this.focusedEl.isConnected || (now && now !== document.body)) return;
    // A window shift re-created the focused row: focus follows the cursor's cell when it is
    // still rendered, and parks on the container only when it is not.
    const cell = this.active ? this.cellAt(this.active) : null;
    if (cell) {
      this.focusedEl = cell;
      cell.focus({ preventScroll: true });
      return;
    }
    this.park();
  }

  private park(): void {
    this.focusedEl = null;
    if (!this.container.hasAttribute('tabindex')) this.container.tabIndex = -1;
    this.container.focus({ preventScroll: true });
  }
}

// ---------------------------------------------------------------------------------------------
// Columns: sizing, pinning and moving
// ---------------------------------------------------------------------------------------------

type Pin = 'start' | 'end';

interface ColumnSpec {
  key: string;
  /** The header's label, for what screen readers hear after a key press. */
  title: string;
  /** The width a user chose, else the column's own Width, within its bounds; null sizes to the content (or fills). */
  width: number | null;
  min: number | null;
  max: number | null;
  /** Takes the room the other columns leave in the row. */
  fill: boolean;
  pin: Pin | null;
  resizable: boolean;
  reorderable: boolean;
}

interface ColumnsOptions {
  /** The widths are laid out here: the table renders a colgroup and its cells clip. */
  sized: boolean;
  /** A leading selection-checkbox column is present (its header carries no key). */
  selectable: boolean;
  /** The body holds rows. The first rows after an empty or loading body are measured afresh. */
  hasRows: boolean;
  /** A dragged column moves with the pointer and the others make room (LiveReorder), instead of a floating label and a landing line. */
  live: boolean;
  /** .NET words what screen readers hear after a key resizes or moves a column (the table's ColumnAnnouncement). */
  announce: boolean;
  /** The visible columns, in display order. */
  columns: ColumnSpec[];
}

/** What a key press did to a column, for screen readers: a new width, a move, or a move with nowhere to go. */
type ColumnChange = 'resized' | 'moved' | 'blocked';

/** The narrowest a column gets without a MinWidth (BzDataTable's ColumnFloor). */
const COLUMN_FLOOR = 32;
/** Pixels per Alt+Shift+arrow press. */
const RESIZE_STEP = 10;
/** How far a pressed header travels before the press becomes a drag. */
const DRAG_THRESHOLD = 5;
/** A run of resize keys reports once it pauses this long (ms). */
const KEY_SETTLE = 500;
/** Within this distance of the scroll box's edge, a header drag scrolls it. */
const EDGE_SCROLL = 40;
/** How long the columns of a live move take to slide into a new place (ms). */
const LIVE_SLIDE = 150;
/** The longest a dropped live move waits for .NET to render the new order before sliding back (ms). */
const LANDING_WAIT = 3000;

/** A column's content widths: without wrapping anything it can wrap (min), and on one line (max). */
interface Natural {
  min: number;
  max: number;
}

interface ResizeDrag {
  key: string;
  handle: HTMLElement;
  pointerId: number;
  startX: number;
  startWidth: number;
  moved: boolean;
}

interface MoveDrag {
  key: string;
  th: HTMLTableCellElement;
  pointerId: number;
  startX: number;
  startY: number;
  lastX: number;
  lastY: number;
  dragging: boolean;
  cancelled: boolean;
  /** The column's display index, and the indexes it may land between (its pin group, inside the columns that stay put). */
  from: number;
  first: number;
  last: number;
  to: number;
  /** The floating label and the landing line (a live move has neither: the column itself moves). */
  ghost: HTMLElement | null;
  indicator: HTMLElement | null;
  live: LiveMove | null;
  scrollFrame: number;
}

/**
 * A live move: the dragged column's cells follow the pointer and the columns it passes slide out
 * of its way. Nothing rendered by .NET moves in the DOM: a style sheet of the script's own
 * translates the cells by their place in the row (so rows the virtualizer adds mid-drag shift
 * too), and it goes once .NET has rendered the dropped order.
 */
interface LiveMove {
  drag: MoveDrag;
  id: string;
  sheet: CSSStyleSheet;
  root: Document | ShadowRoot;
  rtl: boolean;
  /** A pinned column is measured from the scroll box (its group stays put when the box scrolls), any other from the table. */
  pinned: boolean;
  /** The header's keys when the drag began (see headerKeys): a change means the sheet is out of date. */
  before: string;
  /** Per display index: the column's cells' place among their row's cells (1-based, for :nth-child). */
  nth: number[];
  /** Per display index in the move's range: the start edge, in reading order from the box's start edge, and the width. */
  starts: number[];
  widths: number[];
  /** Where the press sat inside the dragged column, from its start edge. */
  grab: number;
  /** The rules that lift the dragged column: over its neighbours, opaque. */
  lift: string;
  motion: boolean;
  /** Once dropped: the order reported, as keys joined by newlines. The sheet goes when the header shows it. */
  landing: string | null;
  timer: number;
}

/** One laid-out column while widths are worked out. */
interface Slot {
  kind: 'fixed' | 'auto' | 'fill';
  width: number;
  /** auto: min-content and max-content; fill: its floor and ceiling. */
  lo: number;
  hi: number;
  /** The ceiling a stretched auto column stops at. */
  max: number;
}

/**
 * The column features of a BzDataTable: sizing, pinning and moving. Like the keyboard grid, it
 * owns the motion and .NET owns the meaning - a drag or a run of keys never calls .NET, and a
 * finished resize (ColumnResized(key, width)) or move (ColumnsReordered(keys)) is reported once.
 *
 * SIZING. With the table's widths laid out here (options.sized), every column gets a pixel width
 * written into the colgroup .NET renders, and the table switches to the fixed layout, so rows the
 * virtualizer adds later take the widths with no work of their own, and a column can be narrower
 * than its content (cells clip). A column's own or chosen width is fixed; one without sizes to its
 * content, measured once per column set; a Fill column shares out the room left in the row, and
 * without one the content-sized columns stretch to fill it, the way the browser's own layout does.
 * Resizing a column freezes the columns before it, so the dragged edge follows the pointer and
 * nothing jumps when .NET's answer lands; the columns after it keep filling.
 *
 * PINNING. A pinned cell is sticky; .NET renders its inset as a variable per place
 * (--bz-pin-s<k> / --bz-pin-e<k>), and this module keeps the variables at the widths of the pinned
 * columns before it.
 *
 * MOVING. A dragged header shows where its column would land: a floating label follows the
 * pointer and a line marks the gap it would drop into, or, with options.live, the column itself
 * follows the pointer and the columns it passes slide out of its way (LiveMove).
 *
 * Contract with BzDataTable:
 *   th[data-bz-column=key]   a data column's header (the selection column's header has none)
 *   th[data-pinned=start|end], [data-resizable], [data-reorderable]
 *   th[data-movable]         a reorderable column with somewhere to go (styling only: the grab cursor)
 *   [data-bz-resizer]        the drag handle on a resizable header's end edge
 *   [data-bz-sort]           the header's sort button (a drag may start on it)
 *   table[data-bz-sized]     set here while the widths are laid out here
 *   table[data-bz-columns]   set here once attached
 *   table[data-bz-live-move] set here while a live move's style sheet is applied
 */
class DataTableColumns {
  private readonly table: HTMLTableElement;
  private readonly container: HTMLElement;
  private readonly mutations: MutationObserver;
  private readonly sizes: ResizeObserver;
  private specs = new Map<string, ColumnSpec>();
  private readonly natural = new Map<string, Natural>();
  /** A width a user just set, shown until .NET's answer lands (settled: the report has returned). */
  private readonly overrides = new Map<string, { width: number; settled: boolean }>();
  /** Columns before one a user resized keep the width they had then. */
  private readonly frozen = new Map<string, number>();
  private observed: Element[] = [];
  private room = 0;
  private applied = false;
  private measuring = false;
  private frame = 0;
  private updates = 0;
  private resizing: ResizeDrag | null = null;
  private moving: MoveDrag | null = null;
  /** A live move's sheet: from the drag's start until .NET renders the dropped order (or it slides back). */
  private live: LiveMove | null = null;
  private keyRun: { key: string; timer: number } | null = null;
  private moved: { key: string; index: number; button: boolean; until: number } | null = null;
  private swallowClick = false;
  /** The newest change still to be worded by .NET, and whether a call for one is out. */
  private unspoken: [ColumnChange, string, number, number] | null = null;
  private speaking = false;

  constructor(
    private readonly root: HTMLElement,
    private readonly ref: DotNetReference,
    private options: ColumnsOptions,
  ) {
    this.table = root.querySelector<HTMLTableElement>('[data-slot="table-container"] > table')!;
    this.container = this.table.parentElement as HTMLElement;

    root.addEventListener('pointerdown', this.onPointerDown);
    root.addEventListener('dblclick', this.onDoubleClick);
    root.addEventListener('keydown', this.onKeyDown);
    root.addEventListener('click', this.onClick, true);

    this.mutations = new MutationObserver(this.onMutations);
    // A move's render can change no element, only which column each header shows: data-bz-column
    // tells (so the layout and a live move's sheet follow that render, not the update after it).
    this.mutations.observe(this.table, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-bz-column'] });
    this.sizes = new ResizeObserver(this.onSizes);

    this.update(options);
    this.table.setAttribute('data-bz-columns', '');
    document.fonts?.addEventListener('loadingdone', this.onFonts);
  }

  // A web font that arrives after the content was measured (one loaded on demand included): the
  // content-sized columns were measured in its fallback's widths, so measure them again.
  private onFonts = (): void => {
    if (this.resizing || this.moving?.dragging || this.live || !this.table.isConnected) return;
    this.natural.clear();
    this.refresh();
  };

  /** New columns, widths, pins or switches from .NET (a render changed them, or answered a report). */
  public update = (options: ColumnsOptions): void => {
    const first = this.updates === 0;
    const hadRows = this.options.hasRows;
    const before = this.specs;
    this.options = options;
    this.updates++;
    this.specs = new Map(options.columns.map((c) => [c.key, c]));
    // A width that changed other than by a resize reported here (the app reset or loaded the
    // widths): the columns frozen by earlier resizes are free again.
    for (const [key, spec] of this.specs) {
      const old = before.get(key);
      if (!old || old.width === spec.width) continue;
      if (spec.width === null || this.overrides.get(key)?.width !== Math.round(spec.width)) {
        this.frozen.clear();
        break;
      }
    }
    // .NET has answered every report that returned before this update.
    for (const [key, o] of this.overrides) if (o.settled) this.overrides.delete(key);
    // The first rows after an empty (or loading) body size the columns.
    if (!first && options.hasRows && !hadRows) this.natural.clear();
    this.refresh();
    this.refocus();
  };

  public dispose = (): void => {
    this.endResize(false);
    this.endMove();
    this.clearLive();
    if (this.keyRun) window.clearTimeout(this.keyRun.timer);
    cancelAnimationFrame(this.frame);
    this.mutations.disconnect();
    this.sizes.disconnect();
    document.fonts?.removeEventListener('loadingdone', this.onFonts);
    this.root.removeEventListener('pointerdown', this.onPointerDown);
    this.root.removeEventListener('dblclick', this.onDoubleClick);
    this.root.removeEventListener('keydown', this.onKeyDown);
    this.root.removeEventListener('click', this.onClick, true);
    this.table.removeAttribute('data-bz-columns');
  };

  // ---- the header ----

  private heads(): HTMLTableCellElement[] {
    return Array.from(this.table.tHead?.rows[0]?.cells ?? []);
  }

  /** The data columns' headers, in display order (the selection column left out). */
  private dataHeads(): HTMLTableCellElement[] {
    return this.heads().filter((th) => th.dataset.bzColumn !== undefined);
  }

  private cols(): HTMLTableColElement[] {
    return Array.from(this.table.querySelectorAll<HTMLTableColElement>(':scope > colgroup > col'));
  }

  private keyOf(th: HTMLTableCellElement): string {
    return th.dataset.bzColumn ?? '';
  }

  private titleOf(key: string): string {
    return this.specs.get(key)?.title || key;
  }

  private get rtl(): boolean {
    return getComputedStyle(this.table).direction === 'rtl';
  }

  /** The header cell (and so the column) a key, pointer or press belongs to. */
  private headOf(target: EventTarget | null): HTMLTableCellElement | null {
    const th = target instanceof Element ? target.closest<HTMLTableCellElement>('th') : null;
    return th && th.closest('table') === this.table && th.dataset.bzColumn !== undefined ? th : null;
  }

  // ---- layout ----

  // Read the header again (its order, pins, the column set) and lay the columns out.
  private refresh(): void {
    this.observe();
    if (this.options.sized) this.layout();
    else this.release();
    this.pins();
  }

  // The scroll box's width drives a laid-out table (filling columns follow it); a table laid out by
  // the browser has its pinned headers watched instead, since their widths set the offsets.
  private observe(): void {
    const targets: Element[] = this.options.sized
      ? [this.container]
      : this.options.columns.some((c) => c.pin)
        ? this.heads().filter((th) => th.dataset.pinned)
        : [];
    if (targets.length === this.observed.length && targets.every((t, i) => t === this.observed[i])) return;
    this.sizes.disconnect();
    targets.forEach((t) => this.sizes.observe(t));
    this.observed = targets;
  }

  private onSizes = (): void => {
    if (this.measuring || this.frame) return;
    // Next frame, never inside the observer: writing widths here would size what it watches.
    this.frame = requestAnimationFrame(() => {
      this.frame = 0;
      if (this.resizing || this.moving?.dragging || this.live) return;
      if (this.options.sized && Math.abs(this.container.clientWidth - this.room) >= 1) this.layout();
      this.pins();
    });
  };

  // A render changed the header (a move landed, a column came or went, a sort icon turned).
  private onMutations = (records: MutationRecord[]): void => {
    if (this.measuring) return;
    const head = this.table.tHead;
    const touched = records.some(
      (r) => r.target === this.table || (head?.contains(r.target) ?? false) || (r.target instanceof Element && r.target.closest('colgroup') !== null),
    );
    if (!touched) return;
    this.landed();
    this.refresh();
    this.refocus();
  };

  private layout(): void {
    const heads = this.heads();
    const cols = this.cols();
    if (heads.length === 0 || cols.length !== heads.length) return;
    // Hidden (a closed tab, a collapsed panel): nothing to measure yet. The box's size observer
    // lays the table out once it shows.
    if (this.container.clientWidth === 0) return;
    if (heads.some((th) => !this.natural.has(this.keyOf(th)))) this.measure(heads, cols);

    const room = (this.room = this.container.clientWidth);
    const { widths, flex } = this.solve(heads, room);
    const style = this.table.style;
    const write = (): number => {
      let total = 0;
      widths.forEach((w, i) => {
        cols[i].style.width = `${w}px`;
        total += w;
      });
      style.width = `${total}px`;
      return total;
    };
    style.tableLayout = 'fixed';
    style.minWidth = '0';
    const total = write();
    this.table.setAttribute('data-bz-sized', '');
    this.applied = true;
    // A filled row that came out wider than the box (collapsed borders add their halves): the
    // flexible column gives the difference back, so no scrollbar appears for a pixel.
    if (flex >= 0 && total <= room + 0.5) {
      const over = this.table.getBoundingClientRect().width - room;
      if (over > 0.5 && widths[flex] - over >= COLUMN_FLOOR) {
        widths[flex] = Math.floor(widths[flex] - over);
        write();
      }
    }
  }

  // Every column's width for a row `room` pixels wide. `flex` is the column that took the rounding
  // left over when the columns fill the row exactly (-1 when they do not).
  private solve(heads: HTMLTableCellElement[], room: number): { widths: number[]; flex: number } {
    const slots: Slot[] = heads.map((th) => {
      const key = this.keyOf(th);
      const natural = this.natural.get(key) ?? { min: th.offsetWidth, max: th.offsetWidth };
      // The selection column: its checkbox's width.
      if (!key) return { kind: 'fixed', width: Math.ceil(natural.max), lo: 0, hi: 0, max: Infinity };
      const spec = this.specs.get(key);
      const min = spec?.min ?? COLUMN_FLOOR;
      const max = spec?.max ?? Infinity;
      const clamp = (w: number): number => Math.min(Math.max(w, min), max);
      const chosen = this.overrides.get(key)?.width ?? this.frozen.get(key) ?? spec?.width ?? null;
      if (chosen !== null) return { kind: 'fixed', width: Math.round(clamp(chosen)), lo: 0, hi: 0, max };
      if (spec?.fill) return { kind: 'fill', width: 0, lo: clamp(spec.min ?? natural.min), hi: Math.max(max, min), max };
      return { kind: 'auto', width: 0, lo: clamp(natural.min), hi: clamp(natural.max), max };
    });

    const sum = (list: Slot[], pick: (s: Slot) => number): number => list.reduce((n, s) => n + pick(s), 0);
    const fixed = sum(slots.filter((s) => s.kind === 'fixed'), (s) => s.width);
    const autos = slots.filter((s) => s.kind === 'auto');
    const fills = slots.filter((s) => s.kind === 'fill');
    let flexible: Slot[] = [];

    if (fills.length > 0) {
      // Content-sized columns keep their content's width (squeezed toward their min-content only
      // when even that does not fit), and the filling columns share what is left.
      squeeze(autos, fixed + sum(autos, (s) => s.hi) + sum(fills, (s) => s.lo) - room);
      share(fills, room - fixed - sum(autos, (s) => s.width));
      flexible = fills;
    } else {
      const natural = fixed + sum(autos, (s) => s.hi);
      if (natural > room) squeeze(autos, natural - room);
      else {
        stretch(autos, room - natural);
        flexible = autos.filter((s) => s.width < s.max);
      }
    }

    // Whole pixels; the last flexible column takes the rounding, so a filled row is exactly as wide as the box.
    const widths = slots.map((s) => (s.kind === 'fixed' ? s.width : Math.floor(s.width)));
    let flex = -1;
    if (flexible.length > 0) {
      flex = slots.lastIndexOf(flexible[flexible.length - 1]);
      const total = widths.reduce((n, w) => n + w, 0);
      if (total < room && room - total < flexible.length + 1) widths[flex] += Math.floor(room - total);
    }
    return { widths, flex };
  }

  // Content widths, read off the browser's own layout twice: the table as wide as its content
  // (max-content) and as narrow as it goes (min-content). Restored before anything paints.
  private measure(heads: HTMLTableCellElement[], cols: HTMLTableColElement[]): void {
    const style = this.table.style;
    const saved = [style.width, style.minWidth, style.tableLayout] as const;
    const savedCols = cols.map((c) => c.style.width);
    this.measuring = true;
    try {
      cols.forEach((c) => (c.style.width = ''));
      style.tableLayout = 'auto';
      style.minWidth = '0';
      style.width = 'max-content';
      const max = heads.map((th) => th.getBoundingClientRect().width);
      style.width = 'min-content';
      const min = heads.map((th) => th.getBoundingClientRect().width);
      // Whole pixels, rounded up: a column half a pixel short of its content cuts it off.
      const up = (w: number): number => Math.ceil(w - 0.01);
      heads.forEach((th, i) => this.natural.set(this.keyOf(th), { min: up(Math.min(min[i], max[i])), max: up(max[i]) }));
    } finally {
      cols.forEach((c, i) => (c.style.width = savedCols[i]));
      [style.width, style.minWidth, style.tableLayout] = saved;
      this.measuring = false;
    }
  }

  // Back to the browser's own layout (the table no longer sizes its columns).
  private release(): void {
    if (!this.applied) return;
    const style = this.table.style;
    style.width = '';
    style.minWidth = '';
    style.tableLayout = '';
    this.cols().forEach((c) => (c.style.width = ''));
    this.table.removeAttribute('data-bz-sized');
    this.applied = false;
  }

  // Each pinned cell's inset: the widths of the pinned columns before it, counted from its edge.
  // .NET renders the k-th one with var(--bz-pin-s<k>) (from the start) or var(--bz-pin-e<k>).
  private pins(): void {
    const heads = this.heads();
    const style = this.table.style;
    const write = (side: 's' | 'e', cells: HTMLTableCellElement[]): void => {
      let offset = 0;
      cells.forEach((th, k) => {
        if (k > 0) style.setProperty(`--bz-pin-${side}${k}`, `${offset}px`);
        offset += th.getBoundingClientRect().width;
      });
      for (let k = Math.max(1, cells.length); style.getPropertyValue(`--bz-pin-${side}${k}`) !== ''; k++)
        style.removeProperty(`--bz-pin-${side}${k}`);
    };
    write('s', heads.filter((th) => th.dataset.pinned === 'start'));
    write('e', heads.filter((th) => th.dataset.pinned === 'end').reverse());
  }

  // ---- resizing ----

  private widthOf(key: string, th: HTMLTableCellElement): number {
    return this.overrides.get(key)?.width ?? th.getBoundingClientRect().width;
  }

  private clampTo(key: string, width: number): number {
    const spec = this.specs.get(key);
    return Math.round(Math.min(Math.max(width, spec?.min ?? COLUMN_FLOOR), spec?.max ?? Infinity));
  }

  // The columns before the one being resized keep the width they have: the dragged edge follows
  // the pointer, and nothing jumps when the new width lands.
  private freezeBefore(key: string): void {
    for (const th of this.dataHeads()) {
      const k = this.keyOf(th);
      if (k === key) return;
      if (!this.frozen.has(k) && !this.overrides.has(k) && this.specs.get(k)?.width == null)
        this.frozen.set(k, th.getBoundingClientRect().width);
    }
  }

  private setWidth(key: string, width: number): number {
    const clamped = this.clampTo(key, width);
    this.overrides.set(key, { width: clamped, settled: false });
    if (this.options.sized) this.layout();
    this.pins();
    return clamped;
  }

  // One report per finished resize. .NET answers with an update; until then the width shown is
  // the one the user set.
  private async report(key: string): Promise<void> {
    const override = this.overrides.get(key);
    if (!override) return;
    const at = this.updates;
    await invokeDotNet(this.ref, 'ColumnResized', key, override.width);
    if (this.overrides.get(key) !== override) return; // a newer resize of the column took over
    if (this.updates > at) {
      // The update came back before the report did: it is .NET's word on the width.
      this.overrides.delete(key);
      this.refresh();
    } else {
      override.settled = true;
    }
  }

  private beginResize(e: PointerEvent, th: HTMLTableCellElement, handle: HTMLElement): void {
    // No focus move, no text selection, no cell cursor move: the press is the handle's.
    e.preventDefault();
    e.stopPropagation();
    this.flushKeys();
    const key = this.keyOf(th);
    this.resizing = { key, handle, pointerId: e.pointerId, startX: e.clientX, startWidth: this.widthOf(key, th), moved: false };
    try {
      handle.setPointerCapture(e.pointerId);
    } catch {
      // A synthetic pointer: the document listeners below still follow it.
    }
    handle.setAttribute('data-active', '');
    this.root.setAttribute('data-bz-resizing', '');
    document.addEventListener('pointermove', this.onResizeMove, true);
    document.addEventListener('pointerup', this.onResizeUp, true);
    document.addEventListener('pointercancel', this.onResizeCancel, true);
    document.addEventListener('keydown', this.onResizeKey, true);
  }

  private onResizeMove = (e: PointerEvent): void => {
    const r = this.resizing;
    if (!r || e.pointerId !== r.pointerId) return;
    const dx = (e.clientX - r.startX) * (this.rtl ? -1 : 1);
    if (!r.moved) {
      if (Math.abs(dx) < 1) return;
      r.moved = true;
      this.freezeBefore(r.key);
    }
    e.preventDefault();
    this.setWidth(r.key, r.startWidth + dx);
  };

  private onResizeUp = (e: PointerEvent): void => {
    if (this.resizing && e.pointerId === this.resizing.pointerId) this.endResize(true);
  };

  private onResizeCancel = (e: PointerEvent): void => {
    if (this.resizing && e.pointerId === this.resizing.pointerId) this.endResize(false);
  };

  // Escape mid-drag puts the width back.
  private onResizeKey = (e: KeyboardEvent): void => {
    if (e.key !== 'Escape' || !this.resizing) return;
    e.preventDefault();
    e.stopPropagation();
    this.endResize(false);
  };

  private endResize(commit: boolean): void {
    const r = this.resizing;
    if (!r) return;
    this.resizing = null;
    document.removeEventListener('pointermove', this.onResizeMove, true);
    document.removeEventListener('pointerup', this.onResizeUp, true);
    document.removeEventListener('pointercancel', this.onResizeCancel, true);
    document.removeEventListener('keydown', this.onResizeKey, true);
    r.handle.removeAttribute('data-active');
    this.root.removeAttribute('data-bz-resizing');
    try {
      r.handle.releasePointerCapture(r.pointerId);
    } catch {
      // Already released.
    }
    if (!r.moved) return;
    if (commit) {
      void this.report(r.key);
    } else {
      this.overrides.delete(r.key);
      this.refresh();
    }
  }

  // A double-click on the edge (or Alt+Shift+Enter): the column takes the width of its widest
  // content in view.
  private fit(key: string, announceIt: boolean): void {
    const heads = this.heads();
    const cols = this.cols();
    if (cols.length === heads.length) this.measure(heads, cols);
    const natural = this.natural.get(key);
    const th = heads.find((h) => this.keyOf(h) === key);
    if (!natural || !th) return;
    this.flushKeys();
    this.freezeBefore(key);
    const width = this.setWidth(key, natural.max);
    void this.report(key);
    if (announceIt) this.say('resized', key, width);
  }

  // Alt+Shift+Left / Right: a step narrower or wider. A run of presses reports once it pauses.
  private step(key: string, th: HTMLTableCellElement, direction: number): void {
    if (this.keyRun && this.keyRun.key !== key) this.flushKeys();
    if (!this.keyRun) this.freezeBefore(key);
    const width = this.setWidth(key, this.widthOf(key, th) + direction * RESIZE_STEP);
    if (this.keyRun) window.clearTimeout(this.keyRun.timer);
    this.keyRun = { key, timer: window.setTimeout(() => this.flushKeys(), KEY_SETTLE) };
    this.say('resized', key, width);
  }

  private flushKeys(): void {
    const run = this.keyRun;
    if (!run) return;
    window.clearTimeout(run.timer);
    this.keyRun = null;
    void this.report(run.key);
  }

  // ---- moving ----

  // Where the column at display index `from` may go: within its pin group, and not past a column
  // that stays put.
  private range(from: number): [number, number] {
    const specs = this.dataHeads().map((th) => this.specs.get(this.keyOf(th)));
    const group = specs[from]?.pin ?? null;
    const fits = (i: number): boolean => (specs[i]?.pin ?? null) === group && specs[i]?.reorderable === true;
    let first = from;
    let last = from;
    while (first > 0 && fits(first - 1)) first--;
    while (last < specs.length - 1 && fits(last + 1)) last++;
    return [first, last];
  }

  private moveTo(key: string, from: number, to: number): void {
    const keys = this.dataHeads().map((th) => this.keyOf(th));
    keys.splice(from, 1);
    keys.splice(to, 0, key);
    void invokeDotNet(this.ref, 'ColumnsReordered', keys);
  }

  // Ctrl+Shift+Left / Right: one place over, in the arrow's direction on screen.
  private shift(key: string, direction: number, button: boolean): void {
    const heads = this.dataHeads();
    const from = heads.findIndex((th) => this.keyOf(th) === key);
    if (from < 0) return;
    const [first, last] = this.range(from);
    const to = from + direction;
    if (to < first || to > last) {
      this.say('blocked', key);
      return;
    }
    // Focus follows the column once .NET's render lands (see refocus).
    this.moved = { key, index: to, button, until: performance.now() + 3000 };
    this.moveTo(key, from, to);
    this.say('moved', key, to + 1, heads.length);
  }

  // What screen readers hear after a key resizes or moves a column (the header keeps the focus, so
  // nothing else would be heard): worded here, or by .NET when the table's ColumnAnnouncement is
  // set. Those calls go one at a time: when presses come faster than .NET answers, only the
  // newest is heard.
  private say(change: ColumnChange, key: string, value = 0, count = 0): void {
    if (!this.options.announce) {
      const title = this.titleOf(key);
      announce(
        change === 'resized'
          ? `${title}: ${value} pixels`
          : change === 'moved'
            ? `${title} moved to column ${value} of ${count}`
            : `${title} cannot move further`,
      );
      return;
    }
    this.unspoken = [change, key, value, count];
    if (!this.speaking) void this.speak();
  }

  private async speak(): Promise<void> {
    this.speaking = true;
    try {
      while (this.unspoken) {
        const [change, key, value, count] = this.unspoken;
        this.unspoken = null;
        const text = await invokeDotNetResult<string>(this.ref, '', 'ColumnAnnouncementText', change, key, value, count);
        // A newer press came in meanwhile: only its words are worth hearing.
        if (text && !this.unspoken) announce(text);
      }
    } finally {
      this.speaking = false;
    }
  }

  // After a keyboard move: once the column's header is in its new place, the focus goes with it
  // (its sort button when that is what held it). The grid's cursor follows the focus.
  private refocus(): void {
    const moved = this.moved;
    if (!moved) return;
    if (performance.now() > moved.until) {
      this.moved = null;
      return;
    }
    const th = this.dataHeads()[moved.index];
    if (!th || this.keyOf(th) !== moved.key) return; // not landed yet
    this.moved = null;
    const button = moved.button ? th.querySelector<HTMLElement>('[data-bz-sort]') : null;
    const target = button ?? th;
    if (target === th && !th.hasAttribute('tabindex')) th.tabIndex = -1;
    target.focus({ preventScroll: true });
    th.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }

  private beginMove(e: PointerEvent, th: HTMLTableCellElement): void {
    this.moving = {
      key: this.keyOf(th),
      th,
      pointerId: e.pointerId,
      startX: e.clientX,
      startY: e.clientY,
      lastX: e.clientX,
      lastY: e.clientY,
      dragging: false,
      cancelled: false,
      from: -1,
      first: -1,
      last: -1,
      to: -1,
      ghost: null,
      indicator: null,
      live: null,
      scrollFrame: 0,
    };
    document.addEventListener('pointermove', this.onMoveMove, true);
    document.addEventListener('pointerup', this.onMoveUp, true);
    document.addEventListener('pointercancel', this.onMoveCancel, true);
    document.addEventListener('keydown', this.onMoveKey, true);
  }

  private onMoveMove = (e: PointerEvent): void => {
    const m = this.moving;
    if (!m || e.pointerId !== m.pointerId) return;
    m.lastX = e.clientX;
    m.lastY = e.clientY;
    if (!m.dragging) {
      if (Math.abs(e.clientX - m.startX) < DRAG_THRESHOLD && Math.abs(e.clientY - m.startY) < DRAG_THRESHOLD) return;
      if (!this.startMove(m)) {
        this.endMove();
        return;
      }
    }
    e.preventDefault();
    if (m.cancelled) return;
    this.track(m);
    this.edgeScroll(m);
  };

  private startMove(m: MoveDrag): boolean {
    m.from = this.dataHeads().indexOf(m.th);
    if (m.from < 0) return false;
    [m.first, m.last] = this.range(m.from);
    if (m.first === m.last) return false; // nowhere it could go
    m.to = m.from;
    m.dragging = true;
    window.getSelection()?.removeAllRanges();
    try {
      m.th.setPointerCapture(m.pointerId);
    } catch {
      // A synthetic pointer: the document listeners still follow it.
    }
    // A live move measures the columns before anything about them changes.
    m.live = this.options.live ? this.beginLive(m) : null;
    m.th.setAttribute('data-moving', '');
    this.root.setAttribute('data-bz-moving', '');
    if (!m.live) {
      this.cols()[this.heads().indexOf(m.th)]?.setAttribute('data-moving', '');
      m.ghost = ghost(this.titleOf(m.key), m.th);
      m.indicator = indicator();
    }
    return true;
  }

  // The landing place: past every column in range whose middle the pointer has crossed, in reading
  // order. The line marks the gap the column would drop into; over its own place nothing would
  // move, and no line shows, wherever the column sits in its range.
  private track(m: MoveDrag): void {
    if (m.live) {
      this.follow(m, m.live);
      return;
    }
    const heads = this.dataHeads();
    const rtl = this.rtl;
    const others: DOMRect[] = [];
    for (let i = m.first; i <= m.last; i++) if (i !== m.from && heads[i]) others.push(heads[i].getBoundingClientRect());
    if (others.length === 0) return;
    let passed = 0;
    while (passed < others.length) {
      const r = others[passed];
      if (rtl ? m.lastX < r.left + r.width / 2 : m.lastX > r.left + r.width / 2) passed++;
      else break;
    }
    m.to = m.first + passed;
    // The gap: before the first column not passed, or after the last one passed.
    const edge =
      m.to === m.from
        ? NaN
        : passed === 0
          ? rtl
            ? others[0].right
            : others[0].left
          : rtl
            ? others[passed - 1].left
            : others[passed - 1].right;
    const box = this.container.getBoundingClientRect();
    const table = this.table.getBoundingClientRect();
    const top = Math.max(box.top, table.top, 0);
    const bottom = Math.min(box.bottom, table.bottom, window.innerHeight);
    if (m.indicator) {
      m.indicator.style.display = Number.isNaN(edge) ? 'none' : '';
      if (!Number.isNaN(edge)) {
        m.indicator.style.left = `${Math.min(Math.max(edge, box.left), box.right) - 1}px`;
        m.indicator.style.top = `${top}px`;
        m.indicator.style.height = `${Math.max(0, bottom - top)}px`;
      }
    }
    if (m.ghost) {
      m.ghost.style.left = `${m.lastX + 12}px`;
      m.ghost.style.top = `${m.lastY + 12}px`;
    }
  }

  // Near the box's edge the drag scrolls it, a little each frame.
  private edgeScroll(m: MoveDrag): void {
    if (m.scrollFrame) return;
    const tick = (): void => {
      m.scrollFrame = 0;
      if (this.moving !== m || !m.dragging || m.cancelled) return;
      const box = this.container.getBoundingClientRect();
      const speed = m.lastX < box.left + EDGE_SCROLL ? -1 : m.lastX > box.right - EDGE_SCROLL ? 1 : 0;
      if (speed === 0 || this.container.scrollWidth <= this.container.clientWidth) return;
      const before = this.container.scrollLeft;
      this.container.scrollLeft += speed * 12;
      if (this.container.scrollLeft === before) return;
      this.track(m);
      m.scrollFrame = requestAnimationFrame(tick);
    };
    m.scrollFrame = requestAnimationFrame(tick);
  }

  private onMoveUp = (e: PointerEvent): void => {
    const m = this.moving;
    if (!m || e.pointerId !== m.pointerId) return;
    const dropped = m.dragging && !m.cancelled && m.to !== m.from;
    const dragged = m.dragging;
    // The press focused the header's sort button (or the header): the focus goes with the column.
    const focused = dropped && m.th.contains(document.activeElement) ? document.activeElement : null;
    this.endMove();
    if (!dragged) return;
    // The click the release produces belongs to the drag, not to the sort button under it.
    this.swallowClick = true;
    window.setTimeout(() => (this.swallowClick = false), 0);
    if (focused) this.moved = { key: m.key, index: m.to, button: focused !== m.th, until: performance.now() + LANDING_WAIT };
    if (m.live) {
      // Cancelled with Escape: already on its way back.
      if (!m.cancelled) this.settleLive(m.live, dropped);
    } else if (dropped) {
      this.moveTo(m.key, m.from, m.to);
    }
  };

  private onMoveCancel = (e: PointerEvent): void => {
    const m = this.moving;
    if (!m || e.pointerId !== m.pointerId) return;
    this.endMove();
    if (m.live && !m.cancelled) this.settleLive(m.live, false);
  };

  // Escape mid-drag: no move (a live move's columns slide back). The release still swallows its click.
  private onMoveKey = (e: KeyboardEvent): void => {
    const m = this.moving;
    if (e.key !== 'Escape' || !m?.dragging || m.cancelled) return;
    e.preventDefault();
    e.stopPropagation();
    m.cancelled = true;
    m.ghost?.remove();
    m.indicator?.remove();
    if (m.live) this.settleLive(m.live, false);
  };

  private endMove(): void {
    const m = this.moving;
    if (!m) return;
    this.moving = null;
    cancelAnimationFrame(m.scrollFrame);
    document.removeEventListener('pointermove', this.onMoveMove, true);
    document.removeEventListener('pointerup', this.onMoveUp, true);
    document.removeEventListener('pointercancel', this.onMoveCancel, true);
    document.removeEventListener('keydown', this.onMoveKey, true);
    m.ghost?.remove();
    m.indicator?.remove();
    m.th.removeAttribute('data-moving');
    this.cols().forEach((c) => c.removeAttribute('data-moving'));
    this.root.removeAttribute('data-bz-moving');
    try {
      m.th.releasePointerCapture(m.pointerId);
    } catch {
      // Never captured, or already released.
    }
  }

  // ---- live moving ----

  /** The visible data columns' keys in display order, one per line. */
  private headerKeys(): string {
    return this.dataHeads()
      .map((th) => this.keyOf(th))
      .join('\n');
  }

  // A live move's measures, taken before anything moves, and its style sheet. Null where the page
  // cannot adopt a style sheet: the drag then shows the label and the line.
  private beginLive(m: MoveDrag): LiveMove | null {
    const root = this.table.getRootNode();
    if (!(root instanceof Document || root instanceof ShadowRoot) || !('adoptedStyleSheets' in root)) return null;
    let sheet: CSSStyleSheet;
    try {
      sheet = new CSSStyleSheet();
    } catch {
      return null;
    }
    const heads = this.dataHeads();
    const rtl = this.rtl;
    const pinned = !!this.specs.get(m.key)?.pin;
    const box = (pinned ? this.container : this.table).getBoundingClientRect();
    const starts: number[] = [];
    const widths: number[] = [];
    for (let i = m.first; i <= m.last; i++) {
      const r = heads[i].getBoundingClientRect();
      starts[i] = rtl ? box.right - r.right : r.left - box.left;
      widths[i] = r.width;
    }
    const live: LiveMove = {
      drag: m,
      id: String(++liveMoves),
      sheet,
      root,
      rtl,
      pinned,
      before: this.headerKeys(),
      nth: heads.map((th) => th.cellIndex + 1),
      starts,
      widths,
      grab: (rtl ? box.right - m.startX : m.startX - box.left) - starts[m.from],
      lift: '',
      motion: !window.matchMedia('(prefers-reduced-motion: reduce)').matches,
      landing: null,
      timer: 0,
    };
    live.lift = this.liftRules(live, m.th);
    this.clearLive();
    this.live = live;
    this.table.setAttribute('data-bz-live-move', live.id);
    this.placeLive(live, 0);
    sheet.replaceSync(this.liveCss(live, m.from, false));
    root.adoptedStyleSheets = [...root.adoptedStyleSheets, sheet];
    return live;
  }

  // The dragged column, lifted: over its neighbours but under the pinned columns (unless it is one),
  // opaque, faintly shaded and edged on both sides. A cell with no fill of its own paints what lies
  // under it - the table's surroundings, with its row's tint over them - so the cells it passes
  // over do not show through. The shade and edges go on each cell's ::after, which pinned cells
  // already have (a collapsed table paints no box-shadow on its cells).
  private liftRules(live: LiveMove, th: HTMLTableCellElement): string {
    const scope = `table[data-bz-columns][data-bz-live-move="${live.id}"]`;
    const n = live.nth[live.drag.from];
    const head = `${scope} > thead > tr > th:nth-child(${n})`;
    const body = `${scope} > tbody > tr > td:nth-child(${n}):not([colspan])`;
    const shade = 'color-mix(in oklab, var(--foreground) 4%, transparent)';
    const edge = 'color-mix(in oklab, var(--foreground) 16%, transparent)';
    const mark = `background-image: linear-gradient(${shade}, ${shade}); border-inline: 1px solid ${edge};`;
    const layer = 'content: ""; position: absolute; inset: 0; z-index: -1; pointer-events: none;';
    const rules = [
      // Full strength while it moves: the dimmed look belongs to the label-and-line drag.
      `${head} { z-index: ${live.pinned ? 13 : 11}; opacity: 1; }`,
      `${body} { z-index: ${live.pinned ? 3 : 1}; }`,
    ];
    // A pinned column's cells are opaque and sticky already, with their own ::after.
    if (live.pinned) {
      rules.push(`${head}::after, ${body}::after { ${mark} }`);
      return rules.join('\n');
    }
    const fill = (selector: string, under: string): void => {
      rules.push(
        `${selector} { background-color: inherit; }`,
        `${selector}::before { ${layer} background: ${under}; }`,
        `${selector}::after { ${layer} background-color: inherit; ${mark} }`,
      );
    };
    rules.push(`${body} { position: relative; }`);
    fill(body, backdrop(this.table.tBodies[0] ?? this.table));
    // A sticky header's cells have a fill of their own: only the shade and edges go over it.
    if (alphaOf(getComputedStyle(th).backgroundColor) < 1) fill(head, backdrop(this.table.tHead ?? this.table));
    else rules.push(`${head}::after { ${layer} ${mark} }`);
    return rules.join('\n');
  }

  // The sheet with the dragged column bound for `to`: the columns between its place and `to` move
  // over by its width (sliding, unless motion is reduced), and the dragged column sits at
  // --bz-live-x, following the pointer (no slide) until it is released.
  private liveCss(live: LiveMove, to: number, settling: boolean): string {
    const m = live.drag;
    const scope = `table[data-bz-columns][data-bz-live-move="${live.id}"]`;
    const cells = (i: number): string =>
      `${scope} > thead > tr > th:nth-child(${live.nth[i]}), ${scope} > tbody > tr > td:nth-child(${live.nth[i]}):not([colspan])`;
    const slide = live.motion ? `translate ${LIVE_SLIDE}ms cubic-bezier(0.2, 0, 0, 1)` : 'none';
    const width = live.widths[m.from];
    const rules = [
      // The pinned columns stay over a column passing under them.
      `${scope} > thead > tr > th[data-pinned] { z-index: 12; }`,
      `${scope} > tbody > tr > td[data-pinned] { z-index: 2; }`,
      `${scope} > tbody > tr > td[data-pinned]:is([data-editing], [data-invalid]) { z-index: 4; }`,
    ];
    for (let i = m.first; i <= m.last; i++) {
      if (i === m.from) continue;
      const shift = m.from < i && i <= to ? -width : to <= i && i < m.from ? width : 0;
      rules.push(`${cells(i)} { translate: ${live.rtl ? -shift : shift}px 0; transition: ${slide}; }`);
    }
    rules.push(`${cells(m.from)} { translate: var(--bz-live-x, 0px) 0; transition: ${settling ? slide : 'none'}; }`, live.lift);
    return rules.join('\n');
  }

  // Where the dragged column's start edge sits once it lands at `to` (reading order).
  private slot(live: LiveMove, to: number): number {
    const m = live.drag;
    return to > m.from ? live.starts[to] + live.widths[to] - live.widths[m.from] : live.starts[to];
  }

  /** The dragged column's offset from its own place, in reading order. */
  private placeLive(live: LiveMove, offset: number): void {
    this.table.style.setProperty('--bz-live-x', `${live.rtl ? -offset : offset}px`);
  }

  // The dragged column under the pointer, kept within its range, and the place it would land: the
  // slot nearest its start edge, so a neighbour slides over once the column covers half of it.
  private follow(m: MoveDrag, live: LiveMove): void {
    if (this.live !== live) return;
    const box = (live.pinned ? this.container : this.table).getBoundingClientRect();
    const at = (live.rtl ? box.right - m.lastX : m.lastX - box.left) - live.grab;
    const lo = live.starts[m.first];
    const hi = live.starts[m.last] + live.widths[m.last] - live.widths[m.from];
    const start = Math.min(Math.max(at, lo), hi);
    let to = m.to;
    let gap = Math.abs(start - this.slot(live, to));
    for (let t = m.first; t <= m.last; t++) {
      const d = Math.abs(start - this.slot(live, t));
      if (d < gap - 0.5) {
        to = t;
        gap = d;
      }
    }
    if (to !== m.to) {
      m.to = to;
      live.sheet.replaceSync(this.liveCss(live, to, false));
    }
    this.placeLive(live, start - live.starts[m.from]);
  }

  // A release (commit) or a cancel: the dragged column slides into its slot, or home. A move is
  // reported once the slide ends; the sheet stays until .NET's render shows the new order, so the
  // columns never flash back to the old one in between.
  private settleLive(live: LiveMove, commit: boolean): void {
    if (this.live !== live) return;
    const m = live.drag;
    const to = commit ? m.to : m.from;
    window.clearTimeout(live.timer);
    live.sheet.replaceSync(this.liveCss(live, to, true));
    this.placeLive(live, this.slot(live, to) - live.starts[m.from]);
    const done = (): void => {
      if (this.live !== live) return;
      if (to === m.from) {
        this.clearLive();
        return;
      }
      const keys = live.before.split('\n');
      keys.splice(m.from, 1);
      keys.splice(to, 0, m.key);
      live.landing = keys.join('\n');
      live.timer = window.setTimeout(() => this.unland(live), LANDING_WAIT);
      // .NET's render, when it takes the order, lands before its answer does: an order still not
      // in by then was refused.
      void invokeDotNet(this.ref, 'ColumnsReordered', keys).finally(() => requestAnimationFrame(() => this.unland(live)));
    };
    if (live.motion) live.timer = window.setTimeout(done, LIVE_SLIDE);
    else done();
  }

  // The header changed under a live move (the dropped order arrived, or a column came or went):
  // the sheet's places are out of date either way, so it goes. Mid-drag, the drag is cancelled.
  private landed(): void {
    const live = this.live;
    if (!live || this.headerKeys() === live.before) return;
    if (live.landing === null && this.moving?.live === live) {
      this.moving.cancelled = true;
      this.moving.live = null;
    }
    this.clearLive();
  }

  // The dropped order did not come back (a controlled order kept the old one): the columns slide home.
  private unland(live: LiveMove): void {
    if (this.live !== live || live.landing === null) return;
    live.landing = null;
    this.settleLive(live, false);
  }

  private clearLive(): void {
    const live = this.live;
    if (!live) return;
    this.live = null;
    window.clearTimeout(live.timer);
    live.root.adoptedStyleSheets = live.root.adoptedStyleSheets.filter((s) => s !== live.sheet);
    this.table.removeAttribute('data-bz-live-move');
    this.table.style.removeProperty('--bz-live-x');
  }

  // ---- events ----

  private onPointerDown = (e: PointerEvent): void => {
    // A live move still landing holds the columns where it dropped them until .NET's render arrives.
    if (e.button !== 0 || this.resizing || this.moving || this.live) return;
    const th = this.headOf(e.target);
    if (!th) return;
    const target = e.target as Element;
    const handle = target.closest<HTMLElement>('[data-bz-resizer]');
    if (handle && th.contains(handle)) {
      if (th.hasAttribute('data-resizable')) this.beginResize(e, th, handle);
      return;
    }
    // A touch on a header scrolls the table; a drag takes a mouse or a pen.
    if (!th.hasAttribute('data-reorderable') || e.pointerType === 'touch') return;
    // A control of the header's own keeps its press; the sort button lets a drag start on it.
    const control = target.closest(INTERACTIVE);
    if (control && th.contains(control) && !control.hasAttribute('data-bz-sort')) return;
    this.beginMove(e, th);
  };

  private onDoubleClick = (e: MouseEvent): void => {
    const handle = e.target instanceof Element ? e.target.closest('[data-bz-resizer]') : null;
    const th = handle ? this.headOf(handle) : null;
    if (!th || !th.hasAttribute('data-resizable')) return;
    e.preventDefault();
    e.stopPropagation();
    this.fit(this.keyOf(th), false);
  };

  private onClick = (e: MouseEvent): void => {
    if (!this.swallowClick) return;
    this.swallowClick = false;
    e.preventDefault();
    e.stopPropagation();
  };

  // On a focused header (or its sort button): Alt+Shift+Left / Right resizes and Alt+Shift+Enter
  // fits; Ctrl+Shift+Left / Right moves. Plain, Ctrl and Shift arrows stay with the cell cursor.
  private onKeyDown = (e: KeyboardEvent): void => {
    if (e.defaultPrevented || e.isComposing || this.live) return;
    const th = this.headOf(e.target);
    if (!th || (e.target !== th && isEditableTarget(e.target))) return;
    const key = this.keyOf(th);
    const arrow = e.key === 'ArrowLeft' || e.key === 'ArrowRight';
    // The arrow's direction on screen: in a right-to-left table the end edge is on the left.
    const outward = (e.key === 'ArrowRight') !== this.rtl ? 1 : -1;
    if (e.altKey && e.shiftKey && !e.ctrlKey && !e.metaKey && (arrow || e.key === 'Enter')) {
      if (!th.hasAttribute('data-resizable')) return;
      if (arrow) this.step(key, th, outward);
      else this.fit(key, true);
    } else if ((e.ctrlKey || e.metaKey) && e.shiftKey && !e.altKey && arrow) {
      if (!th.hasAttribute('data-reorderable')) return;
      this.shift(key, outward, e.target !== th);
    } else {
      return;
    }
    e.preventDefault();
    e.stopPropagation();
  };
}

// The floating label that follows a dragged header. Styled inline from the theme's tokens: it lives
// on the page body, outside anything a style sheet's classes reach.
const ghost = (title: string, th: HTMLElement): HTMLElement => {
  const el = document.createElement('div');
  el.textContent = title;
  el.setAttribute('aria-hidden', 'true');
  const font = getComputedStyle(th);
  Object.assign(el.style, {
    position: 'fixed',
    zIndex: '2147483000',
    pointerEvents: 'none',
    whiteSpace: 'nowrap',
    padding: '4px 10px',
    borderRadius: '6px',
    border: '1px solid var(--border)',
    background: 'var(--popover)',
    color: 'var(--popover-foreground)',
    boxShadow: '0 4px 12px rgb(0 0 0 / 0.15)',
    font: `${font.fontWeight} ${font.fontSize} ${font.fontFamily}`,
    letterSpacing: font.letterSpacing,
    textTransform: font.textTransform,
    left: '-9999px',
    top: '-9999px',
  });
  document.body.appendChild(el);
  return el;
};

// The marker on the boundary a dragged column would land on.
const indicator = (): HTMLElement => {
  const el = document.createElement('div');
  el.setAttribute('aria-hidden', 'true');
  Object.assign(el.style, {
    position: 'fixed',
    zIndex: '2147483000',
    pointerEvents: 'none',
    width: '2px',
    borderRadius: '1px',
    background: 'var(--primary)',
    display: 'none',
  });
  document.body.appendChild(el);
  return el;
};

/** Numbers live moves, so each one's sheet scopes to its own table. */
let liveMoves = 0;

// A computed color's alpha: 0 for transparent, 1 for an opaque color (no alpha written).
const alphaOf = (color: string): number => {
  if (color === 'transparent') return 0;
  const m = /\/\s*([-+.\deE]+)(%?)\s*\)$/.exec(color) ?? /^rgba\((?:[^,]*,){3}\s*([-+.\deE]+)(%?)\s*\)$/.exec(color);
  if (!m) return 1;
  const alpha = parseFloat(m[1]);
  return Number.isNaN(alpha) ? 1 : m[2] ? alpha / 100 : alpha;
};

// What shows behind an element, as one background value: the fills from it outward to the first
// opaque one, the nearest on top (translucent ones as one-color gradients, so they stack). The
// page's canvas color when nothing on the way is opaque.
const backdrop = (from: Element): string => {
  const layers: string[] = [];
  for (let el: Element | null = from; el; el = el.parentElement) {
    const color = getComputedStyle(el).backgroundColor;
    const alpha = alphaOf(color);
    if (alpha <= 0) continue;
    layers.push(color);
    if (alpha >= 1) break;
  }
  const base = layers.length > 0 && alphaOf(layers[layers.length - 1]) >= 1 ? layers.pop()! : 'Canvas';
  return [...layers.map((c) => `linear-gradient(${c}, ${c})`), base].join(', ');
};

let announcer: HTMLElement | null = null;

// A polite live region on the page body, for what a key press did (a new width, a move): the
// header keeps the focus, so nothing else would be heard.
const announce = (text: string): void => {
  if (!announcer || !announcer.isConnected) {
    announcer = document.createElement('div');
    announcer.setAttribute('role', 'status');
    announcer.setAttribute('aria-live', 'polite');
    Object.assign(announcer.style, {
      position: 'absolute',
      width: '1px',
      height: '1px',
      margin: '-1px',
      padding: '0',
      overflow: 'hidden',
      clip: 'rect(0 0 0 0)',
      whiteSpace: 'nowrap',
      border: '0',
    });
    document.body.appendChild(announcer);
  }
  announcer.textContent = text;
};

// Content-sized columns squeezed toward their min-content, in proportion to what each can give.
const squeeze = (slots: Slot[], need: number): void => {
  const give = slots.reduce((n, s) => n + (s.hi - s.lo), 0);
  const ratio = need <= 0 || give <= 0 ? 0 : Math.min(1, need / give);
  slots.forEach((s) => (s.width = s.hi - (s.hi - s.lo) * ratio));
};

// Content-sized columns stretched to fill the row, in proportion to their content, each up to its MaxWidth.
const stretch = (slots: Slot[], extra: number): void => {
  slots.forEach((s) => (s.width = s.hi));
  let open = slots.filter((s) => s.width < s.max);
  while (extra > 0.5 && open.length > 0) {
    const weight = open.reduce((n, s) => n + Math.max(s.hi, 1), 0);
    let spent = 0;
    for (const s of open) {
      const add = Math.min((extra * Math.max(s.hi, 1)) / weight, s.max - s.width);
      s.width += add;
      spent += add;
    }
    extra -= spent;
    open = open.filter((s) => s.width < s.max - 0.01);
    if (spent < 0.01) break;
  }
};

// Filling columns share the room equally, each kept between its floor and its ceiling.
const share = (slots: Slot[], room: number): void => {
  let open = slots.slice();
  let left = room;
  while (open.length > 0) {
    const each = left / open.length;
    const bound = open.filter((s) => each < s.lo || each > s.hi);
    if (bound.length === 0) {
      open.forEach((s) => (s.width = each));
      return;
    }
    for (const s of bound) {
      s.width = each < s.lo ? s.lo : s.hi;
      left -= s.width;
    }
    open = open.filter((s) => !bound.includes(s));
  }
};

/** Attaches the column features (sizing, pinning, moving) to a data table's root element. */
export const createColumns = (root: HTMLElement, dotNetRef: DotNetReference, options: ColumnsOptions): DataTableColumns =>
  new DataTableColumns(root, dotNetRef, options);

// A sticky header's cell: sticky AND stuck to the top. A pinned, resizable or movable header cell
// is sticky too, but with no top offset.
const isStickyHead = (cell: Element): boolean => {
  const style = getComputedStyle(cell);
  return style.position === 'sticky' && style.top !== 'auto';
};

// Input types that support the selection API (setSelectionRange throws on number, email, date...).
const isTextInput = (input: HTMLInputElement): boolean =>
  ['text', 'search', 'tel', 'url', 'password', ''].includes(input.type);

/** Attaches the keyboard grid to an editable data table's root element. */
export const createGrid = (root: HTMLElement, dotNetRef: DotNetReference, options: GridOptions): DataTableGrid =>
  new DataTableGrid(root, dotNetRef, options);
