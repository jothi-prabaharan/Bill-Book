import { describe, expect, it } from 'vitest';
import {
  TrialBalanceReportGroup,
  TrialBalanceReportLine,
  TrialBalanceReportResult,
  TrialBalanceReportTotals,
  accountTypeTitle,
  sameDayLastYear,
  samePeriodLastYear,
  trialBalanceRows,
  trialBalanceVerdict,
} from './trial-balance.models';

/** The Trial Balance's grid rows, its verdict in words and the comparison-date helpers (TK-215). */

const money = (value: number): string => `₹${value.toFixed(2)}`;

const totals = (over: Partial<TrialBalanceReportTotals> = {}): TrialBalanceReportTotals => ({
  openingDebit: 0,
  openingCredit: 0,
  movementDebit: 0,
  movementCredit: 0,
  closingDebit: 0,
  closingCredit: 0,
  compareClosingDebit: null,
  compareClosingCredit: null,
  ...over,
});

const line = (over: Partial<TrialBalanceReportLine>): TrialBalanceReportLine => ({
  accountId: 1,
  accountCode: '1000',
  accountName: 'Cash',
  accountTypeId: 1,
  isContra: false,
  isComputed: false,
  openingDebit: 0,
  openingCredit: 0,
  movementDebit: 0,
  movementCredit: 0,
  closingDebit: 0,
  closingCredit: 0,
  compareClosingDebit: null,
  compareClosingCredit: null,
  ...over,
});

const result = (groups: TrialBalanceReportGroup[], over: Partial<TrialBalanceReportResult> = {}): TrialBalanceReportResult => ({
  from: '2026-07-01',
  to: '2026-07-31',
  compareFrom: null,
  compareTo: null,
  financialYearStart: '2026-04-01',
  compareFinancialYearStart: null,
  groups,
  totals: totals(),
  openingDifference: 0,
  movementDifference: 0,
  closingDifference: 0,
  compareClosingDifference: null,
  isBalanced: true,
  ...over,
});

describe('trialBalanceRows', () => {
  const cash = line({ accountId: 1, accountName: 'Cash', openingDebit: 1000, movementDebit: 500, movementCredit: 200, closingDebit: 1300 });
  const capital = line({
    accountId: 2, accountCode: '3000', accountName: 'Capital', accountTypeId: 3,
    openingCredit: 1000, closingCredit: 1000,
  });
  const sales = line({
    accountId: 3, accountCode: '4000', accountName: 'Sales', accountTypeId: 4,
    movementCredit: 500, closingCredit: 500,
  });

  const groups: TrialBalanceReportGroup[] = [
    { accountTypeId: 1, lines: [cash], totals: totals({ openingDebit: 1000, movementDebit: 500, movementCredit: 200, closingDebit: 1300 }) },
    { accountTypeId: 3, lines: [capital], totals: totals({ openingCredit: 1000, closingCredit: 1000 }) },
    { accountTypeId: 4, lines: [sales], totals: totals({ movementCredit: 500, closingCredit: 500 }) },
  ];

  it('TB-01: each type is a heading, its accounts and its subtotal, then the totals', () => {
    const rows = trialBalanceRows(result(groups, { totals: totals({ closingDebit: 1300, closingCredit: 1500 }) }));

    expect(rows.map((r) => `${r.kind}:${r.accountName}`)).toEqual([
      'group:Assets',
      'line:Cash',
      'subtotal:Total assets',
      'group:Equity',
      'line:Capital',
      'subtotal:Total equity',
      'group:Income',
      'line:Sales',
      'subtotal:Total income',
      'total:Total',
    ]);
  });

  it('TB-02: an empty side of an account is blank and a figure is kept', () => {
    const row = trialBalanceRows(result(groups)).find((r) => r.accountName === 'Cash')!;

    expect(row.openingDebit).toBe(1000);
    expect(row.openingCredit).toBeNull();
    expect(row.movementDebit).toBe(500);
    expect(row.movementCredit).toBe(200);
    expect(row.closingDebit).toBe(1300);
    expect(row.closingCredit).toBeNull();
  });

  it('TB-03: a subtotal leaves its empty side blank, and the grand total shows both sides', () => {
    const rows = trialBalanceRows(result(groups, { totals: totals({ closingDebit: 1300, closingCredit: 1500 }) }));
    const subtotal = rows.find((r) => r.accountName === 'Total assets')!;
    const total = rows.find((r) => r.kind === 'total')!;

    expect(subtotal.closingDebit).toBe(1300);
    expect(subtotal.closingCredit).toBeNull();
    expect(total.openingDebit).toBe(0);
    expect(total.closingDebit).toBe(1300);
    expect(total.closingCredit).toBe(1500);
  });

  it('TB-04: a contra account and the worked-out line keep their flags', () => {
    const contra = line({ accountId: 9, accountName: 'Sales returns', accountTypeId: 4, isContra: true, closingDebit: 50 });
    const carried = line({
      accountId: 0, accountCode: '', accountName: 'Profit of earlier years', accountTypeId: 3,
      isComputed: true, openingCredit: 400, closingCredit: 400,
    });

    const rows = trialBalanceRows(result([
      { accountTypeId: 3, lines: [carried], totals: totals() },
      { accountTypeId: 4, lines: [contra], totals: totals() },
    ]));

    expect(rows.find((r) => r.accountName === 'Sales returns')!.isContra).toBe(true);
    const computed = rows.find((r) => r.accountName === 'Profit of earlier years')!;
    expect(computed.isComputed).toBe(true);
    expect(computed.accountId).toBe(0);
  });

  it('TB-05: the compared period fills the last two columns, blank on a side with nothing', () => {
    const compared = line({ closingDebit: 1300, compareClosingDebit: 900, compareClosingCredit: 0 });
    const rows = trialBalanceRows(result(
      [{ accountTypeId: 1, lines: [compared], totals: totals({ compareClosingDebit: 900, compareClosingCredit: 0 }) }],
      { compareFrom: '2025-07-01', compareTo: '2025-07-31' },
    ));

    const row = rows.find((r) => r.kind === 'line')!;
    expect(row.compareDebit).toBe(900);
    expect(row.compareCredit).toBeNull();
    expect(rows.find((r) => r.kind === 'total')!.compareCredit).toBe(0);
  });

  it('TB-06: no accounts means no rows and no totals', () => {
    expect(trialBalanceRows(result([]))).toEqual([]);
  });
});

describe('trialBalanceVerdict', () => {
  it('TB-07: books that agree are said to be in balance, with the figure', () => {
    const verdict = trialBalanceVerdict(result([], { totals: totals({ closingDebit: 1500, closingCredit: 1500 }) }), money);

    expect(verdict.isBalanced).toBe(true);
    expect(verdict.text).toContain('in balance');
    expect(verdict.text).toContain('₹1500.00');
  });

  it('TB-08: books that disagree say where and by how much, positive however the sign runs', () => {
    const verdict = trialBalanceVerdict(
      result([], { isBalanced: false, openingDifference: 0, movementDifference: -25, closingDifference: 25 }),
      money,
    );

    expect(verdict.isBalanced).toBe(false);
    expect(verdict.text).toContain('out of balance');
    expect(verdict.text).toContain("the period's postings by ₹25.00");
    expect(verdict.text).toContain('the closing balances by ₹25.00');
    expect(verdict.text).not.toContain('opening balances');
  });

  it('TB-09: a disagreement only in the compared period is named as such', () => {
    const verdict = trialBalanceVerdict(
      result([], { isBalanced: false, compareClosingDifference: 10 }),
      money,
    );

    expect(verdict.text).toContain("the compared period's closing balances by ₹10.00");
  });
});

describe('account types and comparison dates', () => {
  it('TB-10: the five fixed types have their names, and an unknown one is not left blank', () => {
    expect([1, 2, 3, 4, 5].map(accountTypeTitle)).toEqual(['Assets', 'Liabilities', 'Equity', 'Income', 'Expenses']);
    expect(accountTypeTitle(9)).toBe('Account type 9');
  });

  it('TB-11: the same days a year earlier, and 29 February becomes 28 February', () => {
    expect(sameDayLastYear('2026-10-31')).toBe('2025-10-31');
    expect(sameDayLastYear('2028-02-29')).toBe('2027-02-28');
    expect(samePeriodLastYear('2026-04-01', '2026-10-22')).toEqual({ from: '2025-04-01', to: '2025-10-22' });
  });
});
