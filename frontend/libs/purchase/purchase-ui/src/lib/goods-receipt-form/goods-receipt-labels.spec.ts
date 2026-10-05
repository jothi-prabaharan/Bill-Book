import { describe, expect, it } from 'vitest';
import { goodsReceiptLabelKind, goodsReceiptLabels } from './goods-receipt-labels';

describe('goods receipt labels (TK-159)', () => {
  it('prints one label per unit accepted, one for a fraction, none for nothing accepted', () => {
    const labels = goodsReceiptLabels([
      { itemId: 1, itemLabel: 'SOAP - Neem soap', description: null, acceptedQuantity: 12 },
      { itemId: 2, itemLabel: 'RICE - Sona masoori', description: null, acceptedQuantity: 2.5 },
      { itemId: 3, itemLabel: 'OIL - Groundnut oil', description: null, acceptedQuantity: 0 },
      { itemId: null, itemLabel: null, description: 'Freight', acceptedQuantity: 1 },
    ]);

    expect(labels.map((l) => [l.itemCode, l.name, l.copies])).toEqual([
      ['SOAP', 'Neem soap', 12],
      ['RICE', 'Sona masoori', 1],
    ]);
  });

  it('carries the batch and expiry, and starts on a batch label when there is one', () => {
    const labels = goodsReceiptLabels([
      { itemId: 1, itemLabel: 'PCM - Paracetamol 500', description: null, acceptedQuantity: 3, batchNumber: 'B12', batchExpiryDate: '2027-03-31' },
    ]);

    expect(labels[0]).toMatchObject({ batchNo: 'B12', expiryDate: '2027-03-31', copies: 3 });
    expect(goodsReceiptLabelKind(labels)).toBe('BatchExpiry');
    expect(goodsReceiptLabelKind([{ name: 'X', copies: 1 }])).toBe('PriceTag');
  });
});
