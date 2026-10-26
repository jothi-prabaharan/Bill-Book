import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  AgedReceivablesDetailCustomer,
  AgedReceivablesDetailDocument,
  AgedReceivablesDetailResult,
  AgedReceivablesDetailTotals,
  agedReceivablesDetailRows,
  agedReceivablesDetailSentence,
  columnOf,
  detailOverdue,
  detailQueryFrom,
  documentKindLabel,
  documentLink,
} from './aged-receivables-detail.models';
import { AgedReceivablesDetailService } from './aged-receivables-detail.service';

/** The Aged Receivables Detail's calls and the helpers its page uses (TK-223). */

function setup(answer: unknown = { customers: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [AgedReceivablesDetailService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(AgedReceivablesDetailService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const doc = (over: Partial<AgedReceivablesDetailDocument> = {}): AgedReceivablesDetailDocument => ({
  kind: 'Invoice',
  typeCode: 'INV',
  documentId: 1,
  documentNo: 'INV-0001',
  documentDate: '2026-08-10',
  dueDate: '2026-09-10',
  daysOverdue: 51,
  original: 80000,
  settled: 40000,
  outstanding: 40000,
  current: 0,
  bucket1: 0,
  bucket2: 40000,
  bucket3: 0,
  bucket4: 0,
  ...over,
});

const totalsOf = (documents: AgedReceivablesDetailDocument[]): AgedReceivablesDetailTotals => {
  const sum = (pick: (d: AgedReceivablesDetailDocument) => number): number => documents.reduce((t, d) => t + pick(d), 0);

  return {
    original: sum((d) => d.original),
    settled: sum((d) => d.settled),
    outstanding: sum((d) => d.outstanding),
    current: sum((d) => d.current),
    bucket1: sum((d) => d.bucket1),
    bucket2: sum((d) => d.bucket2),
    bucket3: sum((d) => d.bucket3),
    bucket4: sum((d) => d.bucket4),
  };
};

const customer = (id: number | null, name: string, documents: AgedReceivablesDetailDocument[]): AgedReceivablesDetailCustomer => ({
  contactId: id,
  contactCode: id === null ? '' : name.slice(0, 4).toUpperCase(),
  contactName: name,
  documents,
  subtotal: totalsOf(documents),
});

const result = (customers: AgedReceivablesDetailCustomer[]): AgedReceivablesDetailResult => ({
  asAtDate: '2026-10-31',
  ageBy: 'DueDate',
  bucketDays: 30,
  baseCurrencyCode: 'INR',
  bucketLabels: ['Current', '1-30 days', '31-60 days', '61-90 days', 'Over 90 days'],
  customers,
  totals: totalsOf(customers.flatMap((c) => c.documents)),
});

describe('AgedReceivablesDetailService', () => {
  it('AD-01: runs with the as-at date alone when nothing else was chosen', async () => {
    const { service, get } = setup();

    await service.run({ asAtDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/aged-receivables-detail');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('asAtDate')).toBe('2026-10-31');
    expect(params.has('ageBy')).toBe(false);
    expect(params.has('bucketDays')).toBe(false);
    expect(params.has('contactId')).toBe(false);
  });

  it('AD-02: sends the age basis, column width and customer that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ asAtDate: '2026-10-31', ageBy: 'InvoiceDate', bucketDays: 7, contactId: 102 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('ageBy')).toBe('InvoiceDate');
    expect(params.get('bucketDays')).toBe('7');
    expect(params.get('contactId')).toBe('102');
  });

  it('AD-03: gives back what the endpoint answered', async () => {
    const answer = result([customer(101, 'Acme Exports', [doc()])]);
    const { service } = setup(answer);

    expect(await service.run({ asAtDate: '2026-10-31' })).toEqual(answer);
  });

  it('AD-04: reads the customers the filter offers from the report’s own lookup', async () => {
    const choices = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Exports' }];
    const { service, get } = setup(choices);

    expect(await service.customers()).toEqual(choices);
    expect(get.mock.calls[0][0]).toBe('/api/statements/aged-receivables-detail/customers');
  });
});

describe('aged receivables detail helpers', () => {
  it('AD-05: a document’s kind reads as a name', () => {
    expect(documentKindLabel('CreditNote')).toBe('Credit note');
    expect(documentKindLabel('OpeningBalance')).toBe('Opening balance');
    expect(documentKindLabel('Invoice')).toBe('Invoice');
  });

  it('AD-06: an invoice, a POS sale, a credit note and a journal open at their own page', () => {
    expect(documentLink({ typeCode: 'INV', documentId: 12 })).toBe('/sales/invoices/12');
    expect(documentLink({ typeCode: 'POS', documentId: 13 })).toBe('/sales/invoices/13');
    expect(documentLink({ typeCode: 'CRN', documentId: 4 })).toBe('/sales/credit-notes/4');
    expect(documentLink({ typeCode: 'JRN', documentId: 7 })).toBe('/accounting/journals/7');
  });

  it('AD-07: a receipt, a payment and anything else are not linked rather than linked to a list', () => {
    expect(documentLink({ typeCode: 'RCM', documentId: 1 })).toBeNull();
    expect(documentLink({ typeCode: 'SPM', documentId: 1 })).toBeNull();
    expect(documentLink({ typeCode: 'OPB', documentId: 1 })).toBeNull();
  });

  it('AD-08: the column a document sits in is the one that holds its amount', () => {
    expect(columnOf(doc({ current: 0, bucket1: 0, bucket2: 40000, bucket3: 0, bucket4: 0 }))).toBe(2);
    expect(columnOf(doc({ current: -8300, bucket2: 0 }))).toBe(0);
    expect(columnOf(doc({ current: 0, bucket2: 0, bucket4: 15000 }))).toBe(4);
    expect(columnOf(doc({ current: 0, bucket2: 0 }))).toBe(0);
  });

  it('AD-09: the grid has a heading, the documents and a subtotal for each customer, then the totals', () => {
    const rows = agedReceivablesDetailRows(
      result([
        customer(101, 'Acme Exports', [doc({ documentId: 1 }), doc({ documentId: 6, documentNo: 'INV-0002' })]),
        customer(null, 'Not tied to a customer', [doc({ typeCode: 'JRN', kind: 'Journal', documentId: 7, documentNo: 'JRN-7', dueDate: null })]),
      ]),
    );

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'document', 'document', 'subtotal', 'heading', 'document', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(rows.length);
    expect(rows[3].name).toBe('Total Acme Exports');
  });

  it('AD-10: a customer with one document has no subtotal, and one customer has no totals, which would repeat the line above', () => {
    expect(agedReceivablesDetailRows(result([customer(101, 'Acme Exports', [doc()])])).map((r) => r.kind)).toEqual(['heading', 'document']);

    // Two documents: the subtotal is the customer's total, and the totals row would repeat it.
    expect(
      agedReceivablesDetailRows(result([customer(101, 'Acme Exports', [doc(), doc({ documentId: 6, documentNo: 'INV-0002' })])])).map((r) => r.kind),
    ).toEqual(['heading', 'document', 'document', 'subtotal']);
  });

  it('AD-11: a document row carries its link, kind, dates and days, and blanks a nought column', () => {
    const row = agedReceivablesDetailRows(result([customer(101, 'Acme Exports', [doc()])]))[1];

    expect(row.name).toBe('INV-0001');
    expect(row.detail).toBe('Invoice');
    expect(row.link).toBe('/sales/invoices/1');
    expect([row.documentDate, row.dueDate, row.daysOverdue]).toEqual(['2026-08-10', '2026-09-10', 51]);
    expect([row.current, row.bucket1, row.bucket2, row.bucket3, row.bucket4]).toEqual([null, null, 40000, null, null]);
    expect([row.original, row.settled, row.outstanding]).toEqual([80000, 40000, 40000]);
    expect(row.column).toBe(2);
  });

  it('AD-12: a document with nothing applied has a blank settled cell, and a credit keeps its minus', () => {
    const row = agedReceivablesDetailRows(
      result([customer(101, 'Acme', [doc({ kind: 'Receipt', typeCode: 'RCM', original: -8300, settled: 0, outstanding: -8300, current: -8300, bucket2: 0, dueDate: null, daysOverdue: null })])]),
    )[1];

    expect(row.settled).toBeNull();
    expect([row.original, row.outstanding, row.current]).toEqual([-8300, -8300, -8300]);
    expect(row.column).toBe(0);
    expect(row.link).toBeNull();
  });

  it('AD-13: the totals row keeps its noughts and a heading has no figures at all', () => {
    const rows = agedReceivablesDetailRows(result([customer(101, 'Acme Exports', [doc()]), customer(102, 'Zenith Traders', [doc({ documentId: 2 })])]));
    const heading = rows[0];
    const total = rows[rows.length - 1];

    expect([heading.original, heading.outstanding, heading.current, heading.column]).toEqual([null, null, null, null]);
    expect([total.current, total.bucket1, total.bucket2, total.bucket3, total.bucket4]).toEqual([0, 0, 80000, 0, 0]);
    expect(total.outstanding).toBe(80000);
  });

  it('AD-14: with no customers there are no rows, not even a total', () => {
    expect(agedReceivablesDetailRows(result([]))).toEqual([]);
  });

  it('AD-15: what is overdue is the four overdue columns and takes no credit off', () => {
    expect(detailOverdue({ bucket1: 13500, bucket2: 40000, bucket3: 0, bucket4: 2500 })).toBe(56000);
  });

  it('AD-16: the sentence counts the documents and the customers, not the row for what no customer claims', () => {
    const answer = result([
      customer(101, 'Acme Exports', [doc(), doc({ documentId: 6, documentNo: 'INV-0002', outstanding: 13500, bucket2: 0, bucket1: 13500 })]),
      customer(null, 'Not tied to a customer', [doc({ typeCode: 'JRN', kind: 'Journal', documentId: 7, outstanding: 2500, bucket2: 0, bucket4: 2500 })]),
    ]);

    expect(agedReceivablesDetailSentence(answer, money)).toBe('₹56000.00 is owed on 3 open documents across 1 customer; ₹56000.00 of invoices is overdue.');
  });

  it('AD-17: the sentence says so when nothing is overdue and counts one document in the singular', () => {
    const answer = result([customer(101, 'Acme Exports', [doc({ outstanding: 300, bucket2: 0, current: 300 })])]);

    expect(agedReceivablesDetailSentence(answer, money)).toBe('₹300.00 is owed on 1 open document across 1 customer; nothing is overdue.');
  });
});

describe('the filters a link opens the detail with', () => {
  const query = (values: Record<string, string>) => detailQueryFrom((name) => values[name] ?? null);

  it('AD-18: reads each filter the summary’s link sends', () => {
    expect(query({ asAtDate: '2026-10-31', ageBy: 'InvoiceDate', bucketDays: '15', contactId: '102' })).toEqual({
      asAtDate: '2026-10-31',
      ageBy: 'InvoiceDate',
      bucketDays: 15,
      contactId: 102,
    });
  });

  it('AD-19: an address with none starts on the defaults', () => {
    expect(query({})).toEqual({ asAtDate: null, ageBy: null, bucketDays: null, contactId: null });
  });

  it('AD-20: a value that is not one of the filter’s own is dropped rather than refused later', () => {
    expect(query({ asAtDate: '31/10/2026', ageBy: 'Whenever', bucketDays: '10', contactId: 'abc' })).toEqual({
      asAtDate: null,
      ageBy: null,
      bucketDays: null,
      contactId: null,
    });
    expect(query({ asAtDate: '2026-13-45', contactId: '-4' }).asAtDate).toBeNull();
    expect(query({ contactId: '-4' }).contactId).toBeNull();
    expect(query({ contactId: '1.5' }).contactId).toBeNull();
  });
});
