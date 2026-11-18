/**
 * Putting a data field into the cell that was clicked (TK-267).
 *
 * The editor used to insert wherever the caret last happened to be. A click on a cell now opens the field list
 * and remembers that cell, and the field goes into it however long the person takes to pick and wherever focus
 * has gone since. These are the rules for where it goes and how it is put there, kept apart from the page so
 * they are the same on every band and can be checked without a browser.
 *
 * Fields are written the way the editor has always written them: a chip that cannot be edited into, holding
 * `{{Tag}}` for a field that prints once and `<<Tag>>` for one that repeats per row, followed by a
 * non-breaking space so the caret has somewhere to be.
 */

/** What the picker knows about a field. */
export interface FieldInfo {
  tag: string;
  description?: string;
}

/** Where a chosen field goes: the cell that gets the chip, and a heading cell that gets the field's name. */
export interface FieldPlan {
  chipCell: HTMLElement | null;
  labelCell: HTMLTableCellElement | null;
}

/** A heading cell is a `th`, or any cell in a `thead`. */
export function isHeadingCell(cell: HTMLTableCellElement): boolean {
  return cell.tagName === 'TH' || cell.closest('thead') !== null;
}

/** The column a cell starts in, counting the columns the cells before it span. */
function startColumn(cell: HTMLTableCellElement): number {
  const row = cell.parentElement as HTMLTableRowElement | null;
  let column = 0;
  for (const sibling of Array.from(row?.cells ?? [])) {
    if (sibling === cell) {
      return column;
    }
    column += sibling.colSpan || 1;
  }
  return column;
}

function cellAtColumn(row: HTMLTableRowElement, column: number): HTMLTableCellElement | null {
  let start = 0;
  for (const cell of Array.from(row.cells)) {
    const span = cell.colSpan || 1;
    if (column >= start && column < start + span) {
      return cell;
    }
    start += span;
  }
  return null;
}

/**
 * The cell under a heading cell: the same column of the table's first row that has a body cell in it. Null when
 * the table has no such row, or has nothing in that column.
 */
export function bodyCellBelow(cell: HTMLTableCellElement): HTMLTableCellElement | null {
  const table = cell.closest('table');
  const here = cell.parentElement as HTMLTableRowElement | null;
  if (!table || !here) {
    return null;
  }

  const body = Array.from(table.rows).find(
    (row) => row !== here && Array.from(row.cells).some((c) => !isHeadingCell(c)),
  );

  return body ? cellAtColumn(body, startColumn(cell)) : null;
}

/**
 * Where a field goes when `target` was the clicked cell. A field that repeats per row, picked for a heading
 * cell, belongs in the column it heads: the chip goes in the cell below and the heading cell is named after the
 * field. Anything else goes in the cell that was clicked, and no cell at all means the band's own end.
 */
export function planFieldInsert(target: HTMLElement | null, isList: boolean): FieldPlan {
  if (target && isList && target.matches('td, th')) {
    const heading = target as HTMLTableCellElement;
    if (isHeadingCell(heading)) {
      const below = bodyCellBelow(heading);
      if (below) {
        return { chipCell: below, labelCell: heading };
      }
    }
  }
  return { chipCell: target, labelCell: null };
}

/** What a heading says for a field: its description when that is short, otherwise its name made readable. */
export function headingLabel(field: FieldInfo): string {
  const described = (field.description ?? '').split(/[.\n]/)[0].trim();
  if (described && described.length <= 40) {
    return described;
  }

  const name = field.tag.split('.').pop() ?? field.tag;
  const words = name
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/_/g, ' ')
    .trim();
  return words.charAt(0).toUpperCase() + words.slice(1).toLowerCase();
}

/** A cell with nothing in it: no text but spaces, no field, and no image. */
export function isBlankCell(cell: HTMLElement): boolean {
  if (cell.querySelector('.pt-chip, img, table')) {
    return false;
  }
  return (cell.textContent ?? '').replace(/\u00a0/g, ' ').trim() === '';
}

/** A cell's place in words, for the picker's heading. */
export function cellPosition(cell: HTMLTableCellElement): string {
  const row = (cell.parentElement as HTMLTableRowElement | null)?.rowIndex ?? 0;
  return `row ${row + 1}, column ${startColumn(cell) + 1}`;
}

/** The chip and the space after it, built as the editor has always written them. */
export function buildChip(doc: Document, tag: string, isList: boolean): { fragment: DocumentFragment; last: Node } {
  const chip = doc.createElement('span');
  chip.setAttribute('contenteditable', 'false');
  chip.className = isList ? 'pt-chip pt-chip--list' : 'pt-chip';
  chip.textContent = isList ? `<<${tag}>>` : `{{${tag}}}`;

  const space = doc.createTextNode('\u00a0');
  const fragment = doc.createDocumentFragment();
  fragment.append(chip, space);
  return { fragment, last: space };
}

/** A cell the browser left with only its placeholder line break takes the chip in place of it. */
function clearPlaceholderBreak(cell: HTMLElement): void {
  if (isBlankCell(cell) && cell.querySelector('br')) {
    cell.textContent = '';
  }
}

/**
 * Puts the chip in: at the caret the click left, when that is inside the cell; otherwise at the end of the
 * cell; with no cell, at the saved caret when it is inside the band, otherwise at the end of the band. Returns
 * a collapsed range just after the chip, for the caret to go to.
 */
export function insertChipAt(
  host: HTMLElement,
  cell: HTMLElement | null,
  saved: Range | null,
  tag: string,
  isList: boolean,
): Range {
  const doc = host.ownerDocument;
  const range = doc.createRange();
  const home = cell ?? host;

  if (saved && home.contains(saved.startContainer) && !isInsideChip(saved.startContainer, home)) {
    range.setStart(saved.startContainer, saved.startOffset);
    range.collapse(true);
  } else {
    if (cell) {
      clearPlaceholderBreak(cell);
    }
    range.selectNodeContents(home);
    range.collapse(false);
  }

  const { fragment, last } = buildChip(doc, tag, isList);
  range.insertNode(fragment);

  const after = doc.createRange();
  after.setStartAfter(last);
  after.collapse(true);
  return after;
}

/** A caret inside a chip would split it, so a click on one counts as a click on the cell around it. */
function isInsideChip(node: Node, boundary: HTMLElement): boolean {
  let current: Node | null = node;
  while (current && current !== boundary) {
    if (current instanceof HTMLElement && current.classList.contains('pt-chip')) {
      return true;
    }
    current = current.parentNode;
  }
  return false;
}

/** Names a heading cell for the field, but only when it has nothing in it: an existing heading is the person's. */
export function nameHeading(cell: HTMLTableCellElement, field: FieldInfo): boolean {
  if (!isBlankCell(cell)) {
    return false;
  }
  cell.textContent = headingLabel(field);
  return true;
}

/** The edges of a box: all the picker needs to know about a cell, the paper's scroller or the window. */
export interface EdgeRect {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

/** Where the picker goes: from the left edge, and from the top when it opens downward or the bottom when upward. */
export interface PickerPlacement {
  left: number;
  top: number | null;
  bottom: number | null;
  maxHeight: number;
}

/** The picker's width, the height it is given when there is room, and the least it is ever squeezed to. */
export const PICKER_WIDTH = 440;
export const PICKER_HEIGHT = 380;
const PICKER_LEAST_HEIGHT = 160;
const PICKER_GAP = 6;
const PICKER_MARGIN = 8;

/**
 * Where the picker sits for an anchor in a window of this size: touching the anchor, below it when there is room
 * and otherwise on whichever side has more, and never off either edge of the window. It hangs from the anchor's
 * own edge rather than being given a height to guess at, so it does not float away from a cell when its list is
 * short, and it is only as tall as the room allows; its list scrolls.
 */
export function pickerPlacement(anchor: EdgeRect, viewport: { width: number; height: number }): PickerPlacement {
  const left = Math.max(PICKER_MARGIN, Math.min(anchor.left, viewport.width - PICKER_WIDTH - PICKER_MARGIN));
  const below = viewport.height - anchor.bottom - PICKER_GAP - PICKER_MARGIN;
  const above = anchor.top - PICKER_GAP - PICKER_MARGIN;

  if (below >= PICKER_HEIGHT || below >= above) {
    return {
      left,
      top: anchor.bottom + PICKER_GAP,
      bottom: null,
      maxHeight: Math.max(PICKER_LEAST_HEIGHT, Math.min(PICKER_HEIGHT, below)),
    };
  }
  return {
    left,
    top: null,
    bottom: viewport.height - anchor.top + PICKER_GAP,
    maxHeight: Math.max(PICKER_LEAST_HEIGHT, Math.min(PICKER_HEIGHT, above)),
  };
}

/** Whether any of a box is inside a view. A box that only touches its edge is out of sight. */
export function isInView(box: EdgeRect, view: EdgeRect): boolean {
  return box.bottom > view.top && box.top < view.bottom && box.right > view.left && box.left < view.right;
}
