import {
  DocumentLine,
  DocumentLineContext,
  LookupRow,
  blankDocumentLine,
  isEmptyLine,
  recalculate,
} from '@bill-book/ui-components';

/** One unit, in the grid's quantity scale. */
const ONE_UNIT = 1_000_000;

/**
 * What arrived, line by line — the goods receipt's own half of a line, kept
 * beside the shared grid (see the form for why).
 */
export interface ReceivingLine {
  acceptedQuantity: number;
  rejectedQuantity: number;
  rejectionReason: string;
  batchNumber: string;
  batchExpiryDate: string;
  purchaseOrderDetailId: number | null;
}

/** A batch read off a pack's GS1 code. */
export interface ScannedBatch {
  batch: string | null;
  expiry: string | null;
}

/**
 * A scanned item on a goods receipt (TK-162): one more received, on the line
 * for that item — and, when the scan carried a batch, on the line for that
 * batch, because one line holds one batch. A batch read off the pack fills a
 * line that has none yet. Otherwise a new line of one, in the first untouched
 * line if there is one.
 */
export function receiveScanned(
  lines: readonly DocumentLine[],
  receiving: readonly ReceivingLine[],
  row: LookupRow,
  scanned: ScannedBatch | null,
  context: DocumentLineContext,
): { lines: DocumentLine[]; receiving: ReceivingLine[]; index: number } {
  const batch = scanned?.batch?.trim() || '';
  const target = lines.findIndex((line, at) => {
    if (line.itemId !== row.id) {
      return false;
    }
    const held = receiving[at]?.batchNumber?.trim() ?? '';
    return !batch || !held || held === batch;
  });

  if (target >= 0) {
    const nextLines = lines.map((line, at) =>
      at === target ? recalculate({ ...line, quantity: line.quantity + ONE_UNIT }, context) : line,
    );
    const nextReceiving = lines.map((_, at) => {
      const existing = receiving[at] ?? newReceiving(lines[at].quantity / ONE_UNIT, null);
      if (at !== target) {
        return existing;
      }
      return {
        ...existing,
        acceptedQuantity: existing.acceptedQuantity + 1,
        batchNumber: existing.batchNumber || batch,
        batchExpiryDate: existing.batchExpiryDate || (batch ? scanned?.expiry ?? '' : ''),
      };
    });
    return { lines: nextLines, receiving: nextReceiving, index: target };
  }

  const empty = lines.findIndex(isEmptyLine);
  const index = empty >= 0 ? empty : lines.length;
  const base = empty >= 0 ? lines[empty] : blankDocumentLine(lines.length + 1);
  const added = recalculate(
    { ...base, itemId: row.id, itemLabel: `${row.code} - ${row.name}`, quantity: ONE_UNIT },
    context,
  );
  const fresh = newReceiving(1, batch ? { batch, expiry: scanned?.expiry ?? null } : null);

  const nextLines = empty >= 0 ? lines.map((line, at) => (at === empty ? added : line)) : [...lines, added];
  const nextReceiving = nextLines.map((line, at) =>
    at === index ? fresh : receiving[at] ?? newReceiving(line.quantity / ONE_UNIT, null),
  );
  return { lines: nextLines, receiving: nextReceiving, index };
}

/** Every line at nothing received, ready to count a delivery in by scanning. */
export function zeroReceived(
  lines: readonly DocumentLine[],
  receiving: readonly ReceivingLine[],
  context: DocumentLineContext,
): { lines: DocumentLine[]; receiving: ReceivingLine[] } {
  return {
    lines: lines.map((line) => recalculate({ ...line, quantity: 0 }, context)),
    receiving: lines.map((line, at) => ({
      ...(receiving[at] ?? newReceiving(0, null)),
      acceptedQuantity: 0,
      rejectedQuantity: 0,
    })),
  };
}

function newReceiving(accepted: number, batch: ScannedBatch | null): ReceivingLine {
  return {
    acceptedQuantity: accepted,
    rejectedQuantity: 0,
    rejectionReason: '',
    batchNumber: batch?.batch ?? '',
    batchExpiryDate: batch?.expiry ?? '',
    purchaseOrderDetailId: null,
  };
}
