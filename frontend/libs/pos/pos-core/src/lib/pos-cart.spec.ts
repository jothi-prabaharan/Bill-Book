import { TaxGroupOption } from '@bill-book/ui-components';
import { describe, expect, it } from 'vitest';
import {
  CartItem,
  QTY_SCALE,
  addItem,
  cartContext,
  cartTotals,
  isInterStateSale,
  removeLine,
  reprice,
  setQuantity,
  setUnitPrice,
} from './pos-cart';

/**
 * The cart is the till's only arithmetic, and all of it is `line-math.ts`
 * underneath. What these pin is what the till adds on top: one line per item,
 * zero quantity removing a line, and the state line moving the tax split when
 * the customer changes.
 */
const gst18: TaxGroupOption = {
  taxGroupId: 18,
  taxMasterId: 180,
  label: 'GST 18%',
  cgstRate: 9,
  sgstRate: 9,
  igstRate: 18,
  cessRate: 0,
};

const soap: CartItem = {
  itemId: 1,
  itemCode: 'ITM-0001',
  itemName: 'Soap',
  unitPrice: 100,
  isPriceInclusive: false,
  taxTreatment: 'Taxable',
  taxGroupId: 18,
};

const rice: CartItem = {
  itemId: 2,
  itemCode: 'ITM-0002',
  itemName: 'Rice',
  unitPrice: 50,
  isPriceInclusive: false,
  taxTreatment: 'Exempt',
  taxGroupId: null,
};

const intra = cartContext(false, true);
const inter = cartContext(true, true);

describe('isInterStateSale', () => {
  it('is intra-state when both GSTINs name the same state', () => {
    expect(isInterStateSale('33AAAAA0000A1Z5', '33BBBBB0000B1Z5')).toBe(false);
  });

  it('is inter-state when the GSTINs name different states', () => {
    expect(isInterStateSale('33AAAAA0000A1Z5', '29BBBBB0000B1Z5')).toBe(true);
  });

  it('treats a customer with no GSTIN as a counter sale, intra-state', () => {
    expect(isInterStateSale('33AAAAA0000A1Z5', null)).toBe(false);
    expect(isInterStateSale('33AAAAA0000A1Z5', '')).toBe(false);
  });

  it('treats a branch with no GSTIN as intra-state', () => {
    expect(isInterStateSale(null, '29BBBBB0000B1Z5')).toBe(false);
  });
});

describe('addItem', () => {
  it('adds one unit at the item price, with CGST and SGST inside the state', () => {
    const lines = addItem([], soap, gst18, intra);

    expect(lines).toHaveLength(1);
    const [line] = lines;
    expect(line.itemId).toBe(1);
    expect(line.lineNumber).toBe(1);
    expect(line.quantity).toBe(QTY_SCALE);
    expect(line.unitPrice).toBe(10_000);
    expect(line.taxableAmount).toBe(10_000);
    expect(line.taxes.map((tax) => tax.component)).toEqual(['Cgst', 'Sgst']);
    expect(line.taxAmount).toBe(1_800);
    expect(line.lineTotal).toBe(11_800);
    expect(line.lineType).toBe('Stock');
  });

  it('puts the whole rate on IGST across a state line', () => {
    const [line] = addItem([], soap, gst18, inter);

    expect(line.taxes.map((tax) => tax.component)).toEqual(['Igst']);
    expect(line.taxAmount).toBe(1_800);
  });

  it('adds to the existing line when the same item is added again', () => {
    let lines = addItem([], soap, gst18, intra);
    lines = addItem(lines, soap, gst18, intra);

    expect(lines).toHaveLength(1);
    expect(lines[0].quantity).toBe(2 * QTY_SCALE);
    expect(lines[0].lineTotal).toBe(23_600);
  });

  it('gives a different item its own numbered line', () => {
    let lines = addItem([], soap, gst18, intra);
    lines = addItem(lines, rice, undefined, intra);

    expect(lines.map((line) => line.lineNumber)).toEqual([1, 2]);
  });

  it('charges no tax on an exempt item', () => {
    const [line] = addItem([], rice, undefined, intra);

    expect(line.taxes).toEqual([]);
    expect(line.lineTotal).toBe(5_000);
  });

  it('backs the tax out of an inclusive price', () => {
    const [line] = addItem([], { ...soap, unitPrice: 118, isPriceInclusive: true }, gst18, intra);

    expect(line.taxableAmount).toBe(10_000);
    expect(line.taxAmount).toBe(1_800);
    expect(line.lineTotal).toBe(11_800);
  });
});

describe('setQuantity', () => {
  it('recalculates the line at the new quantity', () => {
    const lines = setQuantity(addItem([], soap, gst18, intra), 0, 3, intra);

    expect(lines[0].quantity).toBe(3 * QTY_SCALE);
    expect(lines[0].lineTotal).toBe(35_400);
  });

  it('removes the line at zero, and renumbers what is left', () => {
    let lines = addItem([], soap, gst18, intra);
    lines = addItem(lines, rice, undefined, intra);
    lines = setQuantity(lines, 0, 0, intra);

    expect(lines).toHaveLength(1);
    expect(lines[0].itemId).toBe(2);
    expect(lines[0].lineNumber).toBe(1);
  });

  it('removes the line on a negative or unreadable quantity', () => {
    const lines = addItem([], soap, gst18, intra);

    expect(setQuantity(lines, 0, -1, intra)).toEqual([]);
    expect(setQuantity(lines, 0, Number('abc'), intra)).toEqual([]);
  });
});

describe('setUnitPrice', () => {
  it('recalculates the line at the new price', () => {
    const lines = setUnitPrice(addItem([], soap, gst18, intra), 0, 200, intra);

    expect(lines[0].unitPrice).toBe(20_000);
    expect(lines[0].lineTotal).toBe(23_600);
  });

  it('refuses a negative price and leaves the line as it was', () => {
    const lines = addItem([], soap, gst18, intra);

    expect(setUnitPrice(lines, 0, -5, intra)[0].unitPrice).toBe(10_000);
  });
});

describe('removeLine', () => {
  it('drops the line and keeps the numbering contiguous', () => {
    let lines = addItem([], soap, gst18, intra);
    lines = addItem(lines, rice, undefined, intra);

    const left = removeLine(lines, 0);

    expect(left.map((line) => [line.itemId, line.lineNumber])).toEqual([[2, 1]]);
  });
});

describe('reprice', () => {
  it('turns CGST + SGST into IGST when the customer moves across a state line', () => {
    const lines = addItem([], soap, gst18, intra);

    const repriced = reprice(lines, [gst18], inter);

    expect(repriced[0].taxes.map((tax) => tax.component)).toEqual(['Igst']);
    expect(repriced[0].taxAmount).toBe(1_800);
  });

  it('leaves a line whose group has no current rate untaxed', () => {
    const lines = addItem([], soap, undefined, intra);

    expect(reprice(lines, [gst18], inter)[0].taxes).toEqual([]);
  });
});

describe('cartTotals', () => {
  it('sums the lines into the split the return reports', () => {
    let lines = addItem([], soap, gst18, intra);
    lines = addItem(lines, rice, undefined, intra);

    const totals = cartTotals(lines);

    expect(totals.taxableAmount).toBe(15_000);
    expect(totals.cgstAmount).toBe(900);
    expect(totals.sgstAmount).toBe(900);
    expect(totals.igstAmount).toBe(0);
    expect(totals.totalAmount).toBe(16_800);
  });
});
