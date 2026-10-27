import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  AgedPayablesDetailsDocument,
  AgedPayablesDetailsResult,
  AgedPayablesDetailsTotals,
  AgedPayablesDetailsVendor,
  agedPayablesDetailsRows,
  agedPayablesDetailsSentence,
  payableDocumentLink,
  payableKindLabel,
} from './aged-payables-details.models';
import { AgedPayablesDetailsService } from './aged-payables-details.service';

/** The Aged Payables Details' calls and the helpers its page uses (TK-234). */

function setup(answer: unknown = { vendors: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [AgedPayablesDetailsService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(AgedPayablesDetailsService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const doc = (over: Partial<AgedPayablesDetailsDocument> = {}): AgedPayablesDetailsDocument => ({
  kind: 'Bill',
  typeCode: 'BIL',
  documentId: 1,
  documentNo: 'BIL-0001',
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

const totalsOf = (documents: AgedPayablesDetailsDocument[]): AgedPayablesDetailsTotals => {
  const sum = (pick: (d: AgedPayablesDetailsDocument) => number): number => documents.reduce((t, d) => t + pick(d), 0);

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

const vendor = (id: number | null, name: string, documents: AgedPayablesDetailsDocument[], isMsme = false): AgedPayablesDetailsVendor => ({
  contactId: id,
  contactCode: id === null ? '' : name.slice(0, 4).toUpperCase(),
  contactName: name,
  isMsme,
  documents,
  subtotal: totalsOf(documents),
});

const result = (vendors: AgedPayablesDetailsVendor[]): AgedPayablesDetailsResult => ({
  asAtDate: '2026-10-31',
  ageBy: 'DueDate',
  bucketDays: 30,
  baseCurrencyCode: 'INR',
  bucketLabels: ['Current', '1-30 days', '31-60 days', '61-90 days', 'Over 90 days'],
  vendors,
  totals: totalsOf(vendors.flatMap((v) => v.documents)),
});

describe('AgedPayablesDetailsService', () => {
  it('PD-01: runs with the as-at date alone when nothing else was chosen', async () => {
    const { service, get } = setup();

    await service.run({ asAtDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/aged-payables-details');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('asAtDate')).toBe('2026-10-31');
    expect(params.has('ageBy')).toBe(false);
    expect(params.has('bucketDays')).toBe(false);
    expect(params.has('contactId')).toBe(false);
  });

  it('PD-02: sends the age basis, column width and vendor that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ asAtDate: '2026-10-31', ageBy: 'InvoiceDate', bucketDays: 7, contactId: 202 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('ageBy')).toBe('InvoiceDate');
    expect(params.get('bucketDays')).toBe('7');
    expect(params.get('contactId')).toBe('202');
  });

  it('PD-03: gives back what the endpoint answered', async () => {
    const answer = result([vendor(201, 'Boltworks Ltd', [doc()])]);
    const { service } = setup(answer);

    expect(await service.run({ asAtDate: '2026-10-31' })).toEqual(answer);
  });

  it('PD-04: reads the vendors the filter offers from the report’s own lookup', async () => {
    const choices = [{ contactId: 201, contactCode: 'STEELCO', contactName: 'Steelco Supplies' }];
    const { service, get } = setup(choices);

    expect(await service.vendors()).toEqual(choices);
    expect(get.mock.calls[0][0]).toBe('/api/statements/aged-payables-details/vendors');
  });
});

describe('aged payables details helpers', () => {
  it('PD-05: a document’s kind reads as a name', () => {
    expect(payableKindLabel('DebitNote')).toBe('Debit note');
    expect(payableKindLabel('OpeningBalance')).toBe('Opening balance');
    expect(payableKindLabel('Bill')).toBe('Bill');
    expect(payableKindLabel('Receipt')).toBe('Receipt');
  });

  it('PD-06: a bill, a debit note and a journal open at their own page', () => {
    expect(payableDocumentLink({ typeCode: 'BIL', documentId: 12 })).toBe('/purchase/bills/12');
    expect(payableDocumentLink({ typeCode: 'DBN', documentId: 4 })).toBe('/purchase/debit-notes/4');
    expect(payableDocumentLink({ typeCode: 'JRN', documentId: 7 })).toBe('/accounting/journals/7');
  });

  it('PD-07: a payment, a receipt and anything else are not linked rather than linked to a list', () => {
    expect(payableDocumentLink({ typeCode: 'SPM', documentId: 1 })).toBeNull();
    expect(payableDocumentLink({ typeCode: 'RCM', documentId: 1 })).toBeNull();
    expect(payableDocumentLink({ typeCode: 'OPB', documentId: 1 })).toBeNull();
  });

  it('PD-08: the grid has a heading, the documents and a subtotal for each vendor, then the totals', () => {
    const rows = agedPayablesDetailsRows(
      result([
        vendor(201, 'Steelco Supplies', [doc({ documentId: 1 }), doc({ documentId: 6, documentNo: 'BIL-0002' })], true),
        vendor(null, 'Not tied to a vendor', [doc({ typeCode: 'JRN', kind: 'Journal', documentId: 7, documentNo: 'JRN-7', dueDate: null })]),
      ]),
    );

    expect(rows.map((r) => r.kind)).toEqual(['heading', 'document', 'document', 'subtotal', 'heading', 'document', 'total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(rows.length);
    expect(rows[3].name).toBe('Total Steelco Supplies');
  });

  it('PD-09: a vendor with one document has no subtotal, and one vendor has no totals, which would repeat the line above', () => {
    expect(agedPayablesDetailsRows(result([vendor(201, 'Steelco Supplies', [doc()])])).map((r) => r.kind)).toEqual(['heading', 'document']);

    expect(
      agedPayablesDetailsRows(result([vendor(201, 'Steelco Supplies', [doc(), doc({ documentId: 6, documentNo: 'BIL-0002' })])])).map((r) => r.kind),
    ).toEqual(['heading', 'document', 'document', 'subtotal']);
  });

  it('PD-10: a heading carries whether the vendor is an MSME, and no other row does', () => {
    const rows = agedPayablesDetailsRows(result([vendor(201, 'Steelco Supplies', [doc(), doc({ documentId: 2 })], true), vendor(202, 'Boltworks Ltd', [doc({ documentId: 3 })])]));

    expect(rows.filter((r) => r.kind === 'heading').map((r) => r.isMsme)).toEqual([true, false]);
    expect(rows.filter((r) => r.kind !== 'heading').every((r) => !r.isMsme)).toBe(true);
  });

  it('PD-11: a document row carries its link, kind, dates and days, and blanks a nought column', () => {
    const row = agedPayablesDetailsRows(result([vendor(201, 'Steelco Supplies', [doc()])]))[1];

    expect(row.name).toBe('BIL-0001');
    expect(row.detail).toBe('Bill');
    expect(row.link).toBe('/purchase/bills/1');
    expect([row.documentDate, row.dueDate, row.daysOverdue]).toEqual(['2026-08-10', '2026-09-10', 51]);
    expect([row.current, row.bucket1, row.bucket2, row.bucket3, row.bucket4]).toEqual([null, null, 40000, null, null]);
    expect([row.original, row.settled, row.outstanding]).toEqual([80000, 40000, 40000]);
    expect(row.column).toBe(2);
  });

  it('PD-12: a document with nothing applied has a blank settled cell, and a debit keeps its minus', () => {
    const row = agedPayablesDetailsRows(
      result([vendor(201, 'Boltworks', [doc({ kind: 'DebitNote', typeCode: 'DBN', original: -1000, settled: 0, outstanding: -1000, current: -1000, bucket2: 0, dueDate: null, daysOverdue: null })])]),
    )[1];

    expect(row.settled).toBeNull();
    expect([row.original, row.outstanding, row.current]).toEqual([-1000, -1000, -1000]);
    expect(row.column).toBe(0);
    expect(row.link).toBe('/purchase/debit-notes/1');
  });

  it('PD-13: the totals row keeps its noughts and a heading has no figures at all', () => {
    const rows = agedPayablesDetailsRows(result([vendor(201, 'Steelco Supplies', [doc()]), vendor(202, 'Boltworks Ltd', [doc({ documentId: 2 })])]));
    const heading = rows[0];
    const total = rows[rows.length - 1];

    expect([heading.original, heading.outstanding, heading.current, heading.column]).toEqual([null, null, null, null]);
    expect([total.current, total.bucket1, total.bucket2, total.bucket3, total.bucket4]).toEqual([0, 0, 80000, 0, 0]);
    expect(total.outstanding).toBe(80000);
  });

  it('PD-14: with no vendors there are no rows, not even a total', () => {
    expect(agedPayablesDetailsRows(result([]))).toEqual([]);
  });

  it('PD-15: the sentence counts the documents and the vendors, not the row for what no vendor claims', () => {
    const answer = result([
      vendor(201, 'Steelco Supplies', [doc(), doc({ documentId: 6, documentNo: 'BIL-0002', outstanding: 13500, bucket2: 0, bucket1: 13500 })]),
      vendor(null, 'Not tied to a vendor', [doc({ typeCode: 'JRN', kind: 'Journal', documentId: 7, outstanding: 2500, bucket2: 0, bucket4: 2500 })]),
    ]);

    expect(agedPayablesDetailsSentence(answer, money)).toBe('₹56000.00 is owed on 3 open documents across 1 vendor; ₹56000.00 of bills is overdue.');
  });

  it('PD-16: the sentence says so when nothing is overdue and counts one document in the singular', () => {
    const answer = result([vendor(201, 'Steelco Supplies', [doc({ outstanding: 300, bucket2: 0, current: 300 })])]);

    expect(agedPayablesDetailsSentence(answer, money)).toBe('₹300.00 is owed on 1 open document across 1 vendor; nothing is overdue.');
  });
});
