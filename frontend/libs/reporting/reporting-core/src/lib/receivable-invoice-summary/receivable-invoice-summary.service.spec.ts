import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  RECEIVABLE_INVOICE_STATUSES,
  ReceivableInvoiceStatus,
  ReceivableInvoiceSummaryResult,
  ReceivableInvoiceSummaryRow,
  appendInvoicePage,
  hasMoreInvoices,
  isCreditNote,
  isForeignRow,
  receivableInvoiceRows,
  receivableInvoiceSentence,
  receivableStatusLabel,
  receivableStatusTabs,
  receivableStatusTag,
} from './receivable-invoice-summary.models';
import { ReceivableInvoiceSummaryService } from './receivable-invoice-summary.service';

/** The Receivable Invoice Summary's calls and the helpers its page uses (TK-224). */

function setup(answer: unknown = { invoices: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [ReceivableInvoiceSummaryService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(ReceivableInvoiceSummaryService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const row = (over: Partial<ReceivableInvoiceSummaryRow> = {}): ReceivableInvoiceSummaryRow => ({
  typeCode: 'INV',
  documentId: 1,
  documentNo: 'INV-0001',
  documentDate: '2026-10-02',
  dueDate: '2026-10-20',
  daysOverdue: null,
  contactId: 101,
  contactCode: 'ZENITH',
  contactName: 'Zenith Traders',
  currencyCode: 'INR',
  totalInCurrency: 11800,
  taxable: 10000,
  cgst: 900,
  sgst: 900,
  igst: 0,
  cess: 0,
  tax: 1800,
  total: 11800,
  received: 11800,
  due: 0,
  status: 'Paid',
  ...over,
});

const result = (invoices: ReceivableInvoiceSummaryRow[], over: Partial<ReceivableInvoiceSummaryResult> = {}): ReceivableInvoiceSummaryResult => ({
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
    total: invoices.reduce((t, r) => t + r.total, 0),
    received: invoices.reduce((t, r) => t + r.received, 0),
    due: invoices.reduce((t, r) => t + r.due, 0),
  },
  ...over,
});

describe('ReceivableInvoiceSummaryService', () => {
  it('RI-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/receivable-invoice-summary');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'currencyCode', 'status', 'includeCreditNotes', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('RI-02: sends the customer, currency, status, credit notes and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-10-01',
      toDate: '2026-10-31',
      contactId: 102,
      currencyCode: 'USD',
      status: 'Overdue',
      includeCreditNotes: true,
      pageNumber: 2,
      pageSize: 25,
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('102');
    expect(params.get('currencyCode')).toBe('USD');
    expect(params.get('status')).toBe('Overdue');
    expect(params.get('includeCreditNotes')).toBe('true');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('RI-03: leaves credit notes out of the request unless they were asked for', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', includeCreditNotes: false });

    expect(paramsOf(get.mock.calls[0]).has('includeCreditNotes')).toBe(false);
  });

  it('RI-04: gives back what the endpoint answered', async () => {
    const answer = result([row()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('RI-05: reads the customers and currencies the filters offer from the report’s own lookups', async () => {
    const customers = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Exports' }];
    const first = setup(customers);
    expect(await first.service.customers()).toEqual(customers);
    expect(first.get.mock.calls[0][0]).toBe('/api/statements/receivable-invoice-summary/customers');

    TestBed.resetTestingModule();
    const currencies = [{ currencyCode: 'INR' }, { currencyCode: 'USD' }];
    const second = setup(currencies);
    expect(await second.service.currencies()).toEqual(currencies);
    expect(second.get.mock.calls[0][0]).toBe('/api/statements/receivable-invoice-summary/currencies');
  });
});

describe('receivable invoice summary helpers', () => {
  it('RI-06: the statuses run awaiting, partly paid, overdue, paid, void and read as words', () => {
    expect(RECEIVABLE_INVOICE_STATUSES.map((s) => s.value)).toEqual(['AwaitingPayment', 'PartlyPaid', 'Overdue', 'Paid', 'Void']);
    expect(receivableStatusLabel('AwaitingPayment')).toBe('Awaiting payment');
    expect(receivableStatusLabel('PartlyPaid')).toBe('Partly paid');
  });

  it('RI-07: paid reads as done and overdue as a problem', () => {
    expect(receivableStatusTag('Paid')).toBe('tag-success');
    expect(receivableStatusTag('Overdue')).toBe('tag-danger');
    expect(receivableStatusTag('PartlyPaid')).toBe('tag-warning');
    expect(receivableStatusTag('AwaitingPayment')).toBe('tag-info');
    expect(receivableStatusTag('Void')).toBe('tag-neutral');
  });

  it('RI-08: the tabs are All and then every status, with a nought kept', () => {
    const tabs = receivableStatusTabs({ AwaitingPayment: 1, PartlyPaid: 2, Overdue: 1, Paid: 1, Void: 1 });

    expect(tabs.map((t) => t.label)).toEqual(['All', 'Awaiting payment', 'Partly paid', 'Overdue', 'Paid', 'Void']);
    expect(tabs.map((t) => t.value)).toEqual(['', 'AwaitingPayment', 'PartlyPaid', 'Overdue', 'Paid', 'Void']);
    expect(tabs.map((t) => t.count)).toEqual([5, 1, 2, 1, 1, 1]);
  });

  it('RI-09: All leaves the void ones out, as the list does without a status', () => {
    const counts: Record<ReceivableInvoiceStatus, number> = { AwaitingPayment: 0, PartlyPaid: 0, Overdue: 0, Paid: 3, Void: 4 };

    expect(receivableStatusTabs(counts)[0].count).toBe(3);
  });

  it('RI-10: with no counts yet every tab reads nought', () => {
    expect(receivableStatusTabs(undefined).map((t) => t.count)).toEqual([0, 0, 0, 0, 0, 0]);
  });

  it('RI-11: the next page is added to the ones loaded and the counts and totals are the latest answer’s', () => {
    const first = result([row({ documentId: 1 })], { totalInvoices: 3, pageNumber: 1 });
    const next = result([row({ documentId: 2 })], { totalInvoices: 3, pageNumber: 2, totals: { taxable: 9, tax: 9, total: 9, received: 9, due: 9 } });

    const merged = appendInvoicePage(first, next);

    expect(merged.invoices.map((r) => r.documentId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.total).toBe(9);
  });

  it('RI-12: there is more while fewer invoices are loaded than match', () => {
    expect(hasMoreInvoices({ invoices: [row()], totalInvoices: 2 })).toBe(true);
    expect(hasMoreInvoices({ invoices: [row(), row({ documentId: 2 })], totalInvoices: 2 })).toBe(false);
  });

  it('RI-13: a credit note is told by its code, and a foreign row by a currency other than the branch’s', () => {
    expect(isCreditNote({ typeCode: 'CRN' })).toBe(true);
    expect(isCreditNote({ typeCode: 'INV' })).toBe(false);
    expect(isForeignRow({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignRow({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignRow({ currencyCode: 'USD' }, '')).toBe(false);
  });

  it('RI-14: the grid has a row for each invoice and then the totals of every match', () => {
    const rows = receivableInvoiceRows(result([row({ documentId: 1 }), row({ documentId: 2, documentNo: 'INV-0002' })]));

    expect(rows.map((r) => r.kind)).toEqual(['invoice', 'invoice', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(3);
    expect(rows[2].name).toBe('Total');
    expect(rows[2].total).toBe(23600);
  });

  it('RI-15: when not every invoice is loaded the totals row says it is of all of them', () => {
    const rows = receivableInvoiceRows(result([row()], { totalInvoices: 120 }));

    expect(rows[rows.length - 1].name).toBe('Total of all 120');
  });

  it('RI-16: an invoice row links to its page, and a credit note to its own, and knows what it is', () => {
    const rows = receivableInvoiceRows(
      result([row({ typeCode: 'INV', documentId: 7 }), row({ typeCode: 'CRN', documentId: 8, documentNo: 'CRN-8', dueDate: null })]),
    );

    expect(rows[0].link).toBe('/sales/invoices/7');
    expect(rows[1].link).toBe('/sales/credit-notes/8');
    expect([rows[0].isCreditNote, rows[1].isCreditNote]).toEqual([false, true]);
  });

  it('RI-17: a document in another currency carries its own total, and one in the branch’s does not', () => {
    const rows = receivableInvoiceRows(
      result([row({ currencyCode: 'USD', totalInCurrency: 590, total: 47200 }), row({ documentId: 2, currencyCode: 'INR' })]),
    );

    expect(rows[0].ownTotal).toBe(590);
    expect(rows[1].ownTotal).toBeNull();
  });

  it('RI-18: a credit note’s amounts stay negative and the totals row is the server’s figures', () => {
    const rows = receivableInvoiceRows(
      result([row({ typeCode: 'CRN', documentId: 9, taxable: -1000, tax: -180, total: -1180, received: -1180, due: 0 })], {
        totals: { taxable: -1000, tax: -180, total: -1180, received: -1180, due: 0 },
      }),
    );

    expect([rows[0].taxable, rows[0].total, rows[0].received]).toEqual([-1000, -1180, -1180]);
    expect(rows[1].total).toBe(-1180);
  });

  it('RI-19: with no invoices there are no rows, not even the totals', () => {
    expect(receivableInvoiceRows(result([]))).toEqual([]);
  });

  it('RI-20: the sentence says how many documents, what they come to, and what is received and due', () => {
    const answer = result([row({ total: 100, received: 40, due: 60 }), row({ documentId: 2, total: 200, received: 0, due: 200 })]);

    expect(receivableInvoiceSentence(answer, money)).toBe('2 documents for ₹300.00, of which ₹40.00 is received and ₹260.00 is due.');
    expect(receivableInvoiceSentence(result([row()]), money)).toBe('1 document for ₹11800.00, of which ₹11800.00 is received and ₹0.00 is due.');
  });
});
