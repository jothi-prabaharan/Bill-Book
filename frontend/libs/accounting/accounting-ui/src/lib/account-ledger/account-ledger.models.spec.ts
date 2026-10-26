import { describe, expect, it } from 'vitest';
import {
  GeneralLedgerReportResult,
  GeneralLedgerReportRow,
  GeneralLedgerSummaryResult,
  foreignNote,
  isProfitAndLoss,
  ledgerEquation,
  ledgerRows,
  sideLetter,
  summaryRows,
} from './account-ledger.models';

/** The General ledger's grid rows, its summary rows and its helpers (TK-216). */

const money = (value: number): string => `₹${value.toFixed(2)}`;

const row = (over: Partial<GeneralLedgerReportRow>): GeneralLedgerReportRow => ({
  ledgerId: 1,
  ledgerDate: '2026-10-03',
  documentNo: 'INV/2627/0002',
  transactionTypeCode: 'INV',
  transactionId: 42,
  transactionDetailId: 1,
  journalId: null,
  journalNo: null,
  subAccountName: null,
  contactId: null,
  contactName: null,
  projectId: null,
  projectName: null,
  description: 'Invoice to Customer',
  debitAmount: 0,
  creditAmount: 0,
  debitAmountBase: 0,
  creditAmountBase: 0,
  currencyCode: 'INR',
  exchangeRate: 1,
  runningBalance: 0,
  ...over,
});

const ledger = (over: Partial<GeneralLedgerReportResult> = {}): GeneralLedgerReportResult => ({
  accountId: 4001,
  accountCode: '4001',
  accountName: 'Sales',
  accountTypeId: 4,
  isContra: false,
  normalSide: 'Credit',
  from: '2026-10-01',
  to: '2026-10-31',
  financialYearStart: '2026-04-01',
  contactId: null,
  projectId: null,
  openingBalance: 5000,
  closingBalance: 11600,
  totalDebit: 400,
  totalCredit: 7000,
  rows: [
    row({ ledgerId: 1, creditAmountBase: 7000, creditAmount: 7000, runningBalance: 12000, contactName: 'Customer' }),
    row({ ledgerId: 2, ledgerDate: '2026-10-12', documentNo: 'CN/2627/0001', transactionTypeCode: 'CRN', debitAmountBase: 400, debitAmount: 400, runningBalance: 11600 }),
  ],
  ...over,
});

describe('ledgerRows', () => {
  it('GL-01: the opening balance, each posting in order, then the closing balance', () => {
    const rows = ledgerRows(ledger());

    expect(rows.map((r) => r.kind)).toEqual(['opening', 'posting', 'posting', 'closing']);
    expect(rows[0].description).toBe('Opening balance');
    expect(rows[0].balance).toBe(5000);
    expect(rows[3].description).toBe('Closing balance');
    expect(rows[3].balance).toBe(11600);
  });

  it('GL-02: the opening and closing rows have no date, and the closing row carries the totals', () => {
    const rows = ledgerRows(ledger());

    expect(rows[0].ledgerDate).toBeNull();
    expect(rows[3].ledgerDate).toBeNull();
    expect([rows[3].debit, rows[3].credit]).toEqual([400, 7000]);
    expect([rows[0].debit, rows[0].credit]).toEqual([null, null]);
  });

  it('GL-03: a posting keeps its document, contact and balance, and an empty side reads blank', () => {
    const posting = ledgerRows(ledger())[1];

    expect(posting.documentNo).toBe('INV/2627/0002');
    expect(posting.sourceCode).toBe('INV');
    expect(posting.contactName).toBe('Customer');
    expect(posting.debit).toBeNull();
    expect(posting.credit).toBe(7000);
    expect(posting.balance).toBe(12000);
  });

  it('GL-04: an account with no postings is its opening and closing rows alone', () => {
    const rows = ledgerRows(ledger({ rows: [], closingBalance: 5000, totalDebit: 0, totalCredit: 0 }));

    expect(rows.map((r) => r.kind)).toEqual(['opening', 'closing']);
    expect([rows[1].debit, rows[1].credit]).toEqual([null, null]);
  });

  it('GL-05: a journal row keeps the journal to link to', () => {
    const rows = ledgerRows(ledger({ rows: [row({ journalId: 11, journalNo: 'JV-0011', documentNo: 'JRN-9', transactionTypeCode: 'JRN' })] }));

    expect(rows[1].journalId).toBe(11);
  });
});

describe('foreignNote', () => {
  it('GL-06: a posting in another currency says what it was and the rate it was booked at', () => {
    expect(foreignNote(row({ currencyCode: 'USD', exchangeRate: 83.5, debitAmount: 100 }))).toBe('USD 100.00 at 83.5');
    expect(foreignNote(row({ currencyCode: 'USD', exchangeRate: 83.5, creditAmount: 12.5 }))).toBe('USD 12.50 at 83.5');
  });

  it('GL-06b: the amount is written as the caller says, so a page can pass the branch’s format (TK-262)', () => {
    const branch = (value: number): string => (value === 1234567.5 ? '12,34,567.50' : String(value));

    expect(foreignNote(row({ currencyCode: 'USD', exchangeRate: 83.5, debitAmount: 1234567.5 }), branch)).toBe('USD 12,34,567.50 at 83.5');
  });

  it('GL-07: a posting at a rate of one, or with no currency, says nothing', () => {
    expect(foreignNote(row({ currencyCode: 'INR', exchangeRate: 1, debitAmount: 100 }))).toBeNull();
    expect(foreignNote(row({ currencyCode: '', exchangeRate: 2, debitAmount: 100 }))).toBeNull();
  });
});

describe('summaryRows', () => {
  const summary = (): GeneralLedgerSummaryResult => ({
    from: '2026-10-01',
    to: '2026-10-31',
    financialYearStart: '2026-04-01',
    lines: [
      { accountId: 1, accountCode: '1001', accountName: 'Bank', accountTypeId: 1, isContra: false, normalSide: 'Debit', openingBalance: 15000, totalDebit: 7000, totalCredit: 400, closingBalance: 21600 },
      { accountId: 2, accountCode: '4001', accountName: 'Sales', accountTypeId: 4, isContra: false, normalSide: 'Credit', openingBalance: 5000, totalDebit: 0, totalCredit: 7000, closingBalance: 12000 },
    ],
    totalDebit: 7000,
    totalCredit: 7400,
  });

  it('GL-08: each account then the totals, with an empty side blank and the totals keeping both', () => {
    const rows = summaryRows(summary());

    expect(rows.map((r) => r.kind)).toEqual(['line', 'line', 'total']);
    expect([rows[1].debit, rows[1].credit]).toEqual([null, 7000]);
    expect([rows[2].debit, rows[2].credit]).toEqual([7000, 7400]);
  });

  it('GL-09: each line keeps its side, and the totals add no balances because they stand on different sides', () => {
    const rows = summaryRows(summary());

    expect(rows[0].side).toBe('Debit');
    expect(rows[1].side).toBe('Credit');
    expect(rows[2].side).toBeNull();
    expect([rows[2].openingBalance, rows[2].closingBalance]).toEqual([null, null]);
  });

  it('GL-10: no accounts means no rows and no totals', () => {
    expect(summaryRows({ ...summary(), lines: [], totalDebit: 0, totalCredit: 0 })).toEqual([]);
  });
});

describe('sideLetter, isProfitAndLoss and ledgerEquation', () => {
  it('GL-11: Dr for the debit side and Cr for the credit side', () => {
    expect(sideLetter('Debit')).toBe('Dr');
    expect(sideLetter('Credit')).toBe('Cr');
  });

  it('GL-12: income and expense are the accounts whose balance starts again each year', () => {
    expect([1, 2, 3, 4, 5].map(isProfitAndLoss)).toEqual([false, false, false, true, true]);
  });

  it('GL-13: the equation says what grew the account and what reduced it, by the account\'s side', () => {
    expect(ledgerEquation(ledger(), money)).toBe('Opening ₹5000.00 + credits ₹7000.00 − debits ₹400.00 = closing ₹11600.00.');

    const bank = ledger({ normalSide: 'Debit', openingBalance: 15000, closingBalance: 21600, totalDebit: 7000, totalCredit: 400 });
    expect(ledgerEquation(bank, money)).toBe('Opening ₹15000.00 + debits ₹7000.00 − credits ₹400.00 = closing ₹21600.00.');
  });
});
