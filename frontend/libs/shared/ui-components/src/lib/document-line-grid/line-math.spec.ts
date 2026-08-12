import { describe, expect, it } from 'vitest';
import { DocumentLine, DocumentLineContext } from './document-line.model';
import {
  componentsFor,
  recalculate,
  roundHalfAwayFromZero,
  totalsOf,
} from './line-math';

const QTY = 1_000_000;

function context(over: Partial<DocumentLineContext> = {}): DocumentLineContext {
  return {
    isInterState: false,
    allowFreeTextLines: true,
    discountBeforeTax: true,
    discountLevel: 'Line',
    readonly: false,
    currencyDecimals: 2,
    ...over,
  };
}

function line(over: Partial<DocumentLine> = {}): DocumentLine {
  return {
    detailId: 0,
    lineNumber: 1,
    itemId: 1,
    itemLabel: 'Item',
    hsnSacCode: '1234',
    description: null,
    warehouseId: null,
    quantity: 2 * QTY,
    uomId: 1,
    uomLabel: 'NOS',
    conversionFactor: QTY,
    baseQuantity: 2 * QTY,
    unitPrice: 10_000,
    isPriceInclusive: false,
    discountPercent: null,
    discountAmount: 0,
    grossAmount: 0,
    taxableAmount: 0,
    taxTreatment: 'Taxable',
    taxMasterId: 1,
    taxGroupId: 1,
    taxAmount: 0,
    taxes: [
      { component: 'Cgst', subAccountId: 1, rate: 90_000, taxableAmount: 0, amount: 0 },
      { component: 'Sgst', subAccountId: 2, rate: 90_000, taxableAmount: 0, amount: 0 },
    ],
    lineType: 'Stock',
    accountId: null,
    fixedAssetCategoryId: null,
    lineTotal: 0,
    itemBatchId: null,
    lineNotes: null,
    ...over,
  };
}

describe('rounding', () => {
  it('rounds a half away from zero, not up, so a signed round-off is symmetric', () => {
    expect(roundHalfAwayFromZero(2.5)).toBe(3);
    expect(roundHalfAwayFromZero(-2.5)).toBe(-3);
  });
});

describe('an exclusive line', () => {
  it('taxes the full value and splits it across the two intra-state components', () => {
    const result = recalculate(line(), context());

    expect(result.grossAmount).toBe(20_000);
    expect(result.taxableAmount).toBe(20_000);
    expect(result.taxes.map((t) => t.amount)).toEqual([1_800, 1_800]);
    expect(result.lineTotal).toBe(23_600);
  });

  it('takes the discount off the taxable value, so the tax falls with it', () => {
    const result = recalculate(line({ discountPercent: 10 }), context());

    expect(result.discountAmount).toBe(2_000);
    expect(result.taxableAmount).toBe(18_000);
    expect(result.taxAmount).toBe(3_240);
  });

  it('leaves the taxable value alone when the discount comes after tax', () => {
    const result = recalculate(
      line({ discountPercent: 10 }),
      context({ discountBeforeTax: false }),
    );

    expect(result.taxableAmount).toBe(20_000);
    expect(result.taxAmount).toBe(3_600);
  });
});

describe('an inclusive line', () => {
  it('backs the tax out of the price rather than adding it on top', () => {
    const result = recalculate(line({ isPriceInclusive: true }), context());

    // 200.00 inclusive of 18% is 169.49 taxable.
    expect(result.taxableAmount).toBe(16_949);
    expect(result.taxAmount).toBe(3_050);
  });
});

describe('tax treatment', () => {
  it('writes no tax rows for an exempt line, so nothing is charged', () => {
    const result = recalculate(line({ taxTreatment: 'Exempt' }), context());

    expect(result.taxes).toEqual([]);
    expect(result.taxAmount).toBe(0);
    expect(result.lineTotal).toBe(20_000);
  });

  it('keeps the rows for a zero-rated line, so an export stays a reported supply', () => {
    const zero = line({
      taxTreatment: 'ZeroRated',
      taxes: [
        { component: 'Igst', subAccountId: 3, rate: 0, taxableAmount: 0, amount: 0 },
      ],
    });
    const result = recalculate(zero, context({ isInterState: true }));

    expect(result.taxes).toHaveLength(1);
    expect(result.taxes[0].component).toBe('Igst');
    expect(result.taxAmount).toBe(0);
  });
});

describe('place of supply', () => {
  it('is CGST and SGST within a state and IGST across one', () => {
    expect(componentsFor(false)).toEqual(['Cgst', 'Sgst']);
    expect(componentsFor(true)).toEqual(['Igst']);
  });
});

describe('document totals', () => {
  it('sums lines that are already rounded, so the header ties to the lines', () => {
    const a = recalculate(line(), context());
    const b = recalculate(line({ lineNumber: 2, quantity: QTY }), context());

    const totals = totalsOf([a, b]);

    expect(totals.taxableAmount).toBe(30_000);
    expect(totals.cgstAmount).toBe(2_700);
    expect(totals.sgstAmount).toBe(2_700);
    expect(totals.igstAmount).toBe(0);
    expect(totals.totalAmount).toBe(35_400);
    expect(totals.totalAmount).toBe(a.lineTotal + b.lineTotal);
  });
});
