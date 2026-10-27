import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  AgedPayablesSummaryResult,
  AgedPayablesSummaryRow,
  MSME_AGING_LINK,
  PAYABLE_AGE_BASIS_OPTIONS,
  agedPayablesRows,
  agedPayablesSentence,
  msmeCount,
  vendorChoiceLabel,
} from './aged-payables-summary.models';
import { AgedPayablesSummaryService } from './aged-payables-summary.service';

/** The Aged Payables Summary's calls and the helpers its page uses (TK-233). */

function setup(answer: unknown = { vendors: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [AgedPayablesSummaryService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(AgedPayablesSummaryService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const row = (over: Partial<AgedPayablesSummaryRow> = {}): AgedPayablesSummaryRow => ({
  contactId: 201,
  contactCode: 'STEELCO',
  contactName: 'Steelco Supplies',
  isMsme: false,
  current: 0,
  bucket1: 0,
  bucket2: 0,
  bucket3: 0,
  bucket4: 0,
  total: 0,
  ...over,
});

const result = (vendors: AgedPayablesSummaryRow[]): AgedPayablesSummaryResult => {
  const sum = (pick: (r: AgedPayablesSummaryRow) => number): number => vendors.reduce((t, r) => t + pick(r), 0);

  return {
    asAtDate: '2026-10-31',
    ageBy: 'DueDate',
    bucketDays: 30,
    baseCurrencyCode: 'INR',
    bucketLabels: ['Current', '1-30 days', '31-60 days', '61-90 days', 'Over 90 days'],
    vendors,
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

describe('AgedPayablesSummaryService', () => {
  it('AP-01: runs with the as-at date alone when nothing else was chosen', async () => {
    const { service, get } = setup();

    await service.run({ asAtDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/aged-payables-summary');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('asAtDate')).toBe('2026-10-31');
    expect(params.has('ageBy')).toBe(false);
    expect(params.has('bucketDays')).toBe(false);
    expect(params.has('contactId')).toBe(false);
    expect(params.has('showZeroBalances')).toBe(false);
  });

  it('AP-02: sends the age basis, column width and vendor that were chosen', async () => {
    const { service, get } = setup();

    await service.run({ asAtDate: '2026-10-31', ageBy: 'InvoiceDate', bucketDays: 15, contactId: 202 });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('ageBy')).toBe('InvoiceDate');
    expect(params.get('bucketDays')).toBe('15');
    expect(params.get('contactId')).toBe('202');
  });

  it('AP-03: asks for the vendors with nothing owed only when that was ticked', async () => {
    const { service, get } = setup();

    await service.run({ asAtDate: '2026-10-31', showZeroBalances: true });
    await service.run({ asAtDate: '2026-10-31', showZeroBalances: false });

    expect(paramsOf(get.mock.calls[0]).get('showZeroBalances')).toBe('true');
    expect(paramsOf(get.mock.calls[1]).has('showZeroBalances')).toBe(false);
  });

  it('AP-04: gives back what the endpoint answered', async () => {
    const answer = result([row({ current: 100, total: 100 })]);
    const { service } = setup(answer);

    expect(await service.run({ asAtDate: '2026-10-31' })).toEqual(answer);
  });

  it('AP-05: reads the vendors the filter offers from the report’s own lookup', async () => {
    const choices = [{ contactId: 201, contactCode: 'STEELCO', contactName: 'Steelco Supplies' }];
    const { service, get } = setup(choices);

    expect(await service.vendors()).toEqual(choices);
    expect(get.mock.calls[0][0]).toBe('/api/statements/aged-payables-summary/vendors');
  });
});

describe('aged payables summary helpers', () => {
  it('AP-06: the grid has a row for each vendor in the server’s order, then the totals', () => {
    const rows = agedPayablesRows(
      result([
        row({ contactId: 202, contactName: 'Boltworks Ltd', current: 6000, bucket2: 40000, total: 46000 }),
        row({ contactId: null, contactCode: '', contactName: 'Not tied to a vendor', bucket4: 2500, total: 2500 }),
      ]),
    );

    expect(rows.map((r) => r.kind)).toEqual(['vendor', 'unassigned', 'total']);
    expect(rows.map((r) => r.name)).toEqual(['Boltworks Ltd', 'Not tied to a vendor', 'Total']);
    expect(new Set(rows.map((r) => r.key)).size).toBe(3);
  });

  it('AP-07: a nought reads blank on a vendor’s row and the totals keep theirs', () => {
    const rows = agedPayablesRows(result([row({ current: 100, bucket2: 50, total: 150 })]));

    expect([rows[0].current, rows[0].bucket1, rows[0].bucket2, rows[0].bucket3, rows[0].bucket4]).toEqual([100, null, 50, null, null]);
    expect([rows[1].current, rows[1].bucket1, rows[1].bucket2, rows[1].bucket3, rows[1].bucket4]).toEqual([100, 0, 50, 0, 0]);
    expect(rows[1].total).toBe(150);
  });

  it('AP-08: a negative amount is kept as it is so the debit note shows', () => {
    const rows = agedPayablesRows(result([row({ current: -1000, total: -1000 })]));

    expect(rows[0].current).toBe(-1000);
    expect(rows[0].total).toBe(-1000);
  });

  it('AP-09: with no vendors there are no rows, not even a total', () => {
    expect(agedPayablesRows(result([]))).toEqual([]);
  });

  it('AP-10: a row carries whether its vendor is an MSME, and the totals row never does', () => {
    const rows = agedPayablesRows(result([row({ contactId: 201, isMsme: true, bucket4: 15000, total: 15000 }), row({ contactId: 202, contactName: 'Boltworks Ltd' })]));

    expect(rows.map((r) => r.isMsme)).toEqual([true, false, false]);
  });

  it('AP-11: the MSME vendors listed are counted, and the link opens the Sec 43B(h) view', () => {
    expect(msmeCount(result([row({ isMsme: true }), row({ contactId: 202, isMsme: false }), row({ contactId: 203, isMsme: true })]))).toBe(2);
    expect(msmeCount(result([]))).toBe(0);
    expect(MSME_AGING_LINK).toBe('/reports/msme-aging');
  });

  it('AP-12: the sentence says what is owed, to how many vendors, and how much is overdue', () => {
    const answer = result([
      row({ contactId: 202, contactName: 'Boltworks Ltd', current: 6000, bucket2: 40000, total: 46000 }),
      row({ contactId: 201, current: 0, bucket1: 6000, bucket4: 15000, total: 21000 }),
      row({ contactId: null, contactName: 'Not tied to a vendor', bucket4: 2500, total: 2500 }),
    ]);

    // The row for what no vendor claims is not a vendor.
    expect(agedPayablesSentence(answer, money)).toBe('₹69500.00 is owed to 2 vendors; ₹63500.00 of bills is overdue.');
  });

  it('AP-13: the sentence says so when nothing is overdue, and counts one vendor in the singular', () => {
    const answer = result([row({ current: 300, total: 300 })]);

    expect(agedPayablesSentence(answer, money)).toBe('₹300.00 is owed to 1 vendor; nothing is overdue.');
  });

  it('AP-14: the filter offers the due date first and the bill date second', () => {
    expect(PAYABLE_AGE_BASIS_OPTIONS.map((o) => o.value)).toEqual(['DueDate', 'InvoiceDate']);
    expect(PAYABLE_AGE_BASIS_OPTIONS.map((o) => o.label)).toEqual(['Due date', 'Bill date']);
  });

  it('AP-15: a vendor reads as its code then its name', () => {
    expect(vendorChoiceLabel({ contactId: 201, contactCode: 'STEELCO', contactName: 'Steelco Supplies' })).toBe('STEELCO — Steelco Supplies');
  });
});
