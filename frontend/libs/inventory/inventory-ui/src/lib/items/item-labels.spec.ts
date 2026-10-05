import { describe, expect, it } from 'vitest';
import { batchLabels, itemDetailLabel, itemListLabels, labelKindFor, serialLabels } from './item-labels';

const row = (itemId: number, itemProfile = 'Retail') => ({
  itemId,
  itemCode: `ITM${itemId}`,
  itemName: `Item ${itemId}`,
  itemProfile,
  salesPrice: 100,
  mrp: 120,
});

describe('item labels (TK-159)', () => {
  it('labels the ticked items, or every listed one when none is ticked', () => {
    const rows = [row(1), row(2), row(3)];
    expect(itemListLabels(rows, new Set([2])).map((l) => l.itemCode)).toEqual(['ITM2']);
    expect(itemListLabels(rows, new Set()).map((l) => l.itemCode)).toEqual(['ITM1', 'ITM2', 'ITM3']);
    expect(itemListLabels(rows, new Set([1]))[0]).toMatchObject({ name: 'Item 1', price: 100, mrp: 120, copies: 1 });
  });

  it('starts on the label the items need', () => {
    expect(labelKindFor(['Jewellery', 'Jewellery'])).toBe('JewelleryTag');
    expect(labelKindFor(['Pharma'])).toBe('BatchExpiry');
    expect(labelKindFor(['Jewellery', 'Retail'])).toBe('PriceTag');
    expect(labelKindFor([])).toBe('PriceTag');
  });

  it('uses the open item’s primary barcode and its jewellery details', () => {
    const label = itemDetailLabel(
      {
        ...row(7, 'Jewellery'),
        barcodes: [
          { barcode: '111', isPrimary: false, isActive: true },
          { barcode: '8901234567892', isPrimary: true, isActive: true },
        ],
        jewellery: { grossWeight: 10.25, netWeight: 9.8 },
      },
      '22K',
    );
    expect(label).toMatchObject({ barcode: '8901234567892', grossWeightGrams: 10.25, netWeightGrams: 9.8, purity: '22K' });
  });

  it('falls back to the item code when the item has no barcode', () => {
    expect(itemDetailLabel({ ...row(8), barcodes: [], jewellery: null }, null).barcode).toBeNull();
  });
});

describe('batch and serial labels (TK-159)', () => {
  const item = { itemCode: 'PCM', name: 'Paracetamol', barcode: '890', mrp: 30, copies: 1 };

  it('prints one label per active batch with its dates and MRP', () => {
    const labels = batchLabels(item, [
      { batchNumber: 'B1', expiryDate: '2027-01-31', mrp: 32 },
      { batchNumber: 'B2', expiryDate: '2026-12-31', isActive: false },
      { batchNumber: 'B3' },
    ]);
    expect(labels.map((l) => [l.batchNo, l.expiryDate, l.mrp, l.barcode])).toEqual([
      ['B1', '2027-01-31', 32, '890'],
      ['B3', null, 30, '890'],
    ]);
  });

  it('prints one label per serial in stock, barcoded with the serial and carrying the HUID', () => {
    const labels = serialLabels(item, [
      { serialNumber: 'S1', hallmarkNumber: 'AB12CD', status: 'InStock' },
      { serialNumber: 'S2', status: 'Sold' },
    ]);
    expect(labels).toHaveLength(1);
    expect(labels[0]).toMatchObject({ serialNo: 'S1', barcode: 'S1', huid: 'AB12CD' });
  });
});
