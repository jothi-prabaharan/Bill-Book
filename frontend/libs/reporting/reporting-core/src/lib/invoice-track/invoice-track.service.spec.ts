import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  INVOICE_TRACK_STATUSES,
  InvoiceTrackResult,
  InvoiceTrackRow,
  InvoiceTrackStatus,
  appendTrackPage,
  hasMoreTrack,
  invoiceTrackRows,
  invoiceTrackSentence,
  invoiceTrackTabs,
  isForeignTrack,
  isInBooks,
  localIsoDate,
  trackStatusLabel,
  trackStatusTag,
} from './invoice-track.models';
import { InvoiceTrackService } from './invoice-track.service';

/** The Invoice Track Report's calls and the helpers its page uses (TK-226). */

function setup(answer: unknown = { invoices: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [InvoiceTrackService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(InvoiceTrackService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const row = (over: Partial<InvoiceTrackRow> = {}): InvoiceTrackRow => ({
  typeCode: 'INV',
  documentId: 1,
  documentNo: 'INV-0001',
  documentDate: '2026-10-02',
  postedAt: '2026-10-02T09:00:00Z',
  dueDate: '2026-10-20',
  lastPaymentDate: '2026-10-10',
  daysOverdue: null,
  contactId: 101,
  contactCode: 'ZENITH',
  contactName: 'Zenith Traders',
  currencyCode: 'INR',
  totalInCurrency: 11800,
  total: 11800,
  received: 11800,
  due: 0,
  status: 'Paid',
  ...over,
});

const result = (invoices: InvoiceTrackRow[], over: Partial<InvoiceTrackResult> = {}): InvoiceTrackResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  invoices,
  totalInvoices: invoices.length,
  pageNumber: 1,
  pageSize: 25,
  statusCounts: { Draft: 0, ReadyToPost: 0, AwaitingPayment: 0, PartlyPaid: 0, Overdue: 0, Paid: invoices.length, Void: 0 },
  totals: {
    total: invoices.reduce((t, r) => t + r.total, 0),
    received: invoices.reduce((t, r) => t + r.received, 0),
    due: invoices.reduce((t, r) => t + r.due, 0),
  },
  ...over,
});

describe('InvoiceTrackService', () => {
  it('IT-01: runs with both dates and nothing else when nothing was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/invoice-track-report');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['contactId', 'status', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('IT-02: sends the customer, status and page that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', contactId: 102, status: 'Overdue', pageNumber: 2, pageSize: 25 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('contactId')).toBe('102');
    expect(params.get('status')).toBe('Overdue');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('25');
  });

  it('IT-03: gives back what the endpoint answered', async () => {
    const answer = result([row()]);
    const { service } = setup(answer);

    expect(await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' })).toEqual(answer);
  });

  it('IT-04: reads the customers the filter offers from the report’s own lookup', async () => {
    const customers = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Exports' }];
    const { service, get } = setup(customers);

    expect(await service.customers()).toEqual(customers);
    expect(get.mock.calls[0][0]).toBe('/api/statements/invoice-track-report/customers');
  });
});

describe('invoice track helpers', () => {
  it('IT-05: the statuses run along an invoice’s life and read as words', () => {
    expect(INVOICE_TRACK_STATUSES.map((s) => s.value)).toEqual(['Draft', 'ReadyToPost', 'AwaitingPayment', 'PartlyPaid', 'Overdue', 'Paid', 'Void']);
    expect(trackStatusLabel('ReadyToPost')).toBe('Ready to post');
    expect(trackStatusLabel('AwaitingPayment')).toBe('Awaiting payment');
  });

  it('IT-06: paid reads as done, overdue as a problem and what is not in the books as outlined', () => {
    expect(trackStatusTag('Paid')).toBe('tag-success');
    expect(trackStatusTag('Overdue')).toBe('tag-danger');
    expect(trackStatusTag('PartlyPaid')).toBe('tag-warning');
    expect(trackStatusTag('AwaitingPayment')).toBe('tag-info');
    expect(trackStatusTag('Draft')).toBe('tag-outline');
    expect(trackStatusTag('ReadyToPost')).toBe('tag-outline');
    expect(trackStatusTag('Void')).toBe('tag-neutral');
  });

  it('IT-07: the tabs are All and then every status, with a nought kept', () => {
    const tabs = invoiceTrackTabs({ Draft: 1, ReadyToPost: 1, AwaitingPayment: 1, PartlyPaid: 2, Overdue: 1, Paid: 1, Void: 1 });

    expect(tabs.map((t) => t.label)).toEqual(['All', 'Draft', 'Ready to post', 'Awaiting payment', 'Partly paid', 'Overdue', 'Paid', 'Void']);
    expect(tabs.map((t) => t.value)).toEqual(['', 'Draft', 'ReadyToPost', 'AwaitingPayment', 'PartlyPaid', 'Overdue', 'Paid', 'Void']);
    expect(tabs.map((t) => t.count)).toEqual([7, 1, 1, 1, 2, 1, 1, 1]);
  });

  it('IT-08: All leaves the void ones out, as the list does without a status', () => {
    const counts: Record<InvoiceTrackStatus, number> = { Draft: 1, ReadyToPost: 0, AwaitingPayment: 0, PartlyPaid: 0, Overdue: 0, Paid: 3, Void: 4 };

    expect(invoiceTrackTabs(counts)[0].count).toBe(4);
  });

  it('IT-09: with no counts yet every tab reads nought', () => {
    expect(invoiceTrackTabs(undefined).map((t) => t.count)).toEqual([0, 0, 0, 0, 0, 0, 0, 0]);
  });

  it('IT-10: a draft and a ready-to-post invoice are not in the books, and every other status is', () => {
    expect(isInBooks('Draft')).toBe(false);
    expect(isInBooks('ReadyToPost')).toBe(false);
    for (const status of ['AwaitingPayment', 'PartlyPaid', 'Overdue', 'Paid', 'Void'] as const) {
      expect(isInBooks(status)).toBe(true);
    }
  });

  it('IT-11: a foreign row is told by a currency other than the branch’s, whatever the case', () => {
    expect(isForeignTrack({ currencyCode: 'USD' }, 'INR')).toBe(true);
    expect(isForeignTrack({ currencyCode: 'inr' }, 'INR')).toBe(false);
    expect(isForeignTrack({ currencyCode: 'USD' }, '')).toBe(false);
  });

  it('IT-12: the moment of a posting is the day it fell on in the viewer’s own time, and nothing is nothing', () => {
    const local = new Date(2026, 9, 3, 1, 30); // 3 October, half past one in the morning, wherever the viewer is
    expect(localIsoDate(local.toISOString())).toBe('2026-10-03');
    expect(localIsoDate(null)).toBeNull();
    expect(localIsoDate('')).toBeNull();
    expect(localIsoDate('not a date')).toBeNull();
  });

  it('IT-13: the grid has a row for each invoice and then the totals of every posted invoice', () => {
    const rows = invoiceTrackRows(result([row({ documentId: 1 }), row({ documentId: 2, documentNo: 'INV-0002' })]));

    expect(rows.map((r) => r.kind)).toEqual(['invoice', 'invoice', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(3);
    expect(rows[2].name).toBe('Total of posted');
    expect(rows[2].total).toBe(23600);
  });

  it('IT-14: when not every invoice is loaded the totals row says it is of all of them', () => {
    const rows = invoiceTrackRows(result([row()], { totalInvoices: 120 }));

    expect(rows[rows.length - 1].name).toBe('Total of posted, all 120');
  });

  it('IT-15: an invoice row links to its page and carries the dates of its life', () => {
    const [invoice] = invoiceTrackRows(result([row({ documentId: 7, daysOverdue: 16, status: 'Overdue' })]));

    expect(invoice.link).toBe('/sales/invoices/7');
    expect([invoice.documentDate, invoice.dueDate, invoice.lastPaymentDate, invoice.daysOverdue]).toEqual(['2026-10-02', '2026-10-20', '2026-10-10', 16]);
    expect(invoice.postedOn).toBe(localIsoDate('2026-10-02T09:00:00Z'));
  });

  it('IT-16: a draft has no posted day or last payment, and is not in the books so its received and due are left blank', () => {
    const [draft] = invoiceTrackRows(
      result([row({ status: 'Draft', postedAt: null, lastPaymentDate: null, received: 0, due: 0, dueDate: null })]),
    );

    expect([draft.postedOn, draft.lastPaymentDate, draft.dueDate, draft.inBooks]).toEqual([null, null, null, false]);
  });

  it('IT-17: a document in another currency carries its own total, and one in the branch’s does not', () => {
    const rows = invoiceTrackRows(result([row({ currencyCode: 'USD', totalInCurrency: 590, total: 47200 }), row({ documentId: 2, currencyCode: 'INR' })]));

    expect(rows[0].ownTotal).toBe(590);
    expect(rows[1].ownTotal).toBeNull();
  });

  it('IT-18: with no invoices there are no rows, not even the totals', () => {
    expect(invoiceTrackRows(result([]))).toEqual([]);
  });

  it('IT-19: the next page is added to the ones loaded and the counts and totals are the latest answer’s', () => {
    const first = result([row({ documentId: 1 })], { totalInvoices: 3, pageNumber: 1 });
    const next = result([row({ documentId: 2 })], { totalInvoices: 3, pageNumber: 2, totals: { total: 9, received: 9, due: 9 } });

    const merged = appendTrackPage(first, next);

    expect(merged.invoices.map((r) => r.documentId)).toEqual([1, 2]);
    expect(merged.pageNumber).toBe(2);
    expect(merged.totals.total).toBe(9);
  });

  it('IT-20: there is more while fewer invoices are loaded than match', () => {
    expect(hasMoreTrack({ invoices: [row()], totalInvoices: 2 })).toBe(true);
    expect(hasMoreTrack({ invoices: [row(), row({ documentId: 2 })], totalInvoices: 2 })).toBe(false);
  });

  it('IT-21: the sentence says how many invoices, and what the posted ones come to, with what is received and due', () => {
    const answer = result([row({ total: 100, received: 40, due: 60 }), row({ documentId: 2, total: 200, received: 0, due: 200 })]);

    expect(invoiceTrackSentence(answer, money)).toBe('2 invoices. Posted: ₹300.00, of which ₹40.00 is received and ₹260.00 is due.');
    expect(invoiceTrackSentence(result([row()]), money)).toMatch(/^1 invoice\. Posted: /);
  });
});
