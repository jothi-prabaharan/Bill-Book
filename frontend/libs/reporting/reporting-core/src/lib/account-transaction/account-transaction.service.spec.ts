import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  AccountTransactionAccount,
  AccountTransactionResult,
  AccountTransactionRow,
  DEFAULT_TRANSACTION_COLUMNS,
  TRANSACTION_COLUMNS,
  appendTransactionPage,
  foreignAmount,
  hasMoreTransactions,
  loadedPostings,
  sideOf,
  taxLabel,
  taxRateOptions,
  transactionRows,
  transactionSentence,
} from './account-transaction.models';
import { AccountTransactionService } from './account-transaction.service';

/** The Account Transaction report's calls and the helpers its page uses (TK-218). */

function setup(answer: unknown = { accounts: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [AccountTransactionService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(AccountTransactionService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const row = (over: Partial<AccountTransactionRow> = {}): AccountTransactionRow => ({
  ledgerId: 1,
  date: '2026-10-03',
  documentNo: 'INV/2627/0002',
  sourceCode: 'INV',
  transactionId: 70,
  journalId: null,
  relatedAccount: null,
  contactId: null,
  contactCode: null,
  contactName: null,
  description: null,
  reference: null,
  taxRate: null,
  taxName: null,
  permitNo: null,
  currencyCode: 'INR',
  exchangeRate: 1,
  debitSource: 1180,
  creditSource: 0,
  debit: 1180,
  credit: 0,
  runningBalance: 9180,
  ...over,
});

const account = (over: Partial<AccountTransactionAccount> = {}): AccountTransactionAccount => ({
  accountId: 1100,
  accountCode: '1100',
  accountName: 'Accounts Receivable',
  accountTypeId: 1,
  isContra: false,
  normalSide: 'Debit',
  openingBalance: 8000,
  closingBalance: 9615,
  totalDebit: 2015,
  totalCredit: 400,
  postingCount: 3,
  hasEarlierRows: false,
  hasLaterRows: false,
  rows: [],
  ...over,
});

const result = (over: Partial<AccountTransactionResult> = {}): AccountTransactionResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  financialYearStart: '2026-04-01',
  pageNumber: 1,
  pageSize: 100,
  totalPostings: 0,
  totalAccounts: 0,
  totalDebit: 0,
  totalCredit: 0,
  accounts: [],
  ...over,
});

describe('AccountTransactionService', () => {
  it('AT-01: runs with both dates and sends nothing that was not chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/account-transaction');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    for (const name of ['accountIds', 'contactId', 'taxGroupId', 'source', 'pageNumber', 'pageSize']) {
      expect(params.has(name)).toBe(false);
    }
  });

  it('AT-02: sends each chosen account once and the other filters as they are', async () => {
    const { service, get } = setup();

    await service.run({
      fromDate: '2026-10-01',
      toDate: '2026-10-31',
      accountIds: [4001, 1100],
      contactId: 7,
      taxGroupId: 90,
      source: 'INV',
      pageNumber: 2,
      pageSize: 50,
    });

    const params = paramsOf(get.mock.calls[0]);
    expect(params.getAll('accountIds')).toEqual(['4001', '1100']);
    expect(params.get('contactId')).toBe('7');
    expect(params.get('taxGroupId')).toBe('90');
    expect(params.get('source')).toBe('INV');
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('pageSize')).toBe('50');
  });

  it('AT-03: names accounts and contacts by code and the tax rates by their latest revision', async () => {
    const accounts = setup([{ accountId: 1, accountCode: '1001', accountName: 'Bank' }]);
    expect(await accounts.service.accounts()).toEqual([{ value: 1, label: '1001 — Bank' }]);
    expect(accounts.get.mock.calls[0][0]).toBe('/api/accounts');

    TestBed.resetTestingModule();
    const contacts = setup([{ contactId: 7, contactCode: 'C-7', displayName: 'Kaveri Constructions' }]);
    expect(await contacts.service.contacts()).toEqual([{ value: 7, label: 'C-7 — Kaveri Constructions' }]);

    TestBed.resetTestingModule();
    const taxes = setup([{ taxGroupId: 90, taxMasterId: 91, taxName: 'GST 18%', totalRate: 18, effectiveFrom: '2026-01-01', effectiveTo: null }]);
    expect(await taxes.service.taxRates()).toEqual([{ taxGroupId: 90, label: 'GST 18%' }]);
    expect(taxes.get.mock.calls[0][0]).toBe('/api/tax-masters');
  });
});

describe('Account Transaction helpers', () => {
  it('AT-04: the columns shown before a choice are the usual ones, in column order', () => {
    expect(DEFAULT_TRANSACTION_COLUMNS).toEqual(['date', 'documentNo', 'contact', 'description', 'debit', 'credit', 'balance']);
    expect(TRANSACTION_COLUMNS.map((c) => c.key)).toContain('permitNo');
    expect(new Set(TRANSACTION_COLUMNS.map((c) => c.key)).size).toBe(TRANSACTION_COLUMNS.length);
  });

  it('AT-05: a balance is marked with the side it is on', () => {
    expect(sideOf('Debit')).toBe('Dr');
    expect(sideOf('Credit')).toBe('Cr');
  });

  it('AT-06: a foreign posting is noted with its own amount at the booked rate, on either side', () => {
    expect(foreignAmount(row({ currencyCode: 'USD', exchangeRate: 83.5, debitSource: 10, creditSource: 0 }))).toBe('USD 10.00 @ 83.5');
    expect(foreignAmount(row({ currencyCode: 'USD', exchangeRate: 83.5, debitSource: 0, creditSource: 12.5 }))).toBe('USD 12.50 @ 83.5');
    expect(foreignAmount(row())).toBe('');
  });

  it('AT-07: a tax leg is named once, with its rate unless the name already says it', () => {
    expect(taxLabel(row({ taxName: 'GST 18%', taxRate: 18 }))).toBe('GST 18%');
    expect(taxLabel(row({ taxName: 'Output GST', taxRate: 18 }))).toBe('Output GST (18%)');
    expect(taxLabel(row({ taxName: 'GST', taxRate: 17.5 }))).toBe('GST (17.5%)');
    expect(taxLabel(row({ taxName: null, taxRate: 5 }))).toBe('5%');
    expect(taxLabel(row({ taxName: 'Exempt', taxRate: null }))).toBe('Exempt');
    expect(taxLabel(row())).toBe('');
  });

  it('AT-08: an account is its opening balance, its postings and its closing balance with the period totals', () => {
    const rows = transactionRows(account({ rows: [row({ ledgerId: 1, runningBalance: 9180 }), row({ ledgerId: 2, debit: 0, credit: 400, runningBalance: 8780 })] }));

    expect(rows.map((r) => r.kind)).toEqual(['opening', 'posting', 'posting', 'closing']);
    expect(rows[0]).toMatchObject({ description: 'Opening balance', balance: 8000, debit: null, credit: null });
    expect(rows[1]).toMatchObject({ documentNo: 'INV/2627/0002', debit: 1180, credit: null, balance: 9180 });
    expect(rows[2]).toMatchObject({ debit: null, credit: 400, balance: 8780 });
    expect(rows[3]).toMatchObject({ description: 'Closing balance', debit: 2015, credit: 400, balance: 9615 });
  });

  it('AT-09: the opening row waits for the page that has the first posting and the closing row for the last', () => {
    const firstPart = transactionRows(account({ hasLaterRows: true, rows: [row()] }));
    expect(firstPart.map((r) => r.kind)).toEqual(['opening', 'posting']);

    const lastPart = transactionRows(account({ hasEarlierRows: true, rows: [row({ ledgerId: 3 })] }));
    expect(lastPart.map((r) => r.kind)).toEqual(['posting', 'closing']);

    const middle = transactionRows(account({ hasEarlierRows: true, hasLaterRows: true, rows: [row()] }));
    expect(middle.map((r) => r.kind)).toEqual(['posting']);
  });

  it('AT-10: an account asked for with nothing posted shows its balance and no postings', () => {
    const rows = transactionRows(account({ openingBalance: 2500, closingBalance: 2500, totalDebit: 0, totalCredit: 0, postingCount: 0 }));

    expect(rows.map((r) => r.kind)).toEqual(['opening', 'closing']);
    expect(rows[1]).toMatchObject({ debit: null, credit: null, balance: 2500 });
  });

  it('AT-11: a posting carries its tax, foreign note and permit to the grid', () => {
    const [, posting] = transactionRows(
      account({ rows: [row({ taxName: 'GST 18%', taxRate: 18, permitNo: 'DL-77', currencyCode: 'USD', exchangeRate: 83.5, debitSource: 10, contactName: 'Kaveri' })] }),
    );

    expect(posting).toMatchObject({ tax: 'GST 18%', permitNo: 'DL-77', foreign: 'USD 10.00 @ 83.5', contactName: 'Kaveri' });
  });

  it('AT-12: there is more to load while the postings loaded are fewer than those the filters match', () => {
    const loaded = result({ totalPostings: 5, accounts: [account({ rows: [row(), row({ ledgerId: 2 })] })] });

    expect(loadedPostings(loaded)).toBe(2);
    expect(hasMoreTransactions(loaded)).toBe(true);
    expect(hasMoreTransactions(result({ totalPostings: 2, accounts: loaded.accounts }))).toBe(false);
    expect(hasMoreTransactions(result())).toBe(false);
  });

  it('AT-13: the next page goes under the first, and an account that runs over is one account', () => {
    const first = result({
      totalPostings: 4,
      accounts: [
        account({ accountId: 1001, accountCode: '1001', postingCount: 1, rows: [row({ ledgerId: 1 })] }),
        account({ accountId: 1100, hasLaterRows: true, rows: [row({ ledgerId: 2 }), row({ ledgerId: 3 })] }),
      ],
    });
    const next = result({
      pageNumber: 2,
      totalPostings: 4,
      accounts: [
        account({ accountId: 1100, hasEarlierRows: true, hasLaterRows: false, rows: [row({ ledgerId: 4 })] }),
        account({ accountId: 2001, accountCode: '2001', postingCount: 1, rows: [row({ ledgerId: 5 })] }),
      ],
    });

    const merged = appendTransactionPage(first, next);

    expect(merged.pageNumber).toBe(2);
    expect(merged.accounts.map((a) => a.accountId)).toEqual([1001, 1100, 2001]);
    expect(merged.accounts[1].rows.map((r) => r.ledgerId)).toEqual([2, 3, 4]);
    expect(merged.accounts[1].hasEarlierRows).toBe(false);
    expect(merged.accounts[1].hasLaterRows).toBe(false);
    expect(transactionRows(merged.accounts[1]).map((r) => r.kind)).toEqual(['opening', 'posting', 'posting', 'posting', 'closing']);

    // The pages that were loaded are not changed in place.
    expect(first.accounts[1].rows).toHaveLength(2);
    expect(first.accounts[1].hasLaterRows).toBe(true);
  });

  it('AT-14: a page that starts with another account only adds accounts', () => {
    const first = result({ accounts: [account({ accountId: 1001, rows: [row()] })] });
    const next = result({ pageNumber: 2, accounts: [account({ accountId: 1100, rows: [row({ ledgerId: 2 })] })] });

    expect(appendTransactionPage(first, next).accounts.map((a) => a.accountId)).toEqual([1001, 1100]);
  });

  it('AT-15: the period comes to a sentence with the postings, the accounts and both totals', () => {
    expect(transactionSentence(result({ totalPostings: 11, totalAccounts: 7, totalDebit: 5515, totalCredit: 5515 }), money)).toBe(
      '11 postings to 7 accounts: debits ₹5515.00, credits ₹5515.00.',
    );
    expect(transactionSentence(result({ totalPostings: 1, totalAccounts: 1, totalDebit: 180, totalCredit: 0 }), money)).toBe(
      '1 posting to 1 account: debits ₹180.00, credits ₹0.00.',
    );
  });

  it('AT-16: the tax filter offers one rate for each group, the revision in force naming it', () => {
    const options = taxRateOptions([
      { taxGroupId: 90, taxMasterId: 90, taxName: 'GST 17.5', totalRate: 17.5, effectiveFrom: '2025-01-01', effectiveTo: '2025-12-31' },
      { taxGroupId: 90, taxMasterId: 91, taxName: 'GST 18%', totalRate: 18, effectiveFrom: '2026-01-01', effectiveTo: null },
      { taxGroupId: 0, taxMasterId: 95, taxName: 'GST 5%', totalRate: 5, effectiveFrom: '2025-01-01', effectiveTo: null },
      { taxGroupId: 80, taxMasterId: 81, taxName: 'GST 12%', totalRate: 12, effectiveFrom: '2024-01-01', effectiveTo: '2024-12-31' },
      { taxGroupId: 80, taxMasterId: 82, taxName: 'GST 12%', totalRate: 12.5, effectiveFrom: '2025-01-01', effectiveTo: '2025-12-31' },
    ]);

    // A group with none still in force is named by the one that began last; a legacy row with no group is its own.
    expect(options).toEqual([
      { taxGroupId: 80, label: 'GST 12% (12.5%)' },
      { taxGroupId: 90, label: 'GST 18%' },
      { taxGroupId: 95, label: 'GST 5%' },
    ]);
  });
});
