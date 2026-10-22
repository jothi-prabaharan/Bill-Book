import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  BudgetMonthlyTrendCell,
  BudgetMonthlyTrendLine,
  BudgetMonthlyTrendReportResult,
  BudgetMonthlyTrendSection,
  cellValue,
  monthHeader,
  trendLine,
  trendLineOptions,
  trendMeasuresOf,
  trendMonthRows,
  trendRowsOf,
} from './budget-monthly-trend-report.models';
import { BudgetMonthlyTrendReportService } from './budget-monthly-trend-report.service';

/** Budget Monthly Trend's call, and the row and line helpers its page uses (TK-214). */

function setup() {
  const get = vi.fn().mockReturnValue(of({ months: [] }));

  TestBed.configureTestingModule({
    providers: [BudgetMonthlyTrendReportService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(BudgetMonthlyTrendReportService), get };
}

/** Three months of actuals, then nine of budget alone: the as-at date is in June. */
function cells(budget: number, actual: number): BudgetMonthlyTrendCell[] {
  return Array.from({ length: 12 }, (_, i) =>
    i < 3
      ? { budget, actual, variance: actual - budget, variancePercent: ((actual - budget) / budget) * 100 }
      : { budget, actual: null, variance: null, variancePercent: null },
  );
}

const ytd = (budget: number, actual: number): BudgetMonthlyTrendCell => ({
  budget: budget * 3,
  actual: actual * 3,
  variance: (actual - budget) * 3,
  variancePercent: ((actual - budget) / budget) * 100,
});

function line(id: number, code: string, name: string, budget: number, actual: number): BudgetMonthlyTrendLine {
  return { accountId: id, accountCode: code, accountName: name, isContra: false, months: cells(budget, actual), yearToDate: ytd(budget, actual), yearBudget: budget * 12 };
}

function section(name: BudgetMonthlyTrendSection['section'], title: string, lines: BudgetMonthlyTrendLine[]): BudgetMonthlyTrendSection {
  const budget = lines.reduce((sum, l) => sum + l.months[0].budget, 0);
  const actual = lines.reduce((sum, l) => sum + (l.months[0].actual ?? 0), 0);
  return { section: name, title, lines, months: cells(budget || 1, actual), yearToDate: ytd(budget || 1, actual), yearBudget: budget * 12 };
}

const result: BudgetMonthlyTrendReportResult = {
  budget: { budgetId: 1, name: 'FY 2026-27', financialYearStart: '2026-04-01', financialYearEnd: '2027-03-31', status: 'Approved', isBranchDefault: true, byProject: false },
  asOfDate: '2026-06-15',
  asOfPeriod: 3,
  fromDate: '2026-04-01',
  throughDate: '2026-06-30',
  months: Array.from({ length: 12 }, (_, i) => ({
    period: i + 1,
    fromDate: `${i < 9 ? 2026 : 2027}-${String(((i + 3) % 12) + 1).padStart(2, '0')}-01`,
    toDate: '',
    isFuture: i >= 3,
  })),
  sections: [
    section('Revenue', 'Revenue', [line(1, '4001', 'Sales', 100, 110)]),
    section('CostOfSales', 'Cost of sales', [line(2, '5001', 'Cost of Goods Sold', 60, 65)]),
    section('OtherIncome', 'Other income', []),
    section('Expenses', 'Expenses', [line(3, '6001', 'Rent', 10, 10), line(4, '6002', 'Salaries', 8, 9)]),
  ],
  grossProfit: { months: cells(40, 45), yearToDate: ytd(40, 45), yearBudget: 480 },
  netProfit: { months: cells(22, 26), yearToDate: ytd(22, 26), yearBudget: 264 },
};

describe('BudgetMonthlyTrendReportService', () => {
  it('BMT-01: runs its own endpoint with the budget and the as-at date', async () => {
    const { service, get } = setup();

    await service.run({ budgetId: 7, asOfDate: '2026-06-15' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/budget-monthly-trend');
    const params = (get.mock.calls[0][1] as { params: HttpParams }).params;
    expect(params.get('budgetId')).toBe('7');
    expect(params.get('asOfDate')).toBe('2026-06-15');
  });
});

describe('monthHeader, trendMeasuresOf and cellValue', () => {
  it('BMT-02: a month heads its column as its short name and year, in any year of the budget', () => {
    expect(monthHeader('2026-04-01')).toBe('Apr 26');
    expect(monthHeader('2027-03-01')).toBe('Mar 27');
    expect(monthHeader('2026-01-01')).toBe('Jan 26');
    expect(monthHeader('2030-12-01')).toBe('Dec 30');
  });

  it('BMT-03: "all" is the three measures in order and each other choice is itself', () => {
    expect(trendMeasuresOf('All')).toEqual(['Budget', 'Actual', 'Variance']);
    expect(trendMeasuresOf('Actual')).toEqual(['Actual']);
  });

  it('BMT-04: a cell gives its budget, actual or variance, and null where there is no actual yet', () => {
    const month = cells(100, 110);
    expect(cellValue(month[0], 'Budget')).toBe(100);
    expect(cellValue(month[0], 'Actual')).toBe(110);
    expect(cellValue(month[0], 'Variance')).toBe(10);
    expect(cellValue(month[5], 'Budget')).toBe(100);
    expect(cellValue(month[5], 'Actual')).toBeNull();
    expect(cellValue(month[5], 'Variance')).toBeNull();
  });
});

describe('trendRowsOf', () => {
  it('BMT-05: showing all three gives each account three rows, the name drawn in full on the first', () => {
    const rows = trendRowsOf(result, 'All');
    const sales = rows.filter((r) => r.key.startsWith('a1-'));

    expect(sales.map((r) => r.measure)).toEqual(['Budget', 'Actual', 'Variance']);
    expect(sales.map((r) => r.first)).toEqual([true, false, false]);
    expect(sales.every((r) => r.accountName === 'Sales' && r.accountCode === '4001')).toBe(true);
    expect(sales[0].months.slice(0, 3)).toEqual([100, 100, 100]);
    expect(sales[1].months.slice(0, 3)).toEqual([110, 110, 110]);
    expect(sales[2].months.slice(0, 3)).toEqual([10, 10, 10]);
  });

  it('BMT-06: showing one measure gives one row per account, and the months after the as-at month are null for actual and variance only', () => {
    const rows = trendRowsOf(result, 'Actual');
    const sales = rows.filter((r) => r.key.startsWith('a1-'));

    expect(sales).toHaveLength(1);
    expect(sales[0].months.slice(3).every((v) => v === null)).toBe(true);
    expect(trendRowsOf(result, 'Budget').find((r) => r.key.startsWith('a1-'))!.months.slice(3)).toEqual(Array(9).fill(100));
    expect(sales[0].future.slice(0, 4)).toEqual([false, false, false, true]);
  });

  it('BMT-07: the year to date, its percentage on the variance row only, and the full-year budget on the budget row only', () => {
    const rows = trendRowsOf(result, 'All');
    const [budget, actual, variance] = rows.filter((r) => r.key.startsWith('a1-'));

    expect(budget.yearToDate).toBe(300);
    expect(budget.yearBudget).toBe(1200);
    expect(actual.yearToDate).toBe(330);
    expect(actual.yearBudget).toBeNull();
    expect(variance.yearToDate).toBe(30);
    expect(variance.yearToDatePercent).toBeCloseTo(10, 5);
    expect(budget.yearToDatePercent).toBeNull();
  });

  it('BMT-08: a heading for each section with accounts, then its accounts and total; an empty section is left out', () => {
    const rows = trendRowsOf(result, 'Variance');

    expect(rows.filter((r) => r.kind === 'section').map((r) => r.accountName)).toEqual(['Revenue', 'Cost of sales', 'Expenses']);
    expect(rows.map((r) => r.key)).toEqual([
      'Revenue-heading', 'a1-Variance', 'Revenue-total-Variance',
      'CostOfSales-heading', 'a2-Variance', 'CostOfSales-total-Variance', 'gross-Variance',
      'Expenses-heading', 'a3-Variance', 'a4-Variance', 'Expenses-total-Variance',
      'net-Variance',
    ]);
  });

  it('BMT-09: gross profit follows cost of sales, or revenue when cost of sales has no accounts, and net profit ends', () => {
    const noCost: BudgetMonthlyTrendReportResult = { ...result, sections: result.sections.map((s) => (s.section === 'CostOfSales' ? { ...s, lines: [] } : s)) };
    const keys = trendRowsOf(noCost, 'Variance').map((r) => r.key);

    expect(keys.indexOf('gross-Variance')).toBe(keys.indexOf('Revenue-total-Variance') + 1);
    expect(keys[keys.length - 1]).toBe('net-Variance');
  });

  it('BMT-10: with no accounts at all only net profit is left', () => {
    const empty: BudgetMonthlyTrendReportResult = { ...result, sections: result.sections.map((s) => ({ ...s, lines: [] })) };

    expect(trendRowsOf(empty, 'All').map((r) => r.kind)).toEqual(['profit', 'profit', 'profit']);
  });
});

describe('the phone view', () => {
  it('BMT-11: the lines to choose from are net and gross profit, then each section total with its accounts', () => {
    expect(trendLineOptions(result).map((o) => o.label)).toEqual([
      'Net profit', 'Gross profit',
      'Total revenue', '4001 · Sales',
      'Total cost of sales', '5001 · Cost of Goods Sold',
      'Total expenses', '6001 · Rent', '6002 · Salaries',
    ]);
  });

  it('BMT-12: a key names its line, and an unknown one names none', () => {
    expect(trendLine(result, 'net')!.yearBudget).toBe(264);
    expect(trendLine(result, 'gross')!.label).toBe('Gross profit');
    expect(trendLine(result, 'section:Expenses')!.label).toBe('Total expenses');
    expect(trendLine(result, 'account:4')!.label).toBe('6002 · Salaries');
    expect(trendLine(result, 'account:99')).toBeNull();
    expect(trendLine(result, 'nonsense')).toBeNull();
  });

  it('BMT-13: the chosen line is twelve months and the year to date at the foot, the future months marked', () => {
    const rows = trendMonthRows(result, 'account:1');

    expect(rows).toHaveLength(13);
    expect(rows[0].label).toBe('Apr 26');
    expect(rows[11].label).toBe('Mar 27');
    expect(rows.slice(0, 3).every((r) => !r.isFuture)).toBe(true);
    expect(rows.slice(3, 12).every((r) => r.isFuture && r.cell.actual === null)).toBe(true);
    expect(rows[12]).toMatchObject({ key: 'ytd', label: 'Year to date', isTotal: true });
    expect(rows[12].cell.budget).toBe(300);
    expect(trendMonthRows(result, 'nonsense')).toEqual([]);
  });
});
