import { HttpClient, HttpParams } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { BudgetVsActualSectionResult } from '../budget-vs-actual/budget-vs-actual-report.models';
import { hasGrossProfit, projectLabel } from './budget-vs-actual-by-project-report.models';
import { BudgetVsActualByProjectReportService } from './budget-vs-actual-by-project-report.service';

/** Budget vs Actual by Project's calls, and the project helpers its page uses (TK-207). */

function setup(reply: unknown = []) {
  const get = vi.fn().mockReturnValue(of(reply));

  TestBed.configureTestingModule({
    providers: [BudgetVsActualByProjectReportService, { provide: HttpClient, useValue: { get } }],
  });

  return { service: TestBed.inject(BudgetVsActualByProjectReportService), get };
}

function section(name: BudgetVsActualSectionResult['section'], lines: number): BudgetVsActualSectionResult {
  return {
    section: name,
    title: name,
    lines: Array.from({ length: lines }, (_, i) => ({
      accountId: i, accountCode: `${i}`, accountName: `A${i}`, isContra: false,
      budget: 0, actual: 0, variance: 0, variancePercent: null,
    })),
    budget: 0, actual: 0, variance: 0, variancePercent: null,
  };
}

describe('BudgetVsActualByProjectReportService', () => {
  it('BVP-01: runs its own endpoint with the budget, date and period, and the project only when chosen', async () => {
    const { service, get } = setup({ projects: [] });

    await service.run({ budgetId: 3, asOfDate: '2026-05-20', period: 'Month' });
    await service.run({ budgetId: 3, asOfDate: '2026-05-20', period: 'Month', projectId: 12 });

    expect(get.mock.calls[0][0]).toBe('/api/statements/budget-vs-actual-by-project');
    const first = (get.mock.calls[0][1] as { params: HttpParams }).params;
    expect(first.get('budgetId')).toBe('3');
    expect(first.get('asOfDate')).toBe('2026-05-20');
    expect(first.get('period')).toBe('Month');
    expect(first.has('projectId')).toBe(false);
    expect((get.mock.calls[1][1] as { params: HttpParams }).params.get('projectId')).toBe('12');
  });

  it("BVP-02: offers Budget vs Actual's budgets that are kept by project", async () => {
    const { service, get } = setup([
      { budgetId: 1, byProject: false },
      { budgetId: 2, byProject: true },
    ]);

    const budgets = await service.budgets();

    expect(get.mock.calls[0][0]).toBe('/api/statements/budget-vs-actual/budgets');
    expect(budgets.map((b) => b.budgetId)).toEqual([2]);
  });

  it('BVP-03: lists the projects from beside its endpoint', async () => {
    const { service, get } = setup();

    await service.projects();

    expect(get.mock.calls[0][0]).toBe('/api/statements/budget-vs-actual-by-project/projects');
  });
});

describe('projectLabel and hasGrossProfit', () => {
  it('BVP-04: a project reads as its code and name, with its status unless active', () => {
    expect(projectLabel({ projectCode: 'PRJ-001', projectName: 'Website', status: 'Active' })).toBe('PRJ-001 · Website');
    expect(projectLabel({ projectCode: 'PRJ-002', projectName: 'Fit-out', status: 'OnHold' })).toBe('PRJ-002 · Fit-out (on hold)');
    expect(projectLabel({ projectCode: '', projectName: 'Project 99', status: 'Active' })).toBe('Project 99');
  });

  it('BVP-05: gross profit only where there is revenue or a cost of sales', () => {
    expect(hasGrossProfit({ sections: [section('Revenue', 1), section('Expenses', 2)] })).toBe(true);
    expect(hasGrossProfit({ sections: [section('CostOfSales', 1)] })).toBe(true);
    expect(hasGrossProfit({ sections: [section('Revenue', 0), section('Expenses', 2)] })).toBe(false);
  });
});
