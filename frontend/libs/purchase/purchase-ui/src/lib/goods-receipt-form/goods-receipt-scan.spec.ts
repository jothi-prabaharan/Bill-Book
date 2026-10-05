import { describe, expect, it } from 'vitest';
import { DocumentLineContext, blankDocumentLine } from '@bill-book/ui-components';
import { ReceivingLine, receiveScanned, zeroReceived } from './goods-receipt-scan';

const QTY = 1_000_000;

const context: DocumentLineContext = {
  isInterState: false,
  allowFreeTextLines: true,
  discountBeforeTax: true,
  discountLevel: 'Line',
  readonly: false,
  currencyDecimals: 2,
};

const tablet = { id: 5, code: 'PARA500', name: 'Paracetamol 500' };

function row(accepted: number, batchNumber = '', batchExpiryDate = ''): ReceivingLine {
  return { acceptedQuantity: accepted, rejectedQuantity: 0, rejectionReason: '', batchNumber, batchExpiryDate, purchaseOrderDetailId: null };
}

describe('receiveScanned (TK-162)', () => {
  it('scanning an item on a goods receipt raises its received quantity', () => {
    let lines = [blankDocumentLine(1)];
    let receiving = [row(1)];
    for (let i = 0; i < 3; i++) {
      ({ lines, receiving } = receiveScanned(lines, receiving, tablet, null, context));
    }

    expect(lines).toHaveLength(1);
    expect(lines[0].itemId).toBe(5);
    expect(lines[0].quantity).toBe(3 * QTY);
    expect(receiving[0].acceptedQuantity).toBe(3);
  });

  it('keeps what was rejected when it raises the received quantity', () => {
    const line = { ...blankDocumentLine(1), itemId: 5, quantity: 4 * QTY };
    const { receiving } = receiveScanned([line], [{ ...row(3), rejectedQuantity: 1 }], tablet, null, context);

    expect(receiving[0].acceptedQuantity).toBe(4);
    expect(receiving[0].rejectedQuantity).toBe(1);
  });

  it('fills the batch and expiry a pack carried into a line with none', () => {
    const { lines, receiving, index } = receiveScanned(
      [blankDocumentLine(1)],
      [row(1)],
      tablet,
      { batch: 'B12', expiry: '2027-03-31' },
      context,
    );

    expect(index).toBe(0);
    expect(lines[0].itemId).toBe(5);
    expect(receiving[0]).toMatchObject({ acceptedQuantity: 1, batchNumber: 'B12', batchExpiryDate: '2027-03-31' });
  });

  it('a second batch of the same item goes on a line of its own', () => {
    const first = receiveScanned([blankDocumentLine(1)], [row(1)], tablet, { batch: 'B12', expiry: '2027-03-31' }, context);
    const second = receiveScanned(first.lines, first.receiving, tablet, { batch: 'B13', expiry: '2027-06-30' }, context);
    const again = receiveScanned(second.lines, second.receiving, tablet, { batch: 'B12', expiry: '2027-03-31' }, context);

    expect(again.lines).toHaveLength(2);
    expect(again.receiving.map((r) => [r.batchNumber, r.acceptedQuantity])).toEqual([
      ['B12', 2],
      ['B13', 1],
    ]);
    expect(again.index).toBe(0);
  });

  it('a plain barcode adds to the item line whatever batch it holds', () => {
    const line = { ...blankDocumentLine(1), itemId: 5 };
    const { lines, receiving } = receiveScanned([line], [row(1, 'B12')], tablet, null, context);

    expect(lines).toHaveLength(1);
    expect(receiving[0]).toMatchObject({ acceptedQuantity: 2, batchNumber: 'B12' });
  });
});

describe('zeroReceived', () => {
  it('sets every line to nothing received, ready to count a delivery in', () => {
    const line = { ...blankDocumentLine(1), itemId: 5, quantity: 10 * QTY };
    const { lines, receiving } = zeroReceived([line], [{ ...row(8), rejectedQuantity: 2 }], context);

    expect(lines[0].quantity).toBe(0);
    expect(receiving[0]).toMatchObject({ acceptedQuantity: 0, rejectedQuantity: 0 });
  });
});
