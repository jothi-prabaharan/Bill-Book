import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  AGE_BASIS_OPTIONS,
  AgedReceivablesSummaryResult,
  AgedReceivablesSummaryRow,
  BUCKET_SIZES,
  agedReceivablesRows,
  agedReceivablesSentence,
  bucketOptionLabel,
  columnAmounts,
  customerChoiceLabel,
  detailQueryParams,
  overdueOf,
} from './aged-receivables-summary.models';
import { AgedReceivablesSummaryService } from './aged-receivables-summary.service';

/** The Aged Receivables Summary's calls and the helpers its page uses (TK-222). */

function setup(answer: unknown = { customers: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [AgedReceivablesSummaryService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(AgedReceivablesSummaryService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const row = (over: Partial<AgedReceivablesSummaryRow> = {}): AgedReceivablesSummaryRow => ({
  contactId: 101,
  contactCode: 'ACME',
  contactName: 'Acme Exports',
  current: 0,
  bucket1: 0,
  bucket2: 0,
  bucket3: 0,
  bucket4: 0,
  total: 0,
  ...over,
});

const result = (customers: AgedReceivablesSummaryRow[]): AgedReceivablesSummaryResult => {
  const sum = (pick: (r: AgedReceivablesSummaryRow) => number): number => customers.reduce((t, r) => t + pick(r), 0);

  return {
    asAtDate: '2026-10-31',
    ageBy: 'DueDate',
    bucketDays: 30,
    baseCurrencyCode: 'INR',
    bucketLabels: ['Current', '1-30 days', '31-60 days', '61-90 days', 'Over 90 days'],
    customers,
    totals: row({
      contactId: null,
      contactCode: '',
      contactName: 'Total',
      current: sum((r) => r.current),
      bucket1: sum((r) => r.bucket1),
      bucket2: sum((r) => r.bucket2),
      bucket3: sum((r) => r.bucket3),
      bucket4: sum((r) => r.bucket4),
      total: sum((r) => r.total),
    }),
  };
};

describe('AgedReceivablesSummaryService', () => {
  it('AR-01: runs with the as-at date alone when nothing else was chosen', async () => {
    const { service, get } = setup();

    await service.run({ asAtDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/aged-receivables-summary');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('asAtDate')).toBe('2026-10-31');
    expect(params.has('ageBy')).toBe(false);
    expect(params.has('bucketDays')).toBe(false);
    expect(params.has('contactId')).toBe(false);
    expect(params.has('showZeroBalances')).toBe(false);
  });

  it('AR-02: sends the age basis, column width and customer that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ asAtDate: '2026-10-31', ageBy: 'InvoiceDate', bucketDays: 15, contactId: 102 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('ageBy')).toBe('InvoiceDate');
    expect(params.get('bucketDays')).toBe('15');
    expect(params.get('contactId')).toBe('102');
  });

  it('AR-03: asks for the customers with nothing owed only when that was ticked', async () => {
    const { service, get } = setup();

    await service.run({ asAtDate: '2026-10-31', showZeroBalances: true });
    await service.run({ asAtDate: '2026-10-31', showZeroBalances: false });

    expect(paramsOf(get.mock.calls[0]).get('showZeroBalances')).toBe('true');
    expect(paramsOf(get.mock.calls[1]).has('showZeroBalances')).toBe(false);
  });

  it('AR-04: gives back what the endpoint answered', async () => {
    const answer = result([row({ current: 100, total: 100 })]);
    const { service } = setup(answer);

    expect(await service.run({ asAtDate: '2026-10-31' })).toEqual(answer);
  });

  it('AR-05: reads the customers the filter offers from the report’s own lookup', async () => {
    const choices = [{ contactId: 101, contactCode: 'ACME', contactName: 'Acme Exports' }];
    const { service, get } = setup(choices);

    expect(await service.customers()).toEqual(choices);
    expect(get.mock.calls[0][0]).toBe('/api/statements/aged-receivables-summary/customers');
  });
});

describe('aged receivables summary helpers', () => {
  it('AR-06: a row’s five columns are in order, Current first', () => {
    expect(columnAmounts(row({ current: -1, bucket1: 2, bucket2: 3, bucket3: 4, bucket4: 5 }))).toEqual([-1, 2, 3, 4, 5]);
  });

  it('AR-07: what is overdue is the four overdue columns and takes no credit off', () => {
    expect(overdueOf(row({ current: -8300, bucket1: 13500, bucket2: 40000, bucket3: 0, bucket4: 0 }))).toBe(53500);
    expect(overdueOf(row({ current: 500 }))).toBe(0);
  });

  it('AR-08: the grid has a row for each customer in the server’s order, then the totals', () => {
    const rows = agedReceivablesRows(
      result([
        row({ contactId: 101, contactName: 'Acme Exports', current: -8300, bucket1: 13500, bucket2: 40000, total: 45200 }),
        row({ contactId: null, contactCode: '', contactName: 'Not tied to a customer', bucket4: 2500, total: 2500 }),
      ]),
    );

    expect(rows.map((r) => r.kind)).toEqual(['customer', 'unassigned', 'total']);
    expect(rows.map((r) => r.name)).toEqual(['Acme Exports', 'Not tied to a customer', 'Total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(3);
  });

  it('AR-09: a nought reads blank on a customer’s row and the totals keep theirs', () => {
    const rows = agedReceivablesRows(result([row({ current: 100, bucket2: 50, total: 150 })]));

    expect([rows[0].current, rows[0].bucket1, rows[0].bucket2, rows[0].bucket3, rows[0].bucket4]).toEqual([100, null, 50, null, null]);
    expect([rows[1].current, rows[1].bucket1, rows[1].bucket2, rows[1].bucket3, rows[1].bucket4]).toEqual([100, 0, 50, 0, 0]);
    expect(rows[1].total).toBe(150);
  });

  it('AR-10: a negative amount is kept as it is so the credit shows', () => {
    const rows = agedReceivablesRows(result([row({ current: -8300, total: -8300 })]));

    expect(rows[0].current).toBe(-8300);
    expect(rows[0].total).toBe(-8300);
  });

  it('AR-11: with no customers there are no rows, not even a total', () => {
    expect(agedReceivablesRows(result([]))).toEqual([]);
  });

  it('AR-12: the sentence says what is owed, by how many customers, and how much is overdue', () => {
    const answer = result([
      row({ contactId: 101, current: -8300, bucket1: 13500, bucket2: 40000, total: 45200 }),
      row({ contactId: 102, contactName: 'Zenith Traders', current: 6000, bucket1: 6000, bucket4: 15000, total: 27000 }),
      row({ contactId: null, contactName: 'Not tied to a customer', bucket4: 2500, total: 2500 }),
    ]);

    // The row for what no customer claims is not a customer.
    expect(agedReceivablesSentence(answer, money)).toBe('₹74700.00 is owed by 2 customers; ₹77000.00 of invoices is overdue.');
  });

  it('AR-13: the sentence says so when nothing is overdue, and counts one customer in the singular', () => {
    const answer = result([row({ current: 300, total: 300 })]);

    expect(agedReceivablesSentence(answer, money)).toBe('₹300.00 is owed by 1 customer; nothing is overdue.');
  });

  it('AR-14: the filter offers the due date first and the three column widths, 30 by default', () => {
    expect(AGE_BASIS_OPTIONS.map((o) => o.value)).toEqual(['DueDate', 'InvoiceDate']);
    expect([...BUCKET_SIZES]).toEqual([30, 15, 7]);
    expect(bucketOptionLabel(15)).toBe('15 days');
  });

  it('AR-15: a customer reads as its code then its name', () => {
    expect(customerChoiceLabel({ contactId: 101, contactCode: 'ACME', contactName: 'Acme Exports' })).toBe('ACME — Acme Exports');
  });
});

describe('the link to the detail report', () => {
  it('AR-16: carries the date, age basis and width the report was run with, and the customer', () => {
    expect(detailQueryParams(result([]), 101)).toEqual({ asAtDate: '2026-10-31', ageBy: 'DueDate', bucketDays: 30, contactId: 101 });
  });

  it('AR-17: the totals row passes no customer, so the detail is for everyone', () => {
    expect(detailQueryParams({ asAtDate: '2026-10-31', ageBy: 'InvoiceDate', bucketDays: 7 }, null)).toEqual({
      asAtDate: '2026-10-31',
      ageBy: 'InvoiceDate',
      bucketDays: 7,
    });
  });

  it('AR-18: each grid row knows its customer, and the totals and what no customer claims have none', () => {
    const rows = agedReceivablesRows(
      result([row({ contactId: 101, total: 10, current: 10 }), row({ contactId: null, contactName: 'Not tied to a customer', total: 5, bucket4: 5 })]),
    );

    expect(rows.map((r) => r.contactId)).toEqual([101, null, null]);
    expect(rows.map((r) => r.kind)).toEqual(['customer', 'unassigned', 'total']);
  });
});

