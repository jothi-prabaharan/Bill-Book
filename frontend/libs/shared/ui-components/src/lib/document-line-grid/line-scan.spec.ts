import { describe, expect, it } from 'vitest';
import { DocumentLine, DocumentLineContext } from './document-line.model';
import { addScannedItem, isEmptyLine } from './line-scan';
import { LookupRow } from '../lookup-dialog/lookup-row.model';

const QTY = 1_000_000;

const context: DocumentLineContext = {
  isInterState: false,
  allowFreeTextLines: true,
  discountBeforeTax: true,
  discountLevel: 'Line',
  readonly: false,
  currencyDecimals: 2,
};

function blank(lineNumber: number): DocumentLine {
  return {
    detailId: 0,
    lineNumber,
    itemId: null,
    itemLabel: null,
    hsnSacCode: null,
    description: null,
    warehouseId: null,
    quantity: QTY,
    uomId: null,
    uomLabel: null,
    conversionFactor: QTY,
    baseQuantity: QTY,
    unitPrice: 0,
    isPriceInclusive: false,
    discountPercent: null,
    discountAmount: 0,
    grossAmount: 0,
    taxableAmount: 0,
    taxTreatment: 'Taxable',
    taxMasterId: null,
    taxGroupId: null,
    taxAmount: 0,
    taxes: [],
    lineType: 'Stock',
    accountId: null,
    fixedAssetCategoryId: null,
    lineTotal: 0,
    itemBatchId: null,
    lineNotes: null,
  };
}

const soap: LookupRow = { id: 7, code: 'SOAP01', name: 'Neem soap' };
const oil: LookupRow = { id: 8, code: 'OIL01', name: 'Coconut oil' };

describe('addScannedItem (TK-161)', () => {
  it('scanning the same barcode three times gives one line with quantity 3', () => {
    let lines = [blank(1)];
    for (let i = 0; i < 3; i++) {
      lines = addScannedItem(lines, soap, context, blank);
    }

    expect(lines).toHaveLength(1);
    expect(lines[0].itemId).toBe(7);
    expect(lines[0].quantity).toBe(3 * QTY);
    expect(lines[0].baseQuantity).toBe(3 * QTY);
  });

  it('uses the blank line first, then adds a line per new item', () => {
    let lines = addScannedItem([blank(1)], soap, context, blank);
    lines = addScannedItem(lines, oil, context, blank);

    expect(lines.map((l) => l.itemId)).toEqual([7, 8]);
    expect(lines[1].lineNumber).toBe(2);
    expect(lines[1].quantity).toBe(QTY);
    expect(lines[1].itemLabel).toBe('OIL01 - Coconut oil');
  });

  it('never overwrites a line somebody has started', () => {
    const started = { ...blank(1), description: 'Delivery charge', unitPrice: 5_000 };

    const lines = addScannedItem([started], soap, context, blank);

    expect(lines).toHaveLength(2);
    expect(lines[0].description).toBe('Delivery charge');
  });

  it('keeps the price already keyed on the line it adds to', () => {
    const priced = { ...blank(1), itemId: 7, itemLabel: 'SOAP01 - Neem soap', unitPrice: 4_500 };

    const [line] = addScannedItem([priced], soap, context, blank);

    expect(line.unitPrice).toBe(4_500);
    expect(line.quantity).toBe(2 * QTY);
    expect(line.grossAmount).toBe(9_000);
  });

  it('knows an untouched line', () => {
    expect(isEmptyLine(blank(1))).toBe(true);
    expect(isEmptyLine({ ...blank(1), itemId: 1 })).toBe(false);
    expect(isEmptyLine({ ...blank(1), description: 'x' })).toBe(false);
  });
});
