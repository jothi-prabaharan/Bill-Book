import { DocumentLine, DocumentLineContext, recalculate } from '@bill-book/ui-components';

/** One unit, in the grid's quantity scale. */
const ONE_UNIT = 1_000_000;

/** A credit note's line, with what its invoice line can still take back when that is known. */
export type ReturnLine = DocumentLine & { invoiceDetailId?: number | null; returnable?: number | null };

export type ReturnScanOutcome = 'added' | 'full' | 'notOnInvoice';

/**
 * A scanned item on a credit note (TK-161). A credit note's lines are its
 * invoice's, never new ones, so a scan counts one more unit back on the
 * invoice line for that item — up to what that line can still return — rather
 * than adding a line.
 */
export function returnScanned<T extends ReturnLine>(
  lines: readonly T[],
  itemId: number,
  context: DocumentLineContext,
): { lines: T[]; outcome: ReturnScanOutcome } {
  const forItem = lines.map((line, at) => ({ line, at })).filter(({ line }) => line.itemId === itemId);
  if (forItem.length === 0) {
    return { lines: [...lines], outcome: 'notOnInvoice' };
  }

  const room = forItem.find(({ line }) => line.returnable == null || line.quantity + ONE_UNIT <= line.returnable);
  if (!room) {
    return { lines: [...lines], outcome: 'full' };
  }

  return {
    lines: lines.map((line, at) =>
      at === room.at ? (recalculate({ ...line, quantity: line.quantity + ONE_UNIT }, context) as T) : line,
    ),
    outcome: 'added',
  };
}

/** Every line at nothing returned, ready to count the goods back in by scanning. */
export function zeroReturnQuantities<T extends DocumentLine>(lines: readonly T[], context: DocumentLineContext): T[] {
  return lines.map((line) => recalculate({ ...line, quantity: 0 }, context) as T);
}
