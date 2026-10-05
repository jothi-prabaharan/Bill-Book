import { DocumentLine, DocumentLineContext } from './document-line.model';
import { recalculate } from './line-math';
import { LookupRow } from '../lookup-dialog/lookup-row.model';

/** One unit, in the grid's quantity scale. */
const ONE_UNIT = 1_000_000;

/**
 * A scanned item on a document's lines (TK-161): one more of it on the line
 * that already has it, otherwise a line of one — in the first empty line if
 * there is one, so a new form's blank line is used rather than left behind.
 *
 * Pure, so every document form — sales and purchase — adds a scan the same way.
 */
export function addScannedItem<T extends DocumentLine>(
  lines: readonly T[],
  row: LookupRow,
  context: DocumentLineContext,
  blank: (lineNumber: number) => T,
): T[] {
  const existing = lines.findIndex((line) => line.itemId === row.id);
  if (existing >= 0) {
    return lines.map((line, at) =>
      at === existing ? (recalculate({ ...line, quantity: line.quantity + ONE_UNIT }, context) as T) : line,
    );
  }

  const empty = lines.findIndex(isEmptyLine);
  const base = empty >= 0 ? lines[empty] : blank(lines.length + 1);
  const added = recalculate(
    { ...base, itemId: row.id, itemLabel: `${row.code} - ${row.name}`, quantity: ONE_UNIT },
    context,
  ) as T;

  return empty >= 0 ? lines.map((line, at) => (at === empty ? added : line)) : [...lines, added];
}

/** A line nobody has started: no item, no description, no price. */
export function isEmptyLine(line: DocumentLine): boolean {
  return line.itemId === null && !line.description?.trim() && line.unitPrice === 0;
}
