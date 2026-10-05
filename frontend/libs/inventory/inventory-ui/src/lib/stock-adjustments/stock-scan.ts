import { parseGs1 } from '@bill-book/scan-core';

/** The part of a sheet line a scan touches. */
export interface ScannableSheetLine {
  itemId: number | null;
  countedQuantity: number | null;
  quantity: number | null;
  batchNumber: string | null;
  batchExpiryDate: string | null;
}

/** A batch read off a pack. */
export interface SheetBatch {
  batch: string | null;
  expiry: string | null;
}

/**
 * A scanned item on a stock sheet (TK-163): one more counted on a count, one
 * more adjusted on a write-off — on the item's line, or the line for the
 * scanned batch, since one line holds one batch. Otherwise the first empty
 * line takes it, or a new line is added.
 */
export function scanOntoSheet<T extends ScannableSheetLine>(
  lines: readonly T[],
  itemId: number,
  isCount: boolean,
  scanned: SheetBatch | null,
  blank: () => T,
): T[] {
  const batch = scanned?.batch?.trim() || '';
  const field = isCount ? 'countedQuantity' : 'quantity';

  const target = lines.findIndex((line) => {
    if (line.itemId !== itemId) {
      return false;
    }
    const held = line.batchNumber?.trim() ?? '';
    return !batch || !held || held === batch;
  });

  if (target >= 0) {
    return lines.map((line, at) =>
      at === target
        ? {
            ...line,
            [field]: (line[field] ?? 0) + 1,
            batchNumber: line.batchNumber || batch || null,
            batchExpiryDate: line.batchExpiryDate || (batch ? scanned?.expiry ?? null : null),
          }
        : line,
    );
  }

  const empty = lines.findIndex((line) => line.itemId === null);
  const added: T = {
    ...(empty >= 0 ? lines[empty] : blank()),
    itemId,
    [field]: 1,
    batchNumber: batch || null,
    batchExpiryDate: batch ? scanned?.expiry ?? null : null,
  };
  return empty >= 0 ? lines.map((line, at) => (at === empty ? added : line)) : [...lines, added];
}

/**
 * The batch a scan names: from a pack's GS1 code when it is one, otherwise the
 * scanned text itself, as a shop-printed batch label carries only that.
 */
export function batchFromScan(code: string): SheetBatch {
  const gs1 = parseGs1(code);
  if (gs1) {
    return { batch: gs1.batch, expiry: gs1.expiry };
  }
  const text = code.trim();
  return { batch: text || null, expiry: null };
}

/**
 * A scanned serial number added to the one-per-line list (TK-163), never twice:
 * a piece is scanned once, and a second read of it is a slip, not a second unit.
 */
export function addSerial(text: string, code: string): { text: string; added: boolean; count: number } {
  const serial = code.trim();
  const serials = text.split('\n').map((s) => s.trim()).filter((s) => s.length > 0);
  if (!serial || serials.includes(serial)) {
    return { text: serials.join('\n'), added: false, count: serials.length };
  }
  const next = [...serials, serial];
  return { text: next.join('\n'), added: true, count: next.length };
}
