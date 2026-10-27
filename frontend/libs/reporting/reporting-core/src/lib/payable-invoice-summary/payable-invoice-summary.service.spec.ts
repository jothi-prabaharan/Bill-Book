import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  PAYABLE_INVOICE_STATUSES,
  PayableInvoiceStatus,
  PayableInvoiceSummaryResult,
  PayableInvoiceSummaryRow,
  appendPayablePage,
  hasMorePayable,
  isDebitNote,
  isForeignPayable,
  payableBillLink,
  payableInvoiceRows,
  payableInvoiceSentence,
  payableStatusLabel,
  payableStatusTabs,
  payableStatusTag,
} from './payable-invoice-summary.models';
import { PayableInvoiceSummaryService } from './payable-invoice-summary.service';

/** The Payable Invoice Summary's calls and the helpers its page uses (TK-235). */

function setup(answer: unknown = { invoices: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [PayableInvoiceSummaryService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(PayableInvoiceSummaryService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const row = (over: Partial<PayableInvoiceSummaryRow> = {}): PayableInvoiceSummaryRow => ({
  typeCode: 'BIL',
  documentId: 1,
  documentNo: 'BIL-0001',
  vendorBillNo: 'V-1',
  documentDate: '2026-10-02',
  dueDate: '2026-10-17',
  daysOverdue: null,
  contactId: 201,
  contactCode: 'STEELCO',
  contactName: 'Steelco Supplies',
  currencyCode: 'INR',
  totalInCurrency: 11800,
  taxable: 10000,
  cgst: 0,
  sgst: 0,
  igst: 1800,
  cess: 0,
  tax: 1800,
  reverseCharge: false,
  tds: 0,
  total: 11800,
  payable: 11800,
  paid: 11800,
  due: 0,
  status: 'Paid',
  ...over,
});

const result = (invoices: PayableInvoiceSummaryRow[], over: Partial<PayableInvoiceSummaryResult> = {}): PayableInvoiceSummaryResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  invoices,
  totalInvoices: invoices.length,
  pageNumber: 1,
  pageSize: 50,
  statusCounts: { AwaitingPayment: 0, PartlyPaid: 0, Overdue: 0, Paid: invoices.length, Void: 0 },
  totals: {
    taxable: invoices.reduce((t, r) => t + r.taxable, 0),
    tax: invoices.reduce((t, r) => t + r.tax, 0),
    tds: invoices.reduce((t, r) => t + r.tds, 0),
    total: invoices.reduce((t, r) => t + r.total, 0),
    payable: invoices.reduce((t, r) => t + r.payable, 0),
    paid: invoices.reduce((t, r) => t + r.paid, 0),
    due: invoices.reduce((t, r) => t + r.due, 0),
  },
  ...over,
});

describe('PayableInvoiceSummaryService', () => {
  it('PI-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/payable-invoice-summary');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'currencyCode', 'status', 'includeDebitNotes', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('PI-02: sends the vendor, currency, status, debit notes and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-10-01',
      toDate: '2026-10-31',
      contactId: 202,
      currencyCode: 'USD',
      status: 'Overdue',
      includeDebitNotes: true,
      pageNumber: 2,
      pageSize: 25,
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('202');
    expect(params.get('currencyCode')).toBe('USD');
    expect(params.get('status')).toBe('Overdue');
    expect(params.get('includeDebitNotes')).toBe('true');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('PI-03: leaves debit notes out of the request unless they were asked for', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', includeDebitNotes: false });

    expect(paramsOf(get.mock.calls[0]).has('includeDebitNotes')).toBe(false);
  });

  it('PI-04: gives back what the endpoint answered', async () => {
    const answer = result([row()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('PI-05: reads the vendors and currencies the filters offer from the report’s own lookups', async () => {
    const vendors = [{ contactId: 201, contactCode: 'STEELCO', contactName: 'Steelco Supplies' }];
    const first = setup(vendors);
    expect(await first.service.vendors()).toEqual(vendors);
    expect(first.get.mock.calls[0][0]).toBe('/api/statements/payable-invoice-summary/vendors');

    TestBed.resetTestingModule();
    const currencies = [{ currencyCode: 'INR' }, { currencyCode: 'USD' }];
    const second = setup(currencies);
    expect(await second.service.currencies()).toEqual(currencies);
    expect(second.get.mock.calls[0][0]).toBe('/api/statements/payable-invoice-summary/currencies');
  });
});

describe('payable invoice summary helpers', () => {
  it('PI-06: the statuses run awaiting, partly paid, overdue, paid, void and read as words', () => {
    expect(PAYABLE_INVOICE_STATUSES.map((s) => s.value)).toEqual(['AwaitingPayment', 'PartlyPaid', 'Overdue', 'Paid', 'Void']);
    expect(payableStatusLabel('AwaitingPayment')).toBe('Awaiting payment');
    expect(payableStatusLabel('PartlyPaid')).toBe('Partly paid');
  });

  it('PI-07: paid reads as done and overdue as a problem', () => {
    expect(payableStatusTag('Paid')).toBe('tag-success');
    expect(payableStatusTag('Overdue')).toBe('tag-danger');
    expect(payableStatusTag('PartlyPaid')).toBe('tag-warning');
    expect(payableStatusTag('AwaitingPayment')).toBe('tag-info');
    expect(payableStatusTag('Void')).toBe('tag-neutral');
  });

  it('PI-08: the tabs are All and then every status, with a nought kept', () => {
    const tabs = payableStatusTabs({ AwaitingPayment: 2, PartlyPaid: 3, Overdue: 2, Paid: 1, Void: 1 });

    expect(tabs.map((t) => t.label)).toEqual(['All', 'Awaiting payment', 'Partly paid', 'Overdue', 'Paid', 'Void']);
    expect(tabs.map((t) => t.value)).toEqual(['', 'AwaitingPayment', 'PartlyPaid', 'Overdue', 'Paid', 'Void']);
    expect(tabs.map((t) => t.count)).toEqual([8, 2, 3, 2, 1, 1]);
  });

  it('PI-09: All leaves the void ones out, as the list does without a status', () => {
    const counts: Record<PayableInvoiceStatus, number> = { AwaitingPayment: 1, PartlyPaid: 0, Overdue: 0, Paid: 3, Void: 4 };

    expect(payableStatusTabs(counts)[0].count).toBe(4);
  });

  it('PI-10: with no counts yet every tab reads nought', () => {
    expect(payableStatusTabs(undefined).map((t) => t.count)).toEqual([0, 0, 0, 0, 0, 0]);
  });

  it('PI-11: the next page is added to the ones loaded and the counts and totals are the latest answer’s', () => {
    const first = result([row({ documentId: 1 })], { totalInvoices: 3, pageNumber: 1 });
    const next = result([row({ documentId: 2 })], {
      totalInvoices: 3,
      pageNumber: 2,
      totals: { taxable: 9, tax: 9, tds: 0, total: 9, payable: 9, paid: 9, due: 9 },
    });

    const merged = appendPayablePage(first, next);

    expect(merged.invoices.map((r) => r.documentId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.total).toBe(9);
  });

  it('PI-12: there is more while fewer bills are loaded than match', () => {
    expect(hasMorePayable({ invoices: [row()], totalInvoices: 2 })).toBe(true);
    expect(hasMorePayable({ invoices: [row(), row({ documentId: 2 })], totalInvoices: 2 })).toBe(false);
  });

  it('PI-13: a debit note is told by its code, and a foreign row by a currency other than the branch’s', () => {
    expect(isDebitNote({ typeCode: 'DBN' })).toBe(true);
    expect(isDebitNote({ typeCode: 'BIL' })).toBe(false);
    expect(isForeignPayable({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignPayable({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignPayable({ currencyCode: 'USD' }, '')).toBe(false);
  });

  it('PI-14: the grid has a row for each bill and then the totals of every match', () => {
    const rows = payableInvoiceRows(result([row({ documentId: 1 }), row({ documentId: 2, documentNo: 'BIL-0002' })]));

    expect(rows.map((r) => r.kind)).toEqual(['invoice', 'invoice', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(3);
    expect(rows[2].name).toBe('Total');
    expect(rows[2].total).toBe(23600);
  });

  it('PI-15: when not every bill is loaded the totals row says it is of all of them', () => {
    const rows = payableInvoiceRows(result([row()], { totalInvoices: 120 }));

    expect(rows[rows.length - 1].name).toBe('Total of all 120');
  });

  it('PI-16: a bill row links to its page, and a debit note to its own, and knows what it is', () => {
    const rows = payableInvoiceRows(
      result([
        row({ documentId: 7 }),
        row({ typeCode: 'DBN', documentId: 4, documentNo: 'DBN-0004', vendorBillNo: null, dueDate: null, total: -2360, payable: -2360, due: 0 }),
      ]),
    );

    expect([rows[0].link, rows[0].isDebitNote, rows[0].vendorBillNo]).toEqual(['/purchase/bills/7', false, 'V-1']);
    expect([rows[1].link, rows[1].isDebitNote, rows[1].vendorBillNo]).toEqual(['/purchase/debit-notes/4', true, null]);
    expect(payableBillLink({ typeCode: 'XYZ', documentId: 1 })).toBeNull();
  });

  it('PI-17: a document in another currency carries its own total, and one in the branch’s does not', () => {
    const rows = payableInvoiceRows(result([row({ currencyCode: 'USD', totalInCurrency: 100, total: 8000 }), row({ documentId: 2, currencyCode: 'INR' })]));

    expect(rows[0].ownTotal).toBe(100);
    expect(rows[1].ownTotal).toBeNull();
  });

  it('PI-18: a bill with TDS or reverse charge says what the vendor is owed differs from the total, and one without does not', () => {
    const rows = payableInvoiceRows(
      result([
        row({ documentId: 1, tds: 1000, total: 59000, payable: 58000 }),
        row({ documentId: 2, reverseCharge: true, total: 9440, payable: 8000 }),
        row({ documentId: 3 }),
      ]),
    );

    expect(rows.slice(0, 3).map((r) => r.payableDiffers)).toEqual([true, true, false]);
    expect([rows[0].tds, rows[1].reverseCharge, rows[0].payable]).toEqual([1000, true, 58000]);
  });

  it('PI-19: a debit note’s amounts stay negative and the totals row is the server’s figures', () => {
    const answer = result([row({ typeCode: 'DBN', taxable: -2000, tax: -360, total: -2360, payable: -2360, paid: -2360, due: 0 })], {
      totals: { taxable: 99000, tax: 16380, tds: 1000, total: 115380, payable: 112940, paid: 28000, due: 84940 },
    });
    const rows = payableInvoiceRows(answer);

    expect([rows[0].taxable, rows[0].total]).toEqual([-2000, -2360]);
    expect([rows[1].taxable, rows[1].total, rows[1].paid, rows[1].due]).toEqual([99000, 115380, 28000, 84940]);
    expect(rows[1].payableDiffers).toBe(true);
  });

  it('PI-20: with no bills there are no rows, not even the totals', () => {
    expect(payableInvoiceRows(result([]))).toEqual([]);
  });

  it('PI-21: the sentence says how many documents, what they come to, and what is paid and due', () => {
    const answer = result([row({ total: 100, paid: 40, due: 60 }), row({ documentId: 2, total: 200, paid: 0, due: 200 })]);

    expect(payableInvoiceSentence(answer, money)).toBe('2 documents for ₹300.00, of which ₹40.00 is paid and ₹260.00 is due.');
    expect(payableInvoiceSentence(result([row()]), money)).toMatch(/^1 document for /);
  });
});
