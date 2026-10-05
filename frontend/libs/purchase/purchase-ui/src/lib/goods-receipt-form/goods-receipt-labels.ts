import { LabelData, LabelKind, copiesForQuantity } from '@bill-book/ui-components';

/** The parts of a receipt line a label needs. */
export interface ReceiptLabelLine {
  itemId: number | null;
  /** `CODE - Name`, as the line grid shows it. */
  itemLabel: string | null;
  description: string | null;
  acceptedQuantity: number;
  batchNumber?: string | null;
  batchExpiryDate?: string | null;
}

/**
 * Labels for a goods receipt (TK-159): one per unit accepted on each item
 * line, so twelve received is twelve labels; a fraction — kilograms of rice —
 * is one. Lines with no item, and lines with nothing accepted, get none. No
 * price: the receipt carries what the stock cost, which is not for a shelf.
 */
export function goodsReceiptLabels(lines: readonly ReceiptLabelLine[]): LabelData[] {
  return lines
    .filter((line) => line.itemId !== null && line.acceptedQuantity > 0)
    .map((line) => {
      const label = line.itemLabel ?? '';
      const dash = label.indexOf(' - ');
      const code = dash >= 0 ? label.slice(0, dash).trim() : null;
      const name = (dash >= 0 ? label.slice(dash + 3) : label).trim() || line.description?.trim() || 'Item';
      return {
        itemCode: code,
        name,
        batchNo: line.batchNumber?.trim() || null,
        expiryDate: line.batchExpiryDate?.trim() || null,
        copies: copiesForQuantity(line.acceptedQuantity),
      };
    });
}

/** A batch label when any line carries a batch, else a price tag. */
export function goodsReceiptLabelKind(labels: readonly LabelData[]): LabelKind {
  return labels.some((l) => l.batchNo) ? 'BatchExpiry' : 'PriceTag';
}
