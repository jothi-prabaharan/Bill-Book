import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  PayableInvoiceDetailBill,
  PayableInvoiceDetailLine,
  PayableInvoiceDetailResult,
  appendBillDetailPage,
  hasMoreBillDetail,
  isForeignBill,
  payableDetailRows,
  payableDetailSentence,
} from './payable-invoice-detail.models';
import { PayableInvoiceDetailService } from './payable-invoice-detail.service';

/** The Payable Invoice Detail's calls and the helpers its page uses (TK-236). */

function setup(answer: unknown = { bills: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [PayableInvoiceDetailService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(PayableInvoiceDetailService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const line = (over: Partial<PayableInvoiceDetailLine> = {}): PayableInvoiceDetailLine => ({
  lineNumber: 1,
  itemId: 601,
  itemCode: 'STEEL-ROD',
  itemName: 'Steel Rod',
  description: null,
  isCapital: false,
  assetCategoryName: null,
  quantity: 10,
  unitCode: 'PCS',
  unitPrice: 600,
  discountAmount: 0,
  discountPercent: null,
  taxable: 6000,
  taxRate: 18,
  taxAmount: 1080,
  lineTotal: 7080,
  ...over,
});

const bill = (over: Partial<PayableInvoiceDetailBill> = {}): PayableInvoiceDetailBill => ({
  typeCode: 'BIL',
  documentId: 1,
  documentNo: 'BIL-0001',
  vendorBillNo: 'V/0001',
  documentDate: '2026-10-02',
  dueDate: '2026-10-16',
  contactId: 101,
  contactCode: 'STEELCO',
  contactName: 'Steelco Metals',
  currencyCode: 'INR',
  status: 'Paid',
  reverseCharge: false,
  totalInCurrency: 7080,
  total: 7080,
  roundOff: 0,
  lines: [line()],
  linesTaxable: 6000,
  linesTax: 1080,
  linesTotal: 7080,
  ...over,
});

const result = (bills: PayableInvoiceDetailBill[], over: Partial<PayableInvoiceDetailResult> = {}): PayableInvoiceDetailResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  linesFiltered: false,
  bills,
  totalBills: bills.length,
  pageNumber: 1,
  pageSize: 25,
  statusCounts: { AwaitingPayment: 0, PartlyPaid: 0, Overdue: 0, Paid: bills.length, Void: 0 },
  totals: {
    taxable: bills.reduce((t, i) => t + i.linesTaxable, 0),
    tax: bills.reduce((t, i) => t + i.linesTax, 0),
    total: bills.reduce((t, i) => t + i.total, 0),
  },
  ...over,
});

describe('PayableInvoiceDetailService', () => {
  it('PD-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/payable-invoice-detail');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'itemId', 'itemCategoryId', 'status', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('PD-02: sends the vendor, item, category, status and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-10-01',
      toDate: '2026-10-31',
      contactId: 102,
      itemId: 601,
      itemCategoryId: 701,
      status: 'Overdue',
      pageNumber: 2,
      pageSize: 25,
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('102');
    expect(params.get('itemId')).toBe('601');
    expect(params.get('itemCategoryId')).toBe('701');
    expect(params.get('status')).toBe('Overdue');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('PD-03: leaves a cleared filter out of the request', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: null, itemId: null, itemCategoryId: null, status: null });

    const params = paramsOf(get.mock.calls[0]);
    for (const name of ['contactId', 'itemId', 'itemCategoryId', 'status']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('PD-04: gives back what the endpoint answered', async () => {
    const answer = result([bill()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('PD-05: reads the vendors, items and categories the filters offer from the report’s own lookups', async () => {
    const vendors = [{ contactId: 101, contactCode: 'STEELCO', contactName: 'Steelco Metals' }];
    const first = setup(vendors);
    expect(await first.service.vendors()).toEqual(vendors);
    expect(first.get.mock.calls[0][0]).toBe('/api/statements/payable-invoice-detail/vendors');

    TestBed.resetTestingModule();
    const items = [{ itemId: 601, itemCode: 'STEEL-ROD', itemName: 'Steel Rod' }];
    const second = setup(items);
    expect(await second.service.items()).toEqual(items);
    expect(second.get.mock.calls[0][0]).toBe('/api/statements/payable-invoice-detail/items');

    TestBed.resetTestingModule();
    const categories = [{ itemCategoryId: 701, categoryCode: 'HW', categoryName: 'Hardware' }];
    const third = setup(categories);
    expect(await third.service.itemCategories()).toEqual(categories);
    expect(third.get.mock.calls[0][0]).toBe('/api/statements/payable-invoice-detail/item-categories');
  });
});

describe('payable invoice detail helpers', () => {
  it('PD-06: a bill is a heading, its lines and its subtotal, and then the totals of every match close the grid', () => {
    const rows = payableDetailRows(
      result([bill({ lines: [line(), line({ lineNumber: 2, itemName: 'Bolt Box', itemCode: 'BOLT-BOX' })] })]),
    );

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'line', 'line', 'subtotal', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(5);
    expect(rows[0].name).toBe('BIL-0001');
    expect(rows[4].name).toBe('Total');
  });

  it('PD-07: a heading carries the bill’s number, dates, vendor and status and links to its page', () => {
    const heading = payableDetailRows(result([bill({ documentId: 7, status: 'Overdue', reverseCharge: true })]))[0];

    expect([heading.vendorBillNo, heading.reverseCharge]).toEqual(['V/0001', true]);

    expect(heading.link).toBe('/purchase/bills/7');
    expect([heading.documentDate, heading.dueDate, heading.contactName, heading.contactCode, heading.status]).toEqual([
      '2026-10-02',
      '2026-10-16',
      'Steelco Metals',
      'STEELCO',
      'Overdue',
    ]);
  });

  it('PD-08: a line carries its item, quantity, unit price, discount, tax rate and figures as sent', () => {
    const [, row] = payableDetailRows(
      result([bill({ lines: [line({ discountAmount: 1000, discountPercent: 10, description: 'Blue', quantity: 4 })] })]),
    );

    expect(row).toMatchObject({
      kind: 'line',
      itemCode: 'STEEL-ROD',
      itemName: 'Steel Rod',
      description: 'Blue',
      quantity: 4,
      unitCode: 'PCS',
      unitPrice: 600,
      discount: 1000,
      discountPercent: 10,
      taxable: 6000,
      taxRate: 18,
      tax: 1080,
      total: 7080,
    });
  });

  it('PD-09: a line with no item has no code, and keeps the name the server gave it', () => {
    const [, row] = payableDetailRows(result([bill({ lines: [line({ itemId: null, itemCode: null, itemName: 'Freight', unitCode: null })] })]));

    expect([row.itemCode, row.itemName, row.unitCode]).toEqual([null, 'Installation', null]);
  });

  it('PD-10: a round off is a row of its own only when it is something, and the subtotal is the bill’s own total', () => {
    const withRound = payableDetailRows(result([bill({ roundOff: 0.4, totalInCurrency: 7080.4, total: 7080.4 })]));
    expect(withRound.map((r) => r.kind)).toEqual(['heading', 'line', 'roundoff', 'subtotal', 'total']);
    expect(withRound[2].total).toBe(0.4);
    expect(withRound[3].total).toBe(7080.4);

    const none = payableDetailRows(result([bill({ roundOff: 0 })]));
    expect(none.some((r) => r.kind === 'roundoff')).toBe(false);
  });

  it('PD-11: a bill with no round off shown, because lines are filtered, has the lines’ total as its subtotal', () => {
    const rows = payableDetailRows(
      result([bill({ roundOff: null, totalInCurrency: 11800, total: 11800, linesTaxable: 6000, linesTax: 1080, linesTotal: 7080 })], { linesFiltered: true }),
    );

    expect(rows.some((r) => r.kind === 'roundoff')).toBe(false);
    expect(rows[2]).toMatchObject({ kind: 'subtotal', taxable: 6000, tax: 1080, total: 7080, baseTotal: null });
  });

  it('PD-12: a bill with no lines is still listed, with its round off holding the whole total', () => {
    const rows = payableDetailRows(result([bill({ lines: [], linesTaxable: 0, linesTax: 0, linesTotal: 0, roundOff: 7080 })]));

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'roundoff', 'subtotal', 'total']);
    expect(rows[1].total).toBe(7080);
  });

  it('PD-13: a foreign bill’s rows name its currency, and its subtotal also gives the total in base', () => {
    const rows = payableDetailRows(
      result([bill({ currencyCode: 'USD', totalInCurrency: 590, total: 47200, linesTaxable: 500, linesTax: 90, linesTotal: 590, lines: [line({ taxable: 500, taxAmount: 90, lineTotal: 590 })] })]),
    );

    expect(rows.slice(0, 3).map((r) => r.currency)).toEqual(['USD', 'USD', 'USD']);
    expect(rows[2]).toMatchObject({ kind: 'subtotal', total: 590, baseTotal: 47200 });
    // The totals row is in base and names no currency.
    expect(rows[3].currency).toBeNull();
  });

  it('PD-14: a bill in the branch’s currency names none and has no base total', () => {
    const rows = payableDetailRows(result([bill()]));

    expect(rows.every((r) => r.currency === null)).toBe(true);
    expect(rows[2].baseTotal).toBeNull();
  });

  it('PD-15: the totals row is the server’s figures, and says it is of all when not every bill is loaded', () => {
    const rows = payableDetailRows(result([bill()], { totalBills: 120, totals: { taxable: 90, tax: 9, total: 99 } }));

    const total = rows[rows.length - 1];
    expect(total).toMatchObject({ kind: 'total', name: 'Total of all 120 bills', taxable: 90, tax: 9, total: 99 });
  });

  it('PD-16: with no bills there are no rows, not even the totals', () => {
    expect(payableDetailRows(result([]))).toEqual([]);
  });

  it('PD-17: the next page is added to the ones loaded and the counts and totals are the latest answer’s', () => {
    const first = result([bill({ documentId: 1 })], { totalBills: 3, pageNumber: 1 });
    const next = result([bill({ documentId: 2 })], { totalBills: 3, pageNumber: 2, totals: { taxable: 9, tax: 9, total: 9 } });

    const merged = appendBillDetailPage(first, next);

    expect(merged.bills.map((i) => i.documentId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.total).toBe(9);
  });

  it('PD-18: there is more while fewer bills are loaded than match', () => {
    expect(hasMoreBillDetail({ bills: [bill()], totalBills: 2 })).toBe(true);
    expect(hasMoreBillDetail({ bills: [bill(), bill({ documentId: 2 })], totalBills: 2 })).toBe(false);
  });

  it('PD-19: a bill is foreign when its currency is not the branch’s, whatever the case', () => {
    expect(isForeignBill({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignBill({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignBill({ currencyCode: 'USD' }, '')).toBe(false);
    expect(isForeignBill({ currencyCode: '' }, 'INR')).toBe(false);
  });

  it('PD-20: a capital line is marked with its asset category, and an ordinary line is not', () => {
    const rows = payableDetailRows(
      result([
        bill({
          lines: [
            line(),
            line({ lineNumber: 2, itemId: null, itemCode: null, itemName: 'CNC lathe', unitCode: null, isCapital: true, assetCategoryName: 'Plant and machinery' }),
          ],
        }),
      ]),
    );

    expect(rows[1]).toMatchObject({ kind: 'line', isCapital: false, assetCategory: null });
    expect(rows[2]).toMatchObject({ kind: 'line', itemName: 'CNC lathe', isCapital: true, assetCategory: 'Plant and machinery' });
    // Only a line is marked; a heading never is.
    expect(rows[0].isCapital).toBe(false);
  });

  it('PD-21: the sentence says how many bills and what they come to, and says when the lines are narrowed', () => {
    const answer = result([bill(), bill({ documentId: 2 })], { totals: { taxable: 100, tax: 18, total: 118 } });

    expect(payableDetailSentence(answer, money)).toBe('2 bills for ₹118.00: ₹100.00 taxable and ₹18.00 tax.');
    expect(payableDetailSentence(result([bill()]), money)).toMatch(/^1 bill for /);
    expect(payableDetailSentence({ ...answer, linesFiltered: true }, money)).toBe(
      '2 bills with a line for the chosen item or category; those lines come to ₹100.00 taxable and ₹18.00 tax, ₹118.00 in all.',
    );
  });
});
