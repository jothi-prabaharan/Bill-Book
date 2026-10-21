import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { BudgetVsActualBudget } from '../budget-vs-actual/budget-vs-actual-report.models';
import {
  BudgetVsActualByProjectReportRequest,
  BudgetVsActualByProjectReportResult,
  BudgetVsActualProjectOption,
} from './budget-vs-actual-by-project-report.models';

/**
 * Budget vs Actual by Project's own endpoints (TK-207), and the budgets it can
 * be run against: Budget vs Actual's list, the ones kept by project. Owns no
 * state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class BudgetVsActualByProjectReportService {
  private readonly http = inject(HttpClient);

  /** Each project's budget against its postings for the months the as-at date and period cover. */
  async run(request: BudgetVsActualByProjectReportRequest): Promise<BudgetVsActualByProjectReportResult> {
    let params = new HttpParams()
      .set('budgetId', request.budgetId)
      .set('asOfDate', request.asOfDate)
      .set('period', request.period);
    if (request.projectId) {
      params = params.set('projectId', request.projectId);
    }

    return new Promise((resolve, reject) =>
      this.http
        .get<BudgetVsActualByProjectReportResult>('/api/statements/budget-vs-actual-by-project', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's budgets kept by project, the latest year first. */
  async budgets(): Promise<BudgetVsActualBudget[]> {
    const all = await new Promise<BudgetVsActualBudget[]>((resolve, reject) =>
      this.http
        .get<BudgetVsActualBudget[]>('/api/statements/budget-vs-actual/budgets')
        .subscribe({ next: resolve, error: reject }),
    );
    return all.filter((b) => b.byProject);
  }

  /** The branch's projects, by code. */
  async projects(): Promise<BudgetVsActualProjectOption[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<BudgetVsActualProjectOption[]>('/api/statements/budget-vs-actual-by-project/projects')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
