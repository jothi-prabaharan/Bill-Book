import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  ReceivableInvoiceDetailInvoice,
  ReceivableInvoiceDetailLine,
  ReceivableInvoiceDetailResult,
  appendDetailPage,
  categoryChoiceLabel,
  hasMoreDetail,
  isForeignInvoice,
  itemChoiceLabel,
  receivableDetailRows,
  receivableDetailSentence,
} from './receivable-invoice-detail.models';
import { ReceivableInvoiceDetailService } from './receivable-invoice-detail.service';

/** The Receivable Invoice Detail's calls and the helpers its page uses (TK-225). */

function setup(answer: unknown = { invoices: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [ReceivableInvoiceDetailService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(ReceivableInvoiceDetailService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const line = (over: Partial<ReceivableInvoiceDetailLine> = {}): ReceivableInvoiceDetailLine => ({
  lineNumber: 1,
  itemId: 601,
  itemCode: 'WIDGET-A',
  itemName: 'Widget A',
  description: null,
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

const invoice = (over: Partial<ReceivableInvoiceDetailInvoice> = {}): ReceivableInvoiceDetailInvoice => ({
  typeCode: 'INV',
  documentId: 1,
  documentNo: 'INV-0001',
  documentDate: '2026-10-02',
  dueDate: '2026-10-16',
  contactId: 101,
  contactCode: 'ZENITH',
  contactName: 'Zenith Traders',
  currencyCode: 'INR',
  status: 'Paid',
  totalInCurrency: 7080,
  total: 7080,
  roundOff: 0,
  lines: [line()],
  linesTaxable: 6000,
  linesTax: 1080,
  linesTotal: 7080,
  ...over,
});

const result = (invoices: ReceivableInvoiceDetailInvoice[], over: Partial<ReceivableInvoiceDetailResult> = {}): ReceivableInvoiceDetailResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  linesFiltered: false,
  invoices,
  totalInvoices: invoices.length,
  pageNumber: 1,
  pageSize: 25,
  statusCounts: { AwaitingPayment: 0, PartlyPaid: 0, Overdue: 0, Paid: invoices.length, Void: 0 },
  totals: {
    taxable: invoices.reduce((t, i) => t + i.linesTaxable, 0),
    tax: invoices.reduce((t, i) => t + i.linesTax, 0),
    total: invoices.reduce((t, i) => t + i.total, 0),
  },
  ...over,
});

describe('ReceivableInvoiceDetailService', () => {
  it('RD-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/receivable-invoice-detail');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'itemId', 'itemCategoryId', 'status', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('RD-02: sends the customer, item, category, status and page that were chosen', async () => {
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

  it('RD-03: leaves a cleared filter out of the request', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: null, itemId: null, itemCategoryId: null, status: null });

    const params = paramsOf(get.mock.calls[0]);
    for (const name of ['contactId', 'itemId', 'itemCategoryId', 'status']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('RD-04: gives back what the endpoint answered', async () => {
    const answer = result([invoice()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('RD-05: reads the customers, items and categories the filters offer from the report’s own lookups', async () => {
    const customers = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Exports' }];
    const first = setup(customers);
    expect(await first.service.customers()).toEqual(customers);
    expect(first.get.mock.calls[0][0]).toBe('/api/statements/receivable-invoice-detail/customers');

    TestBed.resetTestingModule();
    const items = [{ itemId: 601, itemCode: 'WIDGET-A', itemName: 'Widget A' }];
    const second = setup(items);
    expect(await second.service.items()).toEqual(items);
    expect(second.get.mock.calls[0][0]).toBe('/api/statements/receivable-invoice-detail/items');

    TestBed.resetTestingModule();
    const categories = [{ itemCategoryId: 701, categoryCode: 'HW', categoryName: 'Hardware' }];
    const third = setup(categories);
    expect(await third.service.itemCategories()).toEqual(categories);
    expect(third.get.mock.calls[0][0]).toBe('/api/statements/receivable-invoice-detail/item-categories');
  });
});

describe('receivable invoice detail helpers', () => {
  it('RD-06: an invoice is a heading, its lines and its subtotal, and then the totals of every match close the grid', () => {
    const rows = receivableDetailRows(
      result([invoice({ lines: [line(), line({ lineNumber: 2, itemName: 'Gadget B', itemCode: 'GADGET-B' })] })]),
    );

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'line', 'line', 'subtotal', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(5);
    expect(rows[0].name).toBe('INV-0001');
    expect(rows[4].name).toBe('Total');
  });

  it('RD-07: a heading carries the invoice’s number, dates, customer and status and links to its page', () => {
    const heading = receivableDetailRows(result([invoice({ documentId: 7, status: 'Overdue' })]))[0];

    expect(heading.link).toBe('/sales/invoices/7');
    expect([heading.documentDate, heading.dueDate, heading.contactName, heading.contactCode, heading.status]).toEqual([
      '2026-10-02',
      '2026-10-16',
      'Zenith Traders',
      'ZENITH',
      'Overdue',
    ]);
  });

  it('RD-08: a line carries its item, quantity, unit price, discount, tax rate and figures as sent', () => {
    const [, row] = receivableDetailRows(
      result([invoice({ lines: [line({ discountAmount: 1000, discountPercent: 10, description: 'Blue', quantity: 4 })] })]),
    );

    expect(row).toMatchObject({
      kind: 'line',
      itemCode: 'WIDGET-A',
      itemName: 'Widget A',
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

  it('RD-09: a line with no item has no code, and keeps the name the server gave it', () => {
    const [, row] = receivableDetailRows(result([invoice({ lines: [line({ itemId: null, itemCode: null, itemName: 'Installation', unitCode: null })] })]));

    expect([row.itemCode, row.itemName, row.unitCode]).toEqual([null, 'Installation', null]);
  });

  it('RD-10: a round off is a row of its own only when it is something, and the subtotal is the invoice’s own total', () => {
    const withRound = receivableDetailRows(result([invoice({ roundOff: 0.4, totalInCurrency: 7080.4, total: 7080.4 })]));
    expect(withRound.map((r) => r.kind)).toEqual(['heading', 'line', 'roundoff', 'subtotal', 'total']);
    expect(withRound[2].total).toBe(0.4);
    expect(withRound[3].total).toBe(7080.4);

    const none = receivableDetailRows(result([invoice({ roundOff: 0 })]));
    expect(none.some((r) => r.kind === 'roundoff')).toBe(false);
  });

  it('RD-11: an invoice with no round off shown, because lines are filtered, has the lines’ total as its subtotal', () => {
    const rows = receivableDetailRows(
      result([invoice({ roundOff: null, totalInCurrency: 11800, total: 11800, linesTaxable: 6000, linesTax: 1080, linesTotal: 7080 })], { linesFiltered: true }),
    );

    expect(rows.some((r) => r.kind === 'roundoff')).toBe(false);
    expect(rows[2]).toMatchObject({ kind: 'subtotal', taxable: 6000, tax: 1080, total: 7080, baseTotal: null });
  });

  it('RD-12: an invoice with no lines is still listed, with its round off holding the whole total', () => {
    const rows = receivableDetailRows(result([invoice({ lines: [], linesTaxable: 0, linesTax: 0, linesTotal: 0, roundOff: 7080 })]));

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'roundoff', 'subtotal', 'total']);
    expect(rows[1].total).toBe(7080);
  });

  it('RD-13: a foreign invoice’s rows name its currency, and its subtotal also gives the total in base', () => {
    const rows = receivableDetailRows(
      result([invoice({ currencyCode: 'USD', totalInCurrency: 590, total: 47200, linesTaxable: 500, linesTax: 90, linesTotal: 590, lines: [line({ taxable: 500, taxAmount: 90, lineTotal: 590 })] })]),
    );

    expect(rows.slice(0, 3).map((r) => r.currency)).toEqual(['USD', 'USD', 'USD']);
    expect(rows[2]).toMatchObject({ kind: 'subtotal', total: 590, baseTotal: 47200 });
    // The totals row is in base and names no currency.
    expect(rows[3].currency).toBeNull();
  });

  it('RD-14: an invoice in the branch’s currency names none and has no base total', () => {
    const rows = receivableDetailRows(result([invoice()]));

    expect(rows.every((r) => r.currency === null)).toBe(true);
    expect(rows[2].baseTotal).toBeNull();
  });

  it('RD-15: the totals row is the server’s figures, and says it is of all when not every invoice is loaded', () => {
    const rows = receivableDetailRows(result([invoice()], { totalInvoices: 120, totals: { taxable: 90, tax: 9, total: 99 } }));

    const total = rows[rows.length - 1];
    expect(total).toMatchObject({ kind: 'total', name: 'Total of all 120 invoices', taxable: 90, tax: 9, total: 99 });
  });

  it('RD-16: with no invoices there are no rows, not even the totals', () => {
    expect(receivableDetailRows(result([]))).toEqual([]);
  });

  it('RD-17: the next page is added to the ones loaded and the counts and totals are the latest answer’s', () => {
    const first = result([invoice({ documentId: 1 })], { totalInvoices: 3, pageNumber: 1 });
    const next = result([invoice({ documentId: 2 })], { totalInvoices: 3, pageNumber: 2, totals: { taxable: 9, tax: 9, total: 9 } });

    const merged = appendDetailPage(first, next);

    expect(merged.invoices.map((i) => i.documentId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.total).toBe(9);
  });

  it('RD-18: there is more while fewer invoices are loaded than match', () => {
    expect(hasMoreDetail({ invoices: [invoice()], totalInvoices: 2 })).toBe(true);
    expect(hasMoreDetail({ invoices: [invoice(), invoice({ documentId: 2 })], totalInvoices: 2 })).toBe(false);
  });

  it('RD-19: an invoice is foreign when its currency is not the branch’s, whatever the case', () => {
    expect(isForeignInvoice({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignInvoice({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignInvoice({ currencyCode: 'USD' }, '')).toBe(false);
    expect(isForeignInvoice({ currencyCode: '' }, 'INR')).toBe(false);
  });

  it('RD-20: an item reads by name then code, and a category the same', () => {
    expect(itemChoiceLabel({ itemId: 1, itemCode: 'WIDGET-A', itemName: 'Widget A' })).toBe('Widget A (WIDGET-A)');
    expect(itemChoiceLabel({ itemId: 1, itemCode: '', itemName: 'Widget A' })).toBe('Widget A');
    expect(categoryChoiceLabel({ itemCategoryId: 1, categoryCode: 'HW', categoryName: 'Hardware' })).toBe('Hardware (HW)');
    expect(categoryChoiceLabel({ itemCategoryId: 1, categoryCode: '', categoryName: 'Hardware' })).toBe('Hardware');
  });

  it('RD-21: the sentence says how many invoices and what they come to, and says when the lines are narrowed', () => {
    const answer = result([invoice(), invoice({ documentId: 2 })], { totals: { taxable: 100, tax: 18, total: 118 } });

    expect(receivableDetailSentence(answer, money)).toBe('2 invoices for ₹118.00: ₹100.00 taxable and ₹18.00 tax.');
    expect(receivableDetailSentence(result([invoice()]), money)).toMatch(/^1 invoice for /);
    expect(receivableDetailSentence({ ...answer, linesFiltered: true }, money)).toBe(
      '2 invoices with a line for the chosen item or category; those lines come to ₹100.00 taxable and ₹18.00 tax, ₹118.00 in all.',
    );
  });
});
