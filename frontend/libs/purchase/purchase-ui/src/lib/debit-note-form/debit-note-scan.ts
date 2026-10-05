/** The part of a debit note's line a scan needs. */
export interface ScannableReturnLine {
  itemId: number | null;
  available: number;
  quantity: number;
}

export type DebitScanOutcome = 'added' | 'full' | 'notOnBill';

/**
 * A scanned item on a debit note (TK-162). Its lines are the bill's, never new
 * ones, so a scan sends one more unit back on the bill line for that item, up
 * to what the line still has to return — moving on to the next line for the
 * same item when one is full.
 */
export function debitScanned<T extends ScannableReturnLine>(
  lines: readonly T[],
  itemId: number,
): { lines: T[]; outcome: DebitScanOutcome } {
  if (!lines.some((line) => line.itemId === itemId)) {
    return { lines: [...lines], outcome: 'notOnBill' };
  }
  const target = lines.findIndex((line) => line.itemId === itemId && line.quantity + 1 <= line.available);
  if (target < 0) {
    return { lines: [...lines], outcome: 'full' };
  }
  return {
    lines: lines.map((line, at) => (at === target ? { ...line, quantity: line.quantity + 1 } : line)),
    outcome: 'added',
  };
}
