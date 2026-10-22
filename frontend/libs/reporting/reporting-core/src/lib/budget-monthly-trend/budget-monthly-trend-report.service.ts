import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { BudgetMonthlyTrendReportRequest, BudgetMonthlyTrendReportResult } from './budget-monthly-trend-report.models';

/**
 * Budget Monthly Trend's own endpoint (TK-214). The budgets it can be run
 * against are Budget vs Actual's list, so it has no lookup of its own. Owns no
 * state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class BudgetMonthlyTrendReportService {
  private readonly http = inject(HttpClient);

  /** The budget against the books for each month of its year, with the year to date. */
  async run(request: BudgetMonthlyTrendReportRequest): Promise<BudgetMonthlyTrendReportResult> {
    const params = new HttpParams().set('budgetId', request.budgetId).set('asOfDate', request.asOfDate);

    return new Promise((resolve, reject) =>
      this.http
        .get<BudgetMonthlyTrendReportResult>('/api/statements/budget-monthly-trend', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
