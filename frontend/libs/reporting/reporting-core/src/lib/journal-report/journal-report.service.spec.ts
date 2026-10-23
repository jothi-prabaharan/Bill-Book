import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  JournalReportEntry,
  JournalReportResult,
  appendPage,
  auditNote,
  blankZero,
  hasMore,
  journalVerdict,
  reversalNote,
  sourceName,
  sourceNote,
} from './journal-report.models';
import { JournalReportService } from './journal-report.service';

/** The Journal Report's calls and the helpers its page uses (TK-217). */

function setup(answer: unknown = { journals: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [JournalReportService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(JournalReportService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;
const when = (value: string): string => value.slice(0, 10);

const entry = (over: Partial<JournalReportEntry> = {}): JournalReportEntry => ({
  sourceCode: 'JRN',
  transactionId: 11,
  journalId: 11,
  date: '2026-10-03',
  number: 'JV-0011',
  reference: null,
  narration: null,
  status: 'Posted',
  createdByName: null,
  createdAt: null,
  postedByName: null,
  postedAt: null,
  reversesNumber: null,
  reversedByNumber: null,
  lines: [],
  totalDebit: 100,
  totalCredit: 100,
  isBalanced: true,
  ...over,
});

const result = (over: Partial<JournalReportResult> = {}): JournalReportResult => ({
  fromDate: '2026-04-01',
  toDate: '2026-10-31',
  pageNumber: 1,
  pageSize: 25,
  totalJournals: 2,
  totalDebit: 300,
  totalCredit: 300,
  isBalanced: true,
  journals: [entry(), entry({ transactionId: 12, number: 'JV-0012' })],
  ...over,
});

describe('JournalReportService', () => {
  it('JR-01: asks its own endpoint for the period alone when nothing else is chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-04-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/journal-report');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-04-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['source', 'status', 'accountId', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('JR-02: sends the source, status, account and page when given', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-04-01',
      toDate: '2026-10-31',
      source: 'INV',
      status: 'Reversed',
      accountId: 1001,
      pageNumber: 3,
      pageSize: 50,
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('source')).toBe('INV');
    expect(params.get('status')).toBe('Reversed');
    expect(params.get('accountId')).toBe('1001');
    expect(params.get('pageNumber')).toBe('3');
    expect(params.get('pageSize')).toBe('50');
  });

  it('JR-03: a blank source, status or account is not sent', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-04-01', toDate: '2026-10-31', source: '', status: null, accountId: null });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.has('source')).toBe(false);
    expect(params.has('status')).toBe(false);
    expect(params.has('accountId')).toBe(false);
  });

  it('JR-04: the sources are the document types that post to the ledger, by name', async () => {
    const { service, get } = setup([{ code: 'INV', name: 'Invoice' }]);

    expect(await service.sources()).toEqual([{ code: 'INV', name: 'Invoice' }]);
    expect(get.mock.calls[0][0]).toBe('/api/master/transaction-types');
    expect(paramsOf(get.mock.calls[0]).get('postingOnly')).toBe('true');
  });

  it('JR-05: the accounts are options of code and name', async () => {
    const { service, get } = setup([{ accountId: 4001, accountCode: '4001', accountName: 'Sales' }]);

    expect(await service.accounts()).toEqual([{ value: 4001, label: '4001 — Sales' }]);
    expect(get.mock.calls[0][0]).toBe('/api/accounts');
  });
});

describe('journal helpers', () => {
  it('JR-06: a source is named from Master, or by its code when Master does not name it', () => {
    const sources = [{ code: 'INV', name: 'Invoice' }];

    expect(sourceName('INV', sources)).toBe('Invoice');
    expect(sourceName('XYZ', sources)).toBe('XYZ');
  });

  it('JR-07: a reversal says what it reverses, a reversed journal what reversed it, and an ordinary one nothing', () => {
    expect(reversalNote({ reversesNumber: 'JV-0011', reversedByNumber: null })).toBe('Reverses JV-0011');
    expect(reversalNote({ reversesNumber: null, reversedByNumber: 'JV-0012' })).toBe('Reversed by JV-0012');
    expect(reversalNote({ reversesNumber: 'JV-0010', reversedByNumber: 'JV-0012' })).toBe('Reverses JV-0010. Reversed by JV-0012');
    expect(reversalNote({ reversesNumber: null, reversedByNumber: null })).toBeNull();
  });

  it('JR-08: a journal says who created it and who posted it', () => {
    expect(
      auditNote(
        { createdByName: 'Asha', createdAt: '2026-10-03T09:00:00Z', postedByName: 'Ravi', postedAt: '2026-10-04T10:00:00Z' },
        when,
      ),
    ).toBe('Created by Asha on 2026-10-03. Posted by Ravi on 2026-10-04');
  });

  it("JR-09: a document's own posting, with one person and one moment, says it once as posted", () => {
    expect(
      auditNote(
        { createdByName: 'Asha', createdAt: '2026-10-03T09:00:00Z', postedByName: 'Asha', postedAt: '2026-10-03T09:00:00Z' },
        when,
      ),
    ).toBe('Posted by Asha on 2026-10-03');
  });

  it('JR-10: a draft has only been created, and a journal with no names says nothing of who', () => {
    expect(auditNote({ createdByName: 'Asha', createdAt: '2026-10-03T09:00:00Z', postedByName: null, postedAt: null }, when)).toBe(
      'Created by Asha on 2026-10-03',
    );
    expect(auditNote({ createdByName: null, createdAt: null, postedByName: null, postedAt: null }, when)).toBe('');
  });

  it('JR-11: a line in another currency is noted with its own amount at the booked rate, on either side', () => {
    expect(sourceNote({ currencyCode: 'USD', exchangeRate: 83.5, debitSource: 10, creditSource: 0 })).toBe('USD 10.00 @ 83.5');
    expect(sourceNote({ currencyCode: 'USD', exchangeRate: 83.5, debitSource: 0, creditSource: 12.5 })).toBe('USD 12.50 @ 83.5');
    expect(sourceNote({ currencyCode: 'INR', exchangeRate: 1, debitSource: 100, creditSource: 0 })).toBe('');
  });

  it('JR-12: an empty side reads blank, and a figure is kept', () => {
    expect(blankZero(0)).toBeNull();
    expect(blankZero(250)).toBe(250);
  });

  it('JR-13: the next page goes under those loaded, with the totals of the latest answer', () => {
    const loaded = result({ journals: [entry()] });
    const next = result({ pageNumber: 2, journals: [entry({ transactionId: 12, number: 'JV-0012' })] });

    const joined = appendPage(loaded, next);

    expect(joined.journals.map((j) => j.number)).toEqual(['JV-0011', 'JV-0012']);
    expect(joined.pageNumber).toBe(2);
    expect(joined.totalJournals).toBe(2);
  });

  it('JR-14: there is more to load while the journals shown are fewer than those the filters match', () => {
    expect(hasMore({ totalJournals: 30, journals: new Array(25).fill(entry()) })).toBe(true);
    expect(hasMore({ totalJournals: 25, journals: new Array(25).fill(entry()) })).toBe(false);
  });

  it('JR-15: journals that balance are said to, with the figure', () => {
    const verdict = journalVerdict(result(), money);

    expect(verdict.isBalanced).toBe(true);
    expect(verdict.text).toBe('2 journals: debits and credits both come to ₹300.00.');
    expect(journalVerdict(result({ totalJournals: 1 }), money).text).toBe('1 journal: debits and credits both come to ₹300.00.');
  });

  it('JR-16: journals that do not balance say by how much, positive however the sign runs', () => {
    const verdict = journalVerdict(result({ isBalanced: false, totalDebit: 300, totalCredit: 325 }), money);

    expect(verdict.isBalanced).toBe(false);
    expect(verdict.text).toContain('debits come to ₹300.00 and credits to ₹325.00, out by ₹25.00');
  });

  it('JR-17: no journals is not an imbalance', () => {
    const verdict = journalVerdict(result({ totalJournals: 0, journals: [], totalDebit: 0, totalCredit: 0 }), money);

    expect(verdict).toEqual({ isBalanced: true, text: 'No journals match these filters.' });
  });
});
