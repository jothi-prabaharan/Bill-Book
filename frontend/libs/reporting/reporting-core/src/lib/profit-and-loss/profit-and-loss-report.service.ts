import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { ProfitAndLossReportRequest, ProfitAndLossReportResult } from './profit-and-loss-report.models';

/**
 * The Profit & Loss's own endpoint (TK-200). Owns no state and touches no
 * browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class ProfitAndLossReportService {
  private readonly http = inject(HttpClient);

  /** The profit and loss for a period, and for a comparison period when asked. */
  async run(request: ProfitAndLossReportRequest): Promise<ProfitAndLossReportResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.compareFromDate) {
      params = params.set('compareFromDate', request.compareFromDate);
    }
    if (request.compareToDate) {
      params = params.set('compareToDate', request.compareToDate);
    }
    if (request.isZeroBalanceShown) {
      params = params.set('isZeroBalanceShown', 'true');
    }

    return new Promise((resolve, reject) =>
      this.http
        .get<ProfitAndLossReportResult>('/api/statements/profit-and-loss', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
