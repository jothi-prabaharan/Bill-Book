import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import {
  BudgetVsActualBudget,
  budgetLabel,
  dateWithinBudgetYear,
  defaultBudget,
  variancePercentText,
  varianceTone,
} from './budget-vs-actual-report.models';
import { BudgetVsActualReportService } from './budget-vs-actual-report.service';

/** Budget vs Actual's calls, and the budget, date and variance helpers its page uses (TK-204). */

function setup() {
  const get = vi.fn().mockReturnValue(of([]));

  TestBed.configureTestingModule({
    providers: [BudgetVsActualReportService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(BudgetVsActualReportService), get };
}

function budget(id: number, start: string, status: BudgetVsActualBudget['status'], isBranchDefault = false): BudgetVsActualBudget {
  const year = Number(start.slice(0, 4));
  return {
    budgetId: id,
    name: `Budget ${id}`,
    financialYearStart: start,
    financialYearEnd: `${year + 1}-${start.slice(5, 7) === '04' ? '03-31' : '12-31'}`,
    status,
    isBranchDefault,
    byProject: false,
  };
}

describe('BudgetVsActualReportService', () => {
  it('BVA-01: runs its own endpoint with the budget, the as-at date and the period', async () => {
    const { service, get } = setup();

    await service.run({ budgetId: 7, asOfDate: '2026-06-15', period: 'YearToDate' });

    expect(get.mock.calls[0][0]).toBe('/api/statements/budget-vs-actual');
    const params = (get.mock.calls[0][1] as { params: HttpParams }).params;
    expect(params.get('budgetId')).toBe('7');
    expect(params.get('asOfDate')).toBe('2026-06-15');
    expect(params.get('period')).toBe('YearToDate');
  });

  it('BVA-02: lists the budgets from beside it', async () => {
    const { service, get } = setup();

    await service.budgets();

    expect(get.mock.calls[0][0]).toBe('/api/statements/budget-vs-actual/budgets');
  });
});

describe('defaultBudget', () => {
  const lastYear = budget(1, '2025-04-01', 'Approved', true);
  const draft = budget(2, '2026-04-01', 'Draft');
  const approved = budget(3, '2026-04-01', 'Approved');
  const branchDefault = budget(4, '2026-04-01', 'Approved', true);

  it("BVA-03: the branch's own approved budget for this year first", () => {
    expect(defaultBudget([draft, approved, branchDefault, lastYear], '2026-10-21')).toBe(branchDefault);
  });

  it('BVA-04: else an approved one for this year, else any for this year, else the latest', () => {
    expect(defaultBudget([draft, approved, lastYear], '2026-10-21')).toBe(approved);
    expect(defaultBudget([draft, lastYear], '2026-10-21')).toBe(draft);
    expect(defaultBudget([lastYear], '2026-10-21')).toBe(lastYear);
    expect(defaultBudget([], '2026-10-21')).toBeNull();
  });
});

describe('dateWithinBudgetYear', () => {
  const fy = budget(1, '2026-04-01', 'Approved');

  it("BVA-05: a date outside the budget's year moves to its nearer end", () => {
    expect(dateWithinBudgetYear('2026-03-31', fy)).toBe('2026-04-01');
    expect(dateWithinBudgetYear('2027-04-01', fy)).toBe('2027-03-31');
    expect(dateWithinBudgetYear('2026-10-21', fy)).toBe('2026-10-21');
  });
});

describe('budgetLabel, varianceTone and variancePercentText', () => {
  it('BVA-06: a budget not approved says so', () => {
    expect(budgetLabel(budget(1, '2026-04-01', 'Approved'))).toBe('Budget 1');
    expect(budgetLabel(budget(1, '2026-04-01', 'Draft'))).toBe('Budget 1 (draft)');
  });

  it('BVA-07: a positive variance is favourable, a negative adverse, none neither', () => {
    expect(varianceTone(200)).toBe('favourable');
    expect(varianceTone(-1)).toBe('adverse');
    expect(varianceTone(0)).toBeNull();
    expect(varianceTone(null)).toBeNull();
  });

  it('BVA-08: a percentage is signed to two places, and none is a dash', () => {
    expect(variancePercentText(8.33)).toBe('+8.33%');
    expect(variancePercentText(-13.66)).toBe('−13.66%');
    expect(variancePercentText(0)).toBe('0.00%');
    expect(variancePercentText(null)).toBe('—');
  });
});
