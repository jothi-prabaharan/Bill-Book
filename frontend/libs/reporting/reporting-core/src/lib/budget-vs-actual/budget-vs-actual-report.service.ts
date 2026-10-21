import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { BudgetVsActualBudget, BudgetVsActualReportRequest, BudgetVsActualReportResult } from './budget-vs-actual-report.models';

/**
 * Budget vs Actual's own endpoints (TK-204). Owns no state and touches no
 * browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class BudgetVsActualReportService {
  private readonly http = inject(HttpClient);

  /** The budget against the books for the months the as-at date and period cover. */
  async run(request: BudgetVsActualReportRequest): Promise<BudgetVsActualReportResult> {
    const params = new HttpParams()
      .set('budgetId', request.budgetId)
      .set('asOfDate', request.asOfDate)
      .set('period', request.period);

    return new Promise((resolve, reject) =>
      this.http
        .get<BudgetVsActualReportResult>('/api/statements/budget-vs-actual', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's budgets, the latest year first. */
  async budgets(): Promise<BudgetVsActualBudget[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<BudgetVsActualBudget[]>('/api/statements/budget-vs-actual/budgets')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
