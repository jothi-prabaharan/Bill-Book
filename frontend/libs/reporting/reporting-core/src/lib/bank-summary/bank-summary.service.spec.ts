import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  BankSummaryAccount,
  BankSummaryResult,
  accountDetail,
  bankKindLabel,
  bankSummaryRows,
  bankSummarySentence,
  hasForeignAccounts,
} from './bank-summary.models';
import { BankSummaryService } from './bank-summary.service';

/** The Bank Summary's calls and the helpers its page uses (TK-219). */

function setup(answer: unknown = { accounts: [] }) {
  const get = vi.fn().mockReturnValue(of(answer));

  TestBed.configureTestingModule({
    providers: [BankSummaryService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(BankSummaryService), get };
}

function paramsOf(call: unknown[]): HttpParams {
  return (call[1] as { params: HttpParams }).params;
}

const money = (value: number): string => `₹${value.toFixed(2)}`;

const account = (over: Partial<BankSummaryAccount> = {}): BankSummaryAccount => ({
  bankAccountId: 1,
  accountName: 'HDFC Current',
  accountNumber: '0123456789',
  accountType: 'Current',
  currencyCode: 'INR',
  isActive: true,
  isForeign: false,
  openingBalance: 10000,
  received: 5000,
  spent: 2000,
  revaluation: 0,
  closingBalance: 13000,
  ownOpeningBalance: null,
  ownReceived: null,
  ownSpent: null,
  ownClosingBalance: null,
  ...over,
});

const result = (accounts: BankSummaryAccount[], over: Partial<BankSummaryResult> = {}): BankSummaryResult => ({
  fromDate: '2026-10-01',
  toDate: '2026-10-31',
  baseCurrencyCode: 'INR',
  accounts,
  totalOpeningBalance: accounts.reduce((t, a) => t + a.openingBalance, 0),
  totalReceived: accounts.reduce((t, a) => t + a.received, 0),
  totalSpent: accounts.reduce((t, a) => t + a.spent, 0),
  totalRevaluation: accounts.reduce((t, a) => t + a.revaluation, 0),
  totalClosingBalance: accounts.reduce((t, a) => t + a.closingBalance, 0),
  ...over,
});

describe('BankSummaryService', () => {
  it('BS-01: runs with both dates and no bank account when none was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/bank-summary');
    const params = paramsOf(get.mock.calls[0]);
    expect(params.get('fromDate')).toBe('2026-10-01');
    expect(params.get('toDate')).toBe('2026-10-31');
    expect(params.has('bankAccountId')).toBe(false);
  });

  it('BS-02: sends the bank account that was chosen', async () => {
    const { service, get } = setup();

    await service.run({ fromDate: '2026-10-01', toDate: '2026-10-31', bankAccountId: 7 });

    expect(paramsOf(get.mock.calls[0]).get('bankAccountId')).toBe('7');
  });

  it('BS-03: reads the bank accounts from the Reconciliation Report’s list', async () => {
    const accounts = [{ bankAccountId: 1, accountName: 'HDFC', accountNumber: '1', currencyCode: 'INR', isActive: true }];
    const { service, get } = setup(accounts);

    expect(await service.accounts()).toEqual(accounts);
    expect(get.mock.calls[0][0]).toBe('/api/statements/bank-reconciliation/accounts');
  });
});

describe('Bank Summary helpers', () => {
  it('BS-04: a kind of account reads as a person would say it', () => {
    expect(bankKindLabel('OverDraft')).toBe('Overdraft');
    expect(bankKindLabel('CashCredit')).toBe('Cash credit');
    expect(bankKindLabel('CreditCard')).toBe('Credit card');
    expect(bankKindLabel('EEFC')).toBe('EEFC');
    expect(bankKindLabel('Savings')).toBe('Savings');
  });

  it('BS-05: the second line is the kind and the number, leaving out a number that is only a dash', () => {
    expect(accountDetail({ accountType: 'Current', accountNumber: '0123456789' })).toBe('Current · 0123456789');
    expect(accountDetail({ accountType: 'Cash', accountNumber: '-' })).toBe('Cash');
    expect(accountDetail({ accountType: 'Wallet', accountNumber: '' })).toBe('Wallet');
  });

  it('BS-06: one account is one row and several are followed by their totals', () => {
    const one = bankSummaryRows(result([account()]));
    expect(one.map((r) => r.kind)).toEqual(['account']);

    const two = bankSummaryRows(result([account(), account({ bankAccountId: 2, accountName: 'Petty cash', openingBalance: 500, received: 100, spent: 50, closingBalance: 550 })]));
    expect(two.map((r) => r.kind)).toEqual(['account', 'account', 'total']);
    expect(two[2]).toMatchObject({ name: 'Total', openingBalance: 10500, received: 5100, spent: 2050, closingBalance: 13550, currencyCode: 'INR' });
  });

  it('BS-07: no account is no row, and no revaluation reads blank', () => {
    expect(bankSummaryRows(result([]))).toEqual([]);

    const [row] = bankSummaryRows(result([account()]));
    expect(row.revaluation).toBeNull();

    const [revalued] = bankSummaryRows(result([account({ revaluation: -250, closingBalance: 12750 })]));
    expect(revalued.revaluation).toBe(-250);
  });

  it('BS-08: a foreign account carries its own-currency figures to the grid', () => {
    const [row] = bankSummaryRows(
      result([
        account({ currencyCode: 'USD', isForeign: true, ownOpeningBalance: 100, ownReceived: 20, ownSpent: 5, ownClosingBalance: 115 }),
      ]),
    );

    expect(row).toMatchObject({ isForeign: true, ownOpeningBalance: 100, ownReceived: 20, ownSpent: 5, ownClosingBalance: 115 });
  });

  it('BS-09: the page notes foreign accounts only when there are some', () => {
    expect(hasForeignAccounts(result([account()]))).toBe(false);
    expect(hasForeignAccounts(result([account(), account({ isForeign: true })]))).toBe(true);
  });

  it('BS-10: the period comes to a sentence that leaves out a revaluation of nothing', () => {
    expect(bankSummarySentence(result([account()]), money)).toBe(
      '1 account: opening ₹10000.00 + received ₹5000.00 − spent ₹2000.00 = closing ₹13000.00.',
    );

    expect(bankSummarySentence(result([account({ revaluation: -250, closingBalance: 12750 }), account({ bankAccountId: 2, revaluation: 100, closingBalance: 13100 })]), money)).toBe(
      '2 accounts: opening ₹20000.00 + received ₹10000.00 − spent ₹4000.00 − revaluation ₹150.00 = closing ₹25850.00.',
    );
  });
});
