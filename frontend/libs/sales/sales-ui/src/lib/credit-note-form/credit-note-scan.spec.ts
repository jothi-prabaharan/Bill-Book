import { describe, expect, it } from 'vitest';
import { blankGridLine } from '@bill-book/sales-core';
import { DocumentLineContext } from '@bill-book/ui-components';
import { ReturnLine, returnScanned, zeroReturnQuantities } from './credit-note-scan';

const QTY = 1_000_000;

const context: DocumentLineContext = {
  isInterState: false,
  allowFreeTextLines: false,
  discountBeforeTax: true,
  discountLevel: 'Line',
  readonly: false,
  currencyDecimals: 2,
};

function line(itemId: number, quantity: number, returnable: number | null, invoiceDetailId = itemId): ReturnLine {
  return { ...blankGridLine(1), itemId, quantity: quantity * QTY, unitPrice: 10_000, returnable: returnable === null ? null : returnable * QTY, invoiceDetailId };
}

describe('returnScanned (TK-161)', () => {
  it('counts one more unit back on the invoice line for the scanned item', () => {
    const { lines, outcome } = returnScanned([line(7, 0, 3), line(8, 0, 1)], 7, context);

    expect(outcome).toBe('added');
    expect(lines[0].quantity).toBe(QTY);
    expect(lines[0].grossAmount).toBe(10_000);
    expect(lines[1].quantity).toBe(0);
  });

  it('three scans of the same item give quantity 3 on its line', () => {
    let lines = [line(7, 0, 5)];
    for (let i = 0; i < 3; i++) {
      lines = returnScanned(lines, 7, context).lines;
    }
    expect(lines).toHaveLength(1);
    expect(lines[0].quantity).toBe(3 * QTY);
  });

  it('stops at what the invoice line can still return', () => {
    const { lines, outcome } = returnScanned([line(7, 2, 2)], 7, context);

    expect(outcome).toBe('full');
    expect(lines[0].quantity).toBe(2 * QTY);
  });

  it('moves on to a second invoice line for the same item when the first is full', () => {
    const { lines } = returnScanned([line(7, 1, 1, 101), line(7, 0, 4, 102)], 7, context);

    expect(lines.map((l) => l.quantity)).toEqual([QTY, QTY]);
  });

  it('never adds a line for an item the invoice does not have', () => {
    const { lines, outcome } = returnScanned([line(7, 0, 1)], 9, context);

    expect(outcome).toBe('notOnInvoice');
    expect(lines).toHaveLength(1);
  });

  it('a saved note, whose limit is not known, is limited by the server instead', () => {
    expect(returnScanned([line(7, 4, null)], 7, context).outcome).toBe('added');
  });

  it('sets every line to nothing returned', () => {
    const zeroed = zeroReturnQuantities([line(7, 2, 2), line(8, 1, 1)], context);
    expect(zeroed.map((l) => l.quantity)).toEqual([0, 0]);
    expect(zeroed.map((l) => l.lineTotal)).toEqual([0, 0]);
  });
});
