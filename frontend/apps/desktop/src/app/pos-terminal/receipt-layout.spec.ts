import { describe, expect, it } from 'vitest';
import { ReceiptBranch, ReceiptSale, gstSummary, printable, receiptRows, twoColumns, wrap } from './receipt-layout';

/** The till receipt's layout (TK-41): fixed-width rows from the posted invoice. */
describe('receiptRows', () => {
  const branch: ReceiptBranch = {
    name: 'Anna Stores',
    addressLine1: '12 Car Street',
    city: 'Madurai',
    postalCode: '625001',
    mobileNumber: '9840012345',
    gstin: '33ABCDE1234F1Z5',
  };

  const sale: ReceiptSale = {
    documentNo: 'POS-0001',
    documentDate: '2026-09-24',
    contactName: 'Walk-in customer',
    status: 'Posted',
    subTotal: 150,
    discountAmount: 0,
    taxableAmount: 150,
    cgstAmount: 7.5,
    sgstAmount: 7.5,
    igstAmount: 0,
    cessAmount: 0,
    roundOffAmount: 0,
    totalAmount: 165,
    changeAmount: 35,
    lines: [
      {
        itemLabel: 'SOAP - Sandal soap 100 g',
        quantity: 2,
        unitPrice: 50,
        lineTotal: 105,
        taxes: [
          { taxComponent: 'Cgst', rate: 2.5, taxableAmount: 100, amount: 2.5 },
          { taxComponent: 'Sgst', rate: 2.5, taxableAmount: 100, amount: 2.5 },
        ],
      },
      {
        itemLabel: 'OIL - Gingelly oil 500 ml',
        quantity: 1,
        unitPrice: 50,
        lineTotal: 60,
        taxes: [
          { taxComponent: 'Cgst', rate: 10, taxableAmount: 50, amount: 5 },
          { taxComponent: 'Sgst', rate: 10, taxableAmount: 50, amount: 5 },
        ],
      },
    ],
    tenders: [{ mode: 'Cash', amount: 200 }],
  };

  it.each([
    [58, 32],
    [80, 48],
  ] as const)('keeps every left-aligned row within %i mm paper (%i columns)', (paper, columns) => {
    const rows = receiptRows(branch, sale, { paper });

    for (const row of rows) {
      expect(row.text.length).toBeLessThanOrEqual(columns);
    }
    expect(rows.filter((r) => r.align === 'left' && /^-+$/.test(r.text))[0].text).toHaveLength(columns);
  });

  it('prints the branch header, the bill number, the lines and the total', () => {
    const text = receiptRows(branch, sale, { paper: 80 }).map((r) => r.text);

    expect(text[0]).toBe('Anna Stores');
    expect(text).toContain('Madurai 625001');
    expect(text).toContain('GSTIN: 33ABCDE1234F1Z5');
    expect(text.some((t) => t.startsWith('Bill: POS-0001') && t.endsWith('2026-09-24'))).toBe(true);
    expect(text).toContain('Sandal soap 100 g');
    expect(text.some((t) => t.startsWith('  2 x 50.00') && t.endsWith('105.00'))).toBe(true);
    expect(text.some((t) => t.startsWith('TOTAL') && t.endsWith('165.00'))).toBe(true);
  });

  it('splits GST per component and rate', () => {
    const text = receiptRows(branch, sale, { paper: 80 }).map((r) => r.text);

    expect(text.some((t) => t.startsWith('CGST @2.5%') && t.endsWith('2.50'))).toBe(true);
    expect(text.some((t) => t.startsWith('SGST @10%') && t.endsWith('5.00'))).toBe(true);
  });

  it('prints each tender and the change', () => {
    const text = receiptRows(branch, sale, { paper: 58 }).map((r) => r.text);

    expect(text.some((t) => t.startsWith('Cash') && t.endsWith('200.00'))).toBe(true);
    expect(text.some((t) => t.startsWith('Change') && t.endsWith('35.00'))).toBe(true);
  });

  it('leaves out the walk-in customer but names a real one', () => {
    const walkIn = receiptRows(branch, sale, { paper: 80 }).map((r) => r.text);
    const named = receiptRows(branch, { ...sale, contactName: 'Ravi Traders', contactGstin: '33AAACR1234A1Z1' }, { paper: 80 })
      .map((r) => r.text);

    expect(walkIn.some((t) => t.startsWith('To:'))).toBe(false);
    expect(named).toContain('To: Ravi Traders');
    expect(named).toContain('GSTIN: 33AAACR1234A1Z1');
  });

  it('marks a reprint DUPLICATE and a voided sale VOID', () => {
    const reprint = receiptRows(branch, sale, { paper: 80, reprint: true });
    const voided = receiptRows(branch, { ...sale, status: 'Voided' }, { paper: 80 });

    expect(reprint[0].text).toBe('*** DUPLICATE ***');
    expect(voided.map((r) => r.text)).toContain('VOID - TAX INVOICE');
  });

  it('prints the round-off only when there is one', () => {
    const none = receiptRows(branch, sale, { paper: 80 }).map((r) => r.text);
    const some = receiptRows(branch, { ...sale, roundOffAmount: -0.4 }, { paper: 80 }).map((r) => r.text);

    expect(none.some((t) => t.startsWith('Round off'))).toBe(false);
    expect(some.some((t) => t.startsWith('Round off') && t.endsWith('-0.40'))).toBe(true);
  });
});

describe('gstSummary', () => {
  it('orders CGST, SGST, IGST, then by rate, and drops zero rows', () => {
    const rows = gstSummary([
      {
        quantity: 1,
        unitPrice: 1,
        lineTotal: 1,
        taxes: [
          { taxComponent: 'Igst', rate: 18, taxableAmount: 1, amount: 0 },
          { taxComponent: 'Sgst', rate: 9, taxableAmount: 1, amount: 1 },
          { taxComponent: 'Cgst', rate: 9, taxableAmount: 1, amount: 1 },
          { taxComponent: 'Cgst', rate: 2.5, taxableAmount: 1, amount: 0.3 },
        ],
      },
    ]);

    expect(rows.map((r) => `${r.component}@${r.rate}`)).toEqual(['CGST@2.5', 'CGST@9', 'SGST@9']);
  });
});

describe('text helpers', () => {
  it('replaces what font A cannot print', () => {
    expect(printable('₹10 சோப்')).toBe('Rs10 ????');
  });

  it('right-aligns the amount and clips the label', () => {
    expect(twoColumns('A very long item name indeed', '100.00', 20)).toBe('A very long i 100.00');
    expect(twoColumns('Tea', '5.00', 12)).toBe('Tea     5.00');
  });

  it('wraps at spaces and hard-cuts a word longer than the row', () => {
    expect(wrap('Sandal soap family pack', 12)).toEqual(['Sandal soap', 'family pack']);
    expect(wrap('ABCDEFGHIJKLMNOP', 6)).toEqual(['ABCDEF', 'GHIJKL', 'MNOP']);
  });
});
